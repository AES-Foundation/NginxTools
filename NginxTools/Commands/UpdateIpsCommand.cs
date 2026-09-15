using NginxTools.Services;
using NginxTools.UI;

namespace NginxTools.Commands
{
    public static class UpdateIpsCommand
    {
        /// <summary>
        /// Выполняет команду обновления IP диапазонов.
        /// </summary>
        /// <param name="nginxDir">Путь к исполняемому NGINX.</param>
        /// <param name="settings">Настройки конфигурации.</param>
        /// <param name="reload">Необходимо перезагрузить?</param>
        /// <returns>Возвращает <see langword="int"/> код процесса.</returns>
        public static async Task<int> RunAsync(string nginxDir, Settings settings, bool reload)
        {
            var nginx = new NginxController(nginxDir);
            var updater = new IpSourceUpdater(nginx, settings);

            ConsoleUi.Rule("Обновление IP-диапазонов");

            var results = await updater.RunAllAsync();

            Console.WriteLine();
            ConsoleUi.Rule("Результат");
            foreach (var r in results)
            {
                if (!r.Ok)
                {
                    ConsoleUi.Fail($"{r.Name,-20} ошибка: {r.Error}");
                    continue;
                }

                var status = r.Changed ? "обновлён" : "без изменений";
                var counts = new List<string>();
                if (r.Ipv4Count > 0) counts.Add($"{r.Ipv4Count} IPv4");
                if (r.Ipv6Count > 0) counts.Add($"{r.Ipv6Count} IPv6");

                ConsoleUi.Ok($"{r.Name,-20} {status,-15} {string.Join(", ", counts)}");
            }

            var anyChanged = results.Any(r => r.Ok && r.Changed);
            var anyFailed = results.Any(r => !r.Ok);

            PrintIntegrationHint(nginx);

            if (anyChanged && reload)
            {
                ConsoleUi.WarnMsg("Обнаружены изменения. Проверяем конфигурацию и перезагружаем NGINX...");

                if (await nginx.TestConfigAsync() != 0)
                {
                    ConsoleUi.Fail("Конфигурация некорректна. NGINX НЕ перезагружен — работает со старой версией.");
                    return 1;
                }

                if (await nginx.ReloadAsync() != 0)
                {
                    ConsoleUi.Fail("Ошибка при перезагрузке NGINX.");
                    return 1;
                }
                ConsoleUi.Ok("NGINX успешно перезагружен.");
            }
            else if (anyChanged && !reload)
            {
                ConsoleUi.Ok("Изменения записаны. Чтобы применить, выполните: nginxtools restart");
            }

            return anyFailed ? 1 : 0;
        }

        /// <summary>
        /// Отображает памятку интеграции конфигурации IP диапазонов.
        /// </summary>
        /// <param name="nginx">NGINX экземпляр.</param>
        private static void PrintIntegrationHint(NginxController nginx)
        {
            var mainConfig = nginx.ConfigPath;
            if (!File.Exists(mainConfig)) return;

            var content = File.ReadAllText(mainConfig);
            if (content.Contains("conf/real_ip.conf") || content.Contains("real_ip.conf;"))
                return;

            string[] lines =
            {
                @"В ваш nginx.conf ещё не добавлен include. Откройте файл:",
                @$"     {mainConfig}",
                @"   и внутри блока http { ... } добавьте строку:",
                @"     include conf/real_ip.conf;"
            };
            Console.WriteLine();
            Console.WriteLine(lines);
        }
    }
}
