using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using LiveDJ.Services;

using Microsoft.Extensions.Options;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;

using Uno.Extensions.Navigation;

using Windows.UI; // Colors

namespace LiveDJ.Presentation
{
    public sealed partial class CreateProfile : Page
    {
        private readonly AuthState _state;
        private readonly HttpClient _http;
        
        private readonly AppConfig _cfg;

        private static readonly SolidColorBrush BlackBrush =
            new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x00, 0x00));

        // Two-column source (we render controls manually)
        public ObservableCollection<GenreItem> Genres { get; } = new();

        // Values passed from SignUp or loaded from Firebase
        private string _uid = "";
        private string _name = "";
        private string _email = "";
        private string _stateCode = "";
        private string _city = "";

        public CreateProfile()
        {
            InitializeComponent();
            DataContext = this;

            var sp = (Application.Current as App)!.Host!.Services;
            _state = sp.GetRequiredService<AuthState>();
            _cfg = sp.GetRequiredService<IOptions<AppConfig>>().Value;

            _http = new HttpClient { BaseAddress = new Uri(_cfg.ApiBaseUrl) };
            if (!string.IsNullOrEmpty(_state.IdToken))
            {
                _http.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _state.IdToken);
            }

            
            string[] top =
            {
                "House","Techno","Hip-Hop","R&B","Pop","EDM","Trap","Drum & Bass",
                "Dubstep","Afrobeats","Amapiano","Latin","Reggaeton","Dancehall",
                "Trance","Progressive","Tech House","Deep House","Disco","Funk",
                "Lo-Fi","Chillout","Ambient","Open Format","Top 40"
            };
            foreach (var g in top) Genres.Add(new GenreItem(g));

            BuildGenresUI();
        }

        #region Navigation / Load existing profile

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is IDictionary<string, object> p)
            {
                _uid = p.TryGetValue("Uid", out var u) ? u?.ToString() ?? "" : _uid;
                _name = p.TryGetValue("Name", out var n) ? n?.ToString() ?? "" : _name;
                _email = p.TryGetValue("Email", out var m) ? m?.ToString() ?? "" : _email;
                _stateCode = p.TryGetValue("State", out var s) ? s?.ToString() ?? "" : _stateCode;
                _city = p.TryGetValue("City", out var c) ? c?.ToString() ?? "" : _city;
            }

            StateText.Text = string.IsNullOrWhiteSpace(_stateCode) ? "(not set)" : _stateCode;

            await LoadProfileFromFirebaseAsync();

            base.OnNavigatedTo(e);
        }

        private async Task LoadProfileFromFirebaseAsync()
        {
            if (string.IsNullOrEmpty(_state.IdToken)) return;
            if (!await EnsureUidAsync()) return;

            var dbUrl = $"https://live-dj-f5fad-default-rtdb.firebaseio.com/djs/{_uid}.json?auth={_state.IdToken}";

            try
            {
                var existing = await _http.GetFromJsonAsync<DjProfileDto>(dbUrl);
                if (existing is null) return;

                if (string.IsNullOrWhiteSpace(_name) && !string.IsNullOrWhiteSpace(existing.name)) _name = existing.name!;
                if (string.IsNullOrWhiteSpace(_email) && !string.IsNullOrWhiteSpace(existing.email)) _email = existing.email!;
                if (string.IsNullOrWhiteSpace(_stateCode) && !string.IsNullOrWhiteSpace(existing.state)) _stateCode = existing.state!;
                if (string.IsNullOrWhiteSpace(_city) && !string.IsNullOrWhiteSpace(existing.city)) _city = existing.city!;

                StateText.Text = string.IsNullOrWhiteSpace(_stateCode) ? "(not set)" : _stateCode;

                if (!string.IsNullOrWhiteSpace(existing.photoUrl)) PhotoUrlBox.Text = existing.photoUrl!;
                if (!string.IsNullOrWhiteSpace(existing.backgroundUrl)) BgUrlBox.Text = existing.backgroundUrl!;
                if (!string.IsNullOrWhiteSpace(existing.bio)) BioBox.Text = existing.bio!;
                if (!string.IsNullOrWhiteSpace(existing.portfolioUrl)) PortfolioUrlBox.Text = existing.portfolioUrl!;

                if (existing.genres is { Length: > 0 })
                {
                    var sel = existing.genres.ToHashSet(StringComparer.OrdinalIgnoreCase);
                    foreach (var g in Genres)
                        g.IsSelected = sel.Contains(g.Name);
                    BuildGenresUI(); // re-draw checkboxes to reflect selection
                }
            }
            catch
            {
                // ignore load failures; UI remains editable
            }
        }

        #endregion

        #region Image previews

        private void OnPhotoUrlChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(PhotoUrlBox.Text))
            {
                PhotoPreview.Source = null;
                return;
            }

            try
            {
                PhotoPreview.Source = new BitmapImage(new Uri(PhotoUrlBox.Text.Trim()));
            }
            catch { PhotoPreview.Source = null; }
        }

        private void OnBgUrlChanged(object sender, TextChangedEventArgs e)
        {
            var fallback = new BitmapImage(new Uri("ms-appx:///Assets/ClubSET.png"));

            if (string.IsNullOrWhiteSpace(BgUrlBox.Text))
            {
                BgInlinePreview.Source = null;
                BgPreview.Source = fallback;
                return;
            }

            try
            {
                var img = new BitmapImage(new Uri(BgUrlBox.Text.Trim()));
                BgInlinePreview.Source = img;
                BgPreview.Source = img;
            }
            catch
            {
                BgInlinePreview.Source = null;
                BgPreview.Source = fallback;
            }
        }

        #endregion

        #region Genres (two-column checkbox UI)

        public class GenreItem : INotifyPropertyChanged
        {
            public string Name { get; }
            private bool _isSelected;
            public bool IsSelected
            {
                get => _isSelected;
                set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(); } }
            }
            public GenreItem(string name) => Name = name;

            public event PropertyChangedEventHandler? PropertyChanged;
            private void OnPropertyChanged([CallerMemberName] string? p = null)
                => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
        }

        private void BuildGenresUI()
        {
            LeftGenresStack.Children.Clear();
            RightGenresStack.Children.Clear();

            int half = (Genres.Count + 1) / 2;

            for (int i = 0; i < Genres.Count; i++)
            {
                var item = Genres[i];

                var label = new TextBlock
                {
                    Text = item.Name,
                    FontSize = 16,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = BlackBrush              
                };

                var cb = new CheckBox
                {
                    IsChecked = item.IsSelected,
                    Margin = new Thickness(0, 4, 0, 4),
                    VerticalAlignment = VerticalAlignment.Center,
                    Content = label,
                    Foreground = BlackBrush
                };

                cb.Checked += (_, __) => item.IsSelected = true;
                cb.Unchecked += (_, __) => item.IsSelected = false;

                if (i < half) LeftGenresStack.Children.Add(cb);
                else RightGenresStack.Children.Add(cb);
            }
        }

        #endregion

        #region Save / Skip

        private async void OnSave(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "Saving…";

            if (string.IsNullOrEmpty(_state.IdToken))
            {
                StatusText.Text = "Not authenticated.";
                return;
            }

            if (!await EnsureUidAsync())
            {
                StatusText.Text = "Missing user id.";
                return;
            }

            var selectedGenres = Genres.Where(g => g.IsSelected).Select(g => g.Name).ToArray();

            // Only send fields that actually have values
            var patch = new Dictionary<string, object?>();

            if (!string.IsNullOrWhiteSpace(_name)) patch["name"] = _name;
            if (!string.IsNullOrWhiteSpace(_email)) patch["email"] = _email;
            if (!string.IsNullOrWhiteSpace(_stateCode)) patch["state"] = _stateCode;
            if (!string.IsNullOrWhiteSpace(_city)) patch["city"] = _city;

            if (!string.IsNullOrWhiteSpace(PhotoUrlBox.Text)) patch["photoUrl"] = PhotoUrlBox.Text.Trim();
            if (!string.IsNullOrWhiteSpace(BgUrlBox.Text)) patch["backgroundUrl"] = BgUrlBox.Text.Trim();
            if (!string.IsNullOrWhiteSpace(BioBox.Text)) patch["bio"] = BioBox.Text.Trim();
            if (!string.IsNullOrWhiteSpace(PortfolioUrlBox.Text)) patch["portfolioUrl"] = PortfolioUrlBox.Text.Trim();
            if (selectedGenres.Length > 0) patch["genres"] = selectedGenres;

            if (patch.Count == 0)
            {
                StatusText.Text = "Nothing to update.";
                return;
            }

            var dbUrl = $"https://live-dj-f5fad-default-rtdb.firebaseio.com/djs/{_uid}.json?auth={_state.IdToken}";

            try
            {
                using var req = new HttpRequestMessage(new HttpMethod("PATCH"), dbUrl)
                {
                    Content = JsonContent.Create(
                        patch,
                        options: new JsonSerializerOptions
                        {
                            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                        })
                };

                using var resp = await _http.SendAsync(req);
                if (!resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync();
                    StatusText.Text = $"Save failed: {resp.StatusCode} {body}";
                    return;
                }

                StatusText.Text = "Profile saved!";
                await this.Navigator().NavigateRouteAsync(this, "Main");
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error: {ex.Message}";
            }
        }

        private async void OnSkip(object sender, RoutedEventArgs e)
            => await this.Navigator().NavigateRouteAsync(this, "Main");

        #endregion

        #region Firebase helpers / DTOs

        private async Task<bool> EnsureUidAsync()
        {
            if (!string.IsNullOrEmpty(_uid)) return true;

            // Try to decode JWT for user_id/email
            if (!string.IsNullOrEmpty(_state.IdToken))
            {
                try
                {
                    var parts = _state.IdToken.Split('.');
                    if (parts.Length == 3)
                    {
                        static string Pad(string s)
                            => s.PadRight(s.Length + (4 - s.Length % 4) % 4, '=')
                                .Replace('-', '+').Replace('_', '/');

                        var payloadJson = Encoding.UTF8.GetString(Convert.FromBase64String(Pad(parts[1])));
                        using var doc = JsonDocument.Parse(payloadJson);

                        if (string.IsNullOrEmpty(_uid) &&
                            doc.RootElement.TryGetProperty("user_id", out var uidProp))
                            _uid = uidProp.GetString() ?? "";

                        if (string.IsNullOrEmpty(_email) &&
                            doc.RootElement.TryGetProperty("email", out var emailProp))
                            _email = emailProp.GetString() ?? "";
                    }
                }
                catch { /* ignore */ }
            }

            return !string.IsNullOrEmpty(_uid);
        }

        private sealed class DjProfileDto
        {
            public string? uid { get; set; }
            public string? name { get; set; }
            public string? email { get; set; }
            public string? phone { get; set; }
            public string? address { get; set; }
            public string? city { get; set; }
            public string? state { get; set; }
            public string? photoUrl { get; set; }
            public string? backgroundUrl { get; set; }
            public string? bio { get; set; }
            public string? portfolioUrl { get; set; }
            public string[]? genres { get; set; }
        }

        #endregion
    }
}
