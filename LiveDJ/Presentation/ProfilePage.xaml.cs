using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http.Json;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using LiveDJ.Services;

namespace LiveDJ.Presentation;

public sealed partial class ProfilePage : Page
{
    private readonly AuthState _state;
    private readonly HttpClient _http;

    // Values passed from Signup
    private string _uid = "";
    private string _name = "";
    private string _email = "";
    private string _stateCode = "";
    private string _city = "";

    private readonly List<string> _topAllGenres = new()
{
    "House","Techno","Hip-Hop","R&B","Pop","EDM","Trap","Drum & Bass",
    "Dubstep","Afrobeats","Amapiano","Latin","Reggaeton","Dancehall",
    "Trance","Progressive","Tech House","Deep House","Disco","Funk",
    "Lo-Fi","Chillout","Ambient","Open Format","Top 40"
};

    private readonly HashSet<string> _selectedGenres = new();

    private void UpdateGenresSummary()
    {
        if (_selectedGenres.Count == 0)
        {
            GenresPickerButton.Content = "Choose genres…";
            SelectedGenresText.Text = "None selected";
        }
        else
        {
            var list = _selectedGenres.OrderBy(g => g).ToArray();
            GenresPickerButton.Content = $"{list.Length} selected";
            SelectedGenresText.Text = string.Join(", ", list);
        }
    }

    public ProfilePage()
    {
        InitializeComponent();

        UpdateGenresSummary();

        var sp = (Application.Current as App)!.Host!.Services;
        _state = sp.GetRequiredService<AuthState>();
        var cfg = sp.GetRequiredService<IOptions<AppConfig>>().Value;
        _http = new HttpClient { BaseAddress = new Uri(cfg.ApiBaseUrl) };

        if (!string.IsNullOrEmpty(_state.IdToken))
        {
            _http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _state.IdToken);
        }


    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        // Expect a dictionary payload from SignupPage
        if (e.Parameter is IDictionary<string, object> p)
        {
            _uid = p.TryGetValue("Uid", out var u) ? u?.ToString() ?? "" : "";
            _name = p.TryGetValue("Name", out var n) ? n?.ToString() ?? "" : "";
            _email = p.TryGetValue("Email", out var m) ? m?.ToString() ?? "" : "";
            _stateCode = p.TryGetValue("State", out var s) ? s?.ToString() ?? "" : "";
        }

        StateText.Text = string.IsNullOrWhiteSpace(_stateCode) ? "(not set)" : _stateCode;
        base.OnNavigatedTo(e);
    }

    private void OnPhotoUrlChanged(object sender, TextChangedEventArgs e)
        => PhotoPreview.Source = string.IsNullOrWhiteSpace(PhotoUrlBox.Text)
            ? null
            : new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(PhotoUrlBox.Text));

    private void OnBgUrlChanged(object sender, TextChangedEventArgs e)
    {
        var has = !string.IsNullOrWhiteSpace(BgUrlBox.Text);
        BgInlinePreview.Source = has ? new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(BgUrlBox.Text)) : null;
        BgPreview.Source = has ? new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(BgUrlBox.Text)) :
                                 new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri("ms-appx:///Assets/ClubSET.png"));
    }

    private async void OnPickGenres(object sender, RoutedEventArgs e)
    {
        var lv = new ListView
        {
            SelectionMode = ListViewSelectionMode.Multiple,
            ItemsSource = _topAllGenres,
            MinHeight = 300, // so it’s scrollable and visible
            Width = 380
        };

        // preselect current choices
        foreach (var g in _selectedGenres)
            lv.SelectedItems.Add(g);

        var dlg = new ContentDialog
        {
            Title = "Select Genres",
            PrimaryButtonText = "Done",
            CloseButtonText = "Cancel",
            Content = lv
        };

/*#if WINDOWS || __SKIA__ || __ANDROID__ || __IOS__
        // avoid “XamlRoot must be set” on some targets
        dlg.XamlRoot = this.Content.XamlRoot;
#endif*/

        var res = await dlg.ShowAsync();
        if (res == ContentDialogResult.Primary)
        {
            _selectedGenres.Clear();
            foreach (var g in lv.SelectedItems.Cast<string>())
                _selectedGenres.Add(g);

            UpdateGenresSummary();
        }
    }

    private void OnGenreClicked(object sender, ItemClickEventArgs e)
    {
        
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Saving…";

        var selected = _selectedGenres.ToArray();

        var dto = new DjProfileUpsert
        {
            Uid = _uid,
            Name = _name,
            Email = _email,
            State = _stateCode,
            City = _city,
            PhotoUrl = string.IsNullOrWhiteSpace(PhotoUrlBox.Text) ? null : PhotoUrlBox.Text.Trim(),
            BackgroundUrl = string.IsNullOrWhiteSpace(BgUrlBox.Text) ? null : BgUrlBox.Text.Trim(),
            Bio = string.IsNullOrWhiteSpace(BioBox.Text) ? null : BioBox.Text.Trim(),
            Genres = selected,
            PortfolioUrl = string.IsNullOrWhiteSpace(PortfolioUrlBox.Text) ? null : PortfolioUrlBox.Text.Trim()
        };

        var dbUrl = $"https://live-dj-f5fad-default-rtdb.firebaseio.com/djs/{_uid}.json?auth={_state.IdToken}";

        var req = new HttpRequestMessage(new HttpMethod("PATCH"), dbUrl)
        { Content = JsonContent.Create(dto) };
        var resp = await _http.SendAsync(req);

        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync();
            StatusText.Text = $"Save failed: {resp.StatusCode} {body}";
            return;
        }

        StatusText.Text = "Profile saved!";
        await this.Navigator().NavigateRouteAsync(this, "Main");
    }
    

    private async void OnSkip(object sender, RoutedEventArgs e)
        => await this.Navigator().NavigateRouteAsync(this, "Main");

    private sealed class DjProfileUpsert
    {
        public string Uid { get; set; } = default!;
        public string Name { get; set; } = default!;
        public string Email { get; set; } = default!;
        public string? State { get; set; }
        public string? City {  get; set; } 
        public string? PhotoUrl { get; set; }
        public string? BackgroundUrl { get; set; }
        public string? Bio { get; set; }
        public string[]? Genres { get; set; }
        public string? PortfolioUrl { get; set; }
    }
}
