using System.Security.Principal;

namespace KlasorKasa.Services;

public sealed class WindowsIdentityService
{
    public string CurrentSid => WindowsIdentity.GetCurrent().User?.Value
        ?? throw new InvalidOperationException("Windows kullanıcı SID bilgisi alınamadı.");
    public string CurrentAccount => $"{Environment.UserDomainName}\\{Environment.UserName}";
    public bool IsCurrentUser(string ownerSid) => string.Equals(CurrentSid, ownerSid, StringComparison.OrdinalIgnoreCase);
}
