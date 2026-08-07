using System.Text;
using System.Text.Json;
using KlasorKasa.Models;

namespace KlasorKasa.Services;

public sealed class AclBackupService(AppPaths paths, EncryptionService encryption)
{
    public async Task SaveAsync(VaultMetadata metadata, byte[] masterKey)
    {
        var record = new AclBackupRecord
        {
            FolderId = metadata.VaultId,
            OriginalPath = metadata.OriginalPath,
            OwnerSid = metadata.OwnerSid,
            OperationUtc = DateTime.UtcNow,
            OriginalAclSddl = metadata.OriginalAclSddl,
            State = "Protected"
        };
        var plain = JsonSerializer.SerializeToUtf8Bytes(record, AuthenticationService.JsonOptions);
        try
        {
            var encrypted = encryption.EncryptBytes(plain, masterKey, Context(metadata.VaultId));
            await AtomicFile.WriteAllBytesAsync(Path.Combine(paths.AclBackups, metadata.VaultId.ToString("N") + ".acl"), encrypted);
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(plain);
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(masterKey);
        }
    }

    public void Remove(Guid vaultId)
    {
        var path = Path.Combine(paths.AclBackups, vaultId.ToString("N") + ".acl");
        if (File.Exists(path)) File.Delete(path);
    }

    private static byte[] Context(Guid id) => Encoding.UTF8.GetBytes("KlasorKasa.AclBackup.v1:" + id.ToString("N"));

    private sealed class AclBackupRecord
    {
        public Guid FolderId { get; set; }
        public string OriginalPath { get; set; } = string.Empty;
        public string OwnerSid { get; set; } = string.Empty;
        public DateTime OperationUtc { get; set; }
        public string? OriginalAclSddl { get; set; }
        public string State { get; set; } = string.Empty;
    }
}
