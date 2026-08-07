using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using KlasorKasa.Infrastructure;
using KlasorKasa.Models;
using KlasorKasa.Services;
using KlasorKasa.Views;

namespace KlasorKasa.ViewModels;

public sealed class VaultsViewModel : ObservableObject
{
    private readonly AppServices _services;
    private bool _isBusy;
    private string _statusMessage = string.Empty;
    public ObservableCollection<ProtectedFolder> Vaults { get; } = [];
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public ICommand RefreshCommand { get; }
    public ICommand CreateVaultCommand { get; }
    public ICommand UnlockAllCommand { get; }
    public ICommand LockAllCommand { get; }
    public ICommand PrimaryActionCommand { get; }
    public ICommand OpenFolderCommand { get; }
    public ICommand RemoveProtectionCommand { get; }

    public VaultsViewModel(AppServices services)
    {
        _services = services;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        CreateVaultCommand = new AsyncRelayCommand(CreateVaultAsync);
        UnlockAllCommand = new AsyncRelayCommand(UnlockAllAsync);
        LockAllCommand = new AsyncRelayCommand(LockAllAsync);
        PrimaryActionCommand = new AsyncRelayCommand<ProtectedFolder>(PrimaryActionAsync);
        OpenFolderCommand = new RelayCommand<ProtectedFolder>(v => _services.FolderProtection.OpenFolder(v.OriginalPath));
        RemoveProtectionCommand = new AsyncRelayCommand<ProtectedFolder>(RemoveProtectionAsync);
    }

    public async Task RefreshAsync()
    {
        try
        {
            var items = await _services.Vaults.GetVaultsAsync();
            Vaults.Clear();
            foreach (var item in items.OrderByDescending(x => x.LastOperationUtc)) Vaults.Add(item);
        }
        catch (Exception ex) { StatusMessage = UserMessage(ex); }
    }

    private async Task CreateVaultAsync()
    {
        var vm = new CreateVaultViewModel();
        var dialog = new CreateVaultDialog(vm) { Owner = Application.Current.MainWindow };
        if (dialog.ShowDialog() != true) return;
        IsBusy = true; StatusMessage = "Klasör şifreleniyor ve doğrulanıyor…";
        try
        {
            await _services.FolderProtection.ProtectFolder(vm.Name, vm.FolderPath);
            StatusMessage = "Kasa güvenle oluşturuldu.";
        }
        catch (Exception ex) { StatusMessage = UserMessage(ex); }
        finally { IsBusy = false; await RefreshAsync(); }
    }

    private async Task PrimaryActionAsync(ProtectedFolder vault)
    {
        IsBusy = true;
        var locking = vault.State is VaultState.Open or VaultState.Attention;
        StatusMessage = locking ? $"{vault.Name} kasası kilitleniyor…" : $"{vault.Name} kasası açılıyor…";
        try
        {
            if (locking) await _services.FolderProtection.LockFolder(vault.Id);
            else await _services.FolderProtection.UnlockFolder(vault.Id);
            StatusMessage = locking ? $"{vault.Name} kasası kilitlendi." : $"{vault.Name} kasası açıldı.";
        }
        catch (Exception ex) { StatusMessage = UserMessage(ex); }
        finally { IsBusy = false; await RefreshAsync(); }
    }

    public async Task UnlockAllAsync()
    {
        var targets = Vaults.Where(v => v.State == VaultState.Locked).ToList();
        if (targets.Count == 0) { StatusMessage = "Açılacak kilitli kasa yok."; return; }

        IsBusy = true;
        var opened = 0;
        var failures = new List<string>();
        try
        {
            for (var index = 0; index < targets.Count; index++)
            {
                var vault = targets[index];
                StatusMessage = $"{vault.Name} açılıyor… ({index + 1}/{targets.Count})";
                try
                {
                    await _services.FolderProtection.UnlockFolder(vault.Id);
                    opened++;
                }
                catch (Exception ex)
                {
                    failures.Add($"{vault.Name}: {UserMessage(ex)}");
                    _services.Logging.Log("UnlockAllItem", vault.OriginalPath, "Failure", ex);
                }
            }
        }
        finally
        {
            IsBusy = false;
            await RefreshAsync();
        }

        StatusMessage = failures.Count == 0
            ? $"{opened} kasa başarıyla açıldı."
            : $"{opened} kasa açıldı. Açılamayanlar: {string.Join(" | ", failures)}";
    }

    public async Task LockAllAsync()
    {
        var targets = Vaults.Where(v => v.State is VaultState.Open or VaultState.Attention).ToList();
        if (targets.Count == 0) { StatusMessage = "Kilitlenecek açık kasa yok."; return; }

        IsBusy = true;
        var locked = 0;
        var failures = new List<string>();
        try
        {
            for (var index = 0; index < targets.Count; index++)
            {
                var vault = targets[index];
                StatusMessage = $"{vault.Name} kilitleniyor… ({index + 1}/{targets.Count})";
                try
                {
                    await _services.FolderProtection.LockFolder(vault.Id);
                    locked++;
                }
                catch (Exception ex)
                {
                    failures.Add($"{vault.Name}: {UserMessage(ex)}");
                    _services.Logging.Log("LockAllItem", vault.OriginalPath, "Failure", ex);
                }
            }
        }
        finally
        {
            IsBusy = false;
            await RefreshAsync();
        }

        StatusMessage = failures.Count == 0
            ? $"{locked} kasa güvenle kilitlendi."
            : $"{locked} kasa kilitlendi. Kilitlenemeyenler: {string.Join(" | ", failures)}";
    }

    private async Task RemoveProtectionAsync(ProtectedFolder vault)
    {
        var answer = MessageBox.Show("Kasa koruması kaldırılacak; dosyalar geri yüklenecek, gizli/sistem öznitelikleri temizlenecek ve klasör normal Windows izinleriyle bırakılacak. Devam edilsin mi?",
            "Korumayı Kaldır", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;
        IsBusy = true;
        try { await _services.FolderProtection.RemoveProtection(vault.Id); StatusMessage = "Koruma kaldırıldı. Klasör görünür ve normal Windows izinleriyle kullanılabilir."; }
        catch (Exception ex) { StatusMessage = UserMessage(ex); }
        finally { IsBusy = false; await RefreshAsync(); }
    }

    private static string UserMessage(Exception ex) => ex switch
    {
        UnauthorizedAccessException => ex.Message,
        ArgumentException => ex.Message,
        IOException => ex.Message,
        System.Security.Cryptography.CryptographicException => "Kasa bütünlüğü doğrulanamadı. İşlem güvenle durduruldu.",
        _ => "İşlem tamamlanamadı. Ayrıntılar güvenli uygulama günlüğüne yazıldı."
    };
}
