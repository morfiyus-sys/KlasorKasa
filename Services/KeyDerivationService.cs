using System.Security.Cryptography;

namespace KlasorKasa.Services;

public sealed class KeyDerivationService
{
    public const int SaltSize = 32;
    public const int KeySize = 32;
    public const int DefaultIterations = 600_000;

    public byte[] GenerateSalt() => RandomNumberGenerator.GetBytes(SaltSize);

    public byte[] DeriveKey(string password, byte[] salt, int iterations = DefaultIterations)
    {
        if (string.IsNullOrEmpty(password)) throw new ArgumentException("Parola boş olamaz.", nameof(password));
        if (iterations < 100_000) throw new ArgumentOutOfRangeException(nameof(iterations));
        return Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, KeySize);
    }
}
