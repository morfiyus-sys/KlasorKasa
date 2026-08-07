using System.Diagnostics;
using KlasorKasa.Models;

namespace KlasorKasa.Services;

public sealed class FolderProtectionService(VaultService vaults, SystemFolderGuardService guard, LoggingService logging)
{
    public Task<ProtectedFolder> ProtectFolder(string name, string path, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Kasa adı boş olamaz.");
        guard.ValidateForProtection(path);
        return vaults.CreateVaultAsync(name.Trim(), path, token);
    }
    public Task UnlockFolder(Guid id, CancellationToken token = default) => vaults.UnlockVaultAsync(id, token);
    public Task LockFolder(Guid id, CancellationToken token = default) => vaults.LockVaultAsync(id, token);
    public Task RemoveProtection(Guid id, CancellationToken token = default) => vaults.RemoveProtectionAsync(id, token);
    public void OpenFolder(string path)
    {
        if (!Directory.Exists(path)) throw new DirectoryNotFoundException("Klasör bulunamadı.");
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
        logging.Log("OpenFolder", path, "Success");
    }
    public void HideFolder(string path) => AppPaths.TryHide(path);
    public void UnhideFolder(string path) => File.SetAttributes(path, File.GetAttributes(path) & ~(FileAttributes.Hidden | FileAttributes.System));
}
