using NginxTools.UI;
using System.Text;

namespace NginxTools.Services
{
    public sealed class IpSourceUpdater
    {
        private static readonly HttpClient Http = new()
        {
            Timeout = TimeSpan.FromSeconds(20),
            DefaultRequestHeaders = { { "User-Agent", "nginxtools/2.0" } },
        };

        private readonly NginxController _nginx;
        private readonly Settings _settings;

        public IpSourceUpdater(NginxController nginx, Settings settings)
        {
            _nginx = nginx;
            _settings = settings;
        }

        public sealed record SourceResult(
            string Name,
            bool Ok,
            bool Changed,
            int Ipv4Count,
            int Ipv6Count,
            string? Error);

        /// <summary>
        /// Выполняет процесс обновления IP диапазонов.
        /// </summary>
        /// <param name="ct">Токен отмены.</param>
        /// <returns>Возвращает результаты обновлений.</returns>
        public async Task<IReadOnlyList<SourceResult>> RunAllAsync(CancellationToken ct = default)
        {
            var results = new List<SourceResult>();

            foreach (var source in _settings.IpSources)
            {
                if (!source.Enabled)
                {
                    ConsoleUi.WarnMsg($"[{source.Name}] отключён в настройках — пропускаем.");
                    continue;
                }

                ConsoleUi.Dim($"[{source.Name}] обновление...");
                results.Add(await UpdateOneAsync(source, ct));
            }

            if (results.Any(r => r.Changed))
            {
                WriteAggregateFile(results);
                ConsoleUi.Ok("Сводный файл real_ip.conf обновлён.");
            }
            else
            {
                ConsoleUi.Dim("Сводный файл real_ip.conf не изменился.");
            }

            return results;
        }

        /// <summary>
        /// Выполняет все действия обновления в одном методе.
        /// </summary>
        /// <param name="source">Источник данных из конфигурации.</param>
        /// <param name="ct">Токен отмены.</param>
        /// <returns>Возвращает <see langword="SourceResult"/> с итоговым результатом.</returns>
        private async Task<SourceResult> UpdateOneAsync(IpSourceSettings source, CancellationToken ct)
        {
            try
            {
                var ipv4 = string.IsNullOrWhiteSpace(source.Ipv4Url)
                    ? Array.Empty<string>()
                    : await FetchAsync(source.Ipv4Url!, ct);

                var ipv6 = string.IsNullOrWhiteSpace(source.Ipv6Url)
                    ? Array.Empty<string>()
                    : await FetchAsync(source.Ipv6Url!, ct);

                if (ipv4.Length == 0 && ipv6.Length == 0)
                    return new SourceResult(source.Name, false, false, 0, 0,
                        "получен пустой список");

                var body = BuildBody(source, ipv4, ipv6);
                var path = Path.Combine(_nginx.ConfigDir, source.OutputFile);

                var changed = WriteIfChanged(path, body);

                return new SourceResult(source.Name, true, changed,
                    ipv4.Length, ipv6.Length, null);
            }
            catch (Exception ex)
            {
                return new SourceResult(source.Name, false, false, 0, 0, ex.Message);
            }
        }

        /// <summary>
        /// Делает запрос, на указанные источники.
        /// </summary>
        /// <param name="url">URL адрес с данными.</param>
        /// <param name="ct">Токен отмены.</param>
        /// <returns>Возвращает массив <see langword="string"/>[] с полученными данными диапазонов.</returns>
        private static async Task<string[]> FetchAsync(string url, CancellationToken ct)
        {
            var text = await Http.GetStringAsync(url, ct);
            return text
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(line => !line.StartsWith('#'))
                .ToArray();
        }

        /// <summary>
        /// Собирает содержимое файла БЕЗ timestamp — чтобы можно было сравнивать.
        /// </summary>
        /// <param name="source">Источник IP диапазонов из конфигураций.</param>
        /// <param name="ipv4">Массив с данными IPv4.</param>
        /// <param name="ipv6">Массив с данными IPv6.</param>
        /// <returns>Возвращает <see langword="string"/> с телом информации для файла.</returns>
        private static string BuildBody(IpSourceSettings source, string[] ipv4, string[] ipv6)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# Источник: {source.Name}");
            if (!string.IsNullOrWhiteSpace(source.Ipv4Url))
                sb.AppendLine($"# IPv4: {source.Ipv4Url}");
            if (!string.IsNullOrWhiteSpace(source.Ipv6Url))
                sb.AppendLine($"# IPv6: {source.Ipv6Url}");
            sb.AppendLine("# НЕ РЕДАКТИРУЙТЕ ВРУЧНУЮ — файл генерируется автоматически");
            sb.AppendLine();

            if (ipv4.Length > 0)
            {
                sb.AppendLine("# --- IPv4 ---");
                foreach (var ip in ipv4) sb.AppendLine($"set_real_ip_from {ip};");
                sb.AppendLine();
            }

            if (ipv6.Length > 0)
            {
                sb.AppendLine("# --- IPv6 ---");
                foreach (var ip in ipv6) sb.AppendLine($"set_real_ip_from {ip};");
            }

            return sb.ToString().Replace("\r\n", "\n").TrimEnd() + "\n";
        }

        /// <summary>
        /// Запись, если есть изменения.
        /// </summary>
        /// <param name="path">Путь сохраняемого файла.</param>
        /// <param name="body">Тело данных, ранее сгенерированное.</param>
        /// <returns>Возвращает <see langword="true"/> в случае успешной записи; <see langword="false"/> при отсутствии изменений.</returns>
        private static bool WriteIfChanged(string path, string body)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            if (File.Exists(path))
            {
                var existing = File.ReadAllText(path);
                if (StripTimestamp(existing) == StripTimestamp(body))
                    return false;
            }

            var withHeader =
                $"# Автоматически сгенерировано: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                $"# Утилита: nginxtools\n" +
                body;

            var tmp = path + ".tmp";
            File.WriteAllText(tmp, withHeader, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(tmp, path, overwrite: true);
            return true;
        }

        /// <summary>
        /// Получение временного интервала в строке.
        /// </summary>
        /// <param name="content">Контент, в котором необходимо найти.</param>
        /// <returns>Возвращает <see langword="string" /> со временем.</returns>
        private static string StripTimestamp(string content)
        {
            var lines = content.Split('\n');
            var from = lines.Length >= 2 && lines[0].StartsWith("# Автоматически")
                ? 2 : 0;
            return string.Join('\n', lines.Skip(from));
        }

        /// <summary>
        /// Пишет сводный файл real_ip.conf с real_ip_header и include на все источники.
        /// </summary>
        /// <param name="results">Результат от источников.</param>
        private void WriteAggregateFile(IReadOnlyList<SourceResult> results)
        {
            var active = results.Where(r => r.Ok).Select(r => r.Name).ToHashSet();
            var sb = new StringBuilder();

            sb.AppendLine($"# Автоматически сгенерировано: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("# Утилита: nginxtools");
            sb.AppendLine("# Сводный файл: подключает все активные источники real_ip_from.");
            sb.AppendLine("# Подключите его в nginx.conf внутри блока http { ... }:");
            sb.AppendLine("#     include conf/real_ip.conf;");
            sb.AppendLine("# НЕ РЕДАКТИРУЙТЕ ВРУЧНУЮ — файл генерируется автоматически");
            sb.AppendLine();

            sb.AppendLine($"real_ip_header {_settings.RealIpHeader};");
            sb.AppendLine(_settings.RealIpRecursive
                ? "real_ip_recursive on;"
                : "real_ip_recursive off;");
            sb.AppendLine();

            foreach (var src in _settings.IpSources.Where(s => s.Enabled && active.Contains(s.Name)))
                sb.AppendLine($"include {src.OutputFile};");

            var path = Path.Combine(_nginx.ConfigDir, "real_ip.conf");
            File.WriteAllText(path, sb.ToString().Replace("\r\n", "\n"),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
    }
}
