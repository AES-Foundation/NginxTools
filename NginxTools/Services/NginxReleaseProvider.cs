using NginxTools.Models;
using System.Text.RegularExpressions;

namespace NginxTools.Services
{
    public sealed class NginxReleaseProvider
    {
        private static readonly HttpClient Http = new()
        {
            Timeout = TimeSpan.FromSeconds(30),
            DefaultRequestHeaders = { { "User-Agent", "nginxtools/2.0" } },
        };

        private const string DownloadPage = "https://nginx.org/en/download.html";

        /// <summary>
        /// Возвращает список доступных версий NGINX для текущей платформы.
        /// </summary>
        /// <param name="ct">Токен отмены.</param>
        /// <returns>Возвращает список выпущенных версий NGINX.</returns>
        public async Task<IReadOnlyList<NginxRelease>> GetAvailableAsync(CancellationToken ct = default)
        {
            var html = await Http.GetStringAsync(DownloadPage, ct);
            var releases = new List<NginxRelease>();

            if (OperatingSystem.IsWindows())
                releases.AddRange(ParseWindowsReleases(html));
            else
                releases.AddRange(ParseLinuxReleases(html));

            return releases
                .OrderByDescending(r => r.Channel == "mainline")
                .ThenByDescending(r => r.Version, new VersionComparer())
                .ToList();
        }

        /// <summary>
        /// Парсинг выпусков для Windows.
        /// </summary>
        /// Windows: ссылки вида https://nginx.org/download/nginx-1.27.0.zip
        /// <param name="html">HTML страница, с выпусками.</param>
        /// <returns>Возвращает список выпусков версий NGINX.</returns>
        private static IEnumerable<NginxRelease> ParseWindowsReleases(string html)
        {
            var pattern = new Regex(
                @"nginx-(?<ver>\d+\.\d+\.\d+)\.zip",
                RegexOptions.IgnoreCase);

            var seen = new HashSet<string>();
            foreach (Match m in pattern.Matches(html))
            {
                var ver = m.Groups["ver"].Value;
                if (!seen.Add(ver)) continue;

                var channel = IsMainline(ver) ? "mainline" : "stable";
                yield return new NginxRelease
                {
                    Version = ver,
                    Channel = channel,
                    DownloadUrl = $"https://nginx.org/download/nginx-{ver}.zip",
                    FileName = $"nginx-{ver}.zip",
                    Platform = "win",
                };
            }
        }

        /// <summary>
        /// Парсинг выпусков для Linux.
        /// </summary>
        /// Linux: ссылки вида nginx-1.27.0.tar.gz (сборка из исходников)
        /// <param name="html">HTML страница, с выпусками.</param>
        /// <returns>Возвращает список выпусков версий NGINX.</returns>
        private static IEnumerable<NginxRelease> ParseLinuxReleases(string html)
        {
            var pattern = new Regex(
                @"nginx-(?<ver>\d+\.\d+\.\d+)\.tar\.gz",
                RegexOptions.IgnoreCase);

            var seen = new HashSet<string>();
            foreach (Match m in pattern.Matches(html))
            {
                var ver = m.Groups["ver"].Value;
                if (!seen.Add(ver)) continue;

                var channel = IsMainline(ver) ? "mainline" : "stable";
                yield return new NginxRelease
                {
                    Version = ver,
                    Channel = channel,
                    DownloadUrl = $"https://nginx.org/download/nginx-{ver}.tar.gz",
                    FileName = $"nginx-{ver}.tar.gz",
                    Platform = "linux",
                };
            }
        }

        /// <summary>
        /// Mainline — нечётное второе число (1.27.x), Stable — чётное (1.28.x).
        /// </summary>
        /// <param name="version">Проверка версий на тех, что в разработке.</param>
        /// <returns>Возвращает <see langword="true"/>, если версия является в разработке; <see langword="false"/>, если версия не является в разработке.</returns>
        private static bool IsMainline(string version)
        {
            var parts = version.Split('.');
            if (parts.Length < 2) return false;
            return int.TryParse(parts[1], out var minor) && minor % 2 == 1;
        }

        /// <summary>
        /// Класс-компоратор (сравнитель), для правильной сортировки строк с версиями.
        /// </summary>
        private sealed class VersionComparer : IComparer<string>
        {
            public int Compare(string? a, string? b)
            {
                if (a is null || b is null) return 0;
                var va = Version.Parse(a);
                var vb = Version.Parse(b);
                return va.CompareTo(vb);
            }
        }
    }
}
