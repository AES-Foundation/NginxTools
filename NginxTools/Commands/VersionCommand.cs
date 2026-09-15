using NginxTools.Services;
using NginxTools.UI;

namespace NginxTools.Commands
{
    public static class VersionCommand
    {
        /// <summary>
        /// Выполняет команду получения версии.
        /// </summary>
        /// <param name="nginxDir">Путь к исполняемому NGINX.</param>
        /// <returns>Возвращает <see langword="int"/> код процесса.</returns>
        public static async Task<int> RunAsync(string nginxDir)
        {
            var nginx = new NginxController(nginxDir);

            ConsoleUi.Ok($"NGINX Version {await nginx.GetVersion()}");

            return 0;
        }
    }
}
