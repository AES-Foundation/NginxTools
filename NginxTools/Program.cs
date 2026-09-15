using NginxTools;
using NginxTools.Commands;
using NginxTools.Services;
using NginxTools.UI;
using System.Text;

Console.OutputEncoding = Encoding.UTF8;

var settings = Settings.Load();

var cliPath = GetArg(args, "--nginx-dir");
var envPath = Environment.GetEnvironmentVariable("NGINX_DIR");

var nginxDir = PathResolver.Resolve(cliPath, envPath, settings);
if (nginxDir is null)
{
    ConsoleUi.Fail("Не удалось определить каталог NGINX. См. README, раздел «Конфигурация».");
    return 1;
}

if (args.Length == 0) { PrintHelp(); return 1; }

try
{
    return args[0].ToLowerInvariant() switch
    {
        "init" or "--init" or "-i" => await InitCommand.RunAsync(cliPath is not null ? Path.GetFullPath(cliPath) : Path.Combine(AppContext.BaseDirectory, "nginx")),
        "startup" or "--startup" or "start" or "--start" => await StartupCommand.RunAsync(nginxDir, settings),
        "restart" or "--restart" or "reload" or "--reload" => await RestartCommand.RunAsync(nginxDir, settings),
        "shutdown" or "--shutdown" or "stop" or "--stop" => await ShutdownCommand.RunAsync(nginxDir, TimeSpan.FromSeconds(settings.ShutdownGraceTimeoutSeconds)),
        "update-ips" or "--update-ips" => await UpdateIpsCommand.RunAsync( nginxDir, settings, reload: !HasFlag(args, "--no-reload")),
        "status" or "--status" or "-s" => PrintStatus(nginxDir),
        "version" or "--version" or "-v" => await VersionCommand.RunAsync(nginxDir),
        "upgrade" or "--upgrade" => await UpgradeCommand.RunAsync(nginxDir, settings, args),
        "backup" or "--backup" => await BackupCommand.RunAsync(nginxDir, settings, args),
        "help" or "--help" or "-h" => Help(),
        _ => Unknown(args[0]),
    };
}
catch (Exception ex)
{
    ConsoleUi.Fail($"Критическая ошибка: {ex.Message}");
    return 2;
}

static string? GetArg(string[] args, string name)
{
    for (int i = 0; i < args.Length - 1; i++)
        if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
            return args[i + 1];
    return null;
}

static bool HasFlag(string[] args, string name) =>
    args.Any(a => a.Equals(name, StringComparison.OrdinalIgnoreCase));

static int PrintStatus(string nginxDir)
{
    var nginx = new NginxController(nginxDir);
    ConsoleUi.Dim($"Каталог:   {nginx.NginxDir}");
    ConsoleUi.Dim($"Бинарник:  {nginx.NginxBinary}");
    ConsoleUi.Dim($"Конфиг:    {nginx.ConfigPath}");

    var headers = new[] { "Процесс", "Статус" };
    var rows = new List<string[]>
    {
        new[] { "NGINX", nginx.IsRunning() ? "Запущен" : "Остановлен" },
    };

    return 0;
}

static int Unknown(string cmd)
{
    ConsoleUi.Fail($"Неизвестная команда: {cmd}\n");
    ConsoleUi.Dim("Воспольуйтесь командой help, чтобы посмотреть команды");
    return 1;
}

static int Help()
{
    PrintHelp();
    return 0;
}

static void PrintHelp()
{
    var headers = new[] { "Команда", "Описание" };
    var rows = new List<string[]>
    {
        new[] { "init", "Установить базовый NGINX" },
        new[] { "upgrade", "Обновить NGINX до выбранной версии" },
        new[] { "backup", "Восстановить NGINX из резервной копии" },
        new[] { "backup --list", "показать список бэкапов" },
        new[] { "backup --time <YYYYMMDD-HHMMSS>", "выбрать конкретный бэкап" },
        new[] { "backup --keep", "не удалять бэкап после восстановления" },
        new[] { "backup --yes", "не спрашивать подтверждение" },
        new[] { "startup", "Запустить NGINX (с проверкой конфигурации)" },
        new[] { "restart", "Плавный перезапуск (nginx -s reload)" },
        new[] { "shutdown", "Плавное завершение (nginx -s quit с fallback)" },
        new[] { "update-ips", "Обновление доверительных IP диапазовнов" },
        new[] { "status", "Показать текущий статус" },
        new[] { "version", "Показать версию установленного NGINX" },
        new[] { "help", "Показать эту справку"},
    };

    ConsoleUi.Banner();

    Console.WriteLine("""
        NGINXTOOLS - кроссплатформенное управление NGINX

        Использование:
          nginxtools <команда> [--nginx-dir <путь>]
        """);

    Console.WriteLine("Команды");
    Table.Render(headers, rows, maxWidths: new[] { 40, 75, });

    Console.WriteLine("""
        Приоритет определения каталога NGINX:
          1. Аргумент --nginx-dir
          2. Переменная среды NGINX_DIR
          3. Поле "nginxDir" в nginxtools.settings.json
          4. Автоопределение рядом с исполняемым файлом
        """);
}