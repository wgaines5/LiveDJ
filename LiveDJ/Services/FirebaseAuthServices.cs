using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http.Json;
using LiveDJ.Models;
using Microsoft.Extensions.Options;


namespace LiveDJ.Services;


public class FirebaseAuthService
{
    private readonly HttpClient _http;
    private readonly AppConfig _cfg;
    private readonly AuthState _state;


    public FirebaseAuthService(HttpClient http, IOptions<AppConfig> cfg, AuthState state)
    {
        _http = http;
        _cfg = cfg.Value;
        _state = state;
    }


    public async Task<(bool ok, string? error)> SignInAsync(string email, string password)
    {
        try
        {
            var url = $"{_cfg.FirebaseBaseUrl}/accounts:signInWithPassword?key={_cfg.FirebaseApiKey}";
            var req = new FirebaseSignInRequest(email, password);
            var resp = await _http.PostAsJsonAsync(url, req);
            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync();
                return (false, MapError(err));
            }
            var dto = await resp.Content.ReadFromJsonAsync<FirebaseSignInResponse>();
            if (dto is null) return (false, "Empty response");
            var expires = int.TryParse(dto.expiresIn, out var sec) ? TimeSpan.FromSeconds(sec) : TimeSpan.FromMinutes(55);
            _state.SetTokens(dto.idToken, dto.refreshToken, expires);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }


    public async Task<bool> TryRefreshAsync()
    {
        if (string.IsNullOrEmpty(_state.RefreshToken)) return false;
        var url = $"{_cfg.SecureTokenUrl}/token?key={_cfg.FirebaseApiKey}";
        var req = new FirebaseRefreshRequest("refresh_token", _state.RefreshToken);
        var resp = await _http.PostAsJsonAsync(url, req);
        if (!resp.IsSuccessStatusCode) return false;
        var dto = await resp.Content.ReadFromJsonAsync<FirebaseRefreshResponse>();
        if (dto is null) return false;
        var expires = int.TryParse(dto.expires_in, out var sec) ? TimeSpan.FromSeconds(sec) : TimeSpan.FromMinutes(55);
        _state.SetTokens(dto.id_token, dto.refresh_token, expires);
        return true;
    }


    private static string MapError(string raw) => raw.Contains("EMAIL_NOT_FOUND") ? "Email not found"
    : raw.Contains("INVALID_PASSWORD") ? "Invalid password"
    : raw.Contains("USER_DISABLED") ? "User disabled"
    : "Sign-in failed";

    public async Task<(bool ok, string? error, string? localId)> SignUpAsync(string email, string password)
    {
        try
        {
            var url = $"{_cfg.FirebaseBaseUrl}/accounts:signUp?key={_cfg.FirebaseApiKey}";
            var req = new FirebaseSignUpRequest(email, password);
            var resp = await _http.PostAsJsonAsync(url, req);
            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync();
                return (false, MapError(err), null);
            }
            var dto = await resp.Content.ReadFromJsonAsync<FirebaseSignInResponse>();
            if (dto is null) return (false, "Empty response", null);

            // Store tokens so the user is signed in after sign-up
            var expires = int.TryParse(dto.expiresIn, out var sec) ? TimeSpan.FromSeconds(sec) : TimeSpan.FromMinutes(55);
            _state.SetTokens(dto.idToken, dto.refreshToken, expires);

            return (true, null, dto.localId);
        }
        catch (Exception ex)
        {
            return (false, ex.Message, null);
        }
    }
}
