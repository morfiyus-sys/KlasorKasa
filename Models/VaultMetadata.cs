namespace KlasorKasa.Models;

public sealed class VaultMetadata
{
    public int FormatVersion { get; set; } = 1;
    public Guid VaultId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string OriginalPath { get; set; } = string.Empty;
    public string OwnerSid { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public string? OriginalAclSddl { get; set; }
    public List<VaultFileEntry> Files { get; set; } = [];
    public List<string> EmptyDirectories { get; set; } = [];
}

public sealed class VaultFileEntry
{
    public string BlobId { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public long Length { get; set; }
    public DateTime LastWriteTimeUtc { get; set; }
    public string Sha256 { get; set; } = string.Empty;
}
