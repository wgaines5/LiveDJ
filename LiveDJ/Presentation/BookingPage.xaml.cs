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
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

using Uno.Extensions.Navigation;
using Windows.UI; // Color.FromArgb

namespace LiveDJ.Presentation
{
    public sealed partial class BookingPage : Page
    {
        private HttpClient? _http;
        private AuthState? _auth;
        private AppConfig? _cfg;

        public ObservableCollection<DjListItem> Djs { get; } = new();

        // Availability cache for selected DJ
        private readonly Dictionary<DayOfWeek, List<SlotDto>> _slotsByDow = [];
        private bool _open = false;
        private string _timezone = "UTC";

        // brushes (avoid Microsoft.UI.Colors if it’s problematic)
        private static readonly SolidColorBrush BrushAvail =
            new(Color.FromArgb(0xFF, 0x90, 0xEE, 0x90)); // LightGreen
        private static readonly SolidColorBrush BrushBlack =
            new(Color.FromArgb(0xFF, 0x00, 0x00, 0x00));

        public BookingPage()
        {
            InitializeComponent();
            DataContext = this; // <-- enables {Binding Djs}
        }

        private INavigator Nav => this.Navigator();

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            var sp = (Application.Current as App)?.Host?.Services;
            if (sp is null) return;

            _auth = sp.GetRequiredService<AuthState>();
            _cfg = sp.GetRequiredService<IOptions<AppConfig>>().Value;
            _http = new HttpClient { BaseAddress = new Uri(_cfg.ApiBaseUrl) };

            await LoadDjsAsync();
        }

        #region Load DJs

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

        private sealed class DjDto
        {
            public string? Name { get; set; }
            public string? City { get; set; }
            public string? State { get; set; }
        }

        private async Task LoadDjsAsync()
        {
            if (_http is null) return;

            const string baseUrl = "https://live-dj-f5fad-default-rtdb.firebaseio.com/djs.json";
            var url = string.IsNullOrEmpty(_auth?.IdToken) ? baseUrl : $"{baseUrl}?auth={_auth!.IdToken}";

            Dictionary<string, DjDto>? data = null;
            try { data = await _http.GetFromJsonAsync<Dictionary<string, DjDto>>(url); }
            catch { /* ignore */ }

            Djs.Clear();
            if (data is null) return;

            foreach (var kv in data.Where(k => !string.IsNullOrWhiteSpace(k.Value?.Name))
                                   .OrderBy(k => k.Value!.Name))
            {
                var v = kv.Value!;
                Djs.Add(new DjListItem
                {
                    Uid = kv.Key,
                    Name = v.Name ?? "",
                    City = v.City,
                    State = v.State
                });
            }
        }

        #endregion

        #region Availability

        private sealed class AvailabilityDto
        {
            public bool? Open { get; set; }
            public string? Timezone { get; set; }
            public Dictionary<string, List<SlotDto>>? Days { get; set; }
        }

        internal sealed class SlotDto
        {
            public string? Start { get; set; } // "18:00"
            public string? End { get; set; } // "23:00"
        }

        private static DayOfWeek MapKeyToDow(string key) => key switch
        {
            "mon" => DayOfWeek.Monday,
            "tue" => DayOfWeek.Tuesday,
            "wed" => DayOfWeek.Wednesday,
            "thu" => DayOfWeek.Thursday,
            "fri" => DayOfWeek.Friday,
            "sat" => DayOfWeek.Saturday,
            "sun" => DayOfWeek.Sunday,
            _ => DayOfWeek.Sunday
        };

        private async Task LoadAvailabilityForAsync(string uid)
        {
            if (_http is null) return;

            _slotsByDow.Clear();
            _open = false;
            _timezone = "UTC";
            SlotsList.ItemsSource = null;
            AvailCalendar.SelectedDates.Clear();
            AvailCalendar.InvalidateArrange();

            var url = $"https://live-dj-f5fad-default-rtdb.firebaseio.com/djs/{uid}/availability.json";
            if (!string.IsNullOrEmpty(_auth?.IdToken)) url += $"?auth={_auth.IdToken}";

            AvailabilityDto? dto = null;
            try { dto = await _http.GetFromJsonAsync<AvailabilityDto>(url); }
            catch { /* ignore */ }

            if (dto is null) { StatusText.Text = "No availability found."; return; }

            _open = dto.Open ?? false;
            _timezone = dto.Timezone ?? "UTC";

            if (dto.Days is not null)
            {
                foreach (var kv in dto.Days)
                {
                    var dow = MapKeyToDow(kv.Key);
                    _slotsByDow[dow] = kv.Value ?? new List<SlotDto>();
                }
            }

            StatusText.Text = _open
                ? $"Showing availability (TZ: {_timezone})."
                : "DJ is not open to bookings.";

            // Refresh visual styling
            AvailCalendar.CalendarViewDayItemChanging -= OnCalendarDayItemChanging;
            AvailCalendar.CalendarViewDayItemChanging += OnCalendarDayItemChanging;
            AvailCalendar.InvalidateMeasure();
        }

        // Style calendar day items: highlight days that have any slots
        private void OnCalendarDayItemChanging(CalendarView sender, CalendarViewDayItemChangingEventArgs args)
        {
            var item = args.Item;
            var date = item.Date.Date;
            var has = _slotsByDow.TryGetValue(date.DayOfWeek, out var list) && list.Any();

            if (!_open)
            {
                item.IsBlackout = true;
                item.Background = null;
                item.Foreground = null;
                return;
            }

            item.IsBlackout = !has;

            if (has)
            {
                item.Background = BrushAvail;
                item.Foreground = BrushBlack;
            }
            else
            {
                item.Background = null;
                item.Foreground = null;
            }
        }

        private void OnSelectedDateChanged(CalendarView sender, CalendarViewSelectedDatesChangedEventArgs args)
        {
            if (sender.SelectedDates.Count == 0)
            {
                SlotsList.ItemsSource = null;
                return;
            }

            var date = sender.SelectedDates[0].Date;
            if (!_slotsByDow.TryGetValue(date.DayOfWeek, out var slots) || slots is null || slots.Count == 0)
            {
                SlotsList.ItemsSource = new[] { "No slots for this day." };
                return;
            }

            var lines = slots.Select(s => $"{s.Start} – {s.End} ({_timezone})").ToArray();
            SlotsList.ItemsSource = lines;
        }

        #endregion

        #region UI events

        private async void OnDjPicked(object sender, SelectionChangedEventArgs e)
        {
            if (DjPicker.SelectedItem is not DjListItem picked) return;
            await LoadAvailabilityForAsync(picked.Uid);
        }

        #endregion
    }
}
