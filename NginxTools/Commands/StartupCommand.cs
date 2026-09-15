using NginxTools.Services;
using NginxTools.UI;

namespace NginxTools.Commands
{
    public static class StartupCommand
    {
        /// <summary>
        /// Выполняет команду запуска NGINX.
        /// </summary>
        /// <param name="nginxDir">Путь к исполняемому NGINX.</param>
        /// <param name="settings">Параметры конфигурации.</param>
        /// <param name="skipCfCheck">Пропустить проверку диапазонов.</param>
        /// <returns>Возвращает <see langword="int"/> код процесса.</returns>
        public static async Task<int> RunAsync(string nginxDir, Settings settings, bool skipCfCheck = false)
        {
            var nginx = new NginxController(nginxDir);
            ConsoleUi.Dim($"Каталог NGINX: {nginx.NginxDir}");

            if (nginx.IsRunning())
            {
                ConsoleUi.Fail("NGINX уже запущен. Для применения изменений используйте 'restart'.");
                return 1;
            }

            ConsoleUi.WarnMsg("Обновление IP-диапазонов перед запуском...\n");
            var updater = new IpSourceUpdater(nginx, settings);
            await updater.RunAllAsync();
            Console.WriteLine();

            if (!File.Exists(nginx.ConfigPath))
            {
                ConsoleUi.Fail($"Не найден конфигурационный файл: {nginx.ConfigPath}");
                return 1;
            }

            ConsoleUi.WarnMsg("Проверка конфигурации (nginx -t)...");
            if (await nginx.TestConfigAsync() != 0)
            {
                ConsoleUi.Fail("Конфигурация некорректна. Запуск отменён.");
                return 1;
            }

            ConsoleUi.WarnMsg("Запуск NGINX...");
            var pid = nginx.Start();
            ConsoleUi.Ok($"NGINX запущен (PID {pid}).");
            return 0;
        }
    }
}
