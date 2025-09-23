using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

using LiveDJ.Services;

using Microsoft.Extensions.Options;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

using Uno.Extensions.Navigation;

namespace LiveDJ.Presentation
{
    public sealed partial class MainPage : Page
    {
        private HttpClient? _http;
        private AuthState? _auth;
        private AppConfig? _cfg;

        public ObservableCollection<DjListItem> Djs { get; } = new();

        public MainPage()
        {
            InitializeComponent();
        }

        private INavigator Navigator => this.Navigator();

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            var sp = (Application.Current as App)?.Host?.Services;
            if (sp is null)
            {
                await Task.Yield();
                sp = (Application.Current as App)?.Host?.Services;
                if (sp is null) return;
            }

            _auth = sp.GetRequiredService<AuthState>();
            _cfg = sp.GetRequiredService<IOptions<AppConfig>>().Value;
            _http = new HttpClient { BaseAddress = new Uri(_cfg.ApiBaseUrl) };

            await LoadDjsAsync();
        }

        public sealed class DjListItem
        {
            public string Uid { get; set; } = "";
            public string Name { get; set; } = "";
            public string? City { get; set; }
            public string? State { get; set; }

            public string Display =>
                string.IsNullOrWhiteSpace(City) && string.IsNullOrWhiteSpace(State)
                    ? Name
                    : $"{Name} — {(City ?? "").Trim()} {(State ?? "").Trim()}".Trim();
        }

        private async Task LoadDjsAsync()
        {
            if (_http is null) return;

            // Read all DJs from Firebase
            const string baseUrl = "https://live-dj-f5fad-default-rtdb.firebaseio.com/djs.json";
            var url = string.IsNullOrEmpty(_auth?.IdToken) ? baseUrl : $"{baseUrl}?auth={_auth!.IdToken}";

            Dictionary<string, DjDto>? data = null;
            try
            {
                data = await _http.GetFromJsonAsync<Dictionary<string, DjDto>>(url);
            }
            catch
            {
                // ignore network errors 
            }

            Djs.Clear();
            if (data is null) return;

            foreach (var kvp in data
                     .Where(k => !string.IsNullOrWhiteSpace(k.Value?.name))
                     .OrderBy(k => k.Value!.name))
            {
                var v = kvp.Value!;
                Djs.Add(new DjListItem
                {
                    Uid = kvp.Key,
                    Name = v.name ?? "",
                    City = v.city,
                    State = v.state
                });
            }
        }

        private sealed class DjDto
        {
            public string? name { get; set; }
            public string? city { get; set; }
            public string? state { get; set; }
            public string? email { get; set; }
            public string[]? genres { get; set; }
        }

        private async void OnDjPicked(object sender, SelectionChangedEventArgs e)
        {
            if (DjPicker.SelectedItem is not DjListItem picked) return;

            var payload = new Dictionary<string, object> { ["Uid"] = picked.Uid };
            await Navigator.NavigateRouteAsync(this, "Profile", data: payload);

            DjPicker.SelectedIndex = -1; 
        }

        private async void GoToBooking(object sender, RoutedEventArgs e) =>
            await Navigator.NavigateViewAsync<BookingPage>(this);

        private async void GoToStream(object sender, RoutedEventArgs e) =>
            await Navigator.NavigateViewAsync<StreamPage>(this);

        private async void GoToLogin(object sender, RoutedEventArgs e) =>
            await Navigator.NavigateRouteAsync(this, "Login");

        private async void GoToSignup(object sender, RoutedEventArgs e) =>
            await Navigator.NavigateRouteAsync(this, "Signup");
    }
}
