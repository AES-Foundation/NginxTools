namespace NginxTools.Services
{
    public static class PathResolver
    {
        /// <summary>
        /// Определяет каталог NGINX по следующему приоритету:
        /// 1) аргумент CLI, 2) NGINX_DIR, 3) файл настроек, 4) автоопределение.
        /// Возвращает null, если найти не удалось.
        /// </summary>
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
                    Console.WriteLine($"[auto] Каталог NGINX определён: {found}");
                    return found;
                }
            }

            return null;
        }

        private static string? FromExplicit(string path, string source)
        {
            var full = Path.GetFullPath(path);
            if (!Directory.Exists(full))
            {
                Console.Error.WriteLine($"[{source}] Каталог не существует: {full}");
                return null;
            }
            return full;
        }

        private static string? AutoDetect()
        {
            var baseDir = AppContext.BaseDirectory
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            var binName = OperatingSystem.IsWindows() ? "nginx.exe" : "nginx";

            var candidates = new[]
            {
            Path.Combine(baseDir, "nginx"),        // <exe>/nginx/
            baseDir,                                // <exe>/  (nginx рядом с exe)
            Path.Combine(baseDir, "..", "nginx"),  // <exe>/../nginx/
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
