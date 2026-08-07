namespace KlasorKasa.Models;

public sealed class AppSettings
{
    public string Theme { get; set; } = "Sistem";
    public int AutoLockMinutes { get; set; } = 5;
    public bool StartWithWindows { get; set; }
    public bool LockVaultsOnExit { get; set; } = true;
    public bool EnableAutoLock { get; set; } = true;
    public bool NotifyOnUnlock { get; set; } = true;
    public bool NotifyOnLock { get; set; } = true;
}
