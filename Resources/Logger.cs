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
            var logLine = $"[{timestamp}] {message}{Environment.NewLine}";
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
            catch (Exception ex)
            {
                // Can't log to file since logging failed, write to console instead
                Console.WriteLine($"Failed to flush log messages to file: {ex.Message}");
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
                    // Delete old -prev log if it exists (with retry for transient failures)
                    if (File.Exists(prevLogPath))
                    {
                        try
                        {
                            File.Delete(prevLogPath);
                        }
                        catch (IOException ex)
                        {
                            // If deletion fails (file locked, etc.), try to overwrite instead
                            Console.WriteLine($"Could not delete previous log file: {ex.Message}");
                        }
                    }

                    // Move current log to -prev (with overwrite to handle race conditions)
                    try
                    {
                        File.Move(currentLogPath, prevLogPath, overwrite: true);
                    }
                    catch (IOException ex)
                    {
                        // If move fails, try copy and delete as fallback
                        Console.WriteLine($"Could not move log file, trying copy instead: {ex.Message}");
                        try
                        {
                            File.Copy(currentLogPath, prevLogPath, overwrite: true);
                            File.Delete(currentLogPath);
                        }
                        catch (Exception copyEx)
                        {
                            Console.WriteLine($"Could not copy/delete log file: {copyEx.Message}");
                            // If all else fails, just delete the current log to start fresh
                            try
                            {
                                File.Delete(currentLogPath);
                            }
                            catch
                            {
                                // Give up - we tried our best
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Don't fail app startup on log rotation errors, but log to console
                Console.WriteLine($"Failed to rotate log files: {ex.Message}");
            }
        }
    }
}