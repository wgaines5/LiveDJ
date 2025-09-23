using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Uno.Extensions.Navigation;

using Windows.Foundation;
using Windows.Foundation.Collections;
using LiveDJ.Services;
using Microsoft.Extensions.Options;

namespace LiveDJ.Presentation
{
    public sealed partial class ProfilePage : Page
    {
        private readonly AuthState _state;
        private readonly HttpClient _http;
        private string _uid = "";

        public ProfilePage()
        {
            InitializeComponent();

            var sp = (Application.Current as App)!.Host!.Services;
            _state = sp.GetRequiredService<AuthState>();
            var cfg = sp.GetRequiredService<IOptions<AppConfig>>().Value;
            _http = new HttpClient { BaseAddress = new Uri(cfg.ApiBaseUrl) };
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is IDictionary<string, object> 
                p && p.TryGetValue("Uid", out var u) &&
                u is string uidFromNav &&
                !string.IsNullOrEmpty(uidFromNav))
            {
                _uid = uidFromNav;
            }
            else
            {
                // Fallback to the current signed-in user's uid
                _uid = await JwtUidAsync() ?? "";
            }

            if (string.IsNullOrEmpty(_uid)) return;

            var url = $"https://live-dj-f5fad-default-rtdb.firebaseio.com/djs/{_uid}.json?auth={_state.IdToken}";
            var dto = await _http.GetFromJsonAsync<DjProfileDto>(url);
            if (dto is null) return;


            NameText.Text = dto.name ?? "";
            EmailText.Text = dto.email ?? "";
            LocationText.Text = $"{dto.city ?? ""} {dto.state ?? ""}".Trim();
            BioText.Text = dto.bio ?? "";
            GenresList.ItemsSource = dto.genres ?? Array.Empty<string>();

            if (!string.IsNullOrWhiteSpace(dto.portfolioUrl))
                PortfolioLink.NavigateUri = new Uri(dto.portfolioUrl);

            if (!string.IsNullOrWhiteSpace(dto.photoUrl))
                AvatarBrush.ImageSource = new BitmapImage(new Uri(dto.photoUrl));
            if (!string.IsNullOrWhiteSpace(dto.backgroundUrl))
                BgImage.Source = new BitmapImage(new Uri(dto.backgroundUrl));

            base.OnNavigatedTo(e);
        }

        private async System.Threading.Tasks.Task<string?> JwtUidAsync()
        {
            if (string.IsNullOrEmpty(_state.IdToken)) return null;
            try
            {
                var parts = _state.IdToken.Split('.');
                if (parts.Length != 3) return null;
                static string Pad(string s) => s.PadRight(s.Length + (4 - s.Length % 4) % 4, '=')
                                              .Replace('-', '+').Replace('_', '/');
                var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(Pad(parts[1])));
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                return doc.RootElement.TryGetProperty("user_id", out var p) ? p.GetString() : null;
            }
            catch { return null; }
        }

        private async void OnEdit(object sender, RoutedEventArgs e)
        {
            // pass a minimal payload so CreateProfile can prefill state quickly
            var data = new System.Collections.Generic.Dictionary<string, object>
            {
                ["Uid"] = _uid
            };
            await this.Navigator().NavigateRouteAsync(this, "CreateProfile", data: data);
        }

        private sealed class DjProfileDto
        {
            public string? name { get; set; }
            public string? email { get; set; }
            public string? city { get; set; }
            public string? state { get; set; }
            public string? photoUrl { get; set; }
            public string? backgroundUrl { get; set; }
            public string? bio { get; set; }
            public string? portfolioUrl { get; set; }
            public string[]? genres { get; set; }
        }
    }
}
