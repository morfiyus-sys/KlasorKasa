using System.Text.Json;
using KlasorKasa.Models;
using Microsoft.Win32;

namespace KlasorKasa.Services;

public sealed class SettingsService(AppPaths paths, LoggingService logging)
{
    public AppSettings Current { get; private set; } = new();

    public async Task LoadAsync()
    {
        try
        {
            if (File.Exists(paths.SettingsFile))
                Current = JsonSerializer.Deserialize<AppSettings>(await File.ReadAllBytesAsync(paths.SettingsFile), AuthenticationService.JsonOptions) ?? new();
        }
        catch (Exception ex) { logging.Log("SettingsLoad", result: "DefaultsUsed", exception: ex); Current = new(); }
    }

    public async Task SaveAsync()
    {
        await AtomicFile.WriteAllBytesAsync(paths.SettingsFile, JsonSerializer.SerializeToUtf8Bytes(Current, AuthenticationService.JsonOptions));
        using var runKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
        if (Current.StartWithWindows)
        {
            var executable = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "KlasorKasa.exe");
            runKey.SetValue("KlasorKasa", $"\"{executable}\"");
        }
        else runKey.DeleteValue("KlasorKasa", false);
        logging.Log("SettingsSave", result: "Success");
    }
}
