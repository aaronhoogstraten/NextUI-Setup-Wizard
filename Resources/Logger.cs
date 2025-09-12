using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace NextUI_Setup_Wizard.Resources
{
    public class Logger : IDisposable
    {
        private const string LogFileName = "NextUI-Setup-Wizard.log";
        private const string PrevLogFileName = "NextUI-Setup-Wizard_previous.log";
        private static readonly string LogPath = Path.Combine(FileSystem.CacheDirectory, LogFileName);

        private readonly StringBuilder _messages = new();


        public static void LogImmediate(string message)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var logLine = $"[{timestamp}] {message}";
            File.AppendAllText(LogPath, logLine);
        }

        public void Log(string message)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            _messages.AppendLine($"[{timestamp}] {message}");
        }
        
        public void Dispose()
        {
            try
            {
                if (_messages.Length > 0)
                {
                    File.AppendAllText(LogPath, _messages.ToString());
                }
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Rotates log files on application startup. Moves existing log to -prev and deletes old -prev if it exists.
        /// </summary>
        public static void RotateLogFiles()
        {
            try
            {
                var cacheDirectory = FileSystem.CacheDirectory;
                var currentLogPath = Path.Combine(cacheDirectory, LogFileName);
                var prevLogPath = Path.Combine(cacheDirectory, PrevLogFileName);

                // If current log exists, rotate it
                if (File.Exists(currentLogPath))
                {
                    // Delete old -prev log if it exists
                    if (File.Exists(prevLogPath))
                    {
                        File.Delete(prevLogPath);
                    }

                    // Move current log to -prev
                    File.Move(currentLogPath, prevLogPath);
                }
            }
            catch (Exception)
            {
                // Ignore rotation errors to prevent app startup failures
            }
        }
    }
}