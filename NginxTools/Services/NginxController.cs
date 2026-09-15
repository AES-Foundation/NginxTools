using System.Diagnostics;

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

        /// <summary>Проверяет, запущен ли сейчас NGINX.</summary>
        public bool IsRunning()
        {
            var name = Path.GetFileNameWithoutExtension(NginxBinary);
            return Process.GetProcessesByName(name).Length > 0;
        }

        /// <summary>Проверка конфигурации: nginx -t. Код 0 = ОК.</summary>
        public Task<int> TestConfigAsync(CancellationToken ct = default) =>
            RunAsync($"-t -c \"{ConfigPath}\"", ct);

        /// <summary>Плавная перезагрузка: nginx -s reload.</summary>
        public Task<int> ReloadAsync(CancellationToken ct = default) =>
            RunAsync($"-s reload -c \"{ConfigPath}\"", ct);

        /// <summary>Плавное завершение: nginx -s quit.</summary>
        public Task<int> QuitAsync(CancellationToken ct = default) =>
            RunAsync($"-s quit -c \"{ConfigPath}\"", ct);

        /// <summary>Запуск NGINX как отдельного процесса (detached).</summary>
        public int Start()
        {
            var psi = new ProcessStartInfo
            {
                FileName = NginxBinary,
                Arguments = $"-c \"{ConfigPath}\"",
                WorkingDirectory = NginxDir, // критично: nginx создаёт temp/логи относительно CWD
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
            };

            var p = Process.Start(psi)
                ?? throw new InvalidOperationException("Не удалось запустить NGINX.");
            return p.Id;
        }

        /// <summary>Ждёт, пока NGINX остановится. Возвращает true, если дождались.</summary>
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

        /// <summary>Аварийное завершение всех процессов nginx (fallback).</summary>
        public void KillAll()
        {
            var name = Path.GetFileNameWithoutExtension(NginxBinary);
            foreach (var p in Process.GetProcessesByName(name))
            {
                try { p.Kill(entireProcessTree: true); }
                catch { /* игнорируем, процесс мог завершиться сам */ }
            }
        }

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
                Console.WriteLine(stdout.TrimEnd());
            if (!string.IsNullOrWhiteSpace(stderr))
                Console.Error.WriteLine(stderr.TrimEnd());

            return p.ExitCode;
        }
    }
}
