using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using KlasorKasa.ViewModels;
namespace KlasorKasa.Views;
public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private bool _allowClose;
    private bool _forceSafeExit;
    public MainWindow(MainViewModel vm)
    {
        InitializeComponent(); DataContext = _vm = vm;
        vm.SessionLocked += (_, _) => Dispatcher.Invoke(ShowLoginAfterAutoLock);
        vm.SafeExitRequested += (_, _) => Dispatcher.Invoke(() => { _forceSafeExit = true; Close(); });
    }
    private void Window_Activity(object sender, InputEventArgs e) => _vm.NotifyActivity();
    private async void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowClose) return;
        e.Cancel = true;
        try
        {
            var open = (await App.Services.Vaults.GetVaultsAsync()).Any(v => v.State == Models.VaultState.Open);
            if (open && (_forceSafeExit || App.Services.Settings.Current.LockVaultsOnExit))
                MessageBox.Show("Korunan kasa açık durumda. Program kapanmadan önce kasa kilitlenecek.", "KlasörKasa", MessageBoxButton.OK, MessageBoxImage.Information);
            await _vm.PrepareForExitAsync(_forceSafeExit);
            _allowClose = true; Close();
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Kasa kilitlenemedi", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
    private void ShowLoginAfterAutoLock()
    {
        _allowClose = true;
        var login = new LoginWindow(new LoginViewModel(App.Services));
        Application.Current.MainWindow = login;
        login.Show(); Close();
    }
}
