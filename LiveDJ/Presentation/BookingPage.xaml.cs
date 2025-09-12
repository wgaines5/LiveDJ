using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Uno.Extensions;
using Uno.Extensions.Navigation;

namespace LiveDJ.Presentation;

public sealed partial class BookingPage : Page
{
    private readonly HttpClient _http;
    private Guid _bookingId;

    private static readonly Guid DemoDjId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    public BookingPage()
    {
        InitializeComponent();

        var sp = ((App)Application.Current).Host?.Services;
        var cfg = sp?.GetRequiredService<IOptions<AppConfig>>().Value;
        _http = new HttpClient { BaseAddress = new Uri(cfg?.ApiBaseUrl ?? "https://localhost:5235/") };

        DatePicker.Date = DateTimeOffset.Now;
        TimePicker.Time = TimeSpan.FromHours(DateTime.Now.Hour + 1);
    }

    private async void OnReserve(object sender, RoutedEventArgs e)
    {
        CheckoutBtn.IsEnabled = false;

        // Validate selection
        if (DurationBox.SelectedItem is not ComboBoxItem selected)
        {
            StatusText.Text = "Please select a duration.";
            return;
        }

        var date = DatePicker.Date?.DateTime ?? DateTime.Today;
        var time = TimePicker.Time;
        var localStart = date.Add(time);
        var startUtc = DateTime.SpecifyKind(localStart, DateTimeKind.Local).ToUniversalTime();

        var durationHours = int.Parse((string)selected.Tag);

        var body = new
        {
            DjId = DemoDjId,
            CustomerId = Guid.NewGuid(), // TODO: replace with authenticated user id
            StartUtc = startUtc,
            DurationHours = durationHours
        };

        try
        {
            var resp = await _http.PostAsJsonAsync("api/bookings", body);
            if (!resp.IsSuccessStatusCode)
            {
                StatusText.Text = "This time overlaps with an existing booking. Try another slot.";
                return;
            }

            var json = await resp.Content.ReadFromJsonAsync<ReserveResp>();
            if (json is null)
            {
                StatusText.Text = "Unexpected server response.";
                return;
            }

            _bookingId = json.bookingId;
            StatusText.Text = $"Reserved ✔  Booking: {_bookingId}\nStarts: {json.startUtc:u}";
            CheckoutBtn.IsEnabled = true;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Network error reserving slot: {ex.Message}";
        }
    }

    private async void OnCheckout(object sender, RoutedEventArgs e)
    {
        if (_bookingId == Guid.Empty)
        {
            StatusText.Text = "Reserve a slot first.";
            return;
        }

        try
        {
            var body = new { BookingId = _bookingId, ReturnUrl = "https://localhost:5001" }; // change for prod
            var resp = await _http.PostAsJsonAsync("api/payments/checkout", body);
            var json = await resp.Content.ReadFromJsonAsync<CheckoutResp>();
            if (json?.url is null)
            {
                StatusText.Text = "Unable to start checkout.";
                return;
            }

            _ = Windows.System.Launcher.LaunchUriAsync(new Uri(json.url));
            StatusText.Text = "Opening Stripe Checkout…";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Checkout failed: {ex.Message}";
        }
    }
    private async void OnCancel(object sender, RoutedEventArgs e)
    {
        if (await this.Navigator().CanGoBack())
            await this.Navigator().NavigateBackAsync(this);
        else
            await this.Navigator().NavigateRouteAsync(this, "Main");
    }

    private record ReserveResp(Guid bookingId, DateTime startUtc, DateTime endUtc);
    private record CheckoutResp(string url);
}
