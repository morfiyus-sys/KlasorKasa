using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KlasorKasa.Infrastructure;
using KlasorKasa.Models;

namespace KlasorKasa.Services;

public sealed class AuthenticationService(
    AppPaths paths,
    KeyDerivationService kdf,
    EncryptionService encryption,
    WindowsIdentityService identity,
    SecureSession session,
    LoggingService logging,
    TimeSpan? lockoutDuration = null)
{
    private const string WrapContext = "KlasorKasa.MasterKey.v1";
    private const int MaximumFailedAttempts = 5;
    private readonly TimeSpan _lockoutDuration = lockoutDuration ?? TimeSpan.FromMinutes(1);
    private readonly SemaphoreSlim _profileLock = new(1, 1);

    public bool IsConfigured => File.Exists(paths.ProfileFile);

    public async Task CreatePasswordAsync(string password)
    {
        if (IsConfigured) throw new InvalidOperationException("Ana parola zaten oluşturulmuş.");
        ValidatePassword(password);
        var salt = kdf.GenerateSalt();
        var kek = kdf.DeriveKey(password, salt);
        var master = encryption.GenerateMasterKey();
        try
        {
            var profile = CreateWrappedProfile(master, password, salt, kek);
            await SaveProfileAsync(profile);
            session.SetMasterKey(master);
            logging.Log("PasswordCreated", result: "Success");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kek);
            CryptographicOperations.ZeroMemory(master);
        }
    }

    public async Task<bool> VerifyPasswordAsync(string password)
    {
        if (!IsConfigured) return false;

        await _profileLock.WaitAsync();
        try
        {
            var profile = await LoadProfileInternalAsync();
            if (GetRemainingLockout(profile) > TimeSpan.Zero)
            {
                logging.Log("LoginRateLimited", result: "Locked");
                return false;
            }

            if (!identity.IsCurrentUser(profile.OwnerSid))
            {
                logging.Log("LoginFailure", result: "DifferentWindowsUser");
                return false;
            }

            var kek = kdf.DeriveKey(password, Convert.FromBase64String(profile.KdfSalt), profile.KdfIterations);
            var master = new byte[32];
            try
            {
                using var aes = new AesGcm(kek, 16);
                aes.Decrypt(Convert.FromBase64String(profile.WrapNonce), Convert.FromBase64String(profile.WrappedMasterKey),
                    Convert.FromBase64String(profile.WrapTag), master, Encoding.UTF8.GetBytes(WrapContext));
                session.SetMasterKey(master);
                profile.FailedLoginAttempts = 0;
                profile.LockoutUntilUtc = null;
                await SaveProfileWithoutLockAsync(profile);
                logging.Log("LoginSuccess", result: "Success");
                return true;
            }
            catch (CryptographicException)
            {
                await RecordFailedLoginAsync(profile);
                return false;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(kek);
                CryptographicOperations.ZeroMemory(master);
            }
        }
        catch (Exception ex) when (ex is FormatException or JsonException or IOException)
        {
            logging.Log("LoginFailure", result: "ProfileReadFailure", exception: ex);
            return false;
        }
        finally
        {
            _profileLock.Release();
        }
    }

    public async Task<TimeSpan> GetLockoutRemainingAsync()
    {
        if (!IsConfigured) return TimeSpan.Zero;
        try
        {
            var profile = await LoadProfileAsync();
            return GetRemainingLockout(profile);
        }
        catch (Exception ex) when (ex is FormatException or JsonException or IOException)
        {
            logging.Log("LockoutStateRead", result: "Failure", exception: ex);
            return TimeSpan.Zero;
        }
    }

    public async Task ChangePasswordAsync(string oldPassword, string newPassword)
    {
        if (!await VerifyPasswordAsync(oldPassword)) throw new UnauthorizedAccessException("Parola yanlış.");
        await ResetPasswordFromSessionAsync(newPassword);
    }

    public async Task ResetPasswordFromSessionAsync(string newPassword)
    {
        ValidatePassword(newPassword);
        var profile = await LoadProfileAsync();
        var master = session.GetMasterKeyCopy();
        var salt = kdf.GenerateSalt();
        var kek = kdf.DeriveKey(newPassword, salt);
        try
        {
            WrapMasterKey(profile, master, salt, kek);
            profile.FailedLoginAttempts = 0;
            profile.LockoutUntilUtc = null;
            await SaveProfileAsync(profile);
            logging.Log("PasswordChanged", result: "Success");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kek);
            CryptographicOperations.ZeroMemory(master);
        }
    }

    public Task<UserProfile> LoadProfileAsync() => LoadProfileInternalAsync();

    private async Task RecordFailedLoginAsync(UserProfile profile)
    {
        profile.FailedLoginAttempts++;
        if (profile.FailedLoginAttempts >= MaximumFailedAttempts)
        {
            profile.FailedLoginAttempts = 0;
            profile.LockoutUntilUtc = DateTime.UtcNow.Add(_lockoutDuration);
            logging.Log("LoginLockout", result: "LockedForOneMinute");
        }
        else
        {
            logging.Log("LoginFailure", result: "Failure");
        }
        await SaveProfileWithoutLockAsync(profile);
        await Task.Delay(Math.Min(1000, profile.FailedLoginAttempts * 200));
    }

    private UserProfile CreateWrappedProfile(byte[] master, string password, byte[] salt, byte[] kek)
    {
        var profile = new UserProfile
        {
            OwnerSid = identity.CurrentSid,
            KdfIterations = KeyDerivationService.DefaultIterations,
            CreatedUtc = DateTime.UtcNow
        };
        WrapMasterKey(profile, master, salt, kek);
        return profile;
    }

    private void WrapMasterKey(UserProfile profile, byte[] master, byte[] salt, byte[] kek)
    {
        var nonce = encryption.GenerateNonce();
        var cipher = new byte[master.Length];
        var tag = new byte[16];
        using (var aes = new AesGcm(kek, 16))
            aes.Encrypt(nonce, master, cipher, tag, Encoding.UTF8.GetBytes(WrapContext));
        profile.KdfSalt = Convert.ToBase64String(salt);
        profile.KdfIterations = KeyDerivationService.DefaultIterations;
        profile.WrapNonce = Convert.ToBase64String(nonce);
        profile.WrappedMasterKey = Convert.ToBase64String(cipher);
        profile.WrapTag = Convert.ToBase64String(tag);
    }

    private static TimeSpan GetRemainingLockout(UserProfile profile)
    {
        if (profile.LockoutUntilUtc is null) return TimeSpan.Zero;
        var remaining = profile.LockoutUntilUtc.Value - DateTime.UtcNow;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    private async Task<UserProfile> LoadProfileInternalAsync() =>
        JsonSerializer.Deserialize<UserProfile>(await File.ReadAllBytesAsync(paths.ProfileFile), JsonOptions)
        ?? throw new InvalidDataException("Kullanıcı profili okunamadı.");

    private async Task SaveProfileAsync(UserProfile profile)
    {
        await _profileLock.WaitAsync();
        try { await SaveProfileWithoutLockAsync(profile); }
        finally { _profileLock.Release(); }
    }

    private Task SaveProfileWithoutLockAsync(UserProfile profile) =>
        AtomicFile.WriteAllBytesAsync(paths.ProfileFile, JsonSerializer.SerializeToUtf8Bytes(profile, JsonOptions));

    private static void ValidatePassword(string password)
    {
        if (password.Length < 10) throw new ArgumentException("Parola en az 10 karakter olmalıdır.");
        if (password.Length > 256) throw new ArgumentException("Parola çok uzun.");
    }

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
