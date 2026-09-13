using NginxTools.Services;

namespace NginxTools.Commands
{
    public static class ShutdownCommand
    {
        public static async Task<int> RunAsync(string nginxDir, TimeSpan? gracefulTimeout = null)
        {
            var nginx = new NginxController(nginxDir);
            var timeout = gracefulTimeout ?? TimeSpan.FromSeconds(30);

            if (!nginx.IsRunning())
            {
                Console.WriteLine("NGINX не запущен.");
                return 0;
            }

            Console.WriteLine("Плавное завершение (nginx -s quit). Ожидание завершения текущих запросов...");
            await nginx.QuitAsync();

            if (await nginx.WaitForStopAsync(timeout))
            {
                Console.WriteLine("NGINX корректно завершён.");
                return 0;
            }

            Console.WriteLine($"NGINX не завершился за {timeout.TotalSeconds} секунд. Принудительное завершение...");
            nginx.KillAll();

            if (await nginx.WaitForStopAsync(TimeSpan.FromSeconds(5)))
            {
                Console.WriteLine("NGINX принудительно завершён.");
                return 0;
            }

            Console.Error.WriteLine("Не удалось остановить NGINX.");
            return 1;
        }
    }
}
