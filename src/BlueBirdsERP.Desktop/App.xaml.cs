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
        await _host.StartAsync();

        // Ensure database tables exist
        using (var scope = _host.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PoultryProDbContext>();
            await dbContext.Database.EnsureCreatedAsync();
        }

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.StopAsync();
        _host.Dispose();

        base.OnExit(e);
    }
}
