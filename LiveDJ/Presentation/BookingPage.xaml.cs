using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using LiveDJ.Services; // AuthState
using LiveDJ.Models;
using Microsoft.Extensions.Options; // AppConfig via IOptions
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;



namespace LiveDJ.Presentation;

public sealed partial record DjListItem(string Id, string Name);

public sealed partial class BookingPage : Page
{
    private readonly HttpClient _http;
    private readonly AuthState _auth;
    private readonly AppConfig _cfg;


    private DjListItem? _selectedDj;


    public BookingPage()
    {
        InitializeComponent();


        var sp = (Application.Current as App)!.Host!.Services;
        _auth = sp.GetRequiredService<AuthState>();
        _cfg = sp.GetRequiredService<IOptions<AppConfig>>().Value;


        _http = new HttpClient { BaseAddress = new Uri(_cfg.ApiBaseUrl) };


        Loaded += async (_, __) =>
        {
            // Set sensible defaults
            DatePicker.Date = DateTimeOffset.Now.AddDays(3);
            TimePicker.Time = new TimeSpan(20, 0, 0); // 8:00 PM
            DurationBox.SelectedIndex = 0;


            await LoadDjs();
            await RefreshPricePreview();
            await RefreshAvailability();
        };
    }


    private async Task LoadDjs()
    {
        try
        {
            var djs = await _http.GetFromJsonAsync<List<DjListItem>>("api/djs");
            DjBox.Items.Clear();
            if (djs != null)
            {
                foreach (var dj in djs)
                {
                    DjBox.Items.Add(new ComboBoxItem { Content = dj.Name, Tag = dj });
                }
            }
        }
        catch (Exception ex)
        {
            ShowError($"Failed to load DJs: {ex.Message}");
        }
    }


    private async void OnDjChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DjBox.SelectedItem is not ComboBoxItem item || item.Tag is not DjListItem dj) return;

        _selectedDj = dj; // important for Refresh* methods

        DjDetails? details = null;
        try
        {
            details = await _http.GetFromJsonAsync<DjDetails>($"api/djs/{dj.Id}");
        }
        catch (Exception ex)
        {
            ShowError($"Failed to load DJ details: {ex.Message}");
            return;
        }

        if (details?.Availability != null && details.Availability.Days?.Count > 0)
        {
            AvailabilityText.Text =
                $"Available {string.Join(", ", details.Availability.Days)} " +
                $"from {details.Availability.StartHour:00}:00 to {details.Availability.EndHour:00}:00";
        }
        else
        {
            AvailabilityText.Text = "Availability not set.";
        }

        if (details?.Pricing != null)
        {
            PriceText.Text =
                $"${details.Pricing.HourlyRateCents / 100}/hr (min {details.Pricing.MinHours} hrs)";
        }
        else
        {
            PriceText.Text = "Pricing not set.";
        }

        // Keep your preview/availability flows in sync with duration/date/time selections
        await RefreshPricePreview();
        await RefreshAvailability();
    }



    private async void OnDateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
    {
        await RefreshAvailability();
    }


    private async void OnTimeChanged(object sender, Microsoft.UI.Xaml.Controls.TimePickerValueChangedEventArgs args)
    {
        await RefreshAvailability();
    }


    private async void OnRefreshAvailability(object sender, RoutedEventArgs e)
    {
        await RefreshAvailability();
    }


    private async Task RefreshPricePreview()
    {
        PriceText.Text = string.Empty;
        if (_selectedDj == null) return;
        if (DurationBox.SelectedItem is not ComboBoxItem ci || ci.Tag is null) return;


        var durationHours = int.Parse(ci.Tag.ToString()!);
        try
        {
            // Optional: ask backend for computed quote (accounts for DJ rates, fees, surge, etc.)
            var url = $"api/quotes?djId={_selectedDj.Id}&durationHours={durationHours}";
            var quote = await _http.GetFromJsonAsync<QuoteResponse>(url);
            if (quote != null)
            {
                PriceText.Text = string.Format(CultureInfo.InvariantCulture, "{0} {1:N2}", quote.Currency.ToUpperInvariant(), quote.TotalCents / 100.0);
            }
        }
        catch
        {
            // Non-fatal: pricing preview is optional
        }
    }


    private async Task RefreshAvailability()
    {
        AvailabilityText.Text = "";
        ErrorText.Text = "";
        if (_selectedDj == null) return;
        if (DatePicker.Date is null) return;


        var date = DatePicker.Date.Value.Date.ToString("yyyy-MM-dd");
        try
        {
            var avail = await _http.GetStringAsync($"api/djs/{_selectedDj.Id}/availability?date={date}");
            AvailabilityText.Text = avail;
        }
        catch (Exception ex)
        {
            ShowError($"Failed to load availability: {ex.Message}");
        }
    }


    private async void OnCheckout(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = "";
        if (!ValidateInputs(out var djId, out var eventDate, out var startTime, out var duration)) return;


        try
        {
            var body = new CreateCheckoutRequest
            {
                DjId = djId,
                EventDate = eventDate,
                StartTime = startTime,
                DurationHours = duration
            };


            using var resp = await _http.PostAsJsonAsync("api/checkout/create", body);
            resp.EnsureSuccessStatusCode();
            var json = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()).RootElement;


            var processor = json.GetProperty("processor").GetString();
            if (processor == "stripe")
            {
                var url = json.GetProperty("checkoutUrl").GetString();
                if (!string.IsNullOrEmpty(url))
                    await Windows.System.Launcher.LaunchUriAsync(new Uri(url));
            }
            else
            {
                var url = json.GetProperty("approvalUrl").GetString();
                if (!string.IsNullOrEmpty(url))
                    await Windows.System.Launcher.LaunchUriAsync(new Uri(url));
            }
        }
        catch (Exception ex)
        {
            ShowError($"Checkout failed: {ex.Message}");
        }
    }
    private void OnCancel(object sender, RoutedEventArgs e)
    {
        // Navigate back or clear fields
        if (Frame.CanGoBack)
        {
            Frame.GoBack();
        }
        else
        {
            // Reset form fields if no navigation history
            DatePicker.Date = DateTimeOffset.Now.AddDays(3);
            TimePicker.Time = new TimeSpan(20, 0, 0);
            DurationBox.SelectedIndex = 0;
            DjBox.SelectedIndex = -1;
            AvailabilityText.Text = string.Empty;
            PriceText.Text = string.Empty;
            ErrorText.Text = string.Empty;
        }
    }


    private bool ValidateInputs(out string djId, out string eventDate, out string startTime, out int duration)
    {
        djId = string.Empty; eventDate = string.Empty; startTime = string.Empty; duration = 0;


        if (_selectedDj == null)
        {
            ShowError("Please choose a DJ.");
            return false;
        }
        djId = _selectedDj.Id;


        if (DatePicker.Date is null)
        {
            ShowError("Please choose a date.");
            return false;
        }
        var date = DatePicker.Date.Value.Date; // DateTimeOffset
        eventDate = date.ToString("yyyy-MM-dd");


        var time = TimePicker.Time;
        startTime = time.ToString(@"hh\:mm");


        if (DurationBox.SelectedItem is not ComboBoxItem ci || ci.Tag is null)
        {
            ShowError("Please choose a duration.");
            return false;
        }
        duration = int.Parse(ci.Tag.ToString()!);


        return true;
    }


    private void ShowError(string message)
    {
        ErrorText.Text = message;
    }
    private sealed class QuoteResponse
    {
        public required string Currency { get; set; }
        public int TotalCents { get; set; }
    }
    private sealed class CreateCheckoutRequest
    {
        public required string DjId { get; set; }
        public required string EventDate { get; set; }
        public required string StartTime { get; set; }
        public int DurationHours { get; set; }
    }

 
    


}
