using NginxTools.Services;
using NginxTools.UI;

namespace NginxTools.Commands
{
    public static class RestartCommand
    {
        /// <summary>
        /// Выполнение команды перезапуска NGINX.
        /// </summary>
        /// <param name="nginxDir">Путь к исполняемому NGINX.</param>
        /// <param name="settings">Настройки конфигурации.</param>
        /// <returns>Возвращает <see langword="int"/> код процесса.</returns>
        public static async Task<int> RunAsync(string nginxDir, Settings settings)
        {
            var nginx = new NginxController(nginxDir);
            ConsoleUi.WriteLine($"Каталог NGINX: {nginx.NginxDir}", ConsoleUi.Muted);

            if (!nginx.IsRunning())
            {
                ConsoleUi.Fail(
                    "NGINX не запущен. Используйте команду 'startup' для первого запуска.");
                return 1;
            }

            ConsoleUi.WarnMsg("Проверка конфигурации (nginx -t)...");
            if (await nginx.TestConfigAsync() != 0)
            {
                ConsoleUi.Fail(
                    "Конфигурация некорректна. Перезапуск отменён — NGINX продолжает работать со старой конфигурацией.");
                return 1;
            }

            ConsoleUi.WarnMsg("Плавный перезапуск (nginx -s reload)...");
            var code = await nginx.ReloadAsync();
            if (code == 0)
            {
                ConsoleUi.Ok("Перезапуск выполнен успешно.");
                return 0;
            }

            ConsoleUi.Fail($"Ошибка перезапуска, код {code}.");
            return code;
        }
    }
}
