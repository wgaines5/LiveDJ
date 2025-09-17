using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using LiveDJ.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Uno.Extensions.Navigation;
using Microsoft.Extensions.DependencyInjection;


namespace LiveDJ.Presentation;


public sealed partial class LoginPage : Page
{
    private readonly FirebaseAuthService _auth;
    private readonly AuthState _state;


    public LoginPage()
    {
        InitializeComponent();
        var sp = (Application.Current as App)!.Host!.Services;
        _auth = sp.GetRequiredService<FirebaseAuthService>();
        _state = sp.GetRequiredService<AuthState>();
    }


    private async void OnSignIn(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Signing in…";
        var email = EmailBox.Text?.Trim();
        var pw = PasswordBox.Password ?? string.Empty;
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(pw))
        {
            StatusText.Text = "Enter email and password.";
            return;
        }
        var (ok, err) = await _auth.SignInAsync(email, pw);
        if (!ok) { StatusText.Text = err ?? "Failed"; return; }


        // Navigate to Main after successful login
        await this.Navigator().NavigateRouteAsync(this, "Main");
    }


    private async void OnForgot(object sender, RoutedEventArgs e)
    {
        // (Optional) implement sendOobCode for PASSWORD_RESET using Firebase REST
        ContentDialog dlg = new() { Title = "Coming soon", Content = "Password reset flow not implemented yet.", PrimaryButtonText = "OK" };
        await dlg.ShowAsync();
    }

    private async void OnCancel(object sender, RoutedEventArgs e)
    {
        if (await this.Navigator().CanGoBack())
            await this.Navigator().NavigateBackAsync(this);
        else
            await this.Navigator().NavigateRouteAsync(this, "Main");
    }
}
