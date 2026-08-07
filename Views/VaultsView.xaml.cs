using System.Windows;
using System.Windows.Controls;
using KlasorKasa.Models;
using KlasorKasa.ViewModels;
namespace KlasorKasa.Views;
public partial class VaultsView : UserControl
{
    public VaultsView() { InitializeComponent(); }
    private void More_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ProtectedFolder vault } button || DataContext is not VaultsViewModel vm) return;
        var menu = new ContextMenu();
        var copy = new MenuItem { Header = "Yolu Kopyala" }; copy.Click += (_, _) => Clipboard.SetText(vault.OriginalPath);
        var remove = new MenuItem { Header = "Korumayı Kaldır" }; remove.Click += (_, _) => vm.RemoveProtectionCommand.Execute(vault);
        menu.Items.Add(copy); menu.Items.Add(new Separator()); menu.Items.Add(remove);
        button.ContextMenu = menu; menu.IsOpen = true;
    }
}
