namespace NginxTools.UI
{
    public sealed class InteractiveMenu<T>
    {
        private readonly string _title;
        private readonly IReadOnlyList<T> _items;
        private readonly Func<T, string> _renderer;
        private readonly string _hint;

        private const int ReservedTop = 3;
        private const int ReservedBottom = 1;

        public InteractiveMenu(
            string title,
            IReadOnlyList<T> items,
            Func<T, string> renderer,
            string hint = "↑↓ — навигация,  Enter — выбор,  Esc — отмена")
        {
            _title = title;
            _items = items;
            _renderer = renderer;
            _hint = hint;
        }

        /// <summary>
        /// Показывает интерактивное меню.
        /// </summary>
        /// <returns>Возвращает <see langword="T" /> готовое интерактивное меню.</returns>
        /// <exception cref="InvalidOperationException">Вызывает исключение, при некорретном условии в операции.</exception>
        /// <exception cref="OperationCanceledException">Вызывает исключение, при отменённой операции.</exception>
        public T Show()
        {
            if (_items.Count == 0)
                throw new InvalidOperationException("Список пуст.");

            ConsoleUi.ClearSafely();
            ConsoleUi.TryExpandBuffer(_items.Count + ReservedTop + ReservedBottom + 2);

            int selected = 0;
            int offset = 0;
            bool prevCursor = false;

            try
            {
                if (OperatingSystem.IsWindows())
                {
                    prevCursor = Console.CursorVisible;
                }
                Console.CursorVisible = false;
            }
            catch (PlatformNotSupportedException)
            {
                ConsoleUi.WarnMsg("Операционная система не поддерживает видимость курсора.");
            }
            Console.CursorVisible = false;

            try
            {
                Redraw(ref offset, selected);

                while (true)
                {
                    var key = Console.ReadKey(intercept: true);
                    int page = PageSize();
                    int next = selected;

                    switch (key.Key)
                    {
                        case ConsoleKey.UpArrow: next = selected - 1; break;
                        case ConsoleKey.DownArrow: next = selected + 1; break;
                        case ConsoleKey.Home: next = 0; break;
                        case ConsoleKey.End: next = _items.Count - 1; break;
                        case ConsoleKey.PageUp: next = selected - page; break;
                        case ConsoleKey.PageDown: next = selected + page; break;

                        case ConsoleKey.Enter:
                            FinishWithCursor();
                            return _items[selected];

                        case ConsoleKey.Escape:
                            FinishWithCursor();
                            throw new OperationCanceledException("Выбор отменён.");
                    }

                    if (next < 0) next = _items.Count - 1;
                    if (next >= _items.Count) next = 0;

                    selected = next;

                    Redraw(ref offset, selected);
                }
            }
            finally
            {
                Console.CursorVisible = prevCursor;
            }
        }

        /// <summary>
        /// Считает размер страницы.
        /// </summary>
        /// <returns>Возвращает <see langword="int" /> размер страницы.</returns>
        private static int PageSize()
        {
            int h = ConsoleUi.WindowHeight;
            return Math.Max(3, h - ReservedTop - ReservedBottom);
        }

        /// <summary>
        /// Полная перерисовка видимой области, начиная с текущего WindowTop.
        /// </summary>
        /// <param name="offset">Пропускаемые строки.</param>
        /// <param name="selected">Выделенная строка.</param>
        private void Redraw(ref int offset, int selected)
        {
            int page = PageSize();

            if (_items.Count <= page)
            {
                offset = 0;
            }
            else
            {
                if (selected < offset) offset = selected;
                if (selected >= offset + page) offset = selected - page + 1;
                int maxOffset = Math.Max(0, _items.Count - page);
                if (offset > maxOffset) offset = maxOffset;
                if (offset < 0) offset = 0;
            }

            int winTop = ConsoleUi.WindowTop;
            int winH = ConsoleUi.WindowHeight;
            int width = ConsoleUi.BufferWidth - 1;

            DrawPlain(winTop + 0, Fit(_title, width), ConsoleUi.Header);
            DrawPlain(winTop + 1, Fit(_hint, width), ConsoleUi.Muted);
            DrawPlain(winTop + 2, new string(' ', width), null);

            int visible = Math.Min(page, _items.Count - offset);
            for (int i = 0; i < visible; i++)
            {
                int idx = offset + i;
                int row = winTop + ReservedTop + i;

                if (row >= winTop + winH - ReservedBottom) break;

                string text = Fit("  " + _renderer(_items[idx]), width);

                if (idx == selected)
                    DrawHighlighted(row, text);
                else
                    DrawPlain(row, text, null);
            }

            for (int i = visible; i < page; i++)
            {
                int row = winTop + ReservedTop + i;
                if (row >= winTop + winH - ReservedBottom) break;
                DrawPlain(row, new string(' ', width), null);
            }

            int indRow = Math.Min(
                winTop + ReservedTop + visible,
                winTop + winH - ReservedBottom);

            if (indRow >= winTop && indRow < winTop + winH)
            {
                string indicator = BuildIndicator(offset, page, selected);
                DrawPlain(indRow, Fit(indicator, width), ConsoleUi.Muted);
            }

            int selRow = winTop + ReservedTop + (selected - offset);
            if (selRow >= winTop && selRow < winTop + winH - ReservedBottom)
                ConsoleUi.SafeSetCursor(0, selRow);
        }

        /// <summary>
        /// Создаёт соответствующий окну пропуск.
        /// </summary>
        /// <param name="text">Текст.</param>
        /// <param name="width">Длина создающее пространство справа.</param>
        /// <returns>Возвращает отформатированную строку <see langword="string"/>.</returns>
        private static string Fit(string text, int width)
        {
            if (text.Length >= width) return text.Substring(0, width);
            return text.PadRight(width);
        }

        /// <summary>
        /// Отрисовка плашки.
        /// </summary>
        /// <param name="row">Номер строки.</param>
        /// <param name="text">Текст строки.</param>
        /// <param name="color">Цвет.</param>
        private static void DrawPlain(int row, string text, ConsoleColor? color)
        {
            ConsoleUi.SafeSetCursor(0, row);
            var prev = Console.ForegroundColor;
            if (color.HasValue) Console.ForegroundColor = color.Value;
            Console.Write(text);
            if (color.HasValue) Console.ForegroundColor = prev;
        }

        /// <summary>
        /// Отрисовка подсвечивания.
        /// </summary>
        /// <param name="row">Номер строки.</param>
        /// <param name="text">Текст строки.</param>
        private static void DrawHighlighted(int row, string text)
        {
            ConsoleUi.SafeSetCursor(0, row);
            var prevBg = Console.BackgroundColor;
            var prevFg = Console.ForegroundColor;
            Console.BackgroundColor = ConsoleColor.DarkCyan;
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(text);
            Console.BackgroundColor = prevBg;
            Console.ForegroundColor = prevFg;
        }

        /// <summary>
        /// Построение индикаторов.
        /// </summary>
        /// <param name="offset">Проскаемые строки.</param>
        /// <param name="page">Номер страницы.</param>
        /// <param name="selected">Выделенная страница.</param>
        /// <returns></returns>
        private string BuildIndicator(int offset, int page, int selected)
        {
            if (_items.Count <= page)
                return $"[{selected + 1}/{_items.Count}]";

            string up = offset > 0 ? "↑" : " ";
            string down = offset + page < _items.Count ? "↓" : " ";
            int first = offset + 1;
            int last = Math.Min(offset + page, _items.Count);

            return $"[{selected + 1}/{_items.Count}]  {up}{down}  видно {first}–{last}";
        }

        /// <summary>
        /// Возвращает курсор в разумное место после завершения меню.
        /// </summary>
        private void FinishWithCursor()
        {
            int winTop = ConsoleUi.WindowTop;
            int winH = ConsoleUi.WindowHeight;
            int row = Math.Min(winTop + ReservedTop + PageSize(), winTop + winH - 1);
            ConsoleUi.SafeSetCursor(0, row);
            if (row < ConsoleUi.BufferHeight - 1) Console.WriteLine();
        }
    }
}
