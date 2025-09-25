using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using LiveDJ.Services; // AuthState
using Microsoft.Extensions.Options; // AppConfig


namespace LiveDJ.Presentation;


public sealed partial class DashboardPage : Page
{
    private readonly HttpClient _http;
    private readonly AuthState _auth;
    private readonly AppConfig _cfg;


    public DashboardPage()
    {
        InitializeComponent();


        var sp = (Application.Current as App)!.Host!.Services;
        _auth = sp.GetRequiredService<AuthState>();
        _cfg = sp.GetRequiredService<IOptions<AppConfig>>().Value;


        _http = new HttpClient { BaseAddress = new Uri(_cfg.ApiBaseUrl) };


        Loaded += async (_, __) => await LoadPaymentSettings();
    }
    private async Task LoadPaymentSettings()
    {
        try
        {
            DashErrorText.Text = string.Empty;


            var settings = await _http.GetFromJsonAsync<PaymentSettingsResponse>("api/djs/me/payment");
            if (settings == null) return;


            // Select method
            if (settings.Method == "stripe") PaymentMethodBox.SelectedIndex = 0;
            else if (settings.Method == "paypal") PaymentMethodBox.SelectedIndex = 1;


            // Populate panels
            StripeStatusText.Text = settings.StripeStatus ?? string.Empty;
            PaypalEmailBox.Text = settings.PaypalEmail ?? string.Empty;


            TogglePanels();
        }
        catch (Exception ex)
        {
            DashErrorText.Text = $"Failed to load payment settings: {ex.Message}";
        }
    }


    private void OnPaymentMethodChanged(object sender, SelectionChangedEventArgs e)
    {
        TogglePanels();
        _ = SaveMethodOnly();
    }


    private void TogglePanels()
    {
        var method = ((ComboBoxItem?)PaymentMethodBox.SelectedItem)?.Tag?.ToString();
        StripePanel.Visibility = method == "stripe" ? Visibility.Visible : Visibility.Collapsed;
        PaypalPanel.Visibility = method == "paypal" ? Visibility.Visible : Visibility.Collapsed;
    }
    private async Task SaveMethodOnly()
    {
        try
        {
            var method = ((ComboBoxItem?)PaymentMethodBox.SelectedItem)?.Tag?.ToString();
            if (string.IsNullOrWhiteSpace(method)) return;


            var body = new { method };
            var resp = await _http.PutAsJsonAsync("api/djs/me/payment", body);
            resp.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            DashErrorText.Text = $"Failed to save method: {ex.Message}";
        }
    }


    private async void OnConnectStripe(object sender, RoutedEventArgs e)
    {
        try
        {
            var resp = await _http.PostAsync("api/stripe/connect-link", null);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            var url = json!["url"];
            await Windows.System.Launcher.LaunchUriAsync(new Uri(url));
        }
        catch (Exception ex)
        {
            DashErrorText.Text = $"Stripe connect failed: {ex.Message}";
        }
    }


    private async void OnRefreshStripeStatus(object sender, RoutedEventArgs e)
    {
        try
        {
            var s = await _http.GetFromJsonAsync<StripeStatusResponse>("api/stripe/account-status");
            StripeStatusText.Text = s?.Status ?? "";
        }
        catch (Exception ex)
        {
            DashErrorText.Text = $"Failed to refresh Stripe status: {ex.Message}";
        }
    }
    private async void OnSavePaypal(object sender, RoutedEventArgs e)
    {
        try
        {
            var email = PaypalEmailBox.Text?.Trim();
            if (string.IsNullOrEmpty(email))
            {
                DashErrorText.Text = "Enter a PayPal email.";
                return;
            }


            var body = new { method = "paypal", paypalEmail = email };
            var resp = await _http.PutAsJsonAsync("api/djs/me/payment", body);
            resp.EnsureSuccessStatusCode();
            PaypalStatusText.Text = "Saved.";
        }
        catch (Exception ex)
        {
            DashErrorText.Text = $"Failed to save PayPal: {ex.Message}";
        }
    }


    private async void OnRemovePaypal(object sender, RoutedEventArgs e)
    {
        try
        {
            var body = new { method = "paypal", paypalEmail = (string?)null };
            var resp = await _http.PutAsJsonAsync("api/djs/me/payment", body);
            resp.EnsureSuccessStatusCode();
            PaypalEmailBox.Text = string.Empty;
            PaypalStatusText.Text = "Removed.";
        }
        catch (Exception ex)
        {
            DashErrorText.Text = $"Failed to remove PayPal: {ex.Message}";
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack)
        {
            Frame.GoBack();
        }
        else
        {
            // Reset fields if no navigation history
            PaymentMethodBox.SelectedIndex = -1;
            StripeStatusText.Text = string.Empty;
            PaypalEmailBox.Text = string.Empty;
            PaypalStatusText.Text = string.Empty;
            DashErrorText.Text = string.Empty;
            StripePanel.Visibility = Visibility.Collapsed;
            PaypalPanel.Visibility = Visibility.Collapsed;
        }
    }

    private async void OnSaveAvailability(object sender, RoutedEventArgs e)
    {
        var selectedDates = AvailabilityCalendar.SelectedDates
            .Select(d => d.Date.DayOfWeek.ToString().ToLower())
            .Distinct()
            .ToList();

        var start = StartHourPicker.Time.Hours;
        var end = EndHourPicker.Time.Hours;

        var body = new
        {
            availability = new
            {
                days = selectedDates,
                startHour = start,
                endHour = end
            }
        };

        var resp = await _http.PutAsJsonAsync("api/djs/me/availability", body);
        resp.EnsureSuccessStatusCode();
    }

    private async void OnSavePricing(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(HourlyRateBox.Text, out var hourlyRate) ||
            !int.TryParse(MinHoursBox.Text, out var minHours))
        {
            DashErrorText.Text = "Invalid pricing values.";
            return;
        }

        var body = new
        {
            pricing = new
            {
                hourlyRateCents = hourlyRate * 100,
                minHours = minHours
            }
        };

        var resp = await _http.PutAsJsonAsync("api/djs/me/pricing", body);
        resp.EnsureSuccessStatusCode();
    }


    private sealed class PaymentSettingsResponse
    {
        public string? Method { get; set; } // "stripe" | "paypal"
        public string? PaypalEmail { get; set; }
        public string? StripeStatus { get; set; }
    }


    private sealed class StripeStatusResponse { public string? Status { get; set; } }
}
