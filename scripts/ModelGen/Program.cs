using System.Text;
using System.Text.Json;

// Deterministic OpenAPI -> C# model generator for the Actos .NET SDK.
//
// Usage:
//   dotnet run --project scripts/ModelGen -- [openapi.json path] [output dir]
//
// Defaults resolve relative to the repository root: input ../actos-backend/docs/openapi.json,
// output src/Actos/Models. Reads the Actos backend OpenAPI document and emits one C# record
// per component schema. Enum-valued fields become strings (forward compatible), free-form
// objects (metadata/payload) become JsonElement, maps (VoteMapResponse.votes) become
// Dictionary<string,T>, and allOf compositions are flattened. The output is deterministic and
// re-running the generator is idempotent.

var inputPath = args.Length > 0 ? args[0] : Path.Combine(Directory.GetCurrentDirectory(), "../actos-backend/docs/openapi.json");
var outputDir = args.Length > 1 ? args[1] : Path.Combine(Directory.GetCurrentDirectory(), "src/Actos/Models");

if (!File.Exists(inputPath))
{
    Console.Error.WriteLine($"Input OpenAPI document not found: {inputPath}");
    return 1;
}

using var document = JsonDocument.Parse(File.ReadAllText(inputPath));
var root = document.RootElement;
if (!root.TryGetProperty("components", out var components) ||
    !components.TryGetProperty("schemas", out var schemas))
{
    Console.Error.WriteLine("OpenAPI document has no components.schemas.");
    return 1;
}

if (Directory.Exists(outputDir))
{
    Directory.Delete(outputDir, recursive: true);
}

Directory.CreateDirectory(outputDir);

var names = schemas.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
var emitter = new Emitter(outputDir);

int emitted = 0;
foreach (var name in names)
{
    var schema = schemas.GetProperty(name);
    if (CSharpShape.IsEnum(schema))
    {
        emitter.WriteErrorCodeClass(name, schema.GetProperty("enum"));
        emitted++;
        continue;
    }

    if (CSharpShape.IsPrimitive(schema))
    {
        continue;
    }

    var props = Collector.Collect(schema, schemas);
    if (props.Count == 0)
    {
        continue;
    }

    emitter.WriteRecord(name, props);
    emitted++;
}

Console.WriteLine($"Generated {emitted} model types into {outputDir}");
return 0;

// ---------------------------------------------------------------------------

static class CSharpShape
{
    public static bool IsEnum(JsonElement schema)
    {
        return schema.ValueKind == JsonValueKind.Object &&
               schema.TryGetProperty("type", out var type) &&
               type.GetString() == "string" &&
               schema.TryGetProperty("enum", out _);
    }

    public static bool IsPrimitive(JsonElement schema)
    {
        return schema.ValueKind == JsonValueKind.Object &&
               schema.TryGetProperty("type", out var t) &&
               t.GetString() is "string" or "integer" or "number" or "boolean";
    }
}

static class Naming
{
    public static string Pascal(string snake)
    {
        var parts = snake.Split('_', StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder();
        foreach (var part in parts)
        {
            if (part.Length == 0)
            {
                continue;
            }

            sb.Append(char.ToUpperInvariant(part[0]));
            sb.Append(part.AsSpan(1));
        }

        return sb.Length == 0 ? "Value" : sb.ToString();
    }
}

sealed class PropInfo
{
    public required string JsonName { get; init; }
    public required string Pascal { get; init; }
    public required JsonElement Schema { get; init; }
    public required bool Required { get; init; }
}

static class Collector
{
    public static List<PropInfo> Collect(JsonElement schema, JsonElement schemas)
    {
        var ordered = new List<string>();
        var byName = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var required = new HashSet<string>(StringComparer.Ordinal);

        if (schema.TryGetProperty("allOf", out var allOf))
        {
            foreach (var member in allOf.EnumerateArray())
            {
                var (props, req) = ExtractObject(member, schemas);
                foreach (var kv in props)
                {
                    if (byName.TryAdd(kv.Key, kv.Value))
                    {
                        ordered.Add(kv.Key);
                    }
                }

                required.UnionWith(req);
            }
        }
        else if (schema.TryGetProperty("oneOf", out var oneOf))
        {
            HashSet<string>? intersection = null;
            foreach (var branch in oneOf.EnumerateArray())
            {
                var (props, req) = ExtractObject(branch, schemas);
                foreach (var kv in props)
                {
                    if (byName.TryAdd(kv.Key, kv.Value))
                    {
                        ordered.Add(kv.Key);
                    }
                }

                if (intersection is null)
                {
                    intersection = new HashSet<string>(req, StringComparer.Ordinal);
                }
                else
                {
                    intersection.IntersectWith(req);
                }
            }

            required = intersection ?? required;
        }
        else
        {
            var (props, req) = ExtractObject(schema, schemas);
            foreach (var kv in props)
            {
                byName[kv.Key] = kv.Value;
            }

            ordered = props.Select(p => p.Key).ToList();
            required = new HashSet<string>(req, StringComparer.Ordinal);
        }

        return ordered.Select(k => new PropInfo
        {
            JsonName = k,
            Pascal = Naming.Pascal(k),
            Schema = byName[k],
            Required = required.Contains(k),
        }).ToList();
    }

    private static (List<KeyValuePair<string, JsonElement>> Props, List<string> Required) ExtractObject(JsonElement obj, JsonElement schemas)
    {
        if (obj.TryGetProperty("$ref", out var refValue))
        {
            var target = ResolveRef(refValue.GetString(), schemas);
            obj = target;
        }

        var props = new List<KeyValuePair<string, JsonElement>>();
        if (obj.TryGetProperty("properties", out var propsEl))
        {
            foreach (var prop in propsEl.EnumerateObject())
            {
                props.Add(new KeyValuePair<string, JsonElement>(prop.Name, prop.Value));
            }
        }

        var required = new List<string>();
        if (obj.TryGetProperty("required", out var requiredEl))
        {
            foreach (var r in requiredEl.EnumerateArray())
            {
                required.Add(r.GetString()!);
            }
        }

        return (props, required);
    }

    private static JsonElement ResolveRef(string? refPath, JsonElement schemas)
    {
        if (refPath is null)
        {
            throw new InvalidOperationException("Null $ref encountered.");
        }

        var lastSlash = refPath.LastIndexOf('/');
        var name = refPath.Substring(lastSlash + 1);
        return schemas.GetProperty(name);
    }

    public static (string Type, bool Optional) MapType(JsonElement prop, bool required)
    {
        // $ref
        if (prop.TryGetProperty("$ref", out var refEl))
        {
            var refName = RefName(refEl.GetString());
            var optionalRef = !required;
            if (refName == "ErrorCode")
            {
                return (optionalRef ? "string?" : "string", optionalRef);
            }

            return (refName + (optionalRef ? "?" : ""), optionalRef);
        }

        // oneOf union containing a single nullable ref (e.g. NotificationSummary.actor: null | ActorSummary)
        if (prop.TryGetProperty("oneOf", out var oneOfEl))
        {
            string? singleRef = null;
            var hasNullBranch = false;
            foreach (var branch in oneOfEl.EnumerateArray())
            {
                if (branch.TryGetProperty("$ref", out var br))
                {
                    singleRef = singleRef is null ? RefName(br.GetString()) : null;
                }
                else if (branch.ValueKind == JsonValueKind.Object && branch.TryGetProperty("type", out var bt) && bt.GetString() == "null")
                {
                    hasNullBranch = true;
                }
            }

            if (singleRef is not null)
            {
                var optionalOneOf = hasNullBranch || !required;
                if (singleRef == "ErrorCode")
                {
                    return (optionalOneOf ? "string?" : "string", optionalOneOf);
                }

                return (singleRef + (optionalOneOf ? "?" : ""), optionalOneOf);
            }
        }

        var type = PrimaryType(prop);
        var nullableExplicit = HasExplicitNull(prop);
        var optional = !required || nullableExplicit;

        switch (type)
        {
            case "string":
                return (optional ? "string?" : "string", optional);
            case "integer":
            {
                var baseType = prop.TryGetProperty("format", out var fmt) && fmt.GetString() == "int64" ? "long" : "int";
                return (optional ? baseType + "?" : baseType, optional);
            }
            case "boolean":
                return (optional ? "bool?" : "bool", optional);
            case "number":
                return (optional ? "double?" : "double", optional);
            case "array":
            {
                if (!prop.TryGetProperty("items", out var items))
                {
                    return (optional ? "JsonElement?" : "JsonElement", optional);
                }

                var (element, _) = MapType(items, required: true);
                return (optional ? $"IReadOnlyList<{element}>?" : $"IReadOnlyList<{element}>", optional);
            }
            case "object":
                return MapObject(prop, optional);
            default:
                return (optional ? "JsonElement?" : "JsonElement", optional);
        }
    }

    private static (string, bool) MapObject(JsonElement prop, bool optional)
    {
        if (prop.TryGetProperty("additionalProperties", out var ap))
        {
            if (ap.TryGetProperty("$ref", out var apRef))
            {
                var dict = $"Dictionary<string, {RefName(apRef.GetString())}>";
                return (optional ? dict + "?" : dict, optional);
            }

            if (ap.TryGetProperty("type", out var apType))
            {
                switch (apType.GetString())
                {
                    case "integer":
                    {
                        var v = ap.TryGetProperty("format", out var fmt) && fmt.GetString() == "int64" ? "long" : "int";
                        var dict = $"Dictionary<string, {v}>";
                        return (optional ? dict + "?" : dict, optional);
                    }
                    case "string":
                        var sdict = "Dictionary<string, string>";
                        return (optional ? sdict + "?" : sdict, optional);
                    case "boolean":
                        var bdict = "Dictionary<string, bool>";
                        return (optional ? bdict + "?" : bdict, optional);
                }
            }

            // additionalProperties: true or unsupported shape — treat as free-form.
            return (optional ? "JsonElement?" : "JsonElement", optional);
        }

        // Free-form object (metadata, payload) — passed through untouched.
        return (optional ? "JsonElement?" : "JsonElement", optional);
    }

    private static string? PrimaryType(JsonElement prop)
    {
        if (!prop.TryGetProperty("type", out var type))
        {
            return null;
        }

        if (type.ValueKind == JsonValueKind.String)
        {
            return type.GetString();
        }

        // type may be an array like ["string","null"] — take the first non-null member.
        foreach (var item in type.EnumerateArray())
        {
            var t = item.GetString();
            if (t != "null")
            {
                return t;
            }
        }

        return null;
    }

    private static bool HasExplicitNull(JsonElement prop)
    {
        if (prop.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in type.EnumerateArray())
            {
                if (item.GetString() == "null")
                {
                    return true;
                }
            }
        }

        if (prop.TryGetProperty("oneOf", out var oneOf))
        {
            foreach (var branch in oneOf.EnumerateArray())
            {
                if (branch.ValueKind == JsonValueKind.Object &&
                    branch.TryGetProperty("type", out var bt) &&
                    bt.GetString() == "null")
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string RefName(string? refPath)
    {
        var lastSlash = refPath!.LastIndexOf('/');
        return refPath.Substring(lastSlash + 1);
    }
}

sealed class Emitter
{
    private readonly string _outputDir;

    public Emitter(string outputDir)
        => _outputDir = outputDir;

    public void WriteErrorCodeClass(string name, JsonElement enumEl)
    {
        var sb = new StringBuilder();
        Header(sb);
        sb.Append($"public static class {name}\n{{\n");
        foreach (var value in enumEl.EnumerateArray().Select(e => e.GetString()!))
        {
            sb.Append($"    public const string {Naming.Pascal(value)} = \"{value}\";\n");
        }

        sb.Append("}\n");
        File.WriteAllText(Path.Combine(_outputDir, name + ".cs"), sb.ToString());
    }

    public void WriteRecord(string name, List<PropInfo> props)
    {
        // C# record positional params: optional (defaulted) parameters must come AFTER
        // required ones. Resolve every property, then sort required-first (stable), so the
        // emitted parameter list is valid while [JsonPropertyName] keeps the wire mapping.
        var resolved = props
            .Select(prop =>
            {
                var (type, optional) = Collector.MapType(prop.Schema, prop.Required);
                // A property whose Pascal name collides with the enclosing record name
                // (e.g. Version.version) is disambiguated with a "Value" suffix; wire name
                // via JsonPropertyName is unaffected.
                var pascal = prop.Pascal == name ? prop.Pascal + "Value" : prop.Pascal;
                return (prop, type, optional, pascal);
            })
            .OrderBy(x => x.optional)
            .ToList();

        var sb = new StringBuilder();
        Header(sb);
        sb.Append($"public sealed record {name}(\n");
        foreach (var r in resolved)
        {
            var defaultSuffix = r.optional ? " = null" : "";
            sb.Append($"    [property: System.Text.Json.Serialization.JsonPropertyName(\"{r.prop.JsonName}\")] {r.type} {r.pascal}{defaultSuffix},\n");
        }

        var body = sb.ToString();
        if (props.Count > 0)
        {
            body = body[..^2];
        }

        body += "\n);\n";
        File.WriteAllText(Path.Combine(_outputDir, name + ".cs"), body);
    }

    private static void Header(StringBuilder sb)
    {
        sb.Append("// <auto-generated/>\n");
        sb.Append("// GENERATED BY Actos.ModelGen — DO NOT EDIT MANUALLY.\n");
        sb.Append("// Re-run scripts/ModelGen to regenerate from actos-backend/docs/openapi.json.\n\n");
        sb.Append("#nullable enable\n\n");
        sb.Append("using System.Collections.Generic;\n");
        sb.Append("using System.Text.Json;\n");
        sb.Append("using System.Text.Json.Serialization;\n\n");
        sb.Append("namespace Actos.Models;\n\n");
    }
}