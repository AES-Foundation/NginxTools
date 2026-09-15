using NginxTools;
using NginxTools.Commands;
using NginxTools.Services;
using System.Text;

Console.OutputEncoding = Encoding.UTF8;

var settings = Settings.Load();

var cliPath = GetArg(args, "--nginx-dir");
var envPath = Environment.GetEnvironmentVariable("NGINX_DIR");

var nginxDir = PathResolver.Resolve(cliPath, envPath, settings);
if (nginxDir is null)
{
    Console.Error.WriteLine("Не удалось определить каталог NGINX. См. README, раздел «Конфигурация».");
    return 1;
}

if (args.Length == 0) { PrintHelp(); return 1; }

try
{
    return args[0].ToLowerInvariant() switch
    {
        "startup" => await StartupCommand.RunAsync(nginxDir, settings),
        "restart" => await RestartCommand.RunAsync(nginxDir, settings),
        "shutdown" => await ShutdownCommand.RunAsync(
                             nginxDir,
                             TimeSpan.FromSeconds(settings.ShutdownGraceTimeoutSeconds)),
        "update-ips" => await UpdateIpsCommand.RunAsync(
                             nginxDir, settings,
                             reload: !HasFlag(args, "--no-reload")),
        "status" => PrintStatus(nginxDir),
        "help" or "--help" or "-h" => Help(),
        _ => Unknown(args[0]),
    };
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Критическая ошибка: {ex.Message}");
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
    Console.WriteLine($"Каталог:   {nginx.NginxDir}");
    Console.WriteLine($"Бинарник:  {nginx.NginxBinary}");
    Console.WriteLine($"Конфиг:    {nginx.ConfigPath}");
    Console.WriteLine($"Статус:    {(nginx.IsRunning() ? "запущен" : "остановлен")}");
    return 0;
}

static int Unknown(string cmd)
{
    Console.Error.WriteLine($"Неизвестная команда: {cmd}\n");
    PrintHelp();
    return 1;
}

static int Help()
{
    PrintHelp();
    return 0;
}

static void PrintHelp()
{
    Console.WriteLine("""
        nginxtools — кроссплатформенное управление NGINX

        Использование:
          nginxtools <команда> [--nginx-dir <путь>]

        Команды:
          startup     Запустить NGINX (с проверкой конфигурации)
          restart     Плавный перезапуск (nginx -s reload)
          shutdown    Плавное завершение (nginx -s quit с fallback)
          update-ips  Обновление доверительных IP диапазовнов
          status      Показать текущий статус
          help        Показать эту справку

        Приоритет определения каталога NGINX:
          1. Аргумент --nginx-dir
          2. Переменная среды NGINX_DIR
          3. Поле "nginxDir" в nginxtools.settings.json
          4. Автоопределение рядом с исполняемым файлом
        """);
}