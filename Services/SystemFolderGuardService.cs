namespace KlasorKasa.Services;

public sealed class SystemFolderGuardService(AppPaths paths)
{
    public void ValidateForProtection(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            throw new ArgumentException("Seçilen klasör bulunamadı.");

        var full = Normalize(path);
        var root = Path.GetPathRoot(full);
        if (string.Equals(full, Normalize(root!), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Disk kökleri kasa olarak seçilemez.");
        var drive = new DriveInfo(root!);
        if (!string.Equals(drive.DriveFormat, "NTFS", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("KlasörKasa, NTFS izin koruması için NTFS biçimli bir disk gerektirir.");

        var blockedTrees = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            AppContext.BaseDirectory,
            paths.Root,
            paths.Vaults
        }.Where(p => !string.IsNullOrWhiteSpace(p)).Select(Normalize);

        foreach (var item in blockedTrees)
        {
            if (string.Equals(full, item, StringComparison.OrdinalIgnoreCase) || IsChildOf(item, full) || IsChildOf(full, item))
                throw new ArgumentException("Bu klasör sistem veya KlasörKasa verileri içerdiği için korunamaz.");
        }
        var profile = Normalize(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        if (string.Equals(full, profile, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Kullanıcı profilinin tamamı kasa olarak seçilemez. İçindeki özel bir klasörü seçin.");

        var attributes = File.GetAttributes(full);
        if (attributes.HasFlag(FileAttributes.ReparsePoint))
            throw new ArgumentException("Bağlantı veya yeniden ayrıştırma noktaları kasa olarak seçilemez.");
        foreach (var entry in Directory.EnumerateFileSystemEntries(full, "*", SearchOption.AllDirectories))
            if (File.GetAttributes(entry).HasFlag(FileAttributes.ReparsePoint))
                throw new ArgumentException("Klasör içinde desteklenmeyen bir bağlantı noktası var.");
    }

    private static bool IsChildOf(string parent, string candidate) =>
        candidate.StartsWith(parent.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    private static string Normalize(string path) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
