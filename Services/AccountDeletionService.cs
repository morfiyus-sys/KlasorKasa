using System.Security.Cryptography;
using KlasorKasa.Infrastructure;

namespace KlasorKasa.Services;

public sealed record AccountDeletionResult(int RestoredVaultCount, bool CleanupComplete);
public sealed class AccountDeletionPasswordException() : Exception("Parola yanlış.");

public sealed class AccountDeletionService(
    AppPaths paths,
    AuthenticationService authentication,
    VaultService vaults,
    FolderProtectionService folderProtection,
    SettingsService settings,
    SecureSession session,
    AutoLockService autoLock,
    LoggingService logging)
{
    public async Task<AccountDeletionResult> DeleteAccountAsync(string password, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(password) || !await authentication.VerifyPasswordAsync(password))
            throw new AccountDeletionPasswordException();

        var wasAutoLockRunning = autoLock.IsRunning;
        autoLock.Stop();
        var cleanupStarted = false;
        try
        {
            var registeredVaults = (await vaults.GetVaultsAsync()).ToList();
            foreach (var vault in registeredVaults)
            {
                token.ThrowIfCancellationRequested();
                await folderProtection.RemoveProtection(vault.Id, token);
            }

            var remaining = await vaults.GetVaultsAsync();
            if (remaining.Count != 0)
                throw new IOException("Tüm kasaların koruması kaldırılamadığı için hesap silinmedi.");

            if (Directory.Exists(paths.Working) && Directory.EnumerateFileSystemEntries(paths.Working).Any())
                throw new IOException("Geçici çalışma alanı boşaltılamadığı için hesap silinmedi.");

            token.ThrowIfCancellationRequested();
            settings.RemoveStartupRegistration();
            logging.Log("AccountDelete", result: $"Prepared; RestoredVaults={registeredVaults.Count}");

            var root = GetValidatedRoot();
            OverwriteAndDelete(paths.ProfileFile);
            cleanupStarted = true;
            session.Clear();
            logging.Disable();
            var cleanupComplete = true;
            try { DeleteApplicationData(root); }
            catch { cleanupComplete = false; }
            return new AccountDeletionResult(registeredVaults.Count, cleanupComplete);
        }
        catch (Exception ex)
        {
            if (!cleanupStarted) logging.Log("AccountDelete", result: "Failure", exception: ex);
            if (!cleanupStarted && wasAutoLockRunning && authentication.IsConfigured && session.IsUnlocked)
                autoLock.Start();
            throw;
        }
    }

    private string GetValidatedRoot()
    {
        var root = Path.GetFullPath(paths.Root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.IsNullOrWhiteSpace(root) || Directory.GetParent(root) is null ||
            string.Equals(root, Path.GetPathRoot(root)?.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            throw new IOException("Uygulama veri yolu güvenli biçimde doğrulanamadı.");
        if (Directory.Exists(root) && (File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Uygulama veri yolu bir bağlantıya yönlendiği için güvenli biçimde temizlenemedi.");
        return root;
    }

    private static void DeleteApplicationData(string root)
    {
        if (!Directory.Exists(root)) return;

        DeleteDirectoryContents(root);
        File.SetAttributes(root, FileAttributes.Directory);
        Directory.Delete(root, false);
    }

    private static void DeleteDirectoryContents(string directory)
    {
        foreach (var entry in Directory.EnumerateFileSystemEntries(directory, "*", SearchOption.TopDirectoryOnly).ToList())
        {
            var attributes = File.GetAttributes(entry);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                if ((attributes & FileAttributes.Directory) != 0) Directory.Delete(entry, false);
                else File.Delete(entry);
                continue;
            }

            if ((attributes & FileAttributes.Directory) != 0)
            {
                DeleteDirectoryContents(entry);
                File.SetAttributes(entry, FileAttributes.Directory);
                Directory.Delete(entry, false);
            }
            else
            {
                OverwriteAndDelete(entry);
            }
        }
    }

    private static void OverwriteAndDelete(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            File.Delete(path);
            return;
        }

        File.SetAttributes(path, FileAttributes.Normal);
        var buffer = new byte[64 * 1024];
        try
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None, buffer.Length, FileOptions.WriteThrough))
            {
                var remaining = stream.Length;
                stream.Position = 0;
                while (remaining > 0)
                {
                    RandomNumberGenerator.Fill(buffer);
                    var count = (int)Math.Min(buffer.Length, remaining);
                    stream.Write(buffer, 0, count);
                    remaining -= count;
                }
                stream.Flush(true);
                stream.SetLength(0);
            }
            File.Delete(path);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(buffer);
        }
    }
}
