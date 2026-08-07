using System.Windows;
using System.Windows.Controls;

namespace KlasorKasa.Controls;

public partial class PasswordBoxControl : UserControl
{
    private bool _internalUpdate;
    public static readonly DependencyProperty PasswordProperty = DependencyProperty.Register(nameof(Password), typeof(string), typeof(PasswordBoxControl),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPasswordChanged));
    public string Password { get => (string)GetValue(PasswordProperty); set => SetValue(PasswordProperty, value); }
    public PasswordBoxControl() { InitializeComponent(); }
    public void FocusPassword()
    {
        if (SecretBox.Visibility == Visibility.Visible) SecretBox.Focus();
        else VisibleBox.Focus();
    }
    private static void OnPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (PasswordBoxControl)d;
        if (control._internalUpdate) return;
        control.SecretBox.Password = e.NewValue as string ?? string.Empty;
    }
    private void SecretBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        _internalUpdate = true; Password = SecretBox.Password; _internalUpdate = false;
    }
    private void ToggleButton_OnClick(object sender, RoutedEventArgs e)
    {
        var show = SecretBox.Visibility == Visibility.Visible;
        if (show) { VisibleBox.Text = SecretBox.Password; SecretBox.Visibility = Visibility.Collapsed; VisibleBox.Visibility = Visibility.Visible; VisibleBox.Focus(); VisibleBox.CaretIndex = VisibleBox.Text.Length; }
        else { SecretBox.Password = VisibleBox.Text; VisibleBox.Visibility = Visibility.Collapsed; SecretBox.Visibility = Visibility.Visible; SecretBox.Focus(); }
        ToggleButton.Content = show ? "Gizle" : "Göster";
    }
}
