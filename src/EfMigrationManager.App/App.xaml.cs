namespace EfMigrationManager.App;

using System.IO;
using System.Windows;
using EfMigrationManager.App.ViewModels;
using EfMigrationManager.App.Views;
using EfMigrationManager.Core.Services;
using Microsoft.Build.Locator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (!MSBuildLocator.IsRegistered)
            MSBuildLocator.RegisterDefaults();

        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EfMigrationManager", "logs");
        Directory.CreateDirectory(logDir);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                Path.Combine(logDir, "app-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .CreateLogger();

        _host = Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices(ConfigureServices)
            .Build();

        await _host.StartAsync();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ISettingsService,         SettingsService>();
        services.AddSingleton<ISolutionParserService,   SolutionParserService>();
        services.AddSingleton<IProcessRunnerService,    ProcessRunnerService>();
        services.AddSingleton<IEfDiscoveryService,      EfDiscoveryService>();
        services.AddSingleton<IEfCommandBuilderService, EfCommandBuilderService>();

        services.AddSingleton<TerminalViewModel>();
        services.AddSingleton<SolutionPanelViewModel>();
        services.AddSingleton<MigrationPanelViewModel>();
        services.AddSingleton<SolutionTreeViewModel>();
        services.AddSingleton<MainViewModel>();

        services.AddSingleton<MainWindow>();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
