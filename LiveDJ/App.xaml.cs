using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using Uno.Extensions;
using Uno.Extensions.Configuration;
using Uno.Extensions.Hosting;
using Uno.Extensions.Navigation;
using Uno.Logging;
using Uno.Resizetizer;
using LiveDJ.Presentation;

namespace LiveDJ;

public partial class App : Application
{
    public App() => InitializeComponent();

    protected Window? MainWindow { get; private set; }
    public IHost? Host { get; private set; }

    protected async override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var builder = this.CreateBuilder(args)
            .UseToolkitNavigation()
            .Configure(host => host
#if DEBUG
                .UseEnvironment(Environments.Development)
#endif
                .UseLogging((context, logBuilder) =>
                {
                    logBuilder
                        .SetMinimumLevel(context.HostingEnvironment.IsDevelopment()
                            ? LogLevel.Information
                            : LogLevel.Warning)
                        .CoreLogLevel(LogLevel.Warning);
                }, enableUnoLogging: true)

                // ⬇️ Remove EmbeddedSource/Section entirely to avoid CS1929
                //.UseConfiguration(configBuilder =>
                //    configBuilder.EmbeddedSource<App>().Section<AppConfig>()
                //)

                .UseLocalization()

                .UseHttp((context, services) =>
                {
#if DEBUG
                    services.AddTransient<DelegatingHandler, DebugHttpHandler>();
#endif
                })

                .ConfigureServices((context, services) =>
                {
                    // Bind AppConfig directly (no config builder needed)
                    services.AddOptions<AppConfig>().Configure(o =>
                    {
#if DEBUG
                        o.Environment = "DRIFT";
#else
                    o.Environment = "Prod";
#endif
                        o.ApiBaseUrl = "https://localhost:5235/"; // TODO: set env-specific URL
                    });

                    // other services here...
                })

                .UseNavigation(ReactiveViewModelMappings.ViewModelMappings, RegisterRoutes)
            );

        MainWindow = builder.Window;

#if DEBUG
        MainWindow.UseStudio();
#endif

#if WINDOWS || __SKIA__
    MainWindow.SetWindowIcon();
#endif

        Host = await builder.NavigateAsync<Shell>();
    }

    private static void RegisterRoutes(IViewRegistry views, IRouteRegistry routes)
    {
        views.Register(
            new ViewMap(ViewModel: typeof(ShellModel)),
            new ViewMap<MainPage, MainModel>(),
            new ViewMap<BookingPage>(),
            new ViewMap<StreamPage>());

        routes.Register(
            new RouteMap("",
                View: views.FindByViewModel<ShellModel>(),
                Nested:
                [
                    new ("Main",    View: views.FindByViewModel<MainModel>(), IsDefault:true),
                    new ("Booking", View: views.FindByView<BookingPage>()),
                    new ("Stream",  View: views.FindByView<StreamPage>())
                ]));
    }
}
