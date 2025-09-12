using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Uno.Extensions.Navigation;

namespace LiveDJ.Presentation;

public sealed partial class MainPage : Page
{
    public MainPage() => InitializeComponent();

    private INavigator Navigator => this.Navigator(); // Uno.Extensions helper

    private async void GoToBooking(object sender, RoutedEventArgs e) =>
        await Navigator.NavigateViewAsync<BookingPage>(this);

    private async void GoToStream(object sender, RoutedEventArgs e) =>
        await Navigator.NavigateViewAsync<StreamPage>(this);

    private async void OnLogin(object sender, RoutedEventArgs e) =>
        await Navigator.NavigateViewAsync<StreamPage>(this);
}
