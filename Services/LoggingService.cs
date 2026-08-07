using System.Text;

namespace KlasorKasa.Services;

public sealed class LoggingService(AppPaths paths)
{
    private readonly object _gate = new();
    private bool _disabled;
    public string LogPath => Path.Combine(paths.Logs, "app.log");

    public void Disable()
    {
        lock (_gate) _disabled = true;
    }

    public void Log(string action, string? folder = null, string? result = null, Exception? exception = null)
    {
        var safeFolder = folder?.Replace('\r', ' ').Replace('\n', ' ');
        var safeError = exception is null ? null : $"{exception.GetType().Name}: {exception.Message.Replace('\r', ' ').Replace('\n', ' ')}" +
            (string.IsNullOrWhiteSpace(exception.StackTrace) ? string.Empty : Environment.NewLine + exception.StackTrace);
        var builder = new StringBuilder()
            .AppendLine(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"))
            .AppendLine($"Action: {action}")
            .AppendLine($"WindowsUser: {Environment.UserDomainName}\\{Environment.UserName}");
        if (!string.IsNullOrWhiteSpace(safeFolder)) builder.AppendLine($"Folder: {safeFolder}");
        if (!string.IsNullOrWhiteSpace(result)) builder.AppendLine($"Result: {result}");
        if (!string.IsNullOrWhiteSpace(safeError)) builder.AppendLine($"Error: {safeError}");
        builder.AppendLine();
        lock (_gate)
        {
            if (_disabled) return;
            File.AppendAllText(LogPath, builder.ToString(), Encoding.UTF8);
        }
    }
}
