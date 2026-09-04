namespace Actos.Utils;

/// <summary>
/// Tri-state representation of an optional PATCH field, mirroring the canonical SDK's
/// <c>Patch&lt;T&gt;</c> semantics: <see cref="None"/> (leave the field untouched), <see cref="Set"/> (send a
/// concrete value), or <see cref="Unset"/> (send an explicit <see langword="null"/> to clear it).
/// This deliberately avoids the two-state <c>Option</c>/nullable trap the python/rust SDKs fell into.
/// </summary>
/// <typeparam name="T">The field value type.</typeparam>
public readonly struct Patch<T>
{
    private readonly T? _value;
    private readonly byte _state; // 0 = None, 1 = Set, 2 = Unset

    private Patch(byte state, T? value)
    {
        _state = state;
        _value = value;
    }

    /// <summary>Do not change this field on the server.</summary>
    public static Patch<T> None => new(0, default);

    /// <summary>Set this field to <paramref name="value"/>.</summary>
    public static Patch<T> Set(T value) => new(1, value);

    /// <summary>Send an explicit <see langword="null"/> to clear this field.</summary>
    public static Patch<T> Unset => new(2, default);

    /// <summary>True when the field is left untouched.</summary>
    public bool IsNone => _state == 0;

    /// <summary>True when the field carries a concrete value.</summary>
    public bool IsSet => _state == 1;

    /// <summary>True when the field is cleared with an explicit <see langword="null"/>.</summary>
    public bool IsUnset => _state == 2;

    /// <summary>The value to send when <see cref="IsSet"/> is true.</summary>
    public T? Value => _value;

    /// <inheritdoc />
    public override string ToString()
        => IsNone ? "None" : IsSet ? $"Set({_value})" : "Unset";
}