using System.Windows.Input;
using KlasorKasa.Infrastructure;
using KlasorKasa.Services;

namespace KlasorKasa.ViewModels;

public sealed class SecurityViewModel : ObservableObject
{
    private readonly AppServices _services;
    private string _oldPassword = string.Empty;
    private string _newPassword = string.Empty;
    private string _confirmation = string.Empty;
    private string _message = string.Empty;
    private string _recoveryStatus = "Oluşturulmadı";

    public string OldPassword { get => _oldPassword; set => SetProperty(ref _oldPassword, value); }
    public string NewPassword { get => _newPassword; set => SetProperty(ref _newPassword, value); }
    public string Confirmation { get => _confirmation; set => SetProperty(ref _confirmation, value); }
    public string Message { get => _message; set => SetProperty(ref _message, value); }
    public string RecoveryStatus { get => _recoveryStatus; private set => SetProperty(ref _recoveryStatus, value); }
    public string WindowsAccount => _services.Identity.CurrentAccount;
    public ICommand ChangePasswordCommand { get; }
    public ICommand CreateRecoveryCommand { get; }

    public SecurityViewModel(AppServices services)
    {
        _services = services;
        ChangePasswordCommand = new AsyncRelayCommand(ChangePasswordAsync);
        CreateRecoveryCommand = new AsyncRelayCommand(CreateRecoveryAsync);
        _ = LoadStatusAsync();
    }

    private async Task LoadStatusAsync() =>
        RecoveryStatus = (await _services.Authentication.LoadProfileAsync()).Recovery is null ? "Oluşturulmadı" : "Oluşturuldu";

    private async Task ChangePasswordAsync()
    {
        Message = string.Empty;
        if (NewPassword != Confirmation)
        {
            Message = "Yeni parolalar eşleşmiyor.";
            return;
        }
        try
        {
            await _services.Authentication.ChangePasswordAsync(OldPassword, NewPassword);
            Message = "Parola güncellendi.";
        }
        catch (Exception ex)
        {
            Message = ex is UnauthorizedAccessException ? "Parola yanlış." : ex.Message;
        }
        finally
        {
            OldPassword = NewPassword = Confirmation = string.Empty;
        }
    }

    private async Task CreateRecoveryAsync()
    {
        var profile = await _services.Authentication.LoadProfileAsync();
        if (profile.Recovery is not null)
        {
            var replace = System.Windows.MessageBox.Show(
                "Yeni bir kurtarma anahtarı oluşturulursa önceki anahtar geçersiz olur. Devam edilsin mi?",
                "Kurtarma Anahtarını Yenile", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
            if (replace != System.Windows.MessageBoxResult.Yes) return;
        }

        var key = await _services.Recovery.CreateRecoveryKeyAsync();
        RecoveryStatus = "Oluşturuldu";
        System.Windows.Clipboard.SetText(key);
        System.Windows.MessageBox.Show(
            "Kurtarma anahtarı panoya kopyalandı. Bu anahtarı güvenli bir yerde saklayın; program anahtarı tekrar gösteremez.\n\n" + key,
            "Kurtarma Anahtarı", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        await _services.Recovery.ConfirmRecoveryKeySavedAsync();
    }
}
