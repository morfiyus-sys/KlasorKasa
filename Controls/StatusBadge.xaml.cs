using System.Windows;
using System.Windows.Controls;
using KlasorKasa.Models;
namespace KlasorKasa.Controls;
public partial class StatusBadge : UserControl
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(StatusBadge), new PropertyMetadata(string.Empty));
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(nameof(State), typeof(VaultState), typeof(StatusBadge), new PropertyMetadata(VaultState.Locked));
    public VaultState State { get => (VaultState)GetValue(StateProperty); set => SetValue(StateProperty, value); }
    public StatusBadge() { InitializeComponent(); }
}
