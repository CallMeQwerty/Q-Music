using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using QMusic.Application.DTOs;
using QMusic.Application.Interfaces;
using QMusic.Infrastructure.MusicProviders.YouTube;

namespace QMusic.Infrastructure.Auth;

/// <summary>
/// Implements Google OAuth 2.0 Authorization Code flow for desktop apps.
///
/// Flow:
/// 1. Open user's browser to Google consent screen
/// 2. Listen on localhost:8888/callback for the redirect
/// 3. Exchange the auth code for access + refresh tokens
/// 4. Store tokens via ISettingsService for persistence across sessions
/// 5. Auto-refresh access token when expired
///
/// The access token is then used by other services (playlist sync, etc.)
/// to make authenticated YouTube API calls.
/// </summary>
public sealed class GoogleOAuthService : IAuthenticationService
{
    private const string AuthEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string UserInfoEndpoint = "https://www.googleapis.com/oauth2/v2/userinfo";
    private const string RedirectUri = "http://localhost:8888/callback";
    private const string Scopes = "https://www.googleapis.com/auth/youtube https://www.googleapis.com/auth/userinfo.profile https://www.googleapis.com/auth/userinfo.email";

    private const string TokenSettingsKey = "GoogleOAuth:Token";

    private readonly YouTubeOAuthOptions _oauthOptions;
    private readonly ISettingsService _settings;
    private readonly HttpClient _httpClient;

    private OAuthToken? _token;
    private UserProfile? _cachedProfile;

    public event Action<bool>? AuthenticationStateChanged;

    public GoogleOAuthService(
        IOptions<YouTubeOptions> options,
        ISettingsService settings,
        IHttpClientFactory httpClientFactory)
    {
        _oauthOptions = options.Value.OAuth;
        _settings = settings;
        _httpClient = httpClientFactory.CreateClient();
    }

    public async Task<bool> IsAuthenticatedAsync(CancellationToken ct = default)
    {
        await EnsureTokenLoadedAsync(ct);

        if (_token is null)
            return false;

        // If token is expired, try to refresh
        if (_token.IsExpired && !string.IsNullOrEmpty(_token.RefreshToken))
            return await RefreshTokenAsync(ct);

        return !_token.IsExpired;
    }

    public async Task<bool> LoginAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_oauthOptions.ClientId) || string.IsNullOrEmpty(_oauthOptions.ClientSecret))
            return false;

        var state = Guid.NewGuid().ToString("N");
        var authUrl = $"{AuthEndpoint}?" +
                      $"client_id={Uri.EscapeDataString(_oauthOptions.ClientId)}" +
                      $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}" +
                      $"&response_type=code" +
                      $"&scope={Uri.EscapeDataString(Scopes)}" +
                      $"&access_type=offline" +
                      $"&prompt=consent" +
                      $"&state={state}";

        // Start listening before opening the browser
        var authCode = await ListenForCallbackAsync(state, authUrl, ct);
        if (authCode is null)
            return false;

        // Exchange auth code for tokens
        var tokenResponse = await ExchangeCodeForTokenAsync(authCode, ct);
        if (tokenResponse is null)
            return false;

        _token = new OAuthToken
        {
            AccessToken = tokenResponse.AccessToken,
            RefreshToken = tokenResponse.RefreshToken,
            ExpiresAtUtc = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn - 60) // 60s buffer
        };

        _cachedProfile = null;
        await SaveTokenAsync(ct);
        AuthenticationStateChanged?.Invoke(true);
        return true;
    }

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        _token = null;
        _cachedProfile = null;
        _settings.SetValue<OAuthToken?>(TokenSettingsKey, null);
        await _settings.SaveAsync(ct);
        AuthenticationStateChanged?.Invoke(false);
    }

    public async Task<UserProfile?> GetUserProfileAsync(CancellationToken ct = default)
    {
        if (_cachedProfile is not null)
            return _cachedProfile;

        if (!await IsAuthenticatedAsync(ct))
            return null;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, UserInfoEndpoint);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token!.AccessToken);

            var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStreamAsync(ct);
            var userInfo = await JsonSerializer.DeserializeAsync<GoogleUserInfo>(json, cancellationToken: ct);
            if (userInfo is null)
                return null;

            _cachedProfile = new UserProfile
            {
                DisplayName = userInfo.Name ?? userInfo.Email ?? "User",
                Email = userInfo.Email,
                ProfilePictureUrl = userInfo.Picture
            };

            return _cachedProfile;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    /// <summary>
    /// Returns the current access token for use by other services (e.g. playlist API calls).
    /// Refreshes automatically if expired.
    /// </summary>
    public async Task<string?> GetAccessTokenAsync(CancellationToken ct = default)
    {
        if (!await IsAuthenticatedAsync(ct))
            return null;
        return _token?.AccessToken;
    }

    private async Task<string?> ListenForCallbackAsync(string expectedState, string authUrl, CancellationToken ct)
    {
        using var listener = new HttpListener();
        listener.Prefixes.Add("http://localhost:8888/");
        listener.Start();

        // Open user's default browser
        Process.Start(new ProcessStartInfo(authUrl) { UseShellExecute = true });

        try
        {
            var context = await listener.GetContextAsync().WaitAsync(TimeSpan.FromMinutes(5), ct);
            var query = context.Request.QueryString;
            var code = query["code"];
            var state = query["state"];
            var error = query["error"];

            // Send response to browser
            string responseHtml;
            if (!string.IsNullOrEmpty(error) || state != expectedState || string.IsNullOrEmpty(code))
            {
                responseHtml = BuildCallbackHtml("Authentication Failed", "Something went wrong. You can close this tab.");
                await WriteResponseAsync(context.Response, responseHtml);
                return null;
            }

            responseHtml = BuildCallbackHtml("Signed in to Q-Music", "You're all set!<br>You can close this tab and return to the app.");
            await WriteResponseAsync(context.Response, responseHtml);
            return code;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (TimeoutException)
        {
            return null;
        }
    }

    private static string BuildCallbackHtml(string title, string message)
    {
        return "<!DOCTYPE html><html><head><title>" + title + "</title>" +
            "<style>" +
            "body{background:#121212;color:#e8e8e8;font-family:'Segoe UI',system-ui,sans-serif;" +
            "display:flex;align-items:center;justify-content:center;height:100vh;margin:0}" +
            ".card{text-align:center;padding:40px 60px;background:#1e1e1e;border-radius:12px;border:1px solid #333}" +
            "h2{margin:0 0 12px;color:#ff3333}p{margin:0;color:#aaa;font-size:0.95rem}" +
            "</style></head><body><div class='card'><h2>" + title + "</h2>" +
            "<p>" + message + "</p></div></body></html>";
    }

    private static async Task WriteResponseAsync(HttpListenerResponse response, string html)
    {
        response.ContentType = "text/html";
        response.StatusCode = 200;
        var buffer = System.Text.Encoding.UTF8.GetBytes(html);
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer);
        response.Close();
    }

    private async Task<GoogleTokenResponse?> ExchangeCodeForTokenAsync(string code, CancellationToken ct)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = _oauthOptions.ClientId!,
            ["client_secret"] = _oauthOptions.ClientSecret!,
            ["redirect_uri"] = RedirectUri,
            ["grant_type"] = "authorization_code"
        });

        return await PostTokenRequestAsync(content, ct);
    }

    private async Task<bool> RefreshTokenAsync(CancellationToken ct)
    {
        if (_token?.RefreshToken is null || _oauthOptions.ClientId is null || _oauthOptions.ClientSecret is null)
            return false;

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["refresh_token"] = _token.RefreshToken,
            ["client_id"] = _oauthOptions.ClientId,
            ["client_secret"] = _oauthOptions.ClientSecret,
            ["grant_type"] = "refresh_token"
        });

        var response = await PostTokenRequestAsync(content, ct);
        if (response is null)
            return false;

        _token.AccessToken = response.AccessToken;
        _token.ExpiresAtUtc = DateTime.UtcNow.AddSeconds(response.ExpiresIn - 60);
        // Refresh token is not returned on refresh — keep the existing one

        _cachedProfile = null;
        await SaveTokenAsync(ct);
        return true;
    }

    private async Task<GoogleTokenResponse?> PostTokenRequestAsync(FormUrlEncodedContent content, CancellationToken ct)
    {
        try
        {
            var response = await _httpClient.PostAsync(TokenEndpoint, content, ct);
            if (!response.IsSuccessStatusCode)
                return null;

            var stream = await response.Content.ReadAsStreamAsync(ct);
            return await JsonSerializer.DeserializeAsync<GoogleTokenResponse>(stream, cancellationToken: ct);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    private async Task EnsureTokenLoadedAsync(CancellationToken ct)
    {
        if (_token is not null)
            return;

        await _settings.LoadAsync(ct);
        _token = _settings.GetValue<OAuthToken>(TokenSettingsKey);
    }

    private async Task SaveTokenAsync(CancellationToken ct)
    {
        _settings.SetValue(TokenSettingsKey, _token);
        await _settings.SaveAsync(ct);
    }

    // --- Internal models ---

    private sealed class OAuthToken
    {
        public string AccessToken { get; set; } = string.Empty;
        public string? RefreshToken { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
        public bool IsExpired => DateTime.UtcNow >= ExpiresAtUtc;
    }

    private sealed class GoogleTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }

    private sealed class GoogleUserInfo
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("picture")]
        public string? Picture { get; set; }
    }
}
