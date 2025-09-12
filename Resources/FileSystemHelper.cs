using System;
using System.Linq;

namespace NextUI_Setup_Wizard.Resources
{
    /// <summary>
    /// Helper class for file system operations and utilities
    /// </summary>
    public static class FileSystemHelper
    {
        /// <summary>
        /// Determines if a file name represents a system file that should typically be ignored
        /// during file operations and listings
        /// </summary>
        /// <param name="fileName">The file name to check</param>
        /// <returns>True if the file is a system file that should be ignored, false otherwise</returns>
        public static bool IsSystemFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return false;
                
            // Common system files to ignore
            var systemFiles = new[]
            {
                "System Volume Information",    // Windows
                "WPSettings.dat",   // Windows
                "Thumbs.db",      // Windows
                "desktop.ini",    // Windows
                "autorun.ico",    // Windows
                "autorun.inf",    // Windows
                ".DS_Store",      // macOS
                "._.DS_Store",    // macOS resource fork
                "__MACOSX",        // macOS archive metadata
                ".fseventsd",       // macOS
                ".Spotlight-V100",  // macOS
            };
            
            return systemFiles.Any(sf => fileName.Equals(sf, StringComparison.OrdinalIgnoreCase));
        }
    }
}