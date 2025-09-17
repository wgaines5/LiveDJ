using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiveDJ.Services;


public class AuthState
{
    private const string IdTokenKey = "auth_idToken";
    private const string RefreshTokenKey = "auth_refreshToken";
    private const string ExpiryEpochKey = "auth_expiry"; // UTC epoch seconds


    public string? IdToken { get; private set; }
    public string? RefreshToken { get; private set; }
    public DateTimeOffset? ExpiresAtUtc { get; private set; }


    public bool IsSignedIn => !string.IsNullOrEmpty(IdToken) && ExpiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(1);


    public void SetTokens(string idToken, string refreshToken, TimeSpan expiresIn)
    {
        IdToken = idToken;
        RefreshToken = refreshToken;
        ExpiresAtUtc = DateTimeOffset.UtcNow.Add(expiresIn);
        Persist();
    }


    public void SignOut()
    {
        IdToken = null; RefreshToken = null; ExpiresAtUtc = null;
        var settings = ApplicationData.Current.LocalSettings;
        settings.Values.Remove(IdTokenKey);
        settings.Values.Remove(RefreshTokenKey);
        settings.Values.Remove(ExpiryEpochKey);
    }


    public void LoadFromStorage()
    {
        var s = ApplicationData.Current.LocalSettings;
        IdToken = s.Values[IdTokenKey] as string;
        RefreshToken = s.Values[RefreshTokenKey] as string;
        if (s.Values[ExpiryEpochKey] is long epoch)
            ExpiresAtUtc = DateTimeOffset.FromUnixTimeSeconds(epoch);
    }


    private void Persist()
    {
        var s = ApplicationData.Current.LocalSettings;
        s.Values[IdTokenKey] = IdToken;
        s.Values[RefreshTokenKey] = RefreshToken;
        s.Values[ExpiryEpochKey] = ExpiresAtUtc?.ToUnixTimeSeconds();
    }
}
