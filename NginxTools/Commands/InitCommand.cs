using NginxTools.Models;
using NginxTools.Services;
using NginxTools.UI;

namespace NginxTools.Commands
{
    public static class InitCommand
    {
        /// <summary>
        /// Выполняет команду инициализации NGINX.
        /// </summary>
        /// <param name="nginxDir">Путь к исполняемому NGINX.</param>
        /// <returns>Возвращает <see langword="int"/> код процесса.</returns>
        public static async Task<int> RunAsync(string nginxDir)
        {
            if (Console.IsInputRedirected)
            {
                ConsoleUi.Fail("Команда init требует интерактивного терминала.");
                return 1;
            }

            ConsoleUi.ClearSafely();
            ConsoleUi.Banner();
            ConsoleUi.Dim($"Каталог установки: {nginxDir}");
            Console.WriteLine();

            if (Directory.Exists(nginxDir) &&
                (File.Exists(Path.Combine(nginxDir, "nginx.exe")) ||
                 File.Exists(Path.Combine(nginxDir, "nginx"))))
            {
                if (!ConsolePrompt.Confirm(
                        "В каталоге уже есть NGINX. Продолжить и переустановить?",
                        defaultValue: false))
                {
                    ConsoleUi.Dim("Операция отменена.");
                    return 0;
                }
                Console.WriteLine();
            }

            IReadOnlyList<NginxRelease> releases;
            ConsoleUi.Write("Получение списка версий с nginx.org... ", ConsoleUi.Muted);
            try
            {
                releases = await new NginxReleaseProvider().GetAvailableAsync();
                ConsoleUi.Ok("Готово.");
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                ConsoleUi.Fail($"Не удалось получить список версий: {ex.Message}");
                return 1;
            }

            if (releases.Count == 0)
            {
                ConsoleUi.Fail("Список версий пуст.");
                return 1;
            }
            Console.WriteLine();

            var menu = new InteractiveMenu<NginxRelease>("Выберите версию NGINX:", releases, r => $"{r.Version.PadRight(12)} │ " + $"{(r.Channel == "mainline" ? "Mainline (разработка)" : "Stable (стабильная)")}");

            NginxRelease selected;
            try
            {
                selected = menu.Show();
            }
            catch (OperationCanceledException)
            {
                ConsoleUi.Dim("Операция отменена.");
                return 0;
            }

            Console.WriteLine();
            ConsoleUi.WriteLine(
                $"Выбрано: {selected.Version} ({selected.Channel})",
                ConsoleUi.Success);
            Console.WriteLine();

            var installer = new NginxInstaller(nginxDir);
            using (var bar = new ProgressBar($"Скачивание nginx-{selected.Version}"))
            {
                try
                {
                    var progress = new Progress<(double p, long d, long t)>(v =>
                        bar.Report(v.p, v.d, v.t));

                    await installer.InstallAsync(selected, progress);
                    bar.Complete();
                }
                catch (Exception ex)
                {
                    ConsoleUi.Fail($"Ошибка установки: {ex.Message}");
                    return 1;
                }
            }

            ConsoleUi.Ok("Установка завершена.");
            ConsoleUi.WriteLine($"Каталог: {nginxDir}", ConsoleUi.Muted);
            Console.WriteLine();

            ConsoleUi.Rule("Дальнейшие шаги");
            Console.WriteLine("  1. Утилита уже нашла NGINX в стандартном каталоге.");
            Console.WriteLine();
            Console.WriteLine("  2. Проверить статус:");
            ConsoleUi.WriteLine("     nginxtools status", ConsoleUi.Accent);
            Console.WriteLine();
            Console.WriteLine("  3. Запустить NGINX (с проверкой конфигурации):");
            ConsoleUi.WriteLine("     nginxtools startup", ConsoleUi.Accent);
            Console.WriteLine();
            Console.WriteLine("  4. Обновить IP-диапазоны Cloudflare:");
            ConsoleUi.WriteLine("     nginxtools update-ips", ConsoleUi.Accent);
            Console.WriteLine();

            if (ConsolePrompt.Confirm("Запустить NGINX сейчас?", defaultValue: true))
            {
                var settings = Settings.Load();
                Console.WriteLine();
                return await StartupCommand.RunAsync(nginxDir, settings);
            }

            return 0;
        }
    }
}
