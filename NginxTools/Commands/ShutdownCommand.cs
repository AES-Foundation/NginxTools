using NginxTools.Services;
using NginxTools.UI;

namespace NginxTools.Commands
{
    public static class ShutdownCommand
    {
        /// <summary>
        /// Выполняет команду отключения NGINX.
        /// </summary>
        /// <param name = "nginxDir" > Путь к исполняемому NGINX.</param>
        /// <param name="gracefulTimeout">Время ожидания, до принудительной остановки.</param>
        /// <returns>Возвращает <see langword="int"/> код процесса.</returns>
        public static async Task<int> RunAsync(string nginxDir, TimeSpan? gracefulTimeout = null)
        {
            var nginx = new NginxController(nginxDir);
            var timeout = gracefulTimeout ?? TimeSpan.FromSeconds(30);

            if (!nginx.IsRunning())
            {
                ConsoleUi.Fail("NGINX не запущен.");
                return 0;
            }

            ConsoleUi.WarnMsg("Плавное завершение (nginx -s quit). Ожидание завершения текущих запросов...");
            await nginx.QuitAsync();

            if (await nginx.WaitForStopAsync(timeout))
            {
                ConsoleUi.Ok("NGINX корректно завершён.");
                return 0;
            }

            ConsoleUi.WarnMsg($"NGINX не завершился за {timeout.TotalSeconds} секунд. Принудительное завершение...");
            nginx.KillAll();

            if (await nginx.WaitForStopAsync(TimeSpan.FromSeconds(5)))
            {
                ConsoleUi.Ok("NGINX принудительно завершён.");
                return 0;
            }

            ConsoleUi.Fail("Не удалось остановить NGINX.");
            return 1;
        }
    }
}
