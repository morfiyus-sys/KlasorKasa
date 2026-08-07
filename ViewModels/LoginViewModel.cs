using System.Windows.Input;
using System.Windows.Threading;
using KlasorKasa.Infrastructure;
using KlasorKasa.Services;

namespace KlasorKasa.ViewModels;

public sealed class LoginViewModel : ObservableObject, IDisposable
{
    private readonly AppServices _services;
    private readonly DispatcherTimer _lockoutTimer;
    private string _password = string.Empty;
    private string _errorMessage = string.Empty;
    private string _lockoutMessage = string.Empty;
    private bool _isBusy;
    private bool _isLockedOut;

    public event EventHandler? Authenticated;
    public event EventHandler? RecoveryRequested;

    public string Password { get => _password; set => SetProperty(ref _password, value); }
    public string ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }
    public string LockoutMessage { get => _lockoutMessage; private set => SetProperty(ref _lockoutMessage, value); }
    public bool IsBusy { get => _isBusy; private set { if (SetProperty(ref _isBusy, value)) OnPropertyChanged(nameof(CanLogin)); } }
    public bool IsLockedOut { get => _isLockedOut; private set { if (SetProperty(ref _isLockedOut, value)) OnPropertyChanged(nameof(CanLogin)); } }
    public bool CanLogin => !IsBusy && !IsLockedOut;
    public ICommand LoginCommand { get; }
    public ICommand RecoveryCommand { get; }

    public LoginViewModel(AppServices services)
    {
        _services = services;
        LoginCommand = new AsyncRelayCommand(LoginAsync);
        RecoveryCommand = new RelayCommand(() => RecoveryRequested?.Invoke(this, EventArgs.Empty));
        _lockoutTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _lockoutTimer.Tick += async (_, _) => await RefreshLockoutAsync();
        _ = RefreshLockoutAsync();
    }

    private async Task LoginAsync()
    {
        if (!CanLogin) return;
        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            if (await _services.Authentication.VerifyPasswordAsync(Password))
            {
                Authenticated?.Invoke(this, EventArgs.Empty);
                return;
            }

            var remaining = await _services.Authentication.GetLockoutRemainingAsync();
            if (remaining > TimeSpan.Zero)
            {
                ApplyLockout(remaining);
                _lockoutTimer.Start();
            }
            else
            {
                ErrorMessage = "Parola yanlış.";
            }
        }
        finally
        {
            Password = string.Empty;
            IsBusy = false;
        }
    }

    private async Task RefreshLockoutAsync()
    {
        var remaining = await _services.Authentication.GetLockoutRemainingAsync();
        if (remaining <= TimeSpan.Zero)
        {
            _lockoutTimer.Stop();
            IsLockedOut = false;
            LockoutMessage = string.Empty;
            return;
        }
        ApplyLockout(remaining);
        _lockoutTimer.Start();
    }

    private void ApplyLockout(TimeSpan remaining)
    {
        IsLockedOut = true;
        var seconds = Math.Max(1, (int)Math.Ceiling(remaining.TotalSeconds));
        LockoutMessage = $"Çok fazla hatalı deneme. {seconds} saniye bekleyin.";
    }

    public void Dispose() => _lockoutTimer.Stop();
}
