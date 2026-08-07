using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KlasorKasa.Infrastructure;
using KlasorKasa.Models;

namespace KlasorKasa.Services;

public sealed class VaultService(
    AppPaths paths,
    EncryptionService encryption,
    SecureSession session,
    WindowsIdentityService identity,
    AclService acl,
    AclBackupService aclBackups,
    LoggingService logging)
{
    private static readonly byte[] IndexContext = Encoding.UTF8.GetBytes("KlasorKasa.VaultIndex.v1");
    private readonly SemaphoreSlim _operationLock = new(1, 1);

    public async Task<IReadOnlyList<ProtectedFolder>> GetVaultsAsync()
    {
        var key = session.GetMasterKeyCopy();
        try { return await LoadIndexAsync(key); }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    public async Task<ProtectedFolder> CreateVaultAsync(string name, string sourcePath, CancellationToken token = default)
    {
        await _operationLock.WaitAsync(token);
        try
        {
            var id = Guid.NewGuid();
            var vaultPath = GetVaultPath(id);
            var stage = vaultPath + ".stage-" + Guid.NewGuid().ToString("N");
            var key = session.GetMasterKeyCopy();
            try
            {
                Directory.CreateDirectory(stage);
                acl.ApplyOwnerOnly(stage);
                var metadata = await EncryptFolderToVaultAsync(id, name, sourcePath, stage, acl.CaptureDirectorySddl(sourcePath), key, token);
                await VerifyMetadataFilesAsync(stage, metadata, key, token);
                await SaveMetadataAsync(stage, metadata, key, token);
                Directory.Move(stage, vaultPath);
                AppPaths.TryHide(vaultPath);
                acl.ApplyOwnerOnly(vaultPath);
                await aclBackups.SaveAsync(metadata, key.ToArray());

                var item = ToProtectedFolder(metadata, VaultState.Locked);
                var index = (await LoadIndexAsync(key)).ToList();
                index.Add(item);
                await SaveIndexAsync(index, key, token);

                try { DeletePlaintextTree(sourcePath); }
                catch (Exception ex)
                {
                    item.State = VaultState.Attention;
                    await SaveIndexItemAsync(item, key, token);
                    logging.Log("Protect", sourcePath, "EncryptedButPlaintextRemovalFailed", ex);
                    throw new IOException("Şifreleme tamamlandı ancak özgün klasör kaldırılamadı. Dosyalarınız korunmuştur; klasörü kullanan uygulamaları kapatın.", ex);
                }
                logging.Log("Protect", sourcePath, "Success");
                return item;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
                if (Directory.Exists(stage)) Directory.Delete(stage, true);
            }
        }
        finally { _operationLock.Release(); }
    }

    public async Task UnlockVaultAsync(Guid id, CancellationToken token = default)
    {
        await _operationLock.WaitAsync(token);
        try
        {
            var key = session.GetMasterKeyCopy();
            try
            {
                var item = await GetIndexItemAsync(id, key);
                EnsureOwner(item);
                logging.Log("UnlockStart", item.OriginalPath, "Started");
                if (item.State == VaultState.Open && Directory.Exists(item.OriginalPath)) return;
                if (Directory.Exists(item.OriginalPath)) throw new IOException("Özgün klasör yolunda başka bir klasör zaten var.");
                var metadata = await LoadMetadataAsync(id, key);
                var stage = CreateWorkingStage(metadata.OriginalPath, "opening");
                try
                {
                    foreach (var relative in metadata.EmptyDirectories) Directory.CreateDirectory(SafeCombine(stage, relative));
                    foreach (var file in metadata.Files)
                    {
                        var destination = SafeCombine(stage, file.RelativePath);
                        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                        var actualHash = await encryption.DecryptFileAsync(GetBlobPath(id, file.BlobId), destination, key.ToArray(), token);
                        if (!FixedHashEquals(actualHash, file.Sha256)) throw new CryptographicException("Dosya bütünlüğü doğrulanamadı.");
                        File.SetLastWriteTimeUtc(destination, file.LastWriteTimeUtc);
                    }
                    Directory.Move(stage, metadata.OriginalPath);
                    File.SetAttributes(metadata.OriginalPath, FileAttributes.Directory);
                    acl.ApplyOwnerOnly(metadata.OriginalPath);
                    item.State = VaultState.Open;
                    item.LastOperationUtc = DateTime.UtcNow;
                    await SaveIndexItemAsync(item, key, token);
                    logging.Log("Unlock", metadata.OriginalPath, "Success");
                }
                finally { if (Directory.Exists(stage)) Directory.Delete(stage, true); }
            }
            finally { CryptographicOperations.ZeroMemory(key); }
        }
        catch (Exception ex)
        {
            logging.Log("Unlock", result: "Failure", exception: ex);
            throw;
        }
        finally { _operationLock.Release(); }
    }

    public async Task LockVaultAsync(Guid id, CancellationToken token = default)
    {
        await _operationLock.WaitAsync(token);
        ProtectedFolder? item = null;
        byte[]? operationKey = null;
        var metadataCommitted = false;
        try
        {
            var key = session.GetMasterKeyCopy();
            operationKey = key;
            try
            {
                item = await GetIndexItemAsync(id, key);
                EnsureOwner(item);
                if (!Directory.Exists(item.OriginalPath))
                {
                    item.State = VaultState.Locked;
                    await SaveIndexItemAsync(item, key, token);
                    return;
                }
                if (item.State == VaultState.Attention)
                {
                    if (!await VerifyVaultAsync(id, token)) throw new CryptographicException("Kasa bütünlüğü doğrulanamadı.");
                    DeletePlaintextTree(item.OriginalPath);
                    item.State = VaultState.Locked;
                    item.LastOperationUtc = DateTime.UtcNow;
                    await SaveIndexItemAsync(item, key, token);
                    return;
                }
                var old = await LoadMetadataAsync(id, key);
                var vaultPath = GetVaultPath(id);
                var updated = await EncryptFolderToVaultAsync(id, item.Name, item.OriginalPath, vaultPath, old.OriginalAclSddl, key, token);
                await VerifyMetadataFilesAsync(vaultPath, updated, key, token);
                await SaveMetadataAsync(vaultPath, updated, key, token);
                metadataCommitted = true;
                DeletePlaintextTree(item.OriginalPath);
                DeleteOrphanBlobs(vaultPath, updated);
                item.State = VaultState.Locked;
                item.LastOperationUtc = DateTime.UtcNow;
                item.TotalBytes = updated.Files.Sum(f => f.Length);
                item.FileCount = updated.Files.Count;
                await SaveIndexItemAsync(item, key, token);
                AppPaths.TryHide(vaultPath);
                logging.Log("Lock", item.OriginalPath, "Success");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (metadataCommitted && item is not null && operationKey is not null)
                {
                    item.State = VaultState.Attention;
                    item.LastOperationUtc = DateTime.UtcNow;
                    await SaveIndexItemAsync(item, operationKey, token);
                }
                logging.Log("Lock", result: "Failure", exception: ex);
                throw new IOException("Dosya kullanımda olduğu için kasa kilitlenemedi. Dosyaları kullanan uygulamaları kapatıp yeniden deneyin.", ex);
            }
            finally { CryptographicOperations.ZeroMemory(key); }
        }
        finally { _operationLock.Release(); }
    }

    public async Task RemoveProtectionAsync(Guid id, CancellationToken token = default)
    {
        await _operationLock.WaitAsync(token);
        try
        {
            var key = session.GetMasterKeyCopy();
            try
            {
                var item = await GetIndexItemAsync(id, key);
                EnsureOwner(item);
                var metadata = await LoadMetadataAsync(id, key);
                if (!Directory.Exists(item.OriginalPath))
                {
                    var stage = CreateWorkingStage(item.OriginalPath, "restore");
                    try
                    {
                        foreach (var d in metadata.EmptyDirectories) Directory.CreateDirectory(SafeCombine(stage, d));
                        foreach (var file in metadata.Files)
                        {
                            var target = SafeCombine(stage, file.RelativePath);
                            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                            var hash = await encryption.DecryptFileAsync(GetBlobPath(id, file.BlobId), target, key.ToArray(), token);
                            if (!FixedHashEquals(hash, file.Sha256)) throw new CryptographicException("Dosya bütünlüğü doğrulanamadı.");
                        }
                        Directory.Move(stage, item.OriginalPath);
                    }
                    finally { if (Directory.Exists(stage)) Directory.Delete(stage, true); }
                }
                File.SetAttributes(item.OriginalPath, FileAttributes.Directory);
                acl.RestoreDirectorySddl(item.OriginalPath, metadata.OriginalAclSddl);
                var index = (await LoadIndexAsync(key)).Where(v => v.Id != id).ToList();
                await SaveIndexAsync(index, key, token);
                Directory.Delete(GetVaultPath(id), true);
                aclBackups.Remove(id);
                logging.Log("RemoveProtection", item.OriginalPath, "Success");
            }
            finally { CryptographicOperations.ZeroMemory(key); }
        }
        finally { _operationLock.Release(); }
    }

    public async Task<bool> VerifyVaultAsync(Guid id, CancellationToken token = default)
    {
        var key = session.GetMasterKeyCopy();
        try
        {
            var metadata = await LoadMetadataAsync(id, key);
            await VerifyMetadataFilesAsync(GetVaultPath(id), metadata, key, token);
            return true;
        }
        catch (CryptographicException) { return false; }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    private async Task<VaultMetadata> EncryptFolderToVaultAsync(Guid id, string name, string source, string vaultPath, string? originalAcl, byte[] key, CancellationToken token)
    {
        var blobs = Path.Combine(vaultPath, "blobs");
        Directory.CreateDirectory(blobs);
        var metadata = new VaultMetadata
        {
            VaultId = id,
            DisplayName = name,
            OriginalPath = Path.GetFullPath(source),
            OwnerSid = identity.CurrentSid,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow,
            OriginalAclSddl = originalAcl
        };
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            if (!Directory.EnumerateFileSystemEntries(directory).Any()) metadata.EmptyDirectories.Add(Path.GetRelativePath(source, directory));
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            token.ThrowIfCancellationRequested();
            var before = new FileInfo(file);
            var beforeLength = before.Length;
            var beforeWrite = before.LastWriteTimeUtc;
            var blobId = Guid.NewGuid().ToString("N");
            var hash = await encryption.EncryptFileAsync(file, Path.Combine(blobs, blobId + ".kkb"), key.ToArray(), token);
            before.Refresh();
            if (before.Length != beforeLength || before.LastWriteTimeUtc != beforeWrite)
                throw new IOException("Şifreleme sırasında bir dosya değiştirildi: " + before.Name);
            metadata.Files.Add(new VaultFileEntry
            {
                BlobId = blobId,
                RelativePath = Path.GetRelativePath(source, file),
                Length = beforeLength,
                LastWriteTimeUtc = beforeWrite,
                Sha256 = hash
            });
        }
        return metadata;
    }

    private async Task VerifyMetadataFilesAsync(string vaultPath, VaultMetadata metadata, byte[] key, CancellationToken token)
    {
        foreach (var file in metadata.Files)
            if (!await encryption.VerifyFileAsync(Path.Combine(vaultPath, "blobs", file.BlobId + ".kkb"), key.ToArray(), file.Sha256, token))
                throw new CryptographicException("Şifreleme doğrulaması başarısız oldu. Orijinal dosyalar korunmaya devam ediyor.");
    }

    private async Task SaveMetadataAsync(string vaultPath, VaultMetadata metadata, byte[] key, CancellationToken token)
    {
        var plain = JsonSerializer.SerializeToUtf8Bytes(metadata, AuthenticationService.JsonOptions);
        try
        {
            var cipher = encryption.EncryptBytes(plain, key, MetadataContext(metadata.VaultId));
            await AtomicFile.WriteAllBytesAsync(Path.Combine(vaultPath, "metadata.kkm"), cipher, token);
        }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }

    private async Task<VaultMetadata> LoadMetadataAsync(Guid id, byte[] key)
    {
        var cipher = await File.ReadAllBytesAsync(Path.Combine(GetVaultPath(id), "metadata.kkm"));
        var plain = encryption.DecryptBytes(cipher, key, MetadataContext(id));
        try { return JsonSerializer.Deserialize<VaultMetadata>(plain, AuthenticationService.JsonOptions) ?? throw new InvalidDataException("Kasa metadata bilgisi okunamadı."); }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }

    private async Task<List<ProtectedFolder>> LoadIndexAsync(byte[] key)
    {
        if (!File.Exists(paths.VaultIndexFile)) return [];
        var plain = encryption.DecryptBytes(await File.ReadAllBytesAsync(paths.VaultIndexFile), key, IndexContext);
        try { return JsonSerializer.Deserialize<List<ProtectedFolder>>(plain, AuthenticationService.JsonOptions) ?? []; }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }

    private async Task SaveIndexAsync(List<ProtectedFolder> index, byte[] key, CancellationToken token)
    {
        var plain = JsonSerializer.SerializeToUtf8Bytes(index, AuthenticationService.JsonOptions);
        try { await AtomicFile.WriteAllBytesAsync(paths.VaultIndexFile, encryption.EncryptBytes(plain, key, IndexContext), token); }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }

    private async Task<ProtectedFolder> GetIndexItemAsync(Guid id, byte[] key) =>
        (await LoadIndexAsync(key)).SingleOrDefault(v => v.Id == id) ?? throw new KeyNotFoundException("Kasa bulunamadı.");

    private async Task SaveIndexItemAsync(ProtectedFolder item, byte[] key, CancellationToken token)
    {
        var list = await LoadIndexAsync(key);
        var position = list.FindIndex(v => v.Id == item.Id);
        if (position < 0) list.Add(item); else list[position] = item;
        await SaveIndexAsync(list, key, token);
    }

    private void EnsureOwner(ProtectedFolder item)
    {
        if (!identity.IsCurrentUser(item.OwnerSid)) throw new UnauthorizedAccessException("Bu kasa başka bir Windows kullanıcısına aittir.");
    }

    private string GetVaultPath(Guid id) => Path.Combine(paths.Vaults, id.ToString("N"));
    private string GetBlobPath(Guid id, string blobId) => Path.Combine(GetVaultPath(id), "blobs", blobId + ".kkb");
    private string CreateWorkingStage(string originalPath, string operation)
    {
        var parent = Directory.GetParent(originalPath)?.FullName ?? throw new IOException("Hedef klasör yolu geçersiz.");
        Directory.CreateDirectory(parent);
        var originalVolume = Path.GetPathRoot(Path.GetFullPath(originalPath));
        var appVolume = Path.GetPathRoot(Path.GetFullPath(paths.Working));
        var basePath = string.Equals(originalVolume, appVolume, StringComparison.OrdinalIgnoreCase) ? paths.Working : parent;
        Directory.CreateDirectory(basePath);
        var stage = Path.Combine(basePath, $".{operation}-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        AppPaths.TryHide(stage);
        acl.ApplyOwnerOnly(stage);
        return stage;
    }
    private static byte[] MetadataContext(Guid id) => Encoding.UTF8.GetBytes("KlasorKasa.VaultMetadata.v1:" + id.ToString("N"));
    private static ProtectedFolder ToProtectedFolder(VaultMetadata m, VaultState state) => new()
    {
        Id = m.VaultId, Name = m.DisplayName, OriginalPath = m.OriginalPath, OwnerSid = m.OwnerSid, State = state,
        CreatedUtc = m.CreatedUtc, LastOperationUtc = m.UpdatedUtc, TotalBytes = m.Files.Sum(f => f.Length), FileCount = m.Files.Count
    };

    private static string SafeCombine(string root, string relative)
    {
        var combined = Path.GetFullPath(Path.Combine(root, relative));
        var prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!combined.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Geçersiz kasa yolu.");
        return combined;
    }

    private static bool FixedHashEquals(string a, string b)
    {
        try { return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(a), Convert.FromHexString(b)); }
        catch (FormatException) { return false; }
    }

    private static void DeletePlaintextTree(string path)
    {
        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
        foreach (var directory in Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories)) File.SetAttributes(directory, FileAttributes.Directory);
        File.SetAttributes(path, FileAttributes.Directory);
        Directory.Delete(path, true);
    }

    private static void DeleteOrphanBlobs(string vaultPath, VaultMetadata metadata)
    {
        var keep = metadata.Files.Select(f => f.BlobId + ".kkb").ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var blob in Directory.EnumerateFiles(Path.Combine(vaultPath, "blobs"), "*.kkb"))
            if (!keep.Contains(Path.GetFileName(blob))) File.Delete(blob);
    }
}
