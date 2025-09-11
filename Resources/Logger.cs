using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace NextUI_Setup_Wizard.Resources
{
    public class Logger : IDisposable
    {
        private const string LogFileName = "NextUI-Setup-Wizard.log";
        private readonly StringBuilder _messages = new();
        
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
                    var logPath = Path.Combine(FileSystem.Current.CacheDirectory, LogFileName);
                    File.AppendAllText(logPath, _messages.ToString());
                }
            }
            catch (Exception)
            {
            }
        }
    }
}