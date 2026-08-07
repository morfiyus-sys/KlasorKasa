using System.Security.AccessControl;
using System.Security.Principal;

namespace KlasorKasa.Services;

public sealed class AclService(WindowsIdentityService identity, LoggingService logging)
{
    private static readonly SecurityIdentifier SystemSid = new(WellKnownSidType.LocalSystemSid, null);

    public string? CaptureDirectorySddl(string path)
    {
        try
        {
            const AccessControlSections sections = AccessControlSections.Access | AccessControlSections.Owner | AccessControlSections.Group;
            return new DirectoryInfo(path).GetAccessControl(sections)
                .GetSecurityDescriptorSddlForm(sections);
        }
        catch (Exception ex)
        {
            logging.Log("AclCapture", path, "Unavailable", ex);
            return null;
        }
    }

    public void ApplyOwnerOnly(string rootPath)
    {
        var owner = new SecurityIdentifier(identity.CurrentSid);
        ApplyDirectory(rootPath, owner);
        foreach (var directory in Directory.EnumerateDirectories(rootPath, "*", SearchOption.AllDirectories)) ApplyDirectory(directory, owner);
        foreach (var file in Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories)) ApplyFile(file, owner);
        logging.Log("AclApply", rootPath, "Success");
    }

    public void RestoreDirectorySddl(string path, string? sddl)
    {
        if (string.IsNullOrWhiteSpace(sddl))
        {
            ResetTreeToInherited(path);
            logging.Log("AclRestore", path, "InheritedFallback");
            return;
        }
        var security = new DirectorySecurity();
        security.SetSecurityDescriptorSddlForm(sddl, AccessControlSections.Access | AccessControlSections.Owner | AccessControlSections.Group);
        new DirectoryInfo(path).SetAccessControl(security);
        logging.Log("AclRestore", path, "Success");
    }

    private static void ResetTreeToInherited(string rootPath)
    {
        foreach (var file in Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories)) ResetFile(file);
        foreach (var directory in Directory.EnumerateDirectories(rootPath, "*", SearchOption.AllDirectories).OrderByDescending(p => p.Length))
            ResetDirectory(directory);
        ResetDirectory(rootPath);
    }

    private static void ResetDirectory(string path)
    {
        var info = new DirectoryInfo(path);
        var security = info.GetAccessControl(AccessControlSections.Access);
        security.SetAccessRuleProtection(false, false);
        foreach (FileSystemAccessRule rule in security.GetAccessRules(true, false, typeof(SecurityIdentifier)))
            security.RemoveAccessRuleSpecific(rule);
        info.SetAccessControl(security);
    }

    private static void ResetFile(string path)
    {
        var info = new FileInfo(path);
        var security = info.GetAccessControl(AccessControlSections.Access);
        security.SetAccessRuleProtection(false, false);
        foreach (FileSystemAccessRule rule in security.GetAccessRules(true, false, typeof(SecurityIdentifier)))
            security.RemoveAccessRuleSpecific(rule);
        info.SetAccessControl(security);
    }

    private static void ApplyDirectory(string path, SecurityIdentifier owner)
    {
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(true, false);
        security.SetOwner(owner);
        var inheritance = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
        security.AddAccessRule(new FileSystemAccessRule(owner, FileSystemRights.FullControl, inheritance, PropagationFlags.None, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(SystemSid, FileSystemRights.FullControl, inheritance, PropagationFlags.None, AccessControlType.Allow));
        new DirectoryInfo(path).SetAccessControl(security);
    }

    private static void ApplyFile(string path, SecurityIdentifier owner)
    {
        var security = new FileSecurity();
        security.SetAccessRuleProtection(true, false);
        security.SetOwner(owner);
        security.AddAccessRule(new FileSystemAccessRule(owner, FileSystemRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(SystemSid, FileSystemRights.FullControl, AccessControlType.Allow));
        new FileInfo(path).SetAccessControl(security);
    }
}
