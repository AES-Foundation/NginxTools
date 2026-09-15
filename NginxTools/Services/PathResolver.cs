using NginxTools.UI;

namespace NginxTools.Services
{
    public static class PathResolver
    {
        /// <summary>
        /// Определяет каталог NGINX по следующему приоритету:
        /// 1) аргумент CLI, 2) NGINX_DIR, 3) файл настроек, 4) автоопределение.
        /// Возвращает null, если найти не удалось.
        /// </summary>
        /// <param name="cliPath">Путь к исполняемому файлу.</param>
        /// <param name="envPath">Путь к окружению.</param>
        /// <param name="settings">Настройки, где путь из файла конфигурации.</param>
        /// <returns>Возвращает строку <see langword="string"/> с найденным путём; <see langword="null"/>, если ничего не нашлось.</returns>
        public static string? Resolve(string? cliPath, string? envPath, Settings settings)
        {
            if (!string.IsNullOrWhiteSpace(cliPath))
                return FromExplicit(cliPath, "аргумент --nginx-dir");

            if (!string.IsNullOrWhiteSpace(envPath))
                return FromExplicit(envPath, "переменная NGINX_DIR");

            if (!string.IsNullOrWhiteSpace(settings.NginxDir))
                return FromExplicit(settings.NginxDir, "файл настроек");

            if (settings.AutoDetectNginx)
            {
                var found = AutoDetect();
                if (found is not null)
                {
                    ConsoleUi.WriteLine($"Каталог NGINX определён: {found}", ConsoleUi.Muted);
                    return found;
                }
            }

            return null;
        }

        /// <summary>
        /// Поиск пути.
        /// </summary>
        /// <param name="path">Путь, который необходимо проверить.</param>
        /// <param name="source">Источник, передавший путь.</param>
        /// <returns>Возвращает строку <see langword="string"/> с полным путём; <see langword="null"/>, если ничего не нашлось.</returns>
        private static string? FromExplicit(string path, string source)
        {
            var full = Path.GetFullPath(path);
            if (!Directory.Exists(full))
            {
                ConsoleUi.Fail($"[{source}] Каталог не существует: {full}");
                return null;
            }
            return full;
        }

        /// <summary>
        /// Автоматическое нахождение пути.
        /// </summary>
        /// <returns>Возвращает строку <see langword="string"/> с полным найденным путём. <see langword="null"/>, если ничего не нашлось.</returns>
        private static string? AutoDetect()
        {
            var baseDir = AppContext.BaseDirectory
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            var binName = OperatingSystem.IsWindows() ? "nginx.exe" : "nginx";

            var candidates = new[]
            {
            Path.Combine(baseDir, "nginx"),
            baseDir,
            Path.Combine(baseDir, "..", "nginx"),
        };

            foreach (var candidate in candidates)
            {
                var full = Path.GetFullPath(candidate);
                if (File.Exists(Path.Combine(full, binName)))
                    return full;
            }
            return null;
        }
    }
}
