using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Uno.Extensions.Navigation;

namespace LiveDJ.Presentation;

public partial record MainModel
{
    private readonly INavigator _navigator;

    public MainModel(IStringLocalizer localizer, IOptions<AppConfig> appInfo, INavigator navigator)
    {
        _navigator = navigator;
        Title = $"Live Streaming DJ — {appInfo.Value.Environment}";
    }

    public string Title { get; }

    public async Task GoToBooking() =>
        await _navigator.NavigateViewAsync<BookingPage>(this);

    public async Task GoToStream() =>
        await _navigator.NavigateViewAsync<StreamPage>(this);
}
