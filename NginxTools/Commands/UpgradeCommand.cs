using NginxTools.Models;
using NginxTools.Services;
using NginxTools.UI;
using static NginxTools.Services.NginxUpgrader;

namespace NginxTools.Commands
{
    public static class UpgradeCommand
    {
        /// <summary>
        /// Выполняет команду обновления NGINX.
        /// </summary>
        /// <param name="nginxDir">Путь к исполняемому NGINX.</param>
        /// <param name="settings">Настройки конфигурации.</param>
        /// <param name="args">Аргументы команды.</param>
        /// <returns>Возвращает <see langword="int"/> код процесса.</returns>
        public static async Task<int> RunAsync(string nginxDir, Settings settings, string[] args)
        {
            if (Console.IsInputRedirected)
            {
                ConsoleUi.Fail("Команда upgrade требует интерактивного терминала.");
                return 1;
            }

            ConsoleUi.ClearSafely();
            ConsoleUi.Banner();

            var controller = new NginxController(nginxDir);
            if (!File.Exists(controller.NginxBinary))
            {
                ConsoleUi.Fail($"NGINX не найден: {controller.NginxBinary}");
                ConsoleUi.Dim("Используйте 'nginxtools init' для первичной установки.");
                return 1;
            }

            var upgrader = new NginxUpgrader();
            string current = await controller.GetVersion() ?? "unknown";
            ConsoleUi.Ok($"Текущая версия: {current}");

            ConsoleUi.Dim($"Каталог: {nginxDir}");

            var backups = new BackupManager(nginxDir, settings);
            ConsoleUi.Dim($"Бэкапы:  {backups.BackupsDir}");
            if (settings.RemoveOldBackups)
            {
                ConsoleUi.WarnMsg(
                    "removeOldBackups=true: все существующие бэкапы будут удалены перед созданием нового.");
            }
            Console.WriteLine();

            IReadOnlyList<NginxRelease> allReleases;
            ConsoleUi.Dim("Получение списка версий с nginx.org... ");
            try
            {
                allReleases = await new NginxReleaseProvider().GetAvailableAsync();
                ConsoleUi.Ok("Готово.");
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                ConsoleUi.Fail($"Не удалось получить список версий: {ex.Message}");
                return 1;
            }

            var releases = allReleases.Where(r => r.Version != current).ToList();
            if (releases.Count == 0)
            {
                ConsoleUi.Fail("Нет доступных версий для обновления.");
                return 1;
            }
            Console.WriteLine();

            var menu = new InteractiveMenu<NginxRelease>(
                "Выберите версию для установки:",
                releases,
                r =>
                {
                    string ver = r.Version.PadRight(12);
                    string channel = (r.Channel == "mainline"
                            ? "Mainline (разработка)"
                            : "Stable (стабильная)").PadRight(24);
                    string tag = current is not null && IsNewer(r.Version, current) ? "  ↑" : "";
                    return $"{ver} │ {channel}{tag}";
                });

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

            var direction = current is null
                ? $"Установить {selected.Version}?"
                : IsNewer(selected.Version, current)
                    ? $"Обновить {current} --> {selected.Version}?"
                    : $"Понизить {current} --> {selected.Version}? (откат на старую версию)";

            ConsoleUi.Dim("Будет создан полный бэкап каталога NGINX.");
            ConsoleUi.Dim("Пользовательские файлы (conf/, html/, logs/) не затрагиваются.");
            Console.WriteLine();

            if (!ConsolePrompt.Confirm(direction, defaultValue: true))
            {
                ConsoleUi.Dim("Операция отменена.");
                return 0;
            }

            Console.WriteLine();
            UpgradeResult result;
            using (var bar = new ProgressBar($"Скачивание nginx-{selected.Version}"))
            {
                try
                {
                    var progress = new Progress<(double p, long d, long t)>(v =>
                        bar.Report(v.p, v.d, v.t));

                    result = await upgrader.UpgradeAsync(
                        nginxDir,
                        selected,
                        settings,
                        progress);
                    bar.Complete();
                }
                catch (Exception ex)
                {
                    ConsoleUi.Fail($"Ошибка обновления: {ex.Message}");
                    ConsoleUi.Dim("Прежняя версия восстановлена автоматически из бэкапа.");
                    return 1;
                }
            }

            Console.WriteLine();
            ConsoleUi.Ok($"Обновление завершено: {result.PreviousVersion ?? "?"} --> {result.NewVersion}");
            ConsoleUi.Dim($"Бэкап: {result.Backup.Path}");
            ConsoleUi.Dim($"       {result.Backup.Name}  (v{result.Backup.Version})");

            if (result.WasRunning)
                ConsoleUi.Dim("NGINX был перезапущен автоматически.");
            else
                ConsoleUi.Dim("NGINX сейчас не запущен. Запустите: nginxtools startup");

            Console.WriteLine();
            ConsoleUi.Rule("Что дальше");
            Console.WriteLine("  - Проверить статус:");
            ConsoleUi.WriteLine("    nginxtools status", ConsoleUi.Accent);
            Console.WriteLine();
            Console.WriteLine("  - Обновить IP-диапазоны:");
            ConsoleUi.WriteLine("    nginxtools update-ips", ConsoleUi.Accent);
            Console.WriteLine();
            Console.WriteLine("  - Откатиться назад, если что-то не так:");
            ConsoleUi.WriteLine($"    nginxtools backup --time {result.Backup.Name}", ConsoleUi.Accent);
            Console.WriteLine();
            Console.WriteLine("  - Посмотреть все бэкапы:");
            ConsoleUi.WriteLine("    nginxtools backup --list", ConsoleUi.Accent);

            return 0;
        }

        /// <summary>
        /// Проверяет, является ли версия новей.
        /// </summary>
        /// <param name="a">Версия 1.</param>
        /// <param name="b">Версия 2.</param>
        /// <returns>Возвращает <see langword="true"/>, если версия 1 новее, чем версия 2, иначе <see langword="false"/>.</returns>
        private static bool IsNewer(string a, string b)
        {
            var va = Version.Parse(a);
            var vb = Version.Parse(b);
            return va > vb;
        }
    }
}
