using NginxTools.Models;
using NginxTools.UI;
using System.IO.Compression;

namespace NginxTools.Services
{
    public sealed class NginxUpgrader
    {
        private static readonly HttpClient Http = new()
        {
            Timeout = TimeSpan.FromMinutes(5),
            DefaultRequestHeaders = { { "User-Agent", "nginxtools/2.0" } },
        };

        public sealed record UpgradeResult(
            string? PreviousVersion,
            string NewVersion,
            BackupInfo Backup,
            bool WasRunning);

        /// <summary>
        /// Обновление до другой версии.
        /// </summary>
        /// <param name="nginxDir">Путь до NGINX.</param>
        /// <param name="release">Выпущенная версия.</param>
        /// <param name="settings">Настройки утилиты.</param>
        /// <param name="progress">Прогресс установки.</param>
        /// <param name="ct">Токен отмены.</param>
        /// <returns>Возвращает результат обновления.</returns>
        /// <exception cref="InvalidOperationException">Вызывает исключение, в случае неудачной операции.</exception>
        public async Task<UpgradeResult> UpgradeAsync(
            string nginxDir,
            NginxRelease release,
            Settings settings,
            IProgress<(double p, long d, long t)>? progress,
            CancellationToken ct = default)
        {
            var controller = new NginxController(nginxDir);
            if (!File.Exists(controller.NginxBinary))
                throw new InvalidOperationException(
                    $"NGINX не найден: {controller.NginxBinary}. Используйте 'nginxtools init'.");

            var previousVersion = await controller.GetVersion(ct);
            var backups = new BackupManager(nginxDir, settings);

            var archivePath = Path.Combine(Path.GetTempPath(), release.FileName);
            await DownloadAsync(release.DownloadUrl, archivePath, progress, ct);

            if (settings.RemoveOldBackups)
            {
                ConsoleUi.Dim("Удаление старых бэкапов...");
                backups.RemoveAll();
            }

            ConsoleUi.Write("Создание бэкапа... ", ConsoleUi.Muted);
            var backup = await backups.CreateAsync(
                reason: $"before upgrade {previousVersion ?? "?"} --> {release.Version}",
                ct);
            ConsoleUi.WriteLine($"готово ({backup.Name}).", ConsoleUi.Success);

            var staging = Path.Combine(Path.GetTempPath(),
                "nginxtools-upgrade-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);

            try
            {
                ZipFile.ExtractToDirectory(archivePath, staging, overwriteFiles: true);
                var newRoot = FindNginxRoot(staging);

                var wasRunning = controller.IsRunning();
                if (wasRunning)
                {
                    await controller.QuitAsync(ct);
                    ConsoleUi.WarnMsg("Запущено отключение NGINX");
                    if (!await controller.WaitForStopAsync(TimeSpan.FromSeconds(30), ct))
                    {
                        controller.KillAll();
                        ConsoleUi.Fail("NGINX принудительно остановлен");
                        await controller.WaitForStopAsync(TimeSpan.FromSeconds(5), ct);
                    }
                }

                try
                {
                    ConsoleUi.WriteLine("Установка файлов обновления...", ConsoleUi.Muted);
                    CopyUpgradableFiles(newRoot, nginxDir, updateHtmlIndex: false);

                    if (await controller.TestConfigAsync(ct) != 0)
                        throw new InvalidOperationException(
                            "Конфигурация NGINX не прошла проверку после обновления.");
                }
                catch (Exception ex)
                {
                    var marker = ex switch
                    {
                        HttpRequestException => "(FAILED: network)",
                        InvalidOperationException => "(FAILED: config)",
                        UnauthorizedAccessException => "(FAILED: permissions)",
                        _ => "(FAILED)",
                    };

                    backups.UpdateReason(backup, backup.Reason + " " + marker);

                    ConsoleUi.WarnMsg("Обновление упало — восстанавливаем из бэкапа...");
                    try
                    {
                        await backups.RestoreAsync(backup, ct);
                    }
                    catch (Exception restoreEx)
                    {
                        ConsoleUi.Fail($"Не удалось восстановить из бэкапа: {restoreEx.Message}");
                        ConsoleUi.Dim("Состояние NGINX может быть неконсистентно. " +
                                      "Восстановите вручную: nginxtools backup --time " + backup.Name);
                    }

                    ConsoleUi.WarnMsg("Запуск NGINX...");
                    if (wasRunning) controller.Start();
                    throw;
                }

                if (wasRunning) controller.Start();

                return new UpgradeResult(
                    PreviousVersion: previousVersion,
                    NewVersion: release.Version,
                    Backup: backup,
                    WasRunning: wasRunning);
            }
            finally
            {
                TryDeleteDir(staging);
                try { 
                    File.Delete(archivePath);
                } catch (Exception ex)
                {
                    ConsoleUi.WarnMsg($"Не удалось удалить мусор. {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Находит корень NGINX внутри распакованного staging (обычно единственная папка).
        /// </summary>
        /// <param name="staging">Путь к папке.</param>
        /// <returns>Возвращает строку <see langword="string"/> итогового пути к NGINX.</returns>
        private static string FindNginxRoot(string staging)
        {
            var entries = Directory.GetDirectories(staging);
            if (entries.Length == 1)
            {
                var candidate = entries[0];
                var hasBin = File.Exists(Path.Combine(candidate, "nginx.exe")) ||
                             File.Exists(Path.Combine(candidate, "nginx"));
                if (hasBin) return candidate;
            }
            return staging;
        }

        /// <summary>
        /// Копирует из новой версии только бинарник, docs/ и contrib/.
        /// </summary>
        /// <param name="newRoot">Новая временная директория.</param>
        /// <param name="targetRoot">Рабочая директория.</param>
        /// <param name="updateHtmlIndex">Обновлять HTML документы?</param>
        private static void CopyUpgradableFiles(string newRoot, string targetRoot, bool updateHtmlIndex)
        {
            var binName = OperatingSystem.IsWindows() ? "nginx.exe" : "nginx";
            var newBin = Path.Combine(newRoot, binName);
            if (File.Exists(newBin))
                File.Copy(newBin, Path.Combine(targetRoot, binName), overwrite: true);

            foreach (var dir in new[] { "docs", "contrib" })
                CopyDirectory(Path.Combine(newRoot, dir), Path.Combine(targetRoot, dir));

            if (updateHtmlIndex)
            {
                var idx = Path.Combine(newRoot, "html", "index.html");
                if (File.Exists(idx))
                    File.Copy(idx, Path.Combine(targetRoot, "html", "index.html"), overwrite: true);
            }
        }

        /// <summary>
        /// Копирование директории.
        /// </summary>
        /// <param name="source">Источник директории.</param>
        /// <param name="dest">Целевая директория.</param>
        private static void CopyDirectory(string source, string dest)
        {
            if (!Directory.Exists(source)) return;
            Directory.CreateDirectory(dest);

            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(source, file);
                var target = Path.Combine(dest, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target, overwrite: true);
            }
        }

        /// <summary>
        /// Пытается удалить директорию.
        /// </summary>
        /// <param name="dir">Путь до удаляемой директории.</param>
        private static void TryDeleteDir(string dir)
        {
            try {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }
            catch (Exception ex)
            {
                ConsoleUi.WarnMsg($"Попытка удалить директорию не удалось. {ex.Message}");
            }
        }

        /// <summary>
        /// Асинхронное скачивание файлов.
        /// </summary>
        /// <param name="url">Ссылка на скачивание.</param>
        /// <param name="dest">Целевая директория для установки.</param>
        /// <param name="progress">Шкала прогресса установки.</param>
        /// <param name="ct">Токен отмены.</param>
        /// <returns>Возвращает выполнение задачи.</returns>
        private static async Task DownloadAsync(
            string url, string dest,
            IProgress<(double, long, long)>? progress, CancellationToken ct)
        {
            using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            long total = response.Content.Headers.ContentLength ?? -1L;
            await using var source = await response.Content.ReadAsStreamAsync(ct);
            await using var target = File.Create(dest);

            var buffer = new byte[81920];
            long read = 0;
            int n;
            while ((n = await source.ReadAsync(buffer, ct)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, n), ct);
                read += n;
                double p = total > 0 ? (double)read / total : 0;
                progress?.Report((p, read, total > 0 ? total : 0));
            }
        }
    }
}
