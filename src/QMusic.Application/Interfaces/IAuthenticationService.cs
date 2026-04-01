using QMusic.Application.DTOs;

namespace QMusic.Application.Interfaces;

/// <summary>
/// Abstraction for authenticating with a music provider's user account.
/// Each provider that needs user-level access (playlists, library) implements this.
/// Kept in the Application layer so UI components can depend on it without
/// knowing about Google OAuth, Spotify tokens, etc.
/// </summary>
public interface IAuthenticationService
{
    Task<bool> IsAuthenticatedAsync(CancellationToken ct = default);
    Task<bool> LoginAsync(CancellationToken ct = default);
    Task LogoutAsync(CancellationToken ct = default);
    Task<UserProfile?> GetUserProfileAsync(CancellationToken ct = default);

    event Action<bool>? AuthenticationStateChanged;
}
