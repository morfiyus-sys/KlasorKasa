using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace KlasorKasa.Services;

public static class ThemeManager
{
    public static void Apply(string theme)
    {
        var dark = theme == "Koyu" || (theme == "Sistem" && SystemPrefersDark());
        Set("WindowBackgroundBrush", dark ? "#202428" : "#EAF3F7");
        Set("SidebarBrush", dark ? "#272D32" : "#DFECF2");
        Set("CardBrush", dark ? "#2D3338" : "#F8FBFC");
        Set("PrimaryTextBrush", dark ? "#F3F5F7" : "#1B1B1B");
        Set("SecondaryTextBrush", dark ? "#B7C0C7" : "#5F6368");
        Set("BorderBrush", dark ? "#46515A" : "#D4E0E6");
    }

    private static bool SystemPrefersDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch { return false; }
    }

    private static void Set(string key, string color)
    {
        Application.Current.Resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
    }
}
