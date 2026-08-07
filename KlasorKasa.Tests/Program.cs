using System.Security.Cryptography;
using KlasorKasa.Models;
using KlasorKasa.Services;

var tests = new List<(string Name, Func<Task> Run)>
{
    ("Doğru parola giriş yapar", TestCorrectPassword),
    ("Yanlış parola reddedilir", TestWrongPassword),
    ("AES-GCM tek byte tahrifini reddeder", TestTamperDetection),
    ("Parola değişince eski parola reddedilir", TestPasswordChange),
    ("Aç-değiştir-kilitle-aç değişikliği korur", TestVaultLifecycle),
    ("Şifreleme doğrulanmadan kaynak silinmez", TestPreCommitSafety),
    ("Disk kökü ve sistem klasörü reddedilir", TestSystemGuard),
    ("NTFS ACL yalnızca sahip ve SYSTEM erişimi verir", TestOwnerAcl),
    ("Korumayı kaldır klasörü görünür ve normal bırakır", TestRemoveProtectionRestoresNormalFolder),
    ("Yeni kasa adı seçilen klasörden otomatik üretilir", TestCreateVaultNameIsDerived),
    ("Tümünü aç ve tümünü kilitle bütün kasaları işler", TestUnlockAndLockAll),
    ("Kasa bloblarında plaintext ad ve içerik yoktur", TestMetadataConfidentiality)
};

tests.Add(("Beş yanlış giriş kalıcı bir dakikalık kilit oluşturur", TestPersistentLoginLockout));
tests.Add(("Kurtarma anahtarıyla parola kasaları yeniden şifrelemeden yenilenir", TestRecoveryPasswordReset));
tests.Add(("Geçersiz kurtarma anahtarı reddedilir", TestInvalidRecoveryKey));
tests.Add(("Hesabı sil tüm kasaları korumasız geri getirir", TestDeleteAccountRestoresAllVaults));
tests.Add(("Hesabı sil yanlış parolada hiçbir veriyi değiştirmez", TestDeleteAccountRejectsWrongPassword));
tests.Add(("Hesabı sil yol çakışmasında veri kaybını önler", TestDeleteAccountStopsOnPathConflict));

var passed = 0;
foreach (var test in tests)
{
    try { await test.Run(); Console.WriteLine($"PASS  {test.Name}"); passed++; }
    catch (Exception ex) { Console.WriteLine($"FAIL  {test.Name}: {ex.GetType().Name} - {ex.Message}"); }
}
Console.WriteLine($"RESULT {passed}/{tests.Count}");
return passed == tests.Count ? 0 : 1;

static async Task TestCorrectPassword()
{
    await WithServices(async services =>
    {
        await services.Authentication.CreatePasswordAsync("Guclu-Parola-2026!");
        services.Session.Clear();
        Assert(await services.Authentication.VerifyPasswordAsync("Guclu-Parola-2026!"), "Doğru parola kabul edilmedi.");
    });
}

static async Task TestWrongPassword()
{
    await WithServices(async services =>
    {
        await services.Authentication.CreatePasswordAsync("Guclu-Parola-2026!"); services.Session.Clear();
        Assert(!await services.Authentication.VerifyPasswordAsync("Yanlis-Parola!"), "Yanlış parola kabul edildi.");
    });
}

static async Task TestTamperDetection()
{
    var root = NewTemp();
    try
    {
        var input = Path.Combine(root, "plain.txt"); var blob = Path.Combine(root, "blob.kkb");
        await File.WriteAllTextAsync(input, "bütünlük kontrolü için içerik");
        var service = new EncryptionService(); var key = service.GenerateMasterKey();
        var hash = await service.EncryptFileAsync(input, blob, key.ToArray());
        var bytes = await File.ReadAllBytesAsync(blob); bytes[^5] ^= 0x01; await File.WriteAllBytesAsync(blob, bytes);
        Assert(!await service.VerifyFileAsync(blob, key.ToArray(), hash), "Tahrif edilmiş dosya doğrulandı.");
        CryptographicOperations.ZeroMemory(key);
    }
    finally { Cleanup(root); }
}

static async Task TestPasswordChange()
{
    await WithServices(async services =>
    {
        await services.Authentication.CreatePasswordAsync("Eski-Parola-2026!");
        await services.Authentication.ChangePasswordAsync("Eski-Parola-2026!", "Yeni-Parola-2026!");
        services.Session.Clear(); Assert(!await services.Authentication.VerifyPasswordAsync("Eski-Parola-2026!"), "Eski parola çalıştı.");
        Assert(await services.Authentication.VerifyPasswordAsync("Yeni-Parola-2026!"), "Yeni parola çalışmadı.");
    });
}

static async Task TestVaultLifecycle()
{
    await WithServices(async services =>
    {
        await services.Authentication.CreatePasswordAsync("Guclu-Parola-2026!");
        var source = NewTemp();
        try
        {
            Directory.CreateDirectory(Path.Combine(source, "alt"));
            await File.WriteAllTextAsync(Path.Combine(source, "alt", "belge.txt"), "ilk sürüm");
            var vault = await services.FolderProtection.ProtectFolder("Test Kasası", source);
            Assert(!Directory.Exists(source), "Plaintext kaynak kaldırılmadı.");
            Assert(await services.Vaults.VerifyVaultAsync(vault.Id), "Kasa doğrulanamadı.");
            await services.FolderProtection.UnlockFolder(vault.Id);
            Assert(await File.ReadAllTextAsync(Path.Combine(source, "alt", "belge.txt")) == "ilk sürüm", "İlk içerik bozuk.");
            Assert(!Directory.EnumerateFileSystemEntries(services.Paths.Working).Any(), "Geçici çalışma alanı temizlenmedi.");
            await File.WriteAllTextAsync(Path.Combine(source, "alt", "belge.txt"), "değişen sürüm");
            await services.FolderProtection.LockFolder(vault.Id);
            await services.FolderProtection.UnlockFolder(vault.Id);
            Assert(await File.ReadAllTextAsync(Path.Combine(source, "alt", "belge.txt")) == "değişen sürüm", "Değişiklik korunmadı.");
            await services.FolderProtection.RemoveProtection(vault.Id);
            Assert(Directory.Exists(source), "Koruma kaldırmada klasör geri gelmedi.");
        }
        finally { Cleanup(source); }
    });
}

static async Task TestPreCommitSafety()
{
    var root = NewTemp();
    try
    {
        var source = Path.Combine(root, "source.txt"); var blob = Path.Combine(root, "bad.kkb");
        await File.WriteAllTextAsync(source, "asıl veri");
        var service = new EncryptionService(); var key = service.GenerateMasterKey();
        var hash = await service.EncryptFileAsync(source, blob, key.ToArray());
        var data = await File.ReadAllBytesAsync(blob); data[^1] ^= 0x80; await File.WriteAllBytesAsync(blob, data);
        var verified = await service.VerifyFileAsync(blob, key.ToArray(), hash);
        if (verified) File.Delete(source);
        Assert(File.Exists(source) && await File.ReadAllTextAsync(source) == "asıl veri", "Kaynak veri erken silindi.");
    }
    finally { Cleanup(root); }
}

static Task TestSystemGuard()
{
    var root = NewTemp();
    try
    {
        var paths = new AppPaths(Path.Combine(root, "appdata")); var guard = new SystemFolderGuardService(paths);
        AssertThrows<ArgumentException>(() => guard.ValidateForProtection(Path.GetPathRoot(Environment.SystemDirectory)!));
        AssertThrows<ArgumentException>(() => guard.ValidateForProtection(Environment.GetFolderPath(Environment.SpecialFolder.Windows)));
        return Task.CompletedTask;
    }
    finally { Cleanup(root); }
}

static Task TestOwnerAcl()
{
    var root = NewTemp();
    try
    {
        var data = Path.Combine(root, "appdata");
        var services = new AppServices(data);
        var target = Path.Combine(root, "acl-target"); Directory.CreateDirectory(target);
        services.Acl.ApplyOwnerOnly(target);
        Assert(!string.IsNullOrWhiteSpace(services.Acl.CaptureDirectorySddl(target)), "ACL yedeği alınamadı.");
        var security = new DirectoryInfo(target).GetAccessControl(System.Security.AccessControl.AccessControlSections.Access);
        var rules = security.GetAccessRules(true, true, typeof(System.Security.Principal.SecurityIdentifier))
            .Cast<System.Security.AccessControl.FileSystemAccessRule>().ToList();
        var allowed = rules.Where(r => r.AccessControlType == System.Security.AccessControl.AccessControlType.Allow).Select(r => r.IdentityReference.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert(allowed.Contains(services.Identity.CurrentSid), "Kasa sahibi ACL içinde değil.");
        Assert(allowed.Contains("S-1-5-18"), "SYSTEM ACL içinde değil.");
        Assert(!allowed.Contains("S-1-5-32-545") && !allowed.Contains("S-1-5-32-544"), "Users veya Administrators otomatik yetkilendirildi.");
        services.Acl.RestoreDirectorySddl(target, null);
        Assert(!new DirectoryInfo(target).GetAccessControl().AreAccessRulesProtected, "ACL fallback devralmayı etkinleştirmedi.");
        return Task.CompletedTask;
    }
    finally { Cleanup(root); }
}

static async Task TestRemoveProtectionRestoresNormalFolder()
{
    await WithServices(async services =>
    {
        await services.Authentication.CreatePasswordAsync("Guclu-Parola-2026!");
        var source = NewTemp();
        try
        {
            var expected = "koruma kaldırma doğrulaması";
            await File.WriteAllTextAsync(Path.Combine(source, "belge.txt"), expected);
            var vault = await services.FolderProtection.ProtectFolder("Kaldırma Testi", source);
            Assert(!Directory.Exists(source), "Kasa kilitlenmedi.");
            await services.FolderProtection.RemoveProtection(vault.Id);
            Assert(Directory.Exists(source), "Klasör geri yüklenmedi.");
            var attributes = File.GetAttributes(source);
            Assert(!attributes.HasFlag(FileAttributes.Hidden) && !attributes.HasFlag(FileAttributes.System), "Hidden/System öznitelikleri temizlenmedi.");
            Assert(!new DirectoryInfo(source).GetAccessControl().AreAccessRulesProtected, "Normal ACL devralması etkinleştirilmedi.");
            Assert(await File.ReadAllTextAsync(Path.Combine(source, "belge.txt")) == expected, "Geri yüklenen içerik bozuk.");
            Assert(!(await services.Vaults.GetVaultsAsync()).Any(v => v.Id == vault.Id), "Kasa indeksi kaldırılmadı.");
            Assert(!Directory.Exists(Path.Combine(services.Paths.Vaults, vault.Id.ToString("N"))), "Şifreli vault kaydı kaldırılmadı.");
        }
        finally { Cleanup(source); }
    });
}

static Task TestCreateVaultNameIsDerived()
{
    var root = NewTemp();
    try
    {
        var selected = Path.Combine(root, "Belgelerim");
        Directory.CreateDirectory(selected);
        var viewModel = new KlasorKasa.ViewModels.CreateVaultViewModel { FolderPath = selected };
        Assert(viewModel.Name == "Belgelerim", "Kasa adı klasör adından türetilmedi.");
        Assert(viewModel.HasSelection, "Geçerli klasör seçimi onaylanmadı.");
        bool? closeResult = null;
        viewModel.CloseRequested += (_, result) => closeResult = result;
        viewModel.CreateCommand.Execute(null);
        Assert(closeResult == true, "DEVAM komutu pencereyi onayla kapatmadı.");
        return Task.CompletedTask;
    }
    finally { Cleanup(root); }
}

static async Task TestUnlockAndLockAll()
{
    await WithServices(async services =>
    {
        await services.Authentication.CreatePasswordAsync("Guclu-Parola-2026!");
        var first = NewTemp();
        var second = NewTemp();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(first, "bir.txt"), "bir");
            await File.WriteAllTextAsync(Path.Combine(second, "iki.txt"), "iki");
            await services.FolderProtection.ProtectFolder("Bir", first);
            await services.FolderProtection.ProtectFolder("İki", second);
            var viewModel = new KlasorKasa.ViewModels.VaultsViewModel(services);
            await viewModel.RefreshAsync();
            await viewModel.UnlockAllAsync();
            Assert(Directory.Exists(first) && Directory.Exists(second), "Tümünü Aç bütün kasaları açmadı.");
            await viewModel.LockAllAsync();
            Assert(!Directory.Exists(first) && !Directory.Exists(second), "Tümünü Kilitle bütün kasaları kilitlemedi.");
            Assert((await services.Vaults.GetVaultsAsync()).All(v => v.State == VaultState.Locked), "Kasa durumları kilitli olmadı.");
        }
        finally { Cleanup(first); Cleanup(second); }
    });
}

static async Task TestMetadataConfidentiality()
{
    await WithServices(async services =>
    {
        await services.Authentication.CreatePasswordAsync("Guclu-Parola-2026!");
        var source = NewTemp(); var secretName = "cok-gizli-belge.txt"; var secretContent = "PLAIN-MARKER-99118";
        try
        {
            await File.WriteAllTextAsync(Path.Combine(source, secretName), secretContent);
            var vault = await services.FolderProtection.ProtectFolder("Gizli", source);
            var vaultDir = Path.Combine(services.Paths.Vaults, vault.Id.ToString("N"));
            var names = Directory.EnumerateFiles(vaultDir, "*", SearchOption.AllDirectories).Select(Path.GetFileName).ToArray();
            Assert(!names.Any(n => n!.Contains(secretName, StringComparison.OrdinalIgnoreCase)), "Plaintext dosya adı sızdı.");
            foreach (var file in Directory.EnumerateFiles(vaultDir, "*", SearchOption.AllDirectories))
                Assert(!System.Text.Encoding.UTF8.GetString(await File.ReadAllBytesAsync(file)).Contains(secretContent), "Plaintext içerik sızdı.");
        }
        finally { Cleanup(source); }
    });
}

static async Task TestPersistentLoginLockout()
{
    var root = NewTemp();
    try
    {
        var dataRoot = Path.Combine(root, "appdata");
        var firstRun = new AppServices(dataRoot, TimeSpan.FromSeconds(2));
        await firstRun.Authentication.CreatePasswordAsync("Guclu-Parola-2026!");
        firstRun.Session.Clear();
        for (var attempt = 0; attempt < 5; attempt++)
            Assert(!await firstRun.Authentication.VerifyPasswordAsync("Yanlis-Parola!"), "Yanlış parola kabul edildi.");

        Assert(await firstRun.Authentication.GetLockoutRemainingAsync() > TimeSpan.Zero, "Beşinci hatada kilit oluşmadı.");

        var restarted = new AppServices(dataRoot, TimeSpan.FromSeconds(2));
        Assert(!await restarted.Authentication.VerifyPasswordAsync("Guclu-Parola-2026!"), "Uygulama yeniden başlatılarak kilit aşıldı.");
        await Task.Delay(2200);
        Assert(await restarted.Authentication.VerifyPasswordAsync("Guclu-Parola-2026!"), "Süre dolunca doğru parola kabul edilmedi.");
    }
    finally { Cleanup(root); }
}

static async Task TestRecoveryPasswordReset()
{
    await WithServices(async services =>
    {
        await services.Authentication.CreatePasswordAsync("Eski-Parola-2026!");
        var recoveryKey = await services.Recovery.CreateRecoveryKeyAsync();
        services.Session.Clear();
        Assert(await services.Recovery.UnlockWithRecoveryKeyAsync(recoveryKey), "Kurtarma anahtarı Master Key'i açamadı.");
        await services.Authentication.ResetPasswordFromSessionAsync("Yeni-Parola-2026!");
        services.Session.Clear();
        Assert(!await services.Authentication.VerifyPasswordAsync("Eski-Parola-2026!"), "Eski parola sıfırlamadan sonra çalıştı.");
        Assert(await services.Authentication.VerifyPasswordAsync("Yeni-Parola-2026!"), "Yeni parola sıfırlamadan sonra çalışmadı.");
    });
}

static async Task TestInvalidRecoveryKey()
{
    await WithServices(async services =>
    {
        await services.Authentication.CreatePasswordAsync("Guclu-Parola-2026!");
        await services.Recovery.CreateRecoveryKeyAsync();
        services.Session.Clear();
        var invalidKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        Assert(!await services.Recovery.UnlockWithRecoveryKeyAsync(invalidKey), "Geçersiz kurtarma anahtarı kabul edildi.");
        Assert(!services.Session.IsUnlocked, "Geçersiz anahtar oturumu açtı.");
    });
}

static async Task TestDeleteAccountRestoresAllVaults()
{
    var root = NewTemp();
    var dataRoot = Path.Combine(root, "appdata");
    var lockedSource = Path.Combine(root, "kilitli");
    var openSource = Path.Combine(root, "acik");
    try
    {
        var services = new AppServices(dataRoot);
        await services.Authentication.CreatePasswordAsync("Guclu-Parola-2026!");
        Directory.CreateDirectory(lockedSource);
        Directory.CreateDirectory(openSource);
        await File.WriteAllTextAsync(Path.Combine(lockedSource, "kilitli.txt"), "kilitli içerik");
        await File.WriteAllTextAsync(Path.Combine(openSource, "acik.txt"), "ilk içerik");

        await services.FolderProtection.ProtectFolder("Kilitli", lockedSource);
        var openVault = await services.FolderProtection.ProtectFolder("Açık", openSource);
        await services.FolderProtection.UnlockFolder(openVault.Id);
        await File.WriteAllTextAsync(Path.Combine(openSource, "acik.txt"), "güncel içerik");

        var result = await services.AccountDeletion.DeleteAccountAsync("Guclu-Parola-2026!");

        Assert(result.RestoredVaultCount == 2, "Geri getirilen kasa sayısı yanlış.");
        Assert(result.CleanupComplete, "Hesap veri klasörü tam temizlenmedi.");
        Assert(await File.ReadAllTextAsync(Path.Combine(lockedSource, "kilitli.txt")) == "kilitli içerik", "Kilitli kasa geri getirilmedi.");
        Assert(await File.ReadAllTextAsync(Path.Combine(openSource, "acik.txt")) == "güncel içerik", "Açık kasadaki değişiklik korunmadı.");
        Assert(!Directory.Exists(dataRoot), "Hesap veri klasörü silinmedi.");
        Assert(!services.Session.IsUnlocked, "Master Key bellekten temizlenmedi.");

        var restarted = new AppServices(dataRoot);
        Assert(!restarted.Authentication.IsConfigured, "Uygulama eski hesabı yeniden hatırladı.");
    }
    finally { Cleanup(root); }
}

static async Task TestDeleteAccountRejectsWrongPassword()
{
    var root = NewTemp();
    var dataRoot = Path.Combine(root, "appdata");
    var source = Path.Combine(root, "kaynak");
    try
    {
        var services = new AppServices(dataRoot);
        await services.Authentication.CreatePasswordAsync("Guclu-Parola-2026!");
        Directory.CreateDirectory(source);
        await File.WriteAllTextAsync(Path.Combine(source, "belge.txt"), "korunan içerik");
        var vault = await services.FolderProtection.ProtectFolder("Kasa", source);

        await AssertThrowsAsync<AccountDeletionPasswordException>(() => services.AccountDeletion.DeleteAccountAsync("Yanlis-Parola!"));

        Assert(services.Authentication.IsConfigured, "Yanlış parolada profil silindi.");
        Assert(!Directory.Exists(source), "Yanlış parolada kasa açıldı.");
        Assert(Directory.Exists(Path.Combine(services.Paths.Vaults, vault.Id.ToString("N"))), "Yanlış parolada şifreli kasa silindi.");
    }
    finally { Cleanup(root); Cleanup(source); }
}

static async Task TestDeleteAccountStopsOnPathConflict()
{
    var root = NewTemp();
    var dataRoot = Path.Combine(root, "appdata");
    var source = Path.Combine(root, "cakisma");
    try
    {
        var services = new AppServices(dataRoot);
        await services.Authentication.CreatePasswordAsync("Guclu-Parola-2026!");
        Directory.CreateDirectory(source);
        await File.WriteAllTextAsync(Path.Combine(source, "asli.txt"), "asıl kasa verisi");
        var vault = await services.FolderProtection.ProtectFolder("Çakışma", source);

        Directory.CreateDirectory(source);
        await File.WriteAllTextAsync(Path.Combine(source, "yeni.txt"), "sonradan oluşan veri");

        await AssertThrowsAsync<IOException>(() => services.AccountDeletion.DeleteAccountAsync("Guclu-Parola-2026!"));

        Assert(services.Authentication.IsConfigured, "Çakışmada profil silindi.");
        Assert(await File.ReadAllTextAsync(Path.Combine(source, "yeni.txt")) == "sonradan oluşan veri", "Çakışan klasör değiştirildi.");
        Assert(Directory.Exists(Path.Combine(services.Paths.Vaults, vault.Id.ToString("N"))), "Çakışmada şifreli kasa silindi.");
        Assert((await services.Vaults.GetVaultsAsync()).Any(v => v.Id == vault.Id), "Çakışmada kasa kaydı silindi.");
    }
    finally { Cleanup(root); Cleanup(source); }
}

static async Task WithServices(Func<AppServices, Task> action)
{
    var root = NewTemp();
    try { await action(new AppServices(Path.Combine(root, "appdata"))); }
    finally { Cleanup(root); }
}
static string NewTemp() { var p = Path.Combine(Path.GetTempPath(), "KlasorKasaTests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(p); return p; }
static void Cleanup(string path) { if (!Directory.Exists(path)) return; try { foreach (var f in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)) File.SetAttributes(f, FileAttributes.Normal); Directory.Delete(path, true); } catch { } }
static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
static void AssertThrows<T>(Action action) where T : Exception { try { action(); throw new InvalidOperationException($"{typeof(T).Name} bekleniyordu."); } catch (T) { } }
static async Task AssertThrowsAsync<T>(Func<Task> action) where T : Exception { try { await action(); throw new InvalidOperationException($"{typeof(T).Name} bekleniyordu."); } catch (T) { } }
