using System.Windows.Input;
using KlasorKasa.Infrastructure;
using KlasorKasa.Models;
using KlasorKasa.Services;

namespace KlasorKasa.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly AppServices _services;
    private object _currentViewModel;
    private string _selectedPage = "Ana Sayfa";
    public HomeViewModel Home { get; } = new();
    public VaultsViewModel Vaults { get; }
    public SecurityViewModel Security { get; }
    public SettingsViewModel Settings { get; }
    public object CurrentViewModel { get => _currentViewModel; private set => SetProperty(ref _currentViewModel, value); }
    public string SelectedPage { get => _selectedPage; private set => SetProperty(ref _selectedPage, value); }
    public ICommand NavigateCommand { get; }
    public event EventHandler? SessionLocked;
    public event EventHandler? SafeExitRequested;
    public ICommand SafeExitCommand { get; }

    public MainViewModel(AppServices services)
    {
        _services = services;
        Vaults = new VaultsViewModel(services);
        Security = new SecurityViewModel(services);
        Settings = new SettingsViewModel(services.Settings);
        _currentViewModel = Home;
        NavigateCommand = new RelayCommand<string>(Navigate);
        SafeExitCommand = new RelayCommand(() => SafeExitRequested?.Invoke(this, EventArgs.Empty));
        services.AutoLock.Locked += (_, _) => SessionLocked?.Invoke(this, EventArgs.Empty);
        services.AutoLock.Start();
        _ = RefreshAsync();
    }
    public void NotifyActivity() => _services.AutoLock.NotifyActivity();
    public async Task RefreshAsync()
    {
        await Vaults.RefreshAsync();
        Home.Total = Vaults.Vaults.Count;
        Home.Locked = Vaults.Vaults.Count(v => v.State == VaultState.Locked);
        Home.Open = Vaults.Vaults.Count(v => v.State == VaultState.Open);
    }
    private void Navigate(string page)
    {
        SelectedPage = page;
        CurrentViewModel = page switch
        {
            "Ana Sayfa" => Home,
            "Kasalar" => Vaults,
            "Güvenlik" => Security,
            "Ayarlar" => Settings,
            _ => new AboutViewModel()
        };
        if (page == "Kasalar") _ = Vaults.RefreshAsync();
    }
    public async Task<bool> PrepareForExitAsync(bool forceLock = false)
    {
        _services.AutoLock.Stop();
        if (!forceLock && !_services.Settings.Current.LockVaultsOnExit) return true;
        var open = (await _services.Vaults.GetVaultsAsync()).Where(v => v.State == VaultState.Open).ToList();
        foreach (var vault in open) await _services.FolderProtection.LockFolder(vault.Id);
        _services.Session.Clear();
        return true;
    }
}

public sealed class AboutViewModel
{
    public string Version { get; set; } = typeof(AboutViewModel).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
}
