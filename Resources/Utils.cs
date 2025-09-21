using System;
using System.Threading.Tasks;

namespace NextUI_Setup_Wizard.Resources
{
    public enum OSType
    {
        Windows,
        Mac,
        Unsupported
    }

    public static class Utils
    {
        public static OSType CurrentOS
        {
            get
            {
#if WINDOWS
                return OSType.Windows;
#elif MACCATALYST
                return OSType.Mac;
#else
                return OSType.Unsupported;
#endif
            }
        }

        /// <summary>
        /// Opens a directory in the system's default file manager.
        /// Attempts multiple methods to ensure cross-platform compatibility.
        /// </summary>
        /// <param name="directoryPath">The path to the directory to open</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public static async Task OpenDirectoryAsync(string directoryPath)
        {
            try
            {
                bool didOpen = await Launcher.OpenAsync(new Uri($"file://{directoryPath}"));
                if (!didOpen)
                    await Launcher.OpenAsync(directoryPath);
            }
            catch
            {
            }
        }
    }
}