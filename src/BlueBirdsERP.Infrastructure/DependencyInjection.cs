using BlueBirdsERP.Application.Abstractions;
using BlueBirdsERP.Application.CustomerAccounts;
using BlueBirdsERP.Application.Inventory;
using BlueBirdsERP.Application.Notifications;
using BlueBirdsERP.Application.POS;
using BlueBirdsERP.Application.Security;
using BlueBirdsERP.Infrastructure.Configuration;
using BlueBirdsERP.Infrastructure.Notifications;
using BlueBirdsERP.Infrastructure.Persistence;
using BlueBirdsERP.Infrastructure.Printing;
using BlueBirdsERP.Infrastructure.Reporting;
using BlueBirdsERP.Infrastructure.Security;
using BlueBirdsERP.Infrastructure.Sync;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BlueBirdsERP.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        Action<InfrastructureOptions>? configure = null)
    {
        var options = new InfrastructureOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<ITemporaryPasswordGenerator, TemporaryPasswordGenerator>();
        services.AddSingleton<IWhatsAppNotificationQueue, TwilioWhatsAppNotificationService>();
        services.AddSingleton<EncryptedConfigurationStore>();
        services.AddSingleton<ISystemClock, SystemClock>();
        services.AddSingleton<ISessionService, InMemorySessionService>();
        services.AddSingleton<IRbacAuthorizationService, RbacAuthorizationService>();

        services.AddDbContext<PoultryProDbContext>(builder =>
            builder.UseSqlite(options.Database.LocalPosConnectionString));
        services.AddDbContext<LocalPosDbContext>(builder =>
            builder.UseSqlite(options.Database.LocalPosConnectionString));

        services.AddScoped<EfCoreDataStore>();
        services.AddScoped<IPOSDataStore>(provider => provider.GetRequiredService<EfCoreDataStore>());
        services.AddScoped<IInventoryDataStore>(provider => provider.GetRequiredService<EfCoreDataStore>());
        services.AddScoped<ICustomerAccountDataStore>(provider => provider.GetRequiredService<EfCoreDataStore>());
        services.AddScoped<INotificationDataStore>(provider => provider.GetRequiredService<EfCoreDataStore>());
        services.AddScoped<ISecurityDataStore>(provider => provider.GetRequiredService<EfCoreDataStore>());

        services.AddScoped<ITransactionRunner, EfTransactionRunner>();
        services.AddScoped<IAuditLogger, EfAuditLogger>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IUserManagementService, UserManagementService>();
        services.AddScoped<ILoginSessionFacade, LoginSessionFacade>();
        services.AddScoped<IApplicationBootstrapService, DevelopmentBootstrapService>();
        services.AddScoped<IRecoveryAccessService, RecoveryAccessService>();
        services.AddScoped<IPOSCheckoutService, POSCheckoutService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<ICustomerAccountService, CustomerAccountService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<ISystemSettingsService, SystemSettingsService>();
        services.AddScoped<IDatabaseManagementService, DatabaseManagementService>();
        services.AddScoped<IReceiptPdfService, ReceiptPdfService>();
        services.AddScoped<IReceiptPrinter, WindowsReceiptPrinter>();
        services.AddScoped<IReportingService, ReportingService>();
        services.AddScoped<IAuditLogReader, AuditLogReader>();
        services.AddScoped<IOfflineSyncQueue, LocalOfflineSyncQueue>();

        return services;
    }
}
