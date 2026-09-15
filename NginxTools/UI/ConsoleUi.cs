namespace NginxTools.UI
{
    public static class ConsoleUi
    {
        public static int BufferHeight => Math.Max(1, Console.BufferHeight);
        public static int BufferWidth => Math.Max(1, Console.BufferWidth);
        public static int WindowTop
        {
            get
            {
                try {
                    return Math.Max(0, Console.WindowTop);
                }
                catch {
                    return 0;
                }
            }
        }
        public static int WindowHeight => Console.WindowHeight > 0 ? Console.WindowHeight : BufferHeight;
        public static int WindowWidth => Console.WindowWidth > 0 ? Console.WindowWidth : BufferWidth;

        public static readonly ConsoleColor White = ConsoleColor.White;
        public static readonly ConsoleColor Accent = ConsoleColor.Cyan;
        public static readonly ConsoleColor Header = ConsoleColor.Green;
        public static readonly ConsoleColor Muted = ConsoleColor.DarkGray;
        public static readonly ConsoleColor Warn = ConsoleColor.Yellow;
        public static readonly ConsoleColor Error = ConsoleColor.Red;
        public static readonly ConsoleColor Success = ConsoleColor.Green;

        /// <summary>
        /// Написать на той же строке.
        /// </summary>
        /// <param name="text">Текст.</param>
        /// <param name="color">Цвет текста.</param>
        public static void Write(string text, ConsoleColor? color = null)
        {
            var prev = Console.ForegroundColor;
            if (color.HasValue) Console.ForegroundColor = color.Value;
            Console.Write(text);
            if (color.HasValue) Console.ForegroundColor = prev;
        }

        /// <summary>
        /// Написать на следующей строке.
        /// </summary>
        /// <param name="text">Текст.</param>
        /// <param name="color">Цвет текста.</param>
        public static void WriteLine(string text = "", ConsoleColor? color = null)
        {
            var prev = Console.ForegroundColor;
            if (color.HasValue)
                Console.ForegroundColor = color.Value;
            Console.WriteLine(text);
            if (color.HasValue)
                Console.ForegroundColor = prev;
        }

        /// <summary>
        /// Безопасный SetCursorPosition - молча обрезает по границам буфера.
        /// </summary>
        /// <param name="left">Значение слева.</param>
        /// <param name="top">Значение справа.</param>
        public static void SafeSetCursor(int left, int top)
        {
            int x = Math.Clamp(left, 0, BufferWidth - 1);
            int y = Math.Clamp(top, 0, BufferHeight - 1);
            try {
                Console.SetCursorPosition(x, y);
            }
            catch (Exception ex)
            {
                WarnMsg($"Не удалось безопасно установить курсор. {ex.Message}");
            }
        }

        /// <summary>
        /// Пишет на следующей строке об успехе.
        /// </summary>
        /// <param name="msg">Текст, который необходимо отобразить.</param>
        public static void Ok(string msg) => WriteLine("[SUCCESS] " + msg, Success);

        /// <summary>
        /// Пишет на следующей строке об предупреждении.
        /// </summary>
        /// <param name="msg">Текст, который необходимо отобразить.</param>
        public static void WarnMsg(string m) => WriteLine("[WARNING] " + m, Warn);

        /// <summary>
        /// Пишет на следующей строке об ошибке.
        /// </summary>
        /// <param name="msg">Текст, который необходимо отобразить.</param>
        public static void Fail(string msg) => WriteLine("[ERROR] " + msg, Error);

        /// <summary>
        /// Пишет на следующей строке об информации.
        /// </summary>
        /// <param name="msg">Текст, который необходимо отобразить.</param>
        public static void Dim(string msg) => WriteLine("[INFO] " + msg, Muted);

        /// <summary>
        /// Пишет на следующей строке стилезованное разделение.
        /// </summary>
        /// <param name="title">Заголовок, который необходимо отобразить.</param>
        public static void Rule(string title = "")
        {
            int width = Math.Max(20, Console.WindowWidth - 1);
            if (string.IsNullOrEmpty(title))
            {
                Console.WriteLine(new string('=', width));
                return;
            }
            string text = $" {title} ";
            if (text.Length >= width - 2)
            {
                Console.WriteLine(text);
                return;
            }
            int left = (width - text.Length) / 2;
            int right = width - left - text.Length;
            Console.WriteLine(new string('=', left) + text + new string('=', right));
        }

        /// <summary>
        /// Отображает баннер.
        /// </summary>
        public static void Banner()
        {
            int w = WindowWidth;

            if (w < 40)
            {
                WriteLine("nginxtools", Accent);
                Console.WriteLine();
                return;
            }

            if (w < 80)
            {
                WriteLine("  ▓ nginxtools ▓", Accent);
                Console.WriteLine();
                return;
            }

            string[] lines =
            {
                @"  █████╗ ███████╗███████╗    ███████╗ ██████╗ ██╗   ██╗███╗   ██╗██████╗  █████╗ ████████╗██╗ ██████╗ ███╗   ██╗",
                @" ██╔══██╗██╔════╝██╔════╝    ██╔════╝██╔═══██╗██║   ██║████╗  ██║██╔══██╗██╔══██╗╚══██╔══╝██║██╔═══██╗████╗  ██║",
                @" ███████║█████╗  ███████╗    █████╗  ██║   ██║██║   ██║██╔██╗ ██║██║  ██║███████║   ██║   ██║██║   ██║██╔██╗ ██║",
                @" ██╔══██║██╔══╝  ╚════██║    ██╔══╝  ██║   ██║██║   ██║██║╚██╗██║██║  ██║██╔══██║   ██║   ██║██║   ██║██║╚██╗██║",
                @" ██║  ██║███████╗███████║    ██║     ╚██████╔╝╚██████╔╝██║ ╚████║██████╔╝██║  ██║   ██║   ██║╚██████╔╝██║ ╚████║",
                @" ╚═╝  ╚═╝╚══════╝╚══════╝    ╚═╝      ╚═════╝  ╚═════╝ ╚═╝  ╚═══╝╚═════╝ ╚═╝  ╚═╝   ╚═╝   ╚═╝ ╚═════╝ ╚═╝  ╚═══╝",
            };

            var prev = Console.ForegroundColor;
            Console.ForegroundColor = Accent;
            foreach (var line in lines) Console.WriteLine(line);
            Console.ForegroundColor = prev;
            Console.WriteLine();
        }

        /// <summary>
        /// Пытается очистить экран. Работает в интерактивном терминале,
        /// но не падает при перенаправленном выводе или в CI.
        /// </summary>
        public static void ClearSafely()
        {
            try
            {
                if (!Console.IsOutputRedirected)
                    Console.Clear();
            }
            catch
            {
                try {
                    Console.WriteLine(new string('\n', 2));
                }
                catch (Exception ex)
                {
                    WarnMsg($"Не удалось безопасно очистить консоль. {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Пытается расширить буфер консоли под нужное число строк.
        /// На Windows это работает; на Linux игнорируется (там буфер = окно).
        /// Возвращает фактически доступную высоту буфера.
        /// </summary>
        /// <param name="neededRows">Ищет необходимые строки</param>
        /// <returns></returns>
        public static int TryExpandBuffer(int neededRows)
        {
            int target = Math.Max(neededRows, BufferHeight);

            target = Math.Min(target, short.MaxValue - 1);

            try
            {
                if (OperatingSystem.IsWindows() && target > Console.BufferHeight)
                    Console.BufferHeight = (short)target;
            }
            catch (Exception ex)
            {
                WarnMsg($"Попытка расширить буфер консоли не удалось. {ex.Message}");
            }

            return BufferHeight;
        }
    }
}
