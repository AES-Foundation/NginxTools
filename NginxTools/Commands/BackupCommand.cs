using NginxTools.Models;
using NginxTools.Services;
using NginxTools.UI;

namespace NginxTools.Commands
{
    public static class BackupCommand
    {
        /// <summary>
        /// Выполняет команду восстановления NGINX.
        /// </summary>
        /// <param name="nginxDir">Путь к исполняемому NGINX.</param>
        /// <param name="settings">Настроки конфигурации.</param>
        /// <param name="args">Аргументы команды.</param>
        /// <returns>Возвращает <see langword="int"/> код процесса.</returns>
        public static async Task<int> RunAsync(string nginxDir, Settings settings, string[] args)
        {
            var mgr = new BackupManager(nginxDir, settings);
            var backups = mgr.List();

            if (args.Contains("--list", StringComparer.OrdinalIgnoreCase))
            {
                if (backups.Count == 0)
                {
                    ConsoleUi.WarnMsg($"Бэкапы не найдены в {mgr.BackupsDir}");
                    return 0;
                }

                ConsoleUi.ClearSafely();
                ConsoleUi.Banner();
                ConsoleUi.WriteLine($"Каталог бэкапов: {mgr.BackupsDir}", ConsoleUi.Muted);
                Console.WriteLine();

                var headers = new[] { "Имя", "Версия", "Создан", "Причина" };
                var rows = backups.Select(b => new[]
                {
                    b.Name,
                    string.IsNullOrWhiteSpace(b.Version) || b.Version == "unknown"
                        ? "—"
                        : "v" + b.Version,
                    b.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                    string.IsNullOrWhiteSpace(b.Reason) ? "—" : b.Reason,
                }).ToList();

                Table.Render(headers, rows, maxWidths: new[] { 16, 10, 19, 60 });

                Console.WriteLine();
                ConsoleUi.WriteLine($"Всего: {backups.Count}", ConsoleUi.Muted);
                return 0;
            }

            if (backups.Count == 0)
            {
                ConsoleUi.Fail($"Бэкапы не найдены в {mgr.BackupsDir}");
                ConsoleUi.Dim("Бэкапы создаются автоматически при 'nginxtools upgrade'.");
                return 1;
            }

            var timeArg = GetArg(args, "--time");
            BackupInfo selected;

            if (timeArg is not null)
            {
#pragma warning disable CS8600 // Преобразование литерала, допускающего значение NULL или возможного значения NULL в тип, не допускающий значение NULL.
                selected = backups.FirstOrDefault(b => b.Name == timeArg)
                    ?? backups.FirstOrDefault(b => b.Name.StartsWith(timeArg, StringComparison.Ordinal));
#pragma warning restore CS8600 // Преобразование литерала, допускающего значение NULL или возможного значения NULL в тип, не допускающий значение NULL.
                if (selected is null)
                {
                    ConsoleUi.Fail($"Бэкап '{timeArg}' не найден.");
                    ConsoleUi.Dim("Доступные: " + string.Join(", ", backups.Select(b => b.Name)));
                    return 1;
                }
            }
            else if (backups.Count == 1)
            {
                selected = backups[0];
            }
            else
            {
                if (Console.IsInputRedirected)
                {
                    ConsoleUi.Fail("Укажите --time <имя> при неинтерактивном запуске.");
                    return 1;
                }

                ConsoleUi.ClearSafely();
                ConsoleUi.Banner();
                ConsoleUi.WriteLine($"Каталог бэкапов: {mgr.BackupsDir}", ConsoleUi.Muted);
                Console.WriteLine();

                int nameW = backups.Max(b => b.Name.Length);
                int verW = Math.Max(6, backups.Max(b => ("v" + b.Version).Length));
                const int tsW = 19;
                int reasonW = Math.Clamp(backups.Max(b => b.Reason?.Length ?? 0), 20, 60);

                string RenderBackup(BackupInfo b)
                {
                    string name = b.Name.PadRight(nameW);
                    string ver = ("v" + (string.IsNullOrWhiteSpace(b.Version) ? "unknown" : b.Version)).PadRight(verW);
                    string ts = b.Timestamp.ToString("yyyy-MM-dd HH:mm:ss").PadRight(tsW);
                    string reason = b.Reason ?? "";
                    if (reason.Length > reasonW)
                        reason = reason.Substring(0, reasonW - 1) + "…";
                    else
                        reason = reason.PadRight(reasonW);
                    return $"{name}  {ver}  {ts}  {reason}";
                }

                var menu = new InteractiveMenu<BackupInfo>(
                    "Выберите бэкап для восстановления:",
                    backups,
                    RenderBackup);

                try {
                    selected = menu.Show();
                }
                catch (OperationCanceledException) { 
                    ConsoleUi.Dim("Операция отменена.");
                    return 0;
                }
            }

            Console.WriteLine();
            ConsoleUi.WarnMsg($"Текущий NGINX будет ЗАМЕНЁН содержимым бэкапа {selected.Name}.");
            ConsoleUi.WriteLine($"Версия в бэкапе:  {selected.Version}", ConsoleUi.Muted);
            ConsoleUi.WriteLine($"Создан:           {selected.Timestamp:yyyy-MM-dd HH:mm:ss}", ConsoleUi.Muted);
            if (!string.IsNullOrWhiteSpace(selected.Reason))
                ConsoleUi.WriteLine($"Причина создания: {selected.Reason}", ConsoleUi.Muted);
            Console.WriteLine();

            if (!HasFlag(args, "--yes"))
            {
                if (!ConsolePrompt.Confirm("Продолжить восстановление?", defaultValue: false))
                {
                    ConsoleUi.Dim("Операция отменена.");
                    return 0;
                }
            }

            Console.WriteLine();
            try
            {
                await mgr.RestoreAsync(selected);
            }
            catch (Exception ex)
            {
                ConsoleUi.Fail($"Ошибка восстановления: {ex.Message}");
                ConsoleUi.Dim("Текущее состояние NGINX сохранено (откат восстановления не потребовался).");
                return 1;
            }

            ConsoleUi.Ok($"NGINX восстановлен из бэкапа {selected.Name}.");
            Console.WriteLine();

            var info = new[]
            {
                new[] { "Имя",     selected.Name },
                new[] { "Версия",  string.IsNullOrWhiteSpace(selected.Version) ? "—" : "v" + selected.Version },
                new[] { "Создан",  selected.Timestamp.ToString("yyyy-MM-dd HH:mm:ss") },
                new[] { "Причина", string.IsNullOrWhiteSpace(selected.Reason) ? "—" : selected.Reason },
            };

            foreach (var row in info)
            {
                ConsoleUi.Write($"  {row[0],-8} ", ConsoleUi.Muted);
                Console.WriteLine(row[1]);
            }
            ConsoleUi.WriteLine($"Версия: {selected.Version}", ConsoleUi.Success);

            if (!HasFlag(args, "--keep"))
            {
                mgr.Delete(selected);
                ConsoleUi.WriteLine($"Использованный бэкап удалён: {selected.Name}", ConsoleUi.Muted);
            }
            else
            {
                ConsoleUi.WriteLine($"Бэкап сохранён по флагу --keep: {selected.Name}", ConsoleUi.Muted);
            }

            return 0;
        }

        private static string? GetArg(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            return null;
        }

        private static bool HasFlag(string[] args, string name) =>
            args.Any(a => a.Equals(name, StringComparison.OrdinalIgnoreCase));
    }
}
