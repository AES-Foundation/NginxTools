using NginxTools.UI;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NginxTools
{
    public sealed class Settings
    {
        /// <summary>
        /// Явный путь к каталогу NGINX. Если <see langword="null"/> — используется автоопределение.
        /// </summary>
        [JsonPropertyName("nginxDir")]
        public string? NginxDir { get; set; }

        /// <summary>
        /// Искать ли NGINX рядом с исполняемым файлом.
        /// </summary>
        [JsonPropertyName("autoDetectNginx")]
        public bool AutoDetectNginx { get; set; } = true;

        /// <summary>
        /// Сколько секунд ждать плавного завершения перед принудительным kill.
        /// </summary>
        [JsonPropertyName("shutdownGraceTimeoutSeconds")]
        public int ShutdownGraceTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Заголовок, из которого берётся реальный IP. Единый на весь http-блок.
        /// </summary>
        [JsonPropertyName("realIpHeader")]
        public string RealIpHeader { get; set; } = "CF-Connecting-IP";

        /// <summary>
        /// Использовать рекурсивный поиск реального IP по цепочке доверенных прокси.
        /// </summary>
        [JsonPropertyName("realIpRecursive")]
        public bool RealIpRecursive { get; set; } = true;

        /// <summary>
        /// Каталог для бэкапов. <see langword="null"/> -> рядом с NGINX: "\nginxDir\-backups".
        /// </summary>
        [JsonPropertyName("backupsDir")]
        public string? BackupsDir { get; set; }

        /// <summary>
        /// Удалять старые бэкапы перед созданием нового. По умолчанию <see langword="false"/>.
        /// </summary>
        [JsonPropertyName("removeOldBackups")]
        public bool RemoveOldBackups { get; set; } = false;

        /// <summary>
        /// Источники IP-диапазонов. По умолчанию — только Cloudflare.
        /// </summary>
        [JsonPropertyName("ipSources")]
        public List<IpSourceSettings> IpSources { get; set; } = new()
    {
        new IpSourceSettings
        {
            Name = "cloudflare",
            Enabled = true,
            Ipv4Url = "https://www.cloudflare.com/ips-v4",
            Ipv6Url = "https://www.cloudflare.com/ips-v6",
            OutputFile = "cloudflare_set_real_ip_from.conf",
        },
    };

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
        };

        public static string SettingsPath => Path.Combine(AppContext.BaseDirectory, "nginxtools.settings.json");

        /// <summary>
        /// Загрузка настроек из конфигурации.
        /// </summary>
        /// <returns>Возвращает настройки.</returns>
        public static Settings Load()
        {
            try
            {
                if (!File.Exists(SettingsPath))
                    return new Settings();

                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<Settings>(json, JsonOptions)
                       ?? new Settings();
            }
            catch (Exception ex)
            {
                ConsoleUi.Fail($"Не удалось прочитать {SettingsPath}: {ex.Message}");
                ConsoleUi.WarnMsg("Используются настройки по умолчанию.");
                return new Settings();
            }
        }
    }

    public sealed class IpSourceSettings
    {
        /// <summary>
        /// Имя источника — используется в логах и для имени по умолчанию.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = "unnamed";

        /// <summary>
        /// Включен ли источник. Отключённые не обновляются и не включаются в сводный файл.
        /// </summary>
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// URL со списком IPv4-диапазонов (по одному в строке). <see langword="null" /> — IPv4 не используется.
        /// </summary>
        [JsonPropertyName("ipv4Url")]
        public string? Ipv4Url { get; set; }

        /// <summary>
        /// URL со списком IPv6-диапазонов. <see langword="null" /> — IPv6 не используется.
        /// </summary>
        [JsonPropertyName("ipv6Url")]
        public string? Ipv6Url { get; set; }

        /// <summary>
        /// Имя выходного .conf-файла (относительно каталога conf/ NGINX).
        /// </summary>
        [JsonPropertyName("outputFile")]
        public string OutputFile { get; set; } = "real_ip_source.conf";
    }
}
