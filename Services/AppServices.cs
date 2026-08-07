using KlasorKasa.Infrastructure;

namespace KlasorKasa.Services;

public sealed class AppServices
{
    public AppPaths Paths { get; }
    public LoggingService Logging { get; }
    public WindowsIdentityService Identity { get; }
    public KeyDerivationService KeyDerivation { get; }
    public EncryptionService Encryption { get; }
    public SecureSession Session { get; }
    public AuthenticationService Authentication { get; }
    public SettingsService Settings { get; }
    public AclService Acl { get; }
    public AclBackupService AclBackups { get; }
    public SystemFolderGuardService SystemFolderGuard { get; }
    public VaultService Vaults { get; }
    public FolderProtectionService FolderProtection { get; }
    public RecoveryKeyService Recovery { get; }
    public AutoLockService AutoLock { get; }

    public AppServices(string? dataRoot = null, TimeSpan? loginLockoutDuration = null)
    {
        Paths = new AppPaths(dataRoot);
        Logging = new LoggingService(Paths);
        Identity = new WindowsIdentityService();
        KeyDerivation = new KeyDerivationService();
        Encryption = new EncryptionService();
        Session = new SecureSession();
        Authentication = new AuthenticationService(Paths, KeyDerivation, Encryption, Identity, Session, Logging, loginLockoutDuration);
        Settings = new SettingsService(Paths, Logging);
        Acl = new AclService(Identity, Logging);
        AclBackups = new AclBackupService(Paths, Encryption);
        SystemFolderGuard = new SystemFolderGuardService(Paths);
        Vaults = new VaultService(Paths, Encryption, Session, Identity, Acl, AclBackups, Logging);
        FolderProtection = new FolderProtectionService(Vaults, SystemFolderGuard, Logging);
        Recovery = new RecoveryKeyService(Paths, Authentication, Encryption, KeyDerivation, Session, Logging);
        AutoLock = new AutoLockService(Settings, Vaults, FolderProtection, Session, Logging);
        Settings.LoadAsync().GetAwaiter().GetResult();
    }
}
