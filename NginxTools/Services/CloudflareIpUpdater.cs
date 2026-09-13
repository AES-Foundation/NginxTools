using System.Text;

namespace NginxTools.Services
{
    public class CloudflareIpUpdater
    {
        private static readonly HttpClient Http = new();
        private readonly string _nginxDir;

        public CloudflareIpUpdater(string nginxDir) => _nginxDir = nginxDir;

        public async Task<bool> RunAsync(bool dryRun = false)
        {
            var ipv4 = await FetchAsync("https://www.cloudflare.com/ips-v4");
            var ipv6 = await FetchAsync("https://www.cloudflare.com/ips-v6");

            var content = BuildConfig(ipv4, ipv6);
            var confPath = Path.Combine(_nginxDir, "conf", "cloudflare_set_real_ip_from.conf");

            if (File.Exists(confPath) &&
                await File.ReadAllTextAsync(confPath) == content)
            {
                Console.WriteLine("Изменений нет. Перезагрузка не требуется.");
                return true;
            }

            if (dryRun)
            {
                Console.WriteLine("Будут применены следующие изменения:\n" + content);
                return true;
            }

            // Пишем во временный файл, затем атомарно подменяем
            var tmp = confPath + ".tmp";
            await File.WriteAllTextAsync(tmp, content, new UTF8Encoding(false));
            File.Move(tmp, confPath, overwrite: true);

            // Проверяем конфиг и перезагружаем
            if (!await NginxController.TestConfigAsync(_nginxDir))
            {
                Console.Error.WriteLine("Конфигурация NGINX некорректна. Отмена.");
                return false;
            }

            return await NginxController.ReloadAsync(_nginxDir);
        }

        private static async Task<string[]> FetchAsync(string url) =>
            (await Http.GetStringAsync(url))
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        private static string BuildConfig(string[] ipv4, string[] ipv6)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Автоматически сгенерированный файл. Не редактируйте вручную.");
            sb.AppendLine();
            sb.AppendLine("# --- IPv4 ---");
            foreach (var ip in ipv4) sb.AppendLine($"set_real_ip_from {ip};");
            sb.AppendLine();
            sb.AppendLine("# --- IPv6 ---");
            foreach (var ip in ipv6) sb.AppendLine($"set_real_ip_from {ip};");
            sb.AppendLine();
            sb.AppendLine("real_ip_header CF-Connecting-IP;");
            sb.AppendLine("real_ip_recursive on;");
            return sb.ToString().Replace("\r\n", "\n"); // LF везде
        }
    }
}
