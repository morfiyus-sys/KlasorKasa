using System.Windows;
using KlasorKasa.ViewModels;

namespace KlasorKasa.Views;

public partial class OnboardingWindow : Window
{
    private bool _transitioning;

    public OnboardingWindow(OnboardingViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.RecoveryKeyCreated += (_, key) =>
        {
            var dialog = new RecoveryKeyDialog(key) { Owner = this };
            dialog.ShowDialog();
        };
        viewModel.Completed += (_, _) =>
        {
            _transitioning = true;
            App.ShowMainWindow();
        };
        Closing += (_, _) =>
        {
            if (!_transitioning) Application.Current.Shutdown();
        };
    }
}
