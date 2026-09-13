using System.Text.Json;
using System.Text.Json.Serialization;

namespace NginxTools
{
    public sealed class Settings
    {
        /// <summary>Явный путь к каталогу NGINX. Если null — используется автоопределение.</summary>
        [JsonPropertyName("nginxDir")]
        public string? NginxDir { get; set; }

        /// <summary>Искать ли NGINX рядом с исполняемым файлом.</summary>
        [JsonPropertyName("autoDetectNginx")]
        public bool AutoDetectNginx { get; set; } = true;

        /// <summary>Сколько секунд ждать плавного завершения перед принудительным kill.</summary>
        [JsonPropertyName("shutdownGraceTimeoutSeconds")]
        public int ShutdownGraceTimeoutSeconds { get; set; } = 30;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
        };

        public static string SettingsPath =>
            Path.Combine(AppContext.BaseDirectory, "nginxtools.settings.json");

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
                Console.Error.WriteLine($"Не удалось прочитать {SettingsPath}: {ex.Message}");
                Console.Error.WriteLine("Используются настройки по умолчанию.");
                return new Settings();
            }
        }
    }
}
