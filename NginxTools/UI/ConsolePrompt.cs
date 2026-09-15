namespace NginxTools.UI
{
    public static class ConsolePrompt
    {
        /// <summary>
        /// Запрос подтверждения. Поддерживает Enter, y/n, русские д/н, Esc.
        /// </summary>
        /// <param name="question">Вопрос, задаваемый пользователю.</param>
        /// <param name="defaultValue">Значение по умолчанию в случае нажатия Enter; По умолчанию <see langword="true"/>.</param>
        /// <returns>Возвращает <see langword="true"/> в случае подтверждения [Y]; <see langword="false"/> в случае ответа [N].</returns>
        public static bool Confirm(string question, bool defaultValue = true)
        {
            string hint = defaultValue ? "[Y/n]" : "[y/N]";
            while (true)
            {
                ConsoleUi.Write($"{question} {hint} ", ConsoleUi.Header);
                var key = Console.ReadKey(intercept: true);

                if (key.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine(defaultValue ? "y" : "n");
                    return defaultValue;
                }
                if (key.Key == ConsoleKey.Escape)
                {
                    Console.WriteLine();
                    return false;
                }

                char c = char.ToLowerInvariant(key.KeyChar);
                if (c is 'y' or 'д') { Console.WriteLine("y"); return true; }
                if (c is 'n' or 'н') { Console.WriteLine("n"); return false; }
            }
        }
    }
}
