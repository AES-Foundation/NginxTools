using NginxTools.UI;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace NginxTools.Services
{
    public sealed class NginxController
    {
        public string NginxDir { get; }
        public string NginxBinary { get; }
        public string ConfigPath { get; }
        public string ConfigDir { get; }

        public NginxController(string nginxDir)
        {
            NginxDir = Path.GetFullPath(nginxDir);
            NginxBinary = Path.Combine(NginxDir,
                OperatingSystem.IsWindows() ? "nginx.exe" : "nginx");
            ConfigPath = Path.Combine(NginxDir, "conf", "nginx.conf");
            ConfigDir = Path.Combine(NginxDir, "conf");
        }

        /// <summary>
        /// Проверяет, запущен ли сейчас NGINX.
        /// </summary>
        /// <returns>Возвращает <see langword="true"/>, если NGINX запущен; <see langword="false"/>, если NGINX выключен.</returns>
        public bool IsRunning()
        {
            var name = Path.GetFileNameWithoutExtension(NginxBinary);
            return Process.GetProcessesByName(name).Length > 0;
        }

        /// <summary>
        /// Проверка конфигурации: nginx -t.
        /// </summary>
        /// <param name="ct">Токен отмены.</param>
        /// <returns>Возвращает код <see langword="int"/> = 0 (ОК).</returns>
        public Task<int> TestConfigAsync(CancellationToken ct = default) =>
            RunAsync($"-t -c \"{ConfigPath}\"", ct);

        /// <summary>
        /// Плавная перезагрузка: nginx -s reload.
        /// </summary>
        /// <param name="ct">Токен отмены.</param>
        /// <returns>Возвращает код <see langword="int"/> = 0 (ОК).</returns>
        public Task<int> ReloadAsync(CancellationToken ct = default) =>
            RunAsync($"-s reload -c \"{ConfigPath}\"", ct);

        /// <summary>
        /// Плавное завершение: nginx -s quit.
        /// </summary>
        /// <param name="ct">Токен отмены.</param>
        /// <returns>Возвращает код <see langword="int"/> = 0 (ОК).</returns>
        public Task<int> QuitAsync(CancellationToken ct = default) =>
            RunAsync($"-s quit -c \"{ConfigPath}\"", ct);

        /// <summary>
        /// Запуск NGINX как отдельного процесса (detached).
        /// </summary>
        /// <returns>Возвращает ID процесса NGINX.</returns>
        /// <exception cref="InvalidOperationException">Вызывает исключение, если операция запуска не удалась.</exception>
        public int Start()
        {
            var psi = new ProcessStartInfo
            {
                FileName = NginxBinary,
                Arguments = $"-c \"{ConfigPath}\"",
                WorkingDirectory = NginxDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
            };

            var p = Process.Start(psi)
                ?? throw new InvalidOperationException("Не удалось запустить NGINX.");
            return p.Id;
        }

        /// <summary>
        /// Ждёт, пока NGINX остановится.
        /// </summary>
        /// <param name="timeout">Время ожидания, перед принудительным закрытием.</param>
        /// <param name="ct">Токен отмены.</param>
        /// <returns>Возвращает <see langword="true"/>, если дождались, иначе <see langword="false"/>.</returns>
        public async Task<bool> WaitForStopAsync(TimeSpan timeout, CancellationToken ct = default)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (!IsRunning()) return true;
                await Task.Delay(200, ct);
            }
            return !IsRunning();
        }

        /// <summary>
        /// Аварийное завершение всех процессов nginx (fallback).
        /// </summary>
        public void KillAll()
        {
            var name = Path.GetFileNameWithoutExtension(NginxBinary);
            foreach (var p in Process.GetProcessesByName(name))
            {
                try {
                    p.Kill(entireProcessTree: true);
                }
                catch (Exception ex)
                {
                    ConsoleUi.WarnMsg($"Не удалось завершить все процессы. {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Получение версии NGINX.
        /// </summary>
        /// <param name="ct">Токен отмены.</param>
        /// <returns>Возвращает строку <see langword="string"/>, если версия получена, иначе <see langword="null"/>.</returns>
        public async Task<string?> GetVersion(CancellationToken ct = default)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = NginxBinary,
                    Arguments = "-v",
                    WorkingDirectory = NginxDir,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                };
                using var p = Process.Start(psi)!;
                var err = await p.StandardError.ReadToEndAsync(ct);
                var output = await p.StandardOutput.ReadToEndAsync(ct);
                await p.WaitForExitAsync(ct);

                var text = string.IsNullOrWhiteSpace(err) ? output : err;
                var m = Regex.Match(text, @"nginx/(\d+\.\d+\.\d+)");
                return m.Success ? m.Groups[1].Value : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Выполняет команду контроллера NGINX.
        /// </summary>
        /// <param name="args">Аргументы запуска команды.</param>
        /// <param name="ct">Токен отмены.</param>
        /// <returns>Возвращает код выполнения в <see langword="int"/> формате.</returns>
        /// <exception cref="InvalidOperationException">Вызывает исключение, при возникновении ошибки во время выполнения операции.</exception>
        private async Task<int> RunAsync(string args, CancellationToken ct)
        {
            var psi = new ProcessStartInfo
            {
                FileName = NginxBinary,
                Arguments = args,
                WorkingDirectory = NginxDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            using var p = Process.Start(psi)
                ?? throw new InvalidOperationException("Не удалось запустить NGINX.");

            var stdoutTask = p.StandardOutput.ReadToEndAsync(ct);
            var stderrTask = p.StandardError.ReadToEndAsync(ct);

            await p.WaitForExitAsync(ct);

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (!string.IsNullOrWhiteSpace(stdout))
                ConsoleUi.Dim(stdout.TrimEnd());
            if (!string.IsNullOrWhiteSpace(stderr))
                ConsoleUi.Dim(stderr.TrimEnd());

            return p.ExitCode;
        }
    }
}
