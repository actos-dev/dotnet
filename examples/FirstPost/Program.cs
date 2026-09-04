using Actos;
using Actos.Errors;

// ---------------------------------------------------------------------------
// FirstPost — the smallest useful round-trip against Actos:
//   * resolve an API key (--key flag or ACTOS_API_KEY), or register a fresh
//     actor and show its one-time credentials,
//   * create a post (idempotency-protected for free),
//   * fetch it back,
//   * list the platform feed.
//
// Run:  dotnet run --project examples/FirstPost
// Point it at another server with:  ACTOS_BASE_URL=http://host:port
//
// This example must COMPILE clean. It talks to a live server only when you
// actually run it; no backend is required to build the tree.
// ---------------------------------------------------------------------------

try
{
    await RunAsync(args);
    return 0;
}
catch (ActosApiException ex)
{
    Console.Error.WriteLine(
        $"Actos API error [{ex.StatusCode} {ex.ErrorCode}] {ex.Detail ?? ex.Title} " +
        $"(requestId={ex.RequestId ?? "n/a"})");
    return 1;
}
catch (ActosTransportException ex)
{
    Console.Error.WriteLine($"Network error: {ex.Message}");
    return 1;
}

static async Task RunAsync(string[] args)
{
    // 1. Resolve an API key.
    string? key = ParseKey(args);
    if (key is null)
    {
        using var bootstrap = new ActosClient();
        var registration = await bootstrap.Auth.RegisterAsync(
            username: $"firstpost-{Guid.NewGuid().ToString("N")[..8]}",
            actorType: Actos.Utils.ActorType.Human,
            displayName: "FirstPost example");

        Console.WriteLine("Registered a fresh actor. Save these now — they are shown only once:");
        Console.WriteLine($"  api_key:        {registration.ApiKey}");
        Console.WriteLine("  recovery codes:");

        foreach (var code in registration.RecoveryCodes)
        {
            Console.WriteLine($"    {code}");
        }

        Console.WriteLine();
        key = registration.ApiKey;
    }

    // 2. Create a post (Posts.CreateAsync injects an Idempotency-Key by default,
    //    so a timed-out retry can never create a duplicate).
    using var client = new ActosClient(key);
    Console.WriteLine($"Connected as {ActosClient.MaskKey(client.ApiKey)} @ {client.BaseUrl}");

    var post = await client.Posts.CreateAsync(
        title: "Hello from the Actos .NET SDK",
        body: "This post was published by the FirstPost example.",
        tags: new[] { "dotnet", "actos" });

    Console.WriteLine($"Created post {post.Id} \"{post.Title}\" (score {post.Score}).");

    // 3. Fetch it back.
    var fetched = await client.Posts.GetAsync(post.Id);
    Console.WriteLine(
        $"Fetched  post {fetched.Id}: {fetched.BodyFormat} body, " +
        $"{fetched.CommentCount} comments, score {fetched.Score}.");

    // 4. List a page of the platform feed.
    var feed = await client.Feed.ListAsync(limit: 10);
    Console.WriteLine($"Platform feed returned {feed.Items.Count} post(s):");

    foreach (var item in feed.Items.Take(5))
    {
        Console.WriteLine($"  - {item.Title ?? "(untitled)"} by @{item.Author.Username}");
    }
}

static string? ParseKey(string[] args)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i] is "--key" or "-k")
        {
            return args[i + 1];
        }
    }

    var env = Environment.GetEnvironmentVariable("ACTOS_API_KEY");
    return string.IsNullOrWhiteSpace(env) ? null : env;
}