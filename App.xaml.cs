using System.Windows;
using KlasorKasa.Services;
using KlasorKasa.ViewModels;
using KlasorKasa.Views;

namespace KlasorKasa;

public partial class App : Application
{
    public static AppServices Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            Services?.Logging.Log("UnhandledUiError", result: "Failure", exception: args.Exception);
            MessageBox.Show("Beklenmeyen bir hata oluştu. Ayrıntılar uygulama günlüğüne yazıldı.", "KlasörKasa", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        Services = new AppServices();
        ThemeManager.Apply(Services.Settings.Current.Theme);
        Services.Logging.Log("ApplicationStart", result: "Success");
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        Window entry = Services.Authentication.IsConfigured
            ? new LoginWindow(new LoginViewModel(Services))
            : new OnboardingWindow(new OnboardingViewModel(Services));
        MainWindow = entry;
        entry.Show();
    }

    public static void ShowMainWindow()
    {
        var window = new MainWindow(new MainViewModel(Services));
        Current.MainWindow?.Close();
        Current.MainWindow = window;
        Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
        window.Show();
        EnsureRecoveryKeyForExistingInstallation(window);
    }

    private static async void EnsureRecoveryKeyForExistingInstallation(Window owner)
    {
        try
        {
            var profile = await Services.Authentication.LoadProfileAsync();
            if ((profile.Recovery is not null && profile.RecoveryConfirmed != false) || !Services.Session.IsUnlocked) return;
            var key = await Services.Recovery.CreateRecoveryKeyAsync();
            var dialog = new RecoveryKeyDialog(key) { Owner = owner };
            if (dialog.ShowDialog() == true)
                await Services.Recovery.ConfirmRecoveryKeySavedAsync();
        }
        catch (Exception ex)
        {
            Services.Logging.Log("RecoveryKeyMigration", result: "Failure", exception: ex);
            MessageBox.Show("Kurtarma anahtarı oluşturulamadı. Güvenlik sayfasından yeniden deneyin.",
                "KlasörKasa", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Services?.Session.Clear();
        Services?.Logging.Log("ApplicationExit", result: "Success");
        base.OnExit(e);
    }
}
