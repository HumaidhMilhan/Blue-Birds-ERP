using System.Security.Cryptography;
using BlueBirdsERP.Application.Security;

namespace BlueBirdsERP.Infrastructure.Security;

public sealed class TemporaryPasswordGenerator : ITemporaryPasswordGenerator
{
    private const int PasswordLength = 14;
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%";

    public string Generate()
    {
        return new string(Enumerable.Range(0, PasswordLength)
            .Select(_ => Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)])
            .ToArray());
    }
}
