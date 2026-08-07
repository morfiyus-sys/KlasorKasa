using System.Windows.Input;
using KlasorKasa.Infrastructure;
using KlasorKasa.Services;

namespace KlasorKasa.ViewModels;

public sealed class DeleteAccountViewModel : ObservableObject
{
    public const string RequiredConfirmation = "HESABIMI SİL";

    private readonly AccountDeletionService _accountDeletion;
    private string _password = string.Empty;
    private string _confirmationText = string.Empty;
    private string _message = string.Empty;
    private bool _isAcknowledged;
    private bool _isBusy;

    public event EventHandler<bool>? CloseRequested;

    public string Password
    {
        get => _password;
        set { if (SetProperty(ref _password, value)) OnPropertyChanged(nameof(CanDelete)); }
    }

    public string ConfirmationText
    {
        get => _confirmationText;
        set { if (SetProperty(ref _confirmationText, value)) OnPropertyChanged(nameof(CanDelete)); }
    }

    public string Message { get => _message; private set => SetProperty(ref _message, value); }

    public bool IsAcknowledged
    {
        get => _isAcknowledged;
        set { if (SetProperty(ref _isAcknowledged, value)) OnPropertyChanged(nameof(CanDelete)); }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set { if (SetProperty(ref _isBusy, value)) OnPropertyChanged(nameof(CanDelete)); }
    }

    public bool CanDelete => !IsBusy && IsAcknowledged && !string.IsNullOrWhiteSpace(Password) &&
                             string.Equals(ConfirmationText.Trim(), RequiredConfirmation, StringComparison.Ordinal);

    public ICommand DeleteCommand { get; }
    public ICommand CancelCommand { get; }

    public DeleteAccountViewModel(AccountDeletionService accountDeletion)
    {
        _accountDeletion = accountDeletion;
        DeleteCommand = new AsyncRelayCommand(DeleteAsync);
        CancelCommand = new RelayCommand(() => { if (!IsBusy) CloseRequested?.Invoke(this, false); });
    }

    private async Task DeleteAsync()
    {
        if (!CanDelete) return;
        IsBusy = true;
        Message = "Kasaların koruması kaldırılıyor ve dosyalar özgün konumlarına geri getiriliyor…";
        try
        {
            var result = await _accountDeletion.DeleteAccountAsync(Password);
            Password = string.Empty;
            if (!result.CleanupComplete)
            {
                System.Windows.MessageBox.Show(
                    "Kasaların koruması kaldırıldı ve hesap profili silindi. Bazı geçici günlük veya ayar dosyaları Windows tarafından kullanımda olduğu için tamamen temizlenemedi; uygulama eski hesabı yeniden kullanmayacaktır.",
                    "Hesap Silindi", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
            Message = "Hesap verileri temizlendi.";
            CloseRequested?.Invoke(this, true);
        }
        catch (AccountDeletionPasswordException)
        {
            Message = "Parola yanlış.";
        }
        catch (IOException ex)
        {
            Message = ex.Message;
        }
        catch (Exception)
        {
            Message = "Hesap silinemedi. Kasalarınız korunmaya devam ediyor; ayrıntılar uygulama günlüğüne yazıldı.";
        }
        finally
        {
            Password = string.Empty;
            IsBusy = false;
        }
    }
}
