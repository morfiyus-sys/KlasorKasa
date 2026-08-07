using System.Windows.Input;
using KlasorKasa.Infrastructure;
using KlasorKasa.Services;

namespace KlasorKasa.ViewModels;

public sealed class OnboardingViewModel : ObservableObject
{
    private readonly AppServices _services;
    private string _password = string.Empty;
    private string _confirmation = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _isBusy;
    public event EventHandler? Completed;
    public event EventHandler<string>? RecoveryKeyCreated;
    public string Password { get => _password; set => SetProperty(ref _password, value); }
    public string Confirmation { get => _confirmation; set => SetProperty(ref _confirmation, value); }
    public string ErrorMessage { get => _errorMessage; set => SetProperty(ref _errorMessage, value); }
    public ICommand StartCommand { get; }

    public OnboardingViewModel(AppServices services)
    {
        _services = services;
        StartCommand = new AsyncRelayCommand(StartAsync, () => !_isBusy);
    }
    private async Task StartAsync()
    {
        ErrorMessage = string.Empty;
        if (Password != Confirmation) { ErrorMessage = "Parolalar eşleşmiyor."; return; }
        _isBusy = true;
        try
        {
            await _services.Authentication.CreatePasswordAsync(Password);
            var recoveryKey = await _services.Recovery.CreateRecoveryKeyAsync();
            RecoveryKeyCreated?.Invoke(this, recoveryKey);
            await _services.Recovery.ConfirmRecoveryKeySavedAsync();
            Completed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { _isBusy = false; Password = Confirmation = string.Empty; }
    }
}
