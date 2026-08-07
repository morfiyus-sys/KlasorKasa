using System.ComponentModel;
using System.Windows;
using System.Windows.Media.Animation;
using KlasorKasa.ViewModels;

namespace KlasorKasa.Views;

public partial class LoginWindow : Window
{
    private readonly LoginViewModel _viewModel;
    private bool _transitioning;

    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
        viewModel.Authenticated += OnAuthenticated;
        viewModel.RecoveryRequested += OnRecoveryRequested;
    }

    private void Window_OnLoaded(object sender, RoutedEventArgs e)
    {
        Left = SystemParameters.WorkArea.Right - ActualWidth - 14;
        var finalTop = SystemParameters.WorkArea.Bottom - ActualHeight - 14;
        Top = finalTop + 28;
        Opacity = 0;
        BeginAnimation(TopProperty, new DoubleAnimation(finalTop, TimeSpan.FromMilliseconds(190)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
        BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(160)));
        Activate();
        PasswordInput.FocusPassword();
    }

    private void OnAuthenticated(object? sender, EventArgs e)
    {
        _transitioning = true;
        _viewModel.Dispose();
        App.ShowMainWindow();
    }

    private void OnRecoveryRequested(object? sender, EventArgs e)
    {
        Hide();
        var recovery = new RecoveryWindow(new RecoveryViewModel(App.Services));
        var recovered = recovery.ShowDialog() == true;
        if (recovered)
        {
            _transitioning = true;
            _viewModel.Dispose();
            App.ShowMainWindow();
        }
        else
        {
            Show();
            Activate();
            PasswordInput.FocusPassword();
        }
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e) => Close();

    private void Window_OnClosing(object? sender, CancelEventArgs e)
    {
        if (_transitioning) return;
        _viewModel.Dispose();
        Application.Current.Shutdown();
    }
}
