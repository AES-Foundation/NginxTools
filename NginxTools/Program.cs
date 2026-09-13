using NginxTools.Commands;
using NginxTools.Services;
using System.Text;

// Правильная кодировка в консоли Windows (аналог "chcp 65001")
Console.OutputEncoding = Encoding.UTF8;

// Каталог NGINX берётся из: аргумента --nginx-dir, переменной среды NGINX_DIR
// или значения по умолчанию (ваш текущий путь)
var nginxDir =
    GetArg(args, "--nginx-dir")
    ?? Environment.GetEnvironmentVariable("NGINX_DIR")
    ?? @"F:\Servers\nginx";

if (args.Length == 0)
{
    PrintHelp();
    return 1;
}

switch (args[0].ToLowerInvariant())
{
    case "startup": return await StartupCommand.RunAsync(nginxDir);
    case "restart": return await RestartCommand.RunAsync(nginxDir);
    case "shutdown": return await ShutdownCommand.RunAsync(nginxDir);
    case "status": return PrintStatus(nginxDir);
    case "help" or "--help" or "-h": PrintHelp(); return 0;
    default:
        Console.Error.WriteLine($"Неизвестная команда: {args[0]}\n");
        PrintHelp();
        return 1;
}

static string? GetArg(string[] args, string name)
{
    for (int i = 0; i < args.Length - 1; i++)
        if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
            return args[i + 1];
    return null;
}

static int PrintStatus(string nginxDir)
{
    var nginx = new NginxController(nginxDir);
    Console.WriteLine($"Каталог:   {nginx.NginxDir}");
    Console.WriteLine($"Бинарник:  {nginx.NginxBinary}");
    Console.WriteLine($"Конфиг:    {nginx.ConfigPath}");
    Console.WriteLine($"Статус:    {(nginx.IsRunning() ? "запущен" : "остановлен")}");
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
          status      Показать текущий статус
          help        Показать эту справку

        Каталог NGINX определяется в таком порядке:
          1. Аргумент --nginx-dir
          2. Переменная среды NGINX_DIR
          3. Значение по умолчанию: F:\Servers\nginx
        """);
}
