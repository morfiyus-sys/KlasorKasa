namespace KlasorKasa.Services;

public sealed class AppPaths
{
    public string Root { get; }
    public string Vaults { get; }
    public string Logs { get; }
    public string AclBackups { get; }
    public string Working { get; }
    public string ProfileFile { get; }
    public string SettingsFile { get; }
    public string VaultIndexFile { get; }

    public AppPaths(string? root = null)
    {
        Root = root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KlasorKasa");
        Vaults = Path.Combine(Root, "Vaults");
        Logs = Path.Combine(Root, "Logs");
        AclBackups = Path.Combine(Root, "AclBackups");
        Working = Path.Combine(Root, "Working");
        ProfileFile = Path.Combine(Root, "profile.json");
        SettingsFile = Path.Combine(Root, "settings.json");
        VaultIndexFile = Path.Combine(Root, "vaults.index");
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Vaults);
        Directory.CreateDirectory(Logs);
        Directory.CreateDirectory(AclBackups);
        Directory.CreateDirectory(Working);
        TryHide(Root);
    }

    public static void TryHide(string path)
    {
        try { File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.Hidden | FileAttributes.System); }
        catch { /* Security does not depend on Explorer attributes. */ }
    }
}
