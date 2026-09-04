using Actos.Models;
using Actos.Transport;

namespace Actos.Resources;

/// <summary>
/// Authentication endpoints: registration (no email, one-time recovery codes), key management,
/// recovery, and identity lookup. See the Actos auth flows in the platform CONTEXT.md.
/// </summary>
public sealed class AuthResource
{
    private readonly Actos.Transport.Transport _transport;

    internal AuthResource(Actos.Transport.Transport transport)
        => _transport = transport;

    /// <summary>
    /// Registers a new actor. The returned <see cref="RegisterResponse.ApiKey"/> and
    /// <see cref="RegisterResponse.RecoveryCodes"/> are shown only here and never again.
    /// </summary>
    /// <param name="username">Desired username.</param>
    /// <param name="actorType">One of <see cref="Utils.ActorType"/> values.</param>
    /// <param name="displayName">Optional display name.</param>
    public Task<RegisterResponse> RegisterAsync(
        string username,
        string actorType,
        string? displayName = null,
        CancellationToken cancellationToken = default)
        => _transport.RequestAsync<RegisterResponse>(
            HttpMethod.Post,
            "/auth/register",
            body: new RegisterRequest(actorType, username, displayName),
            cancellationToken: cancellationToken);

    /// <summary>Returns the identity bound to the current API key.</summary>
    public Task<WhoamiResponse> WhoamiAsync(CancellationToken cancellationToken = default)
        => _transport.RequestAsync<WhoamiResponse>(HttpMethod.Get, "/auth/whoami", cancellationToken: cancellationToken);

    /// <summary>Creates a new API key for the current actor.</summary>
    /// <param name="label">Optional human label (e.g. "cli-macbook").</param>
    public Task<CreateKeyResponse> CreateKeyAsync(string? label = null, CancellationToken cancellationToken = default)
        => _transport.RequestAsync<CreateKeyResponse>(
            HttpMethod.Post,
            "/auth/keys",
            body: new CreateKeyRequest(label),
            cancellationToken: cancellationToken);

    /// <summary>Lists the current actor's API keys (secrets are never included).</summary>
    public Task<ListKeysResponse> ListKeysAsync(CancellationToken cancellationToken = default)
        => _transport.RequestAsync<ListKeysResponse>(HttpMethod.Get, "/auth/keys", cancellationToken: cancellationToken);

    /// <summary>Revokes an API key. Idempotent.</summary>
    public Task RevokeKeyAsync(string keyId, CancellationToken cancellationToken = default)
        => _transport.RequestNoContentAsync(
            HttpMethod.Delete,
            $"/auth/keys/{keyId}",
            cancellationToken: cancellationToken);

    /// <summary>
    /// Recovers access using a one-time recovery code. Returns a fresh API key plus the count of
    /// remaining recovery codes; issued codes are consumed.
    /// </summary>
    public Task<RecoverResponse> RecoverAsync(
        string username,
        string recoveryCode,
        CancellationToken cancellationToken = default)
        => _transport.RequestAsync<RecoverResponse>(
            HttpMethod.Post,
            "/auth/recover",
            body: new RecoverRequest(username, recoveryCode),
            cancellationToken: cancellationToken);

    /// <summary>Regenerates ten fresh recovery codes; all previously issued codes are invalidated at once.</summary>
    public Task<RegenerateRecoveryCodesResponse> RegenerateRecoveryCodesAsync(CancellationToken cancellationToken = default)
        => _transport.RequestAsync<RegenerateRecoveryCodesResponse>(
            HttpMethod.Post,
            "/auth/recovery-codes/regenerate",
            cancellationToken: cancellationToken);
}