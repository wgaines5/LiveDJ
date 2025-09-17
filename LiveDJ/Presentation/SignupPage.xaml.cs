using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Uno.Extensions;
using Uno.Extensions.Navigation;
using LiveDJ.Services;

namespace LiveDJ.Presentation;

public sealed partial class SignupPage : Page
{
    private readonly FirebaseAuthService _auth;
    private readonly AuthState _state;
    private readonly HttpClient _http;
    private INavigator Nav => this.Navigator();

    public SignupPage()
    {
        InitializeComponent();
        var sp = (Application.Current as App)!.Host!.Services;
        _auth = sp.GetRequiredService<FirebaseAuthService>();
        _state = sp.GetRequiredService<AuthState>();

        var cfg = sp.GetRequiredService<IOptions<AppConfig>>().Value;
        _http = new HttpClient { BaseAddress = new Uri(cfg.ApiBaseUrl) };
    }

    private async void OnSignUp(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "";

        var name = NameBox.Text?.Trim();
        var email = EmailBox.Text?.Trim();
        var pw = PasswordBox.Password ?? "";
        var pw2 = ConfirmBox.Password ?? "";
        var phone = PhoneBox.Text?.Trim();
        var state = StateBox.Text?.Trim();
        var addr = AddressBox.Text?.Trim();

        // Simple validation
        if (string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(pw) ||
            string.IsNullOrWhiteSpace(pw2))
        {
            StatusText.Text = "Please fill in name, email, and password.";
            return;
        }
        if (pw != pw2)
        {
            StatusText.Text = "Passwords do not match.";
            return;
        }

        try
        {
            StatusText.Text = "Creating account…";
            var (ok, err, uid) = await _auth.SignUpAsync(email, pw);
            if (!ok || string.IsNullOrEmpty(uid))
            {
                StatusText.Text = err ?? "Sign-up failed.";
                return;
            }

            // Attach bearer token for backend profile save
            if (!string.IsNullOrEmpty(_state.IdToken))
            {
                _http.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _state.IdToken);
            }

            // Send profile to your backend
            var profile = new DjProfileCreate
            {
                Uid = uid,
                Name = name!,
                Email = email!,
                Phone = phone,
                State = state,
                Address = addr
            };

            var resp = await _http.PostAsJsonAsync("api/djs", profile);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                StatusText.Text = $"Saved auth, but failed to save profile: {resp.StatusCode} {body}";
                return;
            }

            StatusText.Text = "Welcome! Your DJ account is ready.";
            // go back to Main
            if (await Nav.CanGoBack()) 
                await Nav.NavigateBackAsync(this);

            else await Nav.NavigateRouteAsync(this, "Main");
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
        }
    }

    private async void OnCancel(object sender, RoutedEventArgs e)
    {
        if (await Nav.CanGoBack()) await Nav.NavigateBackAsync(this);
        else await Nav.NavigateRouteAsync(this, "Main");
    }

    private class DjProfileCreate
    {
        public string Uid { get; set; } = default!;
        public string Name { get; set; } = default!;
        public string Email { get; set; } = default!;
        public string? Phone { get; set; }
        public string? State { get; set; }
        public string? Address { get; set; }
    }
}
