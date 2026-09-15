namespace NginxTools.Models
{
    public sealed record BackupInfo(string Name, string Path, DateTime Timestamp, string Version, string Reason);
}
