using NginxTools.Services;

namespace NginxTools.Commands
{
    public static class UpdateIpsCommand
    {
        public static async Task<int> RunAsync(string nginxDir, Settings settings, bool reload)
        {
            var nginx = new NginxController(nginxDir);
            var updater = new IpSourceUpdater(nginx, settings);

            Console.WriteLine("=== Обновление IP-диапазонов ===\n");

            var results = await updater.RunAllAsync();

            Console.WriteLine();
            Console.WriteLine("--- Результат ---");
            foreach (var r in results)
            {
                if (!r.Ok)
                {
                    Console.WriteLine($"[ERROR] {r.Name,-20} ошибка: {r.Error}");
                    continue;
                }

                var status = r.Changed ? "обновлён" : "без изменений";
                var counts = new List<string>();
                if (r.Ipv4Count > 0) counts.Add($"{r.Ipv4Count} IPv4");
                if (r.Ipv6Count > 0) counts.Add($"{r.Ipv6Count} IPv6");

                Console.WriteLine($"[SUCCESS] {r.Name,-20} {status,-15} {string.Join(", ", counts)}");
            }

            var anyChanged = results.Any(r => r.Ok && r.Changed);
            var anyFailed = results.Any(r => !r.Ok);

            PrintIntegrationHint(nginx);

            if (anyChanged && reload)
            {
                Console.WriteLine("\nОбнаружены изменения. Проверяем конфигурацию и перезагружаем NGINX...\n");

                if (await nginx.TestConfigAsync() != 0)
                {
                    Console.Error.WriteLine(
                        "Конфигурация некорректна. NGINX НЕ перезагружен — работает со старой версией.");
                    return 1;
                }

                if (await nginx.ReloadAsync() != 0)
                {
                    Console.Error.WriteLine("Ошибка при перезагрузке NGINX.");
                    return 1;
                }
                Console.WriteLine("NGINX успешно перезагружен.");
            }
            else if (anyChanged && !reload)
            {
                Console.WriteLine(
                    "\nИзменения записаны. Чтобы применить, выполните: nginxtools restart");
            }

            return anyFailed ? 1 : 0;
        }

        private static void PrintIntegrationHint(NginxController nginx)
        {
            var mainConfig = nginx.ConfigPath;
            if (!File.Exists(mainConfig)) return;

            var content = File.ReadAllText(mainConfig);
            if (content.Contains("conf/real_ip.conf") || content.Contains("real_ip.conf;"))
                return;

            Console.WriteLine();
            Console.WriteLine("[WARNING] В ваш nginx.conf ещё не добавлен include. Откройте файл:");
            Console.WriteLine($"     {mainConfig}");
            Console.WriteLine("   и внутри блока http { ... } добавьте строку:");
            Console.WriteLine("     include conf/real_ip.conf;");
        }
    }
}
