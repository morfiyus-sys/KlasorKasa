using System.Windows.Input;
using KlasorKasa.Infrastructure;
using KlasorKasa.Services;

namespace KlasorKasa.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    public IReadOnlyList<int> AutoLockOptions { get; } = [1, 5, 10, 30, 0];
    public IReadOnlyList<string> ThemeOptions { get; } = ["Sistem", "Açık", "Koyu"];
    public string Theme { get => _settings.Current.Theme; set { _settings.Current.Theme = value; OnPropertyChanged(); } }
    public int AutoLockMinutes { get => _settings.Current.AutoLockMinutes; set { _settings.Current.AutoLockMinutes = value; OnPropertyChanged(); } }
    public bool StartWithWindows { get => _settings.Current.StartWithWindows; set { _settings.Current.StartWithWindows = value; OnPropertyChanged(); } }
    public bool LockOnExit { get => _settings.Current.LockVaultsOnExit; set { _settings.Current.LockVaultsOnExit = value; OnPropertyChanged(); } }
    public bool EnableAutoLock { get => _settings.Current.EnableAutoLock; set { _settings.Current.EnableAutoLock = value; OnPropertyChanged(); } }
    public bool NotifyOnUnlock { get => _settings.Current.NotifyOnUnlock; set { _settings.Current.NotifyOnUnlock = value; OnPropertyChanged(); } }
    public bool NotifyOnLock { get => _settings.Current.NotifyOnLock; set { _settings.Current.NotifyOnLock = value; OnPropertyChanged(); } }
    public ICommand SaveCommand { get; }
    public SettingsViewModel(SettingsService settings) { _settings = settings; SaveCommand = new AsyncRelayCommand(SaveAsync); }
    private async Task SaveAsync() { await _settings.SaveAsync(); ThemeManager.Apply(Theme); }
}
