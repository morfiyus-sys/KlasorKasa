using System.Windows.Threading;
using KlasorKasa.Infrastructure;

namespace KlasorKasa.Services;

public sealed class AutoLockService(SettingsService settings, VaultService vaults, FolderProtectionService folders, SecureSession session, LoggingService logging)
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(15) };
    private DateTime _lastActivityUtc = DateTime.UtcNow;
    private bool _busy;
    public bool IsRunning => _timer.IsEnabled;
    public event EventHandler? Locked;

    public void Start()
    {
        _timer.Tick -= OnTick;
        _timer.Tick += OnTick;
        _timer.Start();
    }
    public void Stop() => _timer.Stop();
    public void NotifyActivity() => _lastActivityUtc = DateTime.UtcNow;

    private async void OnTick(object? sender, EventArgs e)
    {
        var value = settings.Current;
        if (_busy || !value.EnableAutoLock || value.AutoLockMinutes <= 0 || DateTime.UtcNow - _lastActivityUtc < TimeSpan.FromMinutes(value.AutoLockMinutes)) return;
        _busy = true;
        try
        {
            var open = (await vaults.GetVaultsAsync()).Where(v => v.State == Models.VaultState.Open).ToList();
            foreach (var vault in open) await folders.LockFolder(vault.Id);
            session.Clear();
            Locked?.Invoke(this, EventArgs.Empty);
            logging.Log("AutoLock", result: "Success");
        }
        catch (Exception ex) { logging.Log("AutoLock", result: "Failure", exception: ex); }
        finally { _busy = false; _lastActivityUtc = DateTime.UtcNow; }
    }
}
