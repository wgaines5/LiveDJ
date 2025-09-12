using Microsoft.Extensions.Options;
using Microsoft.UI.Xaml.Controls;
using System.Net.Http.Json;
using Uno.Extensions;

namespace LiveDJ.Presentation;

public sealed partial class StreamPage : Page
{
    private readonly HttpClient _http;

    public StreamPage()
    {
        InitializeComponent();
        var sp = ((App)Application.Current).Host?.Services;
        var cfg = sp?.GetRequiredService<IOptions<AppConfig>>().Value;
        _http = new HttpClient { BaseAddress = new Uri(cfg?.ApiBaseUrl ?? "https://localhost:5235/") };
    }

    protected override async void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        // If a bookingId was passed in navigation
        var bookingId = (e.Parameter as Guid?) ?? Guid.Empty;

        try
        {
            if (bookingId != Guid.Empty)
            {
                var resp = await _http.GetAsync($"api/bookings/{bookingId}");
                if (resp.IsSuccessStatusCode)
                {
                    var json = await resp.Content.ReadFromJsonAsync<BookingWithStream>();
                    var url = json?.stream?.playbackUrl;
                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        StatusText.Text = $"Playing booking {bookingId}";
                        Player.Source = new Uri(url); // if you embed your own player page, set that URL instead
                        return;
                    }
                }
            }

            // Fallback (demo)
            StatusText.Text = "Waiting for stream… (using demo URL)";
            Player.Source = new Uri($"https://youtu.be/o3CH4ZpAD_4?si=9c9PyDYFhYaE8QyH");
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Unable to load stream: {ex.Message}";
        }
    }

    private record BookingWithStream(object booking, Stream? stream);
    private record Stream(string? playbackUrl);

    private async void OnCancelLS(object sender, RoutedEventArgs e)
    {
        if (await this.Navigator().CanGoBack())
            await this.Navigator().NavigateBackAsync(this);
        else
            await this.Navigator().NavigateRouteAsync(this, "MainPage");
    }
}
