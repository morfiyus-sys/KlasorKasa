namespace KlasorKasa.Models;

public enum VaultState { Locked, Open, Attention }

public sealed class ProtectedFolder
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string OriginalPath { get; set; } = string.Empty;
    public string OwnerSid { get; set; } = string.Empty;
    public VaultState State { get; set; } = VaultState.Locked;
    public DateTime CreatedUtc { get; set; }
    public DateTime LastOperationUtc { get; set; }
    public long TotalBytes { get; set; }
    public int FileCount { get; set; }

    public string StatusText => State switch
    {
        VaultState.Locked => "Kilitli ve Gizli",
        VaultState.Open => "Açık",
        _ => "İlgilenmeniz gerekiyor"
    };
}
