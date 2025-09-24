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
using Microsoft.Extensions.Options;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using LiveDJ.Services;
using Uno.Extensions.Navigation;

namespace LiveDJ.Presentation
{
    public sealed partial class DashboardPage : Page
    {
        private AuthState? _auth;
        private HttpClient? _http;
        private AppConfig? _cfg;

        private string _uid = "";

        public ObservableCollection<DayAvailability> Days { get; } = new();

        public DashboardPage()
        {
            InitializeComponent();

            
            DaysRepeater.ItemsSource = Days;

            
            var order = new (string key, string label)[]
            {
                ("mon","Mon"), ("tue","Tue"), ("wed","Wed"), ("thu","Thu"),
                ("fri","Fri"), ("sat","Sat"), ("sun","Sun")
            };

            foreach (var (key, label) in order)
            {
                Days.Add(new DayAvailability
                {
                    Key = key,
                    Label = label,
                    IsAvailable = false,
                    Start = TimeSpan.FromHours(18),
                    End = TimeSpan.FromHours(23)
                });
            }

            
            TzBox.ItemsSource = new[]
            {
                "UTC",
                "America/New_York",
                "America/Chicago",
                "America/Denver",
                "America/Los_Angeles",
                "Europe/London",
                "Europe/Paris"
            };
            TzBox.SelectedIndex = 1; // default to New York
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            
            var sp = (Application.Current as App)?.Host?.Services;
            if (sp is null) return;

            _auth = sp.GetRequiredService<AuthState>();
            _cfg = sp.GetRequiredService<IOptions<AppConfig>>().Value;
            _http = new HttpClient { BaseAddress = new Uri(_cfg.ApiBaseUrl) };

            // Which profile? Use explicit Uid if provided, else current user
            if (e.Parameter is IDictionary<string, object> p &&
                p.TryGetValue("Uid", out var u) &&
                u is string uidFromNav &&
                !string.IsNullOrEmpty(uidFromNav))
            {
                _uid = uidFromNav;
            }
            else
            {
                _uid = await JwtUidAsync() ?? "";
            }

            if (string.IsNullOrEmpty(_uid)) { StatusText.Text = "Missing user."; return; }

            await LoadProfileHeaderAsync();
            await LoadAvailabilityAsync();
        }

        #region Profile header

        private async Task LoadProfileHeaderAsync()
        {
            if (_http is null || _auth is null) return;

            var url = $"https://live-dj-f5fad-default-rtdb.firebaseio.com/djs/{_uid}.json?auth={_auth.IdToken}";
            DjProfileDto? dto = null;
            try { dto = await _http.GetFromJsonAsync<DjProfileDto>(url); }
            catch { /* network error – ignore for now */ }

            if (dto is null) return;

            NameText.Text = dto.Name ?? "";
            EmailText.Text = dto.Email ?? "";
            LocationText.Text = $"{dto.City ?? ""} {dto.State ?? ""}".Trim();

            if (!string.IsNullOrWhiteSpace(dto.PhotoUrl))
                AvatarBrush.ImageSource = new BitmapImage(new Uri(dto.PhotoUrl));
            if (!string.IsNullOrWhiteSpace(dto.BackgroundUrl))
                BgImage.Source = new BitmapImage(new Uri(dto.BackgroundUrl));
        }

        #endregion

        #region Availability load & save

        private async Task LoadAvailabilityAsync()
        {
            if (_http is null || _auth is null) return;

            var url = $"https://live-dj-f5fad-default-rtdb.firebaseio.com/djs/{_uid}/availability.json?auth={_auth.IdToken}";
            AvailabilityDto? dto = null;
            try { dto = await _http.GetFromJsonAsync<AvailabilityDto>(url); }
            catch { /* ignore */ }

            if (dto is null) return;

            OpenSwitch.IsOn = dto.Open ?? false;

            if (!string.IsNullOrWhiteSpace(dto.Timezone))
            {
                var tzs = (TzBox.ItemsSource as IEnumerable<string>)?.ToList() ?? new();
                var idx = tzs.FindIndex(t => t == dto.Timezone);
                if (idx >= 0) TzBox.SelectedIndex = idx;
                else
                {
                    
                    TzBox.Items.Insert(0, dto.Timezone);
                    TzBox.SelectedIndex = 0;
                }
            }

            if (dto.Days is null) return;

            // Map days
            foreach (var day in Days)
            {
                if (!dto.Days.TryGetValue(day.Key, out var slots) || slots is null || slots.Count == 0)
                {
                    day.IsAvailable = false;
                    continue;
                }

                var s = slots[0];
                day.IsAvailable = true;

                if (TimeSpan.TryParse(s.Start, out var st)) day.Start = st;
                if (TimeSpan.TryParse(s.End, out var et)) day.End = et;
            }
        }

        private async void OnSaveAvailability(object sender, RoutedEventArgs e)
        {
            if (_http is null || _auth is null) return;

            StatusText.Text = "Saving…";

            var dto = new AvailabilityDto
            {
                Open = OpenSwitch.IsOn,
                Timezone = TzBox.SelectedItem?.ToString() ?? "UTC",
                Days = Days.ToDictionary(
                    d => d.Key,
                    d => d.IsAvailable
                            ? new List<SlotDto>
                              {
                                  new SlotDto
                                  {
                                      Start = d.Start.ToString(@"hh\:mm"),
                                      End   = d.End.ToString(@"hh\:mm")
                                  }
                              }
                            : new List<SlotDto>())
            };

            var url = $"https://live-dj-f5fad-default-rtdb.firebaseio.com/djs/{_uid}/availability.json?auth={_auth.IdToken}";
            try
            {
                var resp = await _http.PutAsJsonAsync(url, dto);
                if (!resp.IsSuccessStatusCode)
                {
                    StatusText.Text = $"Save failed: {resp.StatusCode}";
                    return;
                }

                StatusText.Text = "Availability saved!";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error: {ex.Message}";
            }
        }

        #endregion

        #region Nav buttons

        private async void OnEditProfile(object sender, RoutedEventArgs e)
        {
            var payload = new Dictionary<string, object> { ["Uid"] = _uid };
            await this.Navigator().NavigateRouteAsync(this, "CreateProfile", data: payload);
        }

        private async void OnViewProfile(object sender, RoutedEventArgs e)
        {
            var payload = new Dictionary<string, object> { ["Uid"] = _uid };
            await this.Navigator().NavigateRouteAsync(this, "Profile", data: payload);
        }

        #endregion

        #region Helpers & DTOs

        // Pull current user's uid out of the Firebase ID token (user_id claim)
        private async Task<string?> JwtUidAsync()
        {
            var idToken = _auth?.IdToken;
            if (string.IsNullOrEmpty(idToken)) return null;

            try
            {
                var parts = idToken.Split('.');
                if (parts.Length != 3) return null;

                static string Pad(string s) =>
                    s.PadRight(s.Length + (4 - s.Length % 4) % 4, '=')
                     .Replace('-', '+').Replace('_', '/');

                var json = Encoding.UTF8.GetString(Convert.FromBase64String(Pad(parts[1])));
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.TryGetProperty("user_id", out var p)
                    ? p.GetString()
                    : null;
            }
            catch { return null; }
        }

        // One row in the availability list
        public sealed class DayAvailability : INotifyPropertyChanged
        {
            public string Key { get; set; } = "mon";
            public string Label { get; set; } = "Mon";

            private bool _isAvailable;
            public bool IsAvailable
            {
                get => _isAvailable;
                set { if (_isAvailable != value) { _isAvailable = value; OnPropertyChanged(); } }
            }

            private TimeSpan _start;
            public TimeSpan Start
            {
                get => _start;
                set { if (_start != value) { _start = value; OnPropertyChanged(); } }
            }

            private TimeSpan _end;
            public TimeSpan End
            {
                get => _end;
                set { if (_end != value) { _end = value; OnPropertyChanged(); } }
            }

            public event PropertyChangedEventHandler? PropertyChanged;
            private void OnPropertyChanged([CallerMemberName] string? p = null)
                => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
        }

        private sealed class DjProfileDto
        {
            public string? Name { get; set; }
            public string? Email { get; set; }
            public string? City { get; set; }
            public string? State { get; set; }
            public string? PhotoUrl { get; set; }
            public string? BackgroundUrl { get; set; }
        }

        private sealed class AvailabilityDto
        {
            public bool? Open { get; set; }
            public string? Timezone { get; set; }
            public Dictionary<string, List<SlotDto>>? Days { get; set; }
        }

        private sealed class SlotDto
        {
            public string? Start { get; set; } 
            public string? End { get; set; }  
        }

        #endregion
    }
}
