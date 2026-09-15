using NginxTools.Models;
using NginxTools.UI;
using System.IO.Compression;

namespace NginxTools.Services
{
    public sealed class NginxInstaller
    {
        private static readonly HttpClient Http = new()
        {
            Timeout = TimeSpan.FromMinutes(5),
            DefaultRequestHeaders = { { "User-Agent", "nginxtools/2.0" } },
        };

        private readonly string _targetDir;

        public NginxInstaller(string targetDir) => _targetDir = targetDir;

        /// <summary>
        /// Скачивает и устанавливает NGINX выбранной версии.
        /// </summary>
        /// <param name="release">Выпущенная версия NGINX.</param>
        /// <param name="progress">Шкала прогресса установки.</param>
        /// <param name="ct">Токен отмены.</param>
        /// <returns>Возвращает выполнение задачи.</returns>
        /// <exception cref="InvalidOperationException">Вызывает исключение, ошибки во время выполнения операции.</exception>
        /// <exception cref="NotSupportedException">Вызывает исключение, в случае если система не поддерживается.</exception>
        public async Task InstallAsync(
            NginxRelease release,
            IProgress<(double p, long downloaded, long total)>? progress = null,
            CancellationToken ct = default)
        {
            var archivePath = Path.Combine(Path.GetTempPath(), release.FileName);

            await DownloadAsync(release.DownloadUrl, archivePath, progress, ct);

            if (Directory.Exists(_targetDir))
                throw new InvalidOperationException(
                    $"Каталог уже существует: {_targetDir}. Удалите его или используйте другое имя.");

            var staging = _targetDir + ".staging";
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
            Directory.CreateDirectory(staging);

            if (release.Platform == "win")
                ZipFile.ExtractToDirectory(archivePath, staging, overwriteFiles: true);
            else
                throw new NotSupportedException(
                    "Для Linux на этом этапе требуется сборка из исходников. См. документацию.");

            var entries = Directory.GetFileSystemEntries(staging);
            if (entries.Length == 1 && Directory.Exists(entries[0]))
            {
                var inner = entries[0];
                foreach (var entry in Directory.GetFileSystemEntries(inner))
                {
                    var dest = Path.Combine(staging, Path.GetFileName(entry));
                    if (Directory.Exists(entry))
                        Directory.Move(entry, dest);
                    else
                        File.Move(entry, dest);
                }
                Directory.Delete(inner);
            }

            Directory.Move(staging, _targetDir);

            try {
                File.Delete(archivePath);
            }
            catch (Exception ex)
            {
                ConsoleUi.WarnMsg($"Не удалось удалить установочный архив NGINX. {ex.Message}");
            }
        }

        /// <summary>
        /// Устанавливается NGINX.
        /// </summary>
        /// <param name="url">Ссылка на источник установки.</param>
        /// <param name="dest">Путь, куда устанавливается NGINX.</param>
        /// <param name="progress">Шкала прогресса установки.</param>
        /// <param name="ct">Токен отмены.</param>
        /// <returns>Возвращает выполнение задачи.</returns>
        private static async Task DownloadAsync(string url, string dest, IProgress<(double, long, long)>? progress, CancellationToken ct)
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
