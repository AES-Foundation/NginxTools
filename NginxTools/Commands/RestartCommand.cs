using NginxTools.Services;

namespace NginxTools.Commands
{
    public static class RestartCommand
    {
        public static async Task<int> RunAsync(string nginxDir)
        {
            var nginx = new NginxController(nginxDir);
            Console.WriteLine($"Каталог NGINX: {nginx.NginxDir}");

            if (!nginx.IsRunning())
            {
                Console.Error.WriteLine(
                    "NGINX не запущен. Используйте команду 'startup' для первого запуска.");
                return 1;
            }

            Console.WriteLine("Проверка конфигурации (nginx -t)...");
            if (await nginx.TestConfigAsync() != 0)
            {
                Console.Error.WriteLine(
                    "Конфигурация некорректна. Перезапуск отменён — NGINX продолжает работать со старой конфигурацией.");
                return 1;
            }

            Console.WriteLine("Плавный перезапуск (nginx -s reload)...");
            var code = await nginx.ReloadAsync();
            if (code == 0)
            {
                Console.WriteLine("Перезапуск выполнен успешно.");
                return 0;
            }

            Console.Error.WriteLine($"Ошибка перезапуска, код {code}.");
            return code;
        }
    }
}
