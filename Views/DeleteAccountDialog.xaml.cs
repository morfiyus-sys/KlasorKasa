using System.ComponentModel;
using System.Windows;
using KlasorKasa.ViewModels;

namespace KlasorKasa.Views;

public partial class DeleteAccountDialog : Window
{
    private readonly DeleteAccountViewModel _viewModel;
    private bool _allowClose;

    public DeleteAccountDialog(DeleteAccountViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
        viewModel.CloseRequested += (_, result) =>
        {
            _allowClose = true;
            DialogResult = result;
        };
    }

    private void Window_OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_allowClose && _viewModel.IsBusy) e.Cancel = true;
    }
}
