namespace BlueBirdsERP.Infrastructure.Security;

public sealed class EncryptedConfigurationStore
{
    public Task<string> ProtectAsync(string plainText, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("AES-256 encrypted configuration storage will be implemented with deployment-specific key management.");
    }

    public Task<string> UnprotectAsync(string protectedText, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("AES-256 encrypted configuration storage will be implemented with deployment-specific key management.");
    }
}

