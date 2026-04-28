using System.Security.Cryptography;
using System.Text;
using BlueBirdsERP.Infrastructure.Configuration;

namespace BlueBirdsERP.Infrastructure.Security;

public sealed class EncryptedConfigurationStore(InfrastructureOptions options)
{
    private const string Prefix = "aes256:v1";

    public Task<string> ProtectAsync(string plainText, CancellationToken cancellationToken = default)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[16];

        using var aes = new AesGcm(GetKey(), tag.Length);
        aes.Encrypt(nonce, plainBytes, cipherBytes, tag);

        return Task.FromResult($"{Prefix}:{Convert.ToBase64String(nonce)}:{Convert.ToBase64String(cipherBytes)}:{Convert.ToBase64String(tag)}");
    }

    public Task<string> UnprotectAsync(string protectedText, CancellationToken cancellationToken = default)
    {
        if (!protectedText.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return Task.FromResult(protectedText);
        }

        var parts = protectedText.Split(':');
        if (parts.Length != 5)
        {
            throw new InvalidOperationException("Encrypted setting value is not in a supported format.");
        }

        var nonce = Convert.FromBase64String(parts[2]);
        var cipherBytes = Convert.FromBase64String(parts[3]);
        var tag = Convert.FromBase64String(parts[4]);
        var plainBytes = new byte[cipherBytes.Length];

        using var aes = new AesGcm(GetKey(), tag.Length);
        aes.Decrypt(nonce, cipherBytes, tag, plainBytes);

        return Task.FromResult(Encoding.UTF8.GetString(plainBytes));
    }

    private byte[] GetKey()
    {
        var configuredKey = options.Security.EncryptionKey;
        var keyMaterial = string.IsNullOrWhiteSpace(configuredKey)
            ? $"{Environment.MachineName}:{Environment.UserName}:BlueBirdsERP"
            : configuredKey;

        return SHA256.HashData(Encoding.UTF8.GetBytes(keyMaterial));
    }
}
