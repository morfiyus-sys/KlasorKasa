using System.Windows;
using KlasorKasa.ViewModels;

namespace KlasorKasa.Views;

public partial class RecoveryWindow : Window
{
    public RecoveryWindow(RecoveryViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.Completed += (_, _) => DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
