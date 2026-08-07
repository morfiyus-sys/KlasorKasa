using System.Windows.Input;
using KlasorKasa.Infrastructure;
using KlasorKasa.Services;

namespace KlasorKasa.ViewModels;

public sealed class RecoveryViewModel : ObservableObject
{
    private readonly AppServices _services;
    private string _recoveryKey = string.Empty;
    private string _newPassword = string.Empty;
    private string _confirmation = string.Empty;
    private string _message = "Kurtarma anahtarınızı ve yeni parolanızı girin.";
    private bool _isBusy;
    private bool _hasRecoveryKey;

    public event EventHandler? Completed;
    public string RecoveryKey { get => _recoveryKey; set => SetProperty(ref _recoveryKey, value); }
    public string NewPassword { get => _newPassword; set => SetProperty(ref _newPassword, value); }
    public string Confirmation { get => _confirmation; set => SetProperty(ref _confirmation, value); }
    public string Message { get => _message; private set => SetProperty(ref _message, value); }
    public bool IsBusy { get => _isBusy; private set { if (SetProperty(ref _isBusy, value)) OnPropertyChanged(nameof(CanReset)); } }
    public bool HasRecoveryKey { get => _hasRecoveryKey; private set { if (SetProperty(ref _hasRecoveryKey, value)) OnPropertyChanged(nameof(CanReset)); } }
    public bool CanReset => HasRecoveryKey && !IsBusy;
    public ICommand ResetPasswordCommand { get; }

    public RecoveryViewModel(AppServices services)
    {
        _services = services;
        ResetPasswordCommand = new AsyncRelayCommand(ResetPasswordAsync);
        _ = LoadStatusAsync();
    }

    private async Task LoadStatusAsync()
    {
        try
        {
            HasRecoveryKey = (await _services.Authentication.LoadProfileAsync()).Recovery is not null;
            if (!HasRecoveryKey)
                Message = "Bu kurulum için kurtarma anahtarı oluşturulmamış. Güvenlik nedeniyle parola anahtarsız sıfırlanamaz.";
        }
        catch
        {
            HasRecoveryKey = false;
            Message = "Kurtarma bilgisi okunamadı.";
        }
    }

    private async Task ResetPasswordAsync()
    {
        if (!CanReset) return;
        if (NewPassword != Confirmation)
        {
            Message = "Yeni parolalar eşleşmiyor.";
            return;
        }

        IsBusy = true;
        try
        {
            if (!await _services.Recovery.UnlockWithRecoveryKeyAsync(RecoveryKey))
            {
                Message = "Kurtarma anahtarı geçersiz.";
                return;
            }
            await _services.Authentication.ResetPasswordFromSessionAsync(NewPassword);
            Message = "Parolanız yenilendi.";
            Completed?.Invoke(this, EventArgs.Empty);
        }
        catch (ArgumentException ex)
        {
            _services.Session.Clear();
            Message = ex.Message;
        }
        catch
        {
            _services.Session.Clear();
            Message = "Parola sıfırlanamadı. Kurtarma anahtarınızı kontrol edip yeniden deneyin.";
        }
        finally
        {
            RecoveryKey = NewPassword = Confirmation = string.Empty;
            IsBusy = false;
        }
    }
}
