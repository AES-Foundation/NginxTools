using NginxTools.Models;
using NginxTools.UI;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace NginxTools.Services
{
    public sealed class BackupManager
    {
        private static readonly Regex NamePattern = new(@"^\d{8}-\d{6}$", RegexOptions.Compiled);

        private readonly string _nginxDir;

        public string BackupsDir { get; }

        public BackupManager(string nginxDir, Settings settings)
        {
            _nginxDir = Path.GetFullPath(nginxDir);

            BackupsDir = settings.BackupsDir is not null
                ? Path.GetFullPath(settings.BackupsDir)
                : _nginxDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + "-backups";
        }

        /// <summary>
        /// Создаёт список бекапов.
        /// </summary>
        /// <returns>Возвращает список доступных бекапов.</returns>
        public IReadOnlyList<BackupInfo> List()
        {
            if (!Directory.Exists(BackupsDir))
                return Array.Empty<BackupInfo>();

            var result = new List<BackupInfo>();
            foreach (var dir in Directory.GetDirectories(BackupsDir))
            {
                var name = Path.GetFileName(dir);
                if (!NamePattern.IsMatch(name)) continue;

                var meta = ReadMetadata(dir);
                result.Add(new BackupInfo(
                    name,
                    dir,
                    meta?.Created ?? Directory.GetCreationTime(dir),
                    meta?.Version ?? "unknown",
                    meta?.Reason ?? ""));
            }

            return result.OrderByDescending(b => b.Timestamp).ToList();
        }

        /// <summary>
        /// Создаёт полный бэкап каталога NGINX.
        /// </summary>
        /// <param name="reason">Причина создания бекапа.</param>
        /// <param name="ct">Токен отмены.</param>
        /// <returns>Возвращает информацию о бекапе.</returns>
        public async Task<BackupInfo> CreateAsync(string reason, CancellationToken ct = default)
        {
            Directory.CreateDirectory(BackupsDir);
            
            DateTime now = DateTime.Now;
            var ts = now.ToString("yyyyMMdd-HHmmss");
            var target = Path.Combine(BackupsDir, ts);
            int suffix = 1;
            while (Directory.Exists(target))
                target = Path.Combine(BackupsDir, $"{ts}-{suffix++}");

            var nginx = new NginxController(_nginxDir);
            var version = await nginx.GetVersion(ct) ?? "unknown";

            await Task.Run(() => CopyDirectory(_nginxDir, target), ct);

            var info = new BackupInfo(
                Path.GetFileName(target),
                target,
                now,
                version,
                reason);

            WriteMetadata(info);
            return info;
        }

        /// <summary>
        /// Удаляет все сохранённые бекапы.
        /// </summary>
        public void RemoveAll()
        {
            if (!Directory.Exists(BackupsDir)) return;
            foreach (var dir in Directory.GetDirectories(BackupsDir))
            {
                try {
                    Directory.Delete(dir, recursive: true);
                }
                catch (Exception ex)
                {
                    ConsoleUi.WarnMsg($"Не удалось удалить бекап. [RemoveAll]: {ex.Message}");
                }

                try
                {
                    var meta = dir + ".json";
                    if (File.Exists(meta))
                        File.Delete(meta);
                }
                catch (Exception ex)
                {
                    ConsoleUi.WarnMsg($"Не удалось удалить ифнормацию бекапа: [RemoveAll]: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Удаляет конкретный бекап.
        /// </summary>
        /// <param name="info">Информация о бекапе.</param>
        public void Delete(BackupInfo info)
        {
            try { 
                if (Directory.Exists(info.Path)) 
                    Directory.Delete(info.Path, recursive: true); 
            } 
            catch (Exception ex) {
                ConsoleUi.WarnMsg($"Не удалось удалить бекап: [Delete]: {ex.Message}");
            }

            try
            {
                var meta = info.Path + ".json";
                if (File.Exists(meta))
                    File.Delete(meta);
            }
            catch (Exception ex) {
                ConsoleUi.WarnMsg($"Не удалось удалить ифнормацию бекапа: [Delete]: {ex.Message}");
            }
        }

        /// <summary>
        /// Восстанавливает NGINX из бэкапа. Атомарно: сначала двигает текущий
        /// каталог в .trash, потом копирует бэкап на место, при ошибке возвращает.
        /// </summary>
        /// <param name="backup">Информация о бекапе.</param>
        /// <param name="ct">Токен отмены.</param>
        /// <returns>Возвращает выполнение задачи.</returns>
        /// <exception cref="InvalidOperationException">Вызывает исключение, когда выполнение операции невозможно.</exception>
        public async Task RestoreAsync(BackupInfo backup, CancellationToken ct = default)
        {
            if (!Directory.Exists(backup.Path))
                throw new InvalidOperationException($"Каталог бэкапа не найден: {backup.Path}");

            var binName = OperatingSystem.IsWindows() ? "nginx.exe" : "nginx";
            if (!File.Exists(Path.Combine(backup.Path, binName)))
                throw new InvalidOperationException(
                    $"Бэкап {backup.Name} повреждён: не найден {binName}.");

            var nginx = new NginxController(_nginxDir);
            bool wasRunning = nginx.IsRunning();

            if (wasRunning)
            {
                await nginx.QuitAsync(ct);
                if (!await nginx.WaitForStopAsync(TimeSpan.FromSeconds(30), ct))
                {
                    nginx.KillAll();
                    await nginx.WaitForStopAsync(TimeSpan.FromSeconds(5), ct);
                }
            }

            string? trash = null;
            bool liveNowFromBackup = false;

            try
            {
                if (Directory.Exists(_nginxDir))
                {
                    trash = _nginxDir + ".trash-" + Guid.NewGuid().ToString("N");
                    Directory.Move(_nginxDir, trash);
                }

                await Task.Run(() => CopyDirectory(backup.Path, _nginxDir), ct);
                liveNowFromBackup = true;

                if (await nginx.TestConfigAsync(ct) != 0)
                    throw new InvalidOperationException(
                        "Конфигурация NGINX не прошла проверку после отката.");

                if (trash is not null)
                {
                    try
                    {
                        Directory.Delete(trash, recursive: true);
                    }
                    catch (Exception ex)
                    {
                        ConsoleUi.WarnMsg($"Не удалось удалить мусор. {ex.Message}");
                    }

                    trash = null;
                }

                if (wasRunning) nginx.Start();
            }
            catch
            {
                if (liveNowFromBackup && trash is not null)
                {
                    try
                    {
                        if (Directory.Exists(_nginxDir))
                            Directory.Delete(_nginxDir, recursive: true);
                        Directory.Move(trash, _nginxDir);
                        trash = null;
                    }
                    catch (Exception ex)
                    {
                        ConsoleUi.WarnMsg($"Не удалось удалить мусор {ex.Message}");
                    }
                }

                if (wasRunning)
                {
                    try {
                        nginx.Start();
                    }
                    catch (Exception ex)
                    {
                        ConsoleUi.WarnMsg($"Не удалось запустить NGINX. {ex.Message}");
                    }
                }
                throw;
            }
        }

        /// <summary>
        /// Копирует директорию.
        /// </summary>
        /// <param name="src">Копируемая директория.</param>
        /// <param name="dst">Место назначения для копируемоей директории.</param>
        private static void CopyDirectory(string src, string dst)
        {
            Directory.CreateDirectory(dst);

            foreach (var file in Directory.GetFiles(src))
            {
                var name = Path.GetFileName(file);
                if (name.StartsWith("_backup") || name.EndsWith(".trash"))
                    continue;
                File.Copy(file, Path.Combine(dst, name), overwrite: true);
            }

            foreach (var dir in Directory.GetDirectories(src))
            {
                var name = Path.GetFileName(dir);
                if (name.StartsWith(".trash-"))
                    continue;
                CopyDirectory(dir, Path.Combine(dst, name));
            }
        }

        /// <summary>
        /// Запись мета-данных для файла бекапа.
        /// </summary>
        /// <param name="info">Информация о бекапе.</param>
        private void WriteMetadata(BackupInfo info)
        {
            try
            {
                var meta = new BackupMeta
                {
                    Name = info.Name,
                    Created = info.Timestamp,
                    Version = info.Version,
                    Reason = info.Reason,
                };
                var json = JsonSerializer.Serialize(meta, MetadataJsonOptions);
                File.WriteAllText(info.Path + ".json", json);
            }
            catch (Exception ex) {
                ConsoleUi.WarnMsg($"Не удалось записать мета-данные: {ex.Message}");
            }
        }

        /// <summary>
        /// Чтение мета-данных бекапа.
        /// </summary>
        /// <param name="backupDir">Путь до бекапа.</param>
        /// <returns>Возвращает данные о бекапе.</returns>
        private BackupMeta? ReadMetadata(string backupDir)
        {
            try
            {
                var path = backupDir + ".json";
                if (!File.Exists(path))
                    return null;

                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<BackupMeta>(json, MetadataJsonOptions);
            }
            catch {
                return null;
            }
        }

        /// <summary>
        /// Обновляет только поле reason в метаданных существующего бэкапа.
        /// Сам каталог бэкапа не трогается.
        /// </summary>
        /// <param name="backup">Информация о бекапе.</param>
        /// <param name="newReason">Новая причина.</param>
        /// <returns>Возвращает <see langword="true"/>, если причина изменена, иначе <see langword="false"/>.</returns>
        public bool UpdateReason(BackupInfo backup, string newReason)
        {
            try
            {
                var meta = new BackupMeta
                {
                    Name = backup.Name,
                    Created = backup.Timestamp,
                    Version = backup.Version,
                    Reason = newReason,
                };
                var json = JsonSerializer.Serialize(meta, MetadataJsonOptions);
                File.WriteAllText(backup.Path + ".json", json);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Параметры сериализации мета-данных.
        /// </summary>
        private static readonly JsonSerializerOptions MetadataJsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        private sealed class BackupMeta
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = "";

            [JsonPropertyName("created")]
            public DateTime Created { get; set; }

            [JsonPropertyName("version")]
            public string Version { get; set; } = "unknown";

            [JsonPropertyName("reason")]
            public string Reason { get; set; } = "";
        }
    }
}
