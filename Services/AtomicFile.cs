namespace KlasorKasa.Services;

internal static class AtomicFile
{
    public static async Task WriteAllBytesAsync(string path, byte[] data, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
        await File.WriteAllBytesAsync(temp, data, cancellationToken);
        using (var stream = new FileStream(temp, FileMode.Open, FileAccess.Read, FileShare.None, 4096, FileOptions.WriteThrough))
            stream.Flush(true);
        try
        {
            if (File.Exists(path)) File.Replace(temp, path, null, true);
            else File.Move(temp, path);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
