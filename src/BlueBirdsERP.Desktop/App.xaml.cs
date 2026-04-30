using System.IO;
using System.Windows;
using BlueBirdsERP.Desktop.Services;
using BlueBirdsERP.Desktop.ViewModels;
using BlueBirdsERP.Infrastructure;
using BlueBirdsERP.Infrastructure.Configuration;
using BlueBirdsERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BlueBirdsERP.Desktop;

public partial class App : System.Windows.Application
{
    private readonly IHost _host;

    public App()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddInfrastructure(opt =>
                {
                    opt.EnvironmentName = "Development";
                    opt.Database = new DatabaseOptions
                    {
                        LocalPosConnectionString = "Data Source=bluebirds-mvp.sqlite3"
                    };
                    opt.DevelopmentBootstrap = new DevelopmentBootstrapOptions
                    {
                        Enabled = true,
                        Username = "admin",
                        Password = "admin123"
                    };
                });

                services.AddSingleton<INavigationService, NavigationService>();

                // Shell
                services.AddSingleton<MainViewModel>();

                // Pages
                services.AddTransient<LoginViewModel>();
                services.AddTransient<PosCheckoutViewModel>();
                services.AddTransient<DashboardViewModel>();
                services.AddTransient<CreditorsViewModel>();
                services.AddTransient<AnalyticsViewModel>();
                services.AddTransient<InventoryViewModel>();
                services.AddTransient<SettingsViewModel>();

                // Windows
                services.AddSingleton<MainWindow>();
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        var logPath = Path.Combine(AppContext.BaseDirectory, "crash.log");

        // Global exception handlers to prevent silent crashes
        DispatcherUnhandledException += (s, args) =>
        {
            var fullError = $"[UNHANDLED {DateTime.Now:yyyy-MM-dd HH:mm:ss}]\n{args.Exception}\n\nInner: {args.Exception.InnerException}";
            System.Diagnostics.Debug.WriteLine(fullError);
            File.AppendAllText(logPath, fullError + "\n\n");
            MessageBox.Show(
                $"An unexpected error occurred:\n\n{args.Exception.Message}\n\n{args.Exception.InnerException?.Message}",
                "Application Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            var fullError = $"[FATAL {DateTime.Now:yyyy-MM-dd HH:mm:ss}]\n{ex}";
            System.Diagnostics.Debug.WriteLine(fullError);
            File.AppendAllText(logPath, fullError + "\n\n");
        };

        try
        {
            await _host.StartAsync();

            // Ensure database tables exist
            using (var scope = _host.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<PoultryProDbContext>();
                await dbContext.Database.EnsureCreatedAsync();
            }

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            File.AppendAllText(logPath, $"[STARTUP ERROR {DateTime.Now:yyyy-MM-dd HH:mm:ss}]\n{ex}\n\n");
            MessageBox.Show($"Startup failed:\n\n{ex.Message}\n\n{ex.InnerException?.Message}\n\nLog: {logPath}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.StopAsync();
        _host.Dispose();

        base.OnExit(e);
    }
}
