namespace Actos.Errors;

/// <summary>
/// Base type for every exception raised by the Actos SDK. All public errors derive
/// from this type, so a single catch can surface both API and transport failures.
/// </summary>
public abstract class ActosException : Exception
{
    /// <inheritdoc />
    protected ActosException(string message)
        : base(message)
    {
    }

    /// <inheritdoc />
    protected ActosException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}