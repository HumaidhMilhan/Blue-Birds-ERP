using BlueBirdsERP.Application.Abstractions;
using BlueBirdsERP.Domain.Entities;
using BlueBirdsERP.Domain.Enums;
using BlueBirdsERP.Infrastructure.Persistence;
using BlueBirdsERP.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace BlueBirdsERP.Infrastructure.Configuration;

public sealed class SystemSettingsService(
    PoultryProDbContext dbContext,
    EncryptedConfigurationStore encryptionStore,
    IAuditLogger auditLogger) : ISystemSettingsService
{
    public async Task<IReadOnlyList<SystemSettingResult>> GetSettingsAsync(
        SystemSettingsQuery query,
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin(query.Role);

        var settings = await dbContext.SystemSettings
            .OrderBy(setting => setting.SettingKey)
            .ToListAsync(cancellationToken);
        var results = new List<SystemSettingResult>();

        foreach (var setting in settings)
        {
            var value = setting.IsSecret
                ? query.RevealSecrets ? await encryptionStore.UnprotectAsync(setting.SettingValue, cancellationToken) : "********"
                : setting.SettingValue;
            results.Add(new SystemSettingResult(
                setting.SettingKey,
                value,
                setting.ValueType,
                setting.IsSecret,
                setting.UpdatedAt));
        }

        return results;
    }

    public async Task<IReadOnlyList<SystemSettingResult>> UpdateSettingsAsync(
        UpdateSystemSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin(request.Role);
        if (request.Settings.Count == 0)
        {
            throw new InvalidOperationException("At least one setting is required.");
        }

        foreach (var update in request.Settings)
        {
            Validate(update);
            var existing = await dbContext.SystemSettings
                .SingleOrDefaultAsync(setting => setting.SettingKey == update.Key.Trim(), cancellationToken);
            var before = existing is null
                ? null
                : $"{{\"key\":\"{existing.SettingKey}\",\"valueType\":\"{existing.ValueType}\",\"isSecret\":{JsonBool(existing.IsSecret)}}}";
            var setting = existing ?? new SystemSetting { SettingKey = update.Key.Trim() };
            setting.ValueType = update.IsSecret ? SystemSettingValueType.EncryptedString : update.ValueType;
            setting.IsSecret = update.IsSecret;
            setting.SettingValue = update.IsSecret
                ? await encryptionStore.ProtectAsync(update.Value, cancellationToken)
                : update.Value.Trim();
            setting.UpdatedBy = request.UpdatedBy;
            setting.UpdatedAt = DateTimeOffset.UtcNow;

            if (existing is null)
            {
                await dbContext.SystemSettings.AddAsync(setting, cancellationToken);
            }

            await auditLogger.WriteAsync(new AuditEntry(
                request.UpdatedBy,
                request.Role,
                "SYSTEM_SETTING_UPDATE",
                "SETTINGS",
                nameof(SystemSetting),
                Guid.Empty,
                before,
                $"{{\"key\":\"{setting.SettingKey}\",\"valueType\":\"{setting.ValueType}\",\"isSecret\":{JsonBool(setting.IsSecret)}}}"), cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetSettingsAsync(new SystemSettingsQuery(request.UpdatedBy, request.Role), cancellationToken);
    }

    private static void EnsureAdmin(UserRole role)
    {
        if (role != UserRole.Admin)
        {
            throw new InvalidOperationException("Only Admin users can manage system settings.");
        }
    }

    private static void Validate(SystemSettingUpdate update)
    {
        if (string.IsNullOrWhiteSpace(update.Key))
        {
            throw new InvalidOperationException("Setting key is required.");
        }

        if (update.Value is null)
        {
            throw new InvalidOperationException("Setting value is required.");
        }
    }

    private static string JsonBool(bool value)
    {
        return value ? "true" : "false";
    }
}
