using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QMusic.Infrastructure;

namespace QMusic.Desktop;

/// <summary>
/// Application entry point. This is where the outermost ring of Clean Architecture
/// does its one unique job: composing the dependency graph.
///
/// Program.cs is the "Composition Root" — the only place that knows about ALL layers.
/// It creates the DI container, registers services, and launches the WPF app.
/// </summary>
public static class Program
{
    [STAThread]
    public static void Main()
    {
        // Build configuration from appsettings files.
        // appsettings.json = defaults (committed), appsettings.local.json = overrides with secrets (gitignored).
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.local.json", optional: true)
            .Build();

        var services = new ServiceCollection();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddInfrastructure(configuration);

        // WPF + Blazor Hybrid requires these framework services
        services.AddWpfBlazorWebView();
#if DEBUG
        services.AddBlazorWebViewDeveloperTools();
#endif

        var serviceProvider = services.BuildServiceProvider();

        var app = new App();
        app.Resources.Add("services", serviceProvider);

        var mainWindow = new MainWindow();
        mainWindow.BlazorWebView.Services = serviceProvider;
        app.Run(mainWindow);
    }
}
