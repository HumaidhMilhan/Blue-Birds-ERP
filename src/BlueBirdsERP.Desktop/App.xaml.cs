using System.IO;
using System.Windows;
using BlueBirdsERP.Desktop.Services;
using BlueBirdsERP.Desktop.ViewModels;
using BlueBirdsERP.Infrastructure;
using BlueBirdsERP.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BlueBirdsERP.Desktop;

public partial class App : System.Windows.Application
{
    private ServiceProvider? _serviceProvider;

    public App()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                WriteStartupFailure(exception);
            }
        };

        DispatcherUnhandledException += (_, args) =>
        {
            WriteStartupFailure(args.Exception);
            args.Handled = false;
        };
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            base.OnStartup(e);

            var dataDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BlueBirdsERP");
            Directory.CreateDirectory(dataDirectory);

            var services = new ServiceCollection();
            services.AddInfrastructure(options =>
            {
#if DEBUG
                options.EnvironmentName = "Development";
                options.DevelopmentBootstrap = new DevelopmentBootstrapOptions
                {
                    Enabled = true,
                    Username = "Kratos",
                    Password = "Kratossparta"
                };
#endif
                options.Database = new DatabaseOptions
                {
                    Provider = "SQLite",
                    LocalPosConnectionString = $"Data Source={Path.Combine(dataDirectory, "bluebirds-mvp.sqlite3")}",
                    BackupDirectory = Path.Combine(dataDirectory, "backups")
                };
            });
            services.AddScoped<DesktopStartupService>();
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<MainWindow>();

            _serviceProvider = services.BuildServiceProvider();

            using (var scope = _serviceProvider.CreateScope())
            {
                scope.ServiceProvider
                    .GetRequiredService<DesktopStartupService>()
                    .InitializeAsync()
                    .GetAwaiter()
                    .GetResult();
            }

            MainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            MainWindow.Show();
        }
        catch (Exception exception)
        {
            WriteStartupFailure(exception);
            throw;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }

    private static void WriteStartupFailure(Exception exception)
    {
        var logPath = Path.Combine(Path.GetTempPath(), "BlueBirdsERP.Desktop.startup.log");
        var message = $"[{DateTimeOffset.Now:O}]{Environment.NewLine}{exception}{Environment.NewLine}";
        File.AppendAllText(logPath, message);
    }
}
