using NginxTools.Services;

namespace NginxTools.Commands
{
    public static class StartupCommand
    {
        public static async Task<int> RunAsync(string nginxDir, Settings settings, bool skipCfCheck = false)
        {
            var nginx = new NginxController(nginxDir);
            Console.WriteLine($"Каталог NGINX: {nginx.NginxDir}");

            if (nginx.IsRunning())
            {
                Console.Error.WriteLine("NGINX уже запущен. Для применения изменений используйте 'restart'.");
                return 1;
            }

            Console.WriteLine("Обновление IP-диапазонов перед запуском...\n");
            var updater = new IpSourceUpdater(nginx, settings);
            await updater.RunAllAsync();
            Console.WriteLine();

            if (!File.Exists(nginx.ConfigPath))
            {
                Console.Error.WriteLine($"Не найден конфигурационный файл: {nginx.ConfigPath}");
                return 1;
            }

            Console.WriteLine("Проверка конфигурации (nginx -t)...");
            if (await nginx.TestConfigAsync() != 0)
            {
                Console.Error.WriteLine("Конфигурация некорректна. Запуск отменён.");
                return 1;
            }

            Console.WriteLine("Запуск NGINX...");
            var pid = nginx.Start();
            Console.WriteLine($"NGINX запущен (PID {pid}).");
            return 0;
        }
    }
}
