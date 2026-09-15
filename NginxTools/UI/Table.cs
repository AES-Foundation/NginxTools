namespace NginxTools.UI
{
    public static class Table
    {
        /// <summary>
        /// Рисует таблицу в стиле box-drawing: рамка, заголовок, авто-ширины.
        /// Если данных больше, чем помещается, ячейки обрезаются с многоточием.
        /// </summary>
        /// <param name="headers">Верхняя часть таблицы (заголовки).</param>
        /// <param name="rows">Строки таблицы (данные).</param>
        /// <param name="maxWidths">Максимальная длинна столбцов.</param>
        /// <exception cref="ArgumentException">Вызывает исключение, говорящее о разности количества ячеек в таблице.</exception>
        public static void Render(
            string[] headers,
            IReadOnlyList<string[]> rows,
            int[]? maxWidths = null)
        {
            if (headers.Length == 0) return;
            if (rows.Any(r => r.Length != headers.Length))
                throw new ArgumentException("Все строки должны иметь одинаковое количество ячеек.");

            int cols = headers.Length;
            var widths = new int[cols];

            for (int i = 0; i < cols; i++)
            {
                widths[i] = DisplayWidth(headers[i]);
                foreach (var row in rows)
                    widths[i] = Math.Max(widths[i], DisplayWidth(row[i]));
                if (maxWidths is not null && i < maxWidths.Length && maxWidths[i] > 0)
                    widths[i] = Math.Min(widths[i], maxWidths[i]);
            }

            int maxTotal = Math.Max(20, ConsoleUi.BufferWidth - 2);
            int Total() => widths.Sum() + 3 * cols + 1;
            while (Total() > maxTotal)
            {
                int widest = 0;
                for (int i = 1; i < cols; i++)
                    if (widths[i] > widths[widest]) widest = i;
                if (widths[widest] <= 6) break;
                widths[widest]--;
            }

            ConsoleUi.WriteLine(BuildBorder('┌', '┬', '┐', widths), ConsoleUi.Muted);

            ConsoleUi.Write("│ ", ConsoleUi.Muted);
            for (int i = 0; i < cols; i++)
            {
                ConsoleUi.Write(Pad(headers[i], widths[i]), ConsoleUi.Header);
                ConsoleUi.Write(" │ ", ConsoleUi.Muted);
            }
            Console.WriteLine();

            ConsoleUi.WriteLine(BuildBorder('├', '┼', '┤', widths), ConsoleUi.Muted);

            foreach (var row in rows)
            {
                ConsoleUi.Write("│ ", ConsoleUi.Muted);
                for (int i = 0; i < cols; i++)
                {
                    Console.Write(Pad(row[i], widths[i]));
                    ConsoleUi.Write(" │ ", ConsoleUi.Muted);
                }
                Console.WriteLine();
            }

            ConsoleUi.WriteLine(BuildBorder('└', '┴', '┘', widths), ConsoleUi.Muted);
        }

        private static string BuildBorder(char left, char middle, char right, int[] widths)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(left);
            for (int i = 0; i < widths.Length; i++)
            {
                sb.Append(new string('─', widths[i] + 2));
                sb.Append(i < widths.Length - 1 ? middle : right);
            }
            return sb.ToString();
        }

        private static string Pad(string text, int width)
        {
            int w = DisplayWidth(text);
            if (w > width) text = Truncate(text, width);
            else if (w < width) text += new string(' ', width - w);
            return text;
        }

        private static int DisplayWidth(string s) => s.Length;

        private static string Truncate(string s, int max)
        {
            if (s.Length <= max) return s;
            if (max <= 1) return s.Substring(0, max);
            return s.Substring(0, max - 1) + "…";
        }
    }
}