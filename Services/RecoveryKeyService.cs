using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KlasorKasa.Infrastructure;
using KlasorKasa.Models;

namespace KlasorKasa.Services;

public sealed class RecoveryKeyService(AppPaths paths, AuthenticationService authentication, EncryptionService encryption,
    KeyDerivationService kdf, SecureSession session, LoggingService logging)
{
    private static readonly byte[] Context = Encoding.UTF8.GetBytes("KlasorKasa.Recovery.v1");

    public async Task<string> CreateRecoveryKeyAsync()
    {
        var profile = await authentication.LoadProfileAsync();
        var recoveryKey = RandomNumberGenerator.GetBytes(32);
        var salt = kdf.GenerateSalt();
        var wrapKey = DeriveRecoveryWrapKey(recoveryKey, salt);
        var master = session.GetMasterKeyCopy();
        try
        {
            var nonce = encryption.GenerateNonce();
            var cipher = new byte[32];
            var tag = new byte[16];
            using (var aes = new AesGcm(wrapKey, 16)) aes.Encrypt(nonce, master, cipher, tag, Context);
            profile.Recovery = new RecoveryEnvelope
            {
                Salt = Convert.ToBase64String(salt), Nonce = Convert.ToBase64String(nonce),
                Ciphertext = Convert.ToBase64String(cipher), Tag = Convert.ToBase64String(tag)
            };
            profile.RecoveryConfirmed = false;
            await AtomicFile.WriteAllBytesAsync(paths.ProfileFile, JsonSerializer.SerializeToUtf8Bytes(profile, AuthenticationService.JsonOptions));
            logging.Log("RecoveryKeyCreated", result: "Success");
            return Convert.ToBase64String(recoveryKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(wrapKey);
            CryptographicOperations.ZeroMemory(master);
            CryptographicOperations.ZeroMemory(recoveryKey);
        }
    }

    public async Task ConfirmRecoveryKeySavedAsync()
    {
        var profile = await authentication.LoadProfileAsync();
        if (profile.Recovery is null) throw new InvalidOperationException("Kurtarma anahtarı oluşturulmamış.");
        profile.RecoveryConfirmed = true;
        await AtomicFile.WriteAllBytesAsync(paths.ProfileFile, JsonSerializer.SerializeToUtf8Bytes(profile, AuthenticationService.JsonOptions));
        logging.Log("RecoveryKeyConfirmed", result: "Success");
    }

    public async Task<bool> UnlockWithRecoveryKeyAsync(string encodedKey)
    {
        try
        {
            var profile = await authentication.LoadProfileAsync();
            if (profile.Recovery is null) return false;
            var raw = Convert.FromBase64String(encodedKey.Trim());
            if (raw.Length != 32) return false;
            var wrap = DeriveRecoveryWrapKey(raw, Convert.FromBase64String(profile.Recovery.Salt));
            var master = new byte[32];
            try
            {
                using var aes = new AesGcm(wrap, 16);
                aes.Decrypt(Convert.FromBase64String(profile.Recovery.Nonce), Convert.FromBase64String(profile.Recovery.Ciphertext),
                    Convert.FromBase64String(profile.Recovery.Tag), master, Context);
                session.SetMasterKey(master);
                logging.Log("RecoveryUnlock", result: "Success");
                return true;
            }
            finally { CryptographicOperations.ZeroMemory(raw); CryptographicOperations.ZeroMemory(wrap); CryptographicOperations.ZeroMemory(master); }
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            logging.Log("RecoveryUnlock", result: "Failure");
            return false;
        }
    }

    private static byte[] DeriveRecoveryWrapKey(byte[] recoveryKey, byte[] salt)
    {
        using var hmac = new HMACSHA256(recoveryKey);
        return hmac.ComputeHash(salt.Concat(Context).ToArray());
    }
}
