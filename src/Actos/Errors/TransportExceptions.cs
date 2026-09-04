namespace Actos.Errors;

/// <summary>
/// A failure that prevented an HTTP response from being produced at all
/// (no connection, timeout, DNS failure, and so on).
/// </summary>
public class ActosTransportException : ActosException
{
    /// <inheritdoc />
    internal ActosTransportException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// The request timed out before a response was received.
/// </summary>
public sealed class ActosTimeoutException : ActosTransportException
{
    internal ActosTimeoutException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// A connection-level failure occurred before a response was received
/// (DNS, refused socket, reset connection, and so on).
/// </summary>
public sealed class ActosConnectionException : ActosTransportException
{
    internal ActosConnectionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}