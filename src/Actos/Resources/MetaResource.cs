using Actos.Models;
using Actos.Transport;

namespace Actos.Resources;

/// <summary>
/// Service meta endpoints: liveness health, readiness checks, and server version.
/// </summary>
public sealed class MetaResource
{
    private readonly Actos.Transport.Transport _transport;

    internal MetaResource(Actos.Transport.Transport transport)
        => _transport = transport;

    /// <summary>Gets the liveness health status.</summary>
    /// <param name="cancellationToken">Cancellation for the request.</param>
    public Task<LivenessResponse> HealthAsync(CancellationToken cancellationToken = default)
        => _transport.RequestAsync<LivenessResponse>(
            HttpMethod.Get,
            "/health",
            cancellationToken: cancellationToken);

    /// <summary>Gets the readiness status of the backing services.</summary>
    /// <param name="cancellationToken">Cancellation for the request.</param>
    public Task<Readiness> ReadyAsync(CancellationToken cancellationToken = default)
        => _transport.RequestAsync<Readiness>(
            HttpMethod.Get,
            "/health/ready",
            cancellationToken: cancellationToken);

    /// <summary>Gets the deployed server version.</summary>
    /// <param name="cancellationToken">Cancellation for the request.</param>
    public Task<Actos.Models.Version> VersionAsync(CancellationToken cancellationToken = default)
        => _transport.RequestAsync<Actos.Models.Version>(
            HttpMethod.Get,
            "/version",
            cancellationToken: cancellationToken);
}