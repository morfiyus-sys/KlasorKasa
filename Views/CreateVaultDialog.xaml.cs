using System.Windows;
using KlasorKasa.ViewModels;
namespace KlasorKasa.Views;
public partial class CreateVaultDialog : Window
{
    public CreateVaultDialog(CreateVaultViewModel vm) { InitializeComponent(); DataContext = vm; vm.CloseRequested += (_, result) => DialogResult = result; }
}
