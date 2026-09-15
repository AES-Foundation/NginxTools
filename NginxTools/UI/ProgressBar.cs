namespace NginxTools.UI
{
    public sealed class ProgressBar : IDisposable
    {
        private readonly string _label;
        private readonly int _barWidth;
        private readonly int _top;
        private readonly bool _prevCursor;

        public ProgressBar(string label, int barWidth = 30)
        {
            _label = label;
            _barWidth = Math.Clamp(barWidth, 10, Math.Max(10, ConsoleUi.BufferWidth - 40));

            Console.WriteLine();
            _top = Math.Min(Console.CursorTop, ConsoleUi.BufferHeight - 1);
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    _prevCursor = Console.CursorVisible;
                }
                Console.CursorVisible = false;
            }
            catch (PlatformNotSupportedException)
            {
                ConsoleUi.WarnMsg("Операционная система не поддерживает видимость курсора.");
            }
        }

        /// <summary>
        /// Обновляет шкалу прогрессии.
        /// </summary>
        /// <param name="progress01">Прогросс.</param>
        /// <param name="downloaded">Скачано.</param>
        /// <param name="total">Всего.</param>
        public void Report(double progress01, long downloaded = 0, long total = 0)
        {
            double p = Math.Clamp(progress01, 0, 1);
            int filled = (int)Math.Round(p * _barWidth);
            int empty = _barWidth - filled;

            string bar = new string('█', filled) + new string('░', empty);
            string percent = $"{(int)(p * 100),3}%";
            string size = total > 0
                ? $"  {FormatSize(downloaded)} / {FormatSize(total)}"
                : downloaded > 0 ? $"  {FormatSize(downloaded)}" : "";

            ConsoleUi.SafeSetCursor(0, _top);

            int width = Math.Max(40, ConsoleUi.BufferWidth - 1);
            var line = $"{_label} {bar} {percent}{size}";
            if (line.Length >= width) line = line.Substring(0, width - 1);
            else line = line.PadRight(width - 1);

            var prevFg = Console.ForegroundColor;
            Console.Write(line);
            Console.ForegroundColor = prevFg;
        }

        /// <summary>
        /// Устанавливает и завершает работу шкалы прогресса.
        /// </summary>
        public void Complete()
        {
            Report(1.0);
            Console.WriteLine();
            Console.CursorVisible = _prevCursor;
        }

        /// <summary>
        /// Прекращает скрытие курсора.
        /// </summary>
        public void Dispose()
        {
            Console.CursorVisible = _prevCursor;
        }

        /// <summary>
        /// Форматирование размеров файлов.
        /// </summary>
        /// <param name="bytes">Размер файла в байтах.</param>
        /// <returns>Возвращает отформатированный <see langword="string"/> с размером файла.</returns>
        private static string FormatSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024L * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024L * 1024 * 1024) return $"{bytes / 1024.0 / 1024.0:F1} MB";
            return $"{bytes / 1024.0 / 1024.0 / 1024.0:F2} GB";
        }
    }
}
