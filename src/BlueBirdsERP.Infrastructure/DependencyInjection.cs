using BlueBirdsERP.Application.Abstractions;
using BlueBirdsERP.Infrastructure.Configuration;
using BlueBirdsERP.Infrastructure.Notifications;
using BlueBirdsERP.Infrastructure.Printing;
using BlueBirdsERP.Infrastructure.Security;
using BlueBirdsERP.Infrastructure.Sync;
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
        services.AddSingleton<IWhatsAppNotificationQueue, TwilioWhatsAppNotificationService>();
        services.AddSingleton<IReceiptPrinter, WindowsReceiptPrinter>();
        services.AddSingleton<IOfflineSyncQueue, LocalOfflineSyncQueue>();
        services.AddSingleton<EncryptedConfigurationStore>();

        return services;
    }
}
