using System.Windows;
using System.ComponentModel;

namespace KlasorKasa.Views;

public partial class RecoveryKeyDialog : Window
{
    private bool _confirmed;
    public RecoveryKeyDialog(string recoveryKey)
    {
        InitializeComponent();
        KeyBox.Text = recoveryKey;
    }

    private void Copy_OnClick(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(KeyBox.Text);
    }

    private void Continue_OnClick(object sender, RoutedEventArgs e)
    {
        _confirmed = true;
        DialogResult = true;
    }

    private void Window_OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_confirmed) e.Cancel = true;
    }
}
