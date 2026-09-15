namespace NginxTools.Models
{
    public sealed class NginxRelease
    {
        public required string Version { get; init; }
        public required string Channel { get; init; }
        public required string DownloadUrl { get; init; }
        public required string FileName { get; init; }
        public required string Platform { get; init; }
        public long SizeBytes { get; init; }

        public string ChannelDisplay => Channel == "mainline" ? "Mainline (Разработка)" : "Stable (стабильная)";
        public string SizeDisplay => SizeBytes > 0 ? $"{SizeBytes / 1024.0 / 1024.0:F1} MB" : "—";
    }
}
