namespace KlasorKasa.Models;

public sealed class UserProfile
{
    public int FormatVersion { get; set; } = 1;
    public string OwnerSid { get; set; } = string.Empty;
    public int KdfIterations { get; set; } = 600_000;
    public string KdfSalt { get; set; } = string.Empty;
    public string WrapNonce { get; set; } = string.Empty;
    public string WrappedMasterKey { get; set; } = string.Empty;
    public string WrapTag { get; set; } = string.Empty;
    public RecoveryEnvelope? Recovery { get; set; }
    public bool? RecoveryConfirmed { get; set; }
    public DateTime CreatedUtc { get; set; }
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockoutUntilUtc { get; set; }
}

public sealed class RecoveryEnvelope
{
    public string Salt { get; set; } = string.Empty;
    public string Nonce { get; set; } = string.Empty;
    public string Ciphertext { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
}
