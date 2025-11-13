using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;

namespace NextUI_Setup_Wizard.Resources
{
    /// <summary>
    /// Service for extracting Android platform-tools from a zip file
    /// </summary>
    public class PlatformToolsExtractor
    {
        private string _extractionPath;
        private bool _usingExistingPath = false;

        public PlatformToolsExtractor()
        {
            _extractionPath = Path.Combine(FileSystem.CacheDirectory, "platform-tools");
        }

        /// <summary>
        /// Gets the path where platform-tools are extracted
        /// </summary>
        public string ExtractionPath => _extractionPath;

        /// <summary>
        /// Gets the path to the ADB executable
        /// </summary>
        public string AdbExecutablePath
        {
            get
            {
                var adbName = Utils.CurrentOS == OSType.Windows ? "adb.exe" : "adb";
                if (_usingExistingPath)
                {
                    return Path.Combine(_extractionPath, adbName);
                }
                else
                {
                    return Path.Combine(_extractionPath, "platform-tools", adbName);
                }
            }
        }

        /// <summary>
        /// Checks if platform-tools are already extracted and ADB is available
        /// </summary>
        /// <returns>True if ADB is available, false otherwise</returns>
        public bool IsAdbAvailable()
        {
            return File.Exists(AdbExecutablePath);
        }

        /// <summary>
        /// Attempts to auto-detect platform-tools installations (e.g., Homebrew on macOS)
        /// </summary>
        /// <returns>True if a valid installation was found and set, false otherwise</returns>
        public bool TryAutoDetectExistingInstallation()
        {
            try
            {
                // Check for Homebrew installation on macOS (Apple Silicon only)
                if (Utils.CurrentOS == OSType.Mac)
                {
                    var basePath = "/opt/homebrew/Caskroom/android-platform-tools";

                    if (Directory.Exists(basePath))
                    {
                        Logger.LogImmediate("macOS homebrew android-platform-tools exists");

                        // Scan for version subdirectories
                        var versionDirs = Directory.GetDirectories(basePath);
                        foreach (var versionDir in versionDirs)
                        {
                            var platformToolsPath = Path.Combine(versionDir, "platform-tools");
                            if (Directory.Exists(platformToolsPath))
                            {
                                var adbPath = Path.Combine(platformToolsPath, "adb");
                                if (File.Exists(adbPath))
                                {
                                    Logger.LogImmediate("macOS homebrew adb exists");
                                    _extractionPath = platformToolsPath;
                                    _usingExistingPath = true;
                                    return true;
                                }
                            }
                        }
                    }
                }

                // Could add other auto-detection logic here for different platforms/package managers
                return false;
            }
            catch (Exception ex)
            {
                Logger.LogImmediate($"Failed to auto-detect existing platform-tools installation: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Sets the path to an existing platform-tools installation
        /// </summary>
        /// <param name="existingPath">Path to existing platform-tools directory</param>
        /// <returns>True if the path is valid and contains ADB, false otherwise</returns>
        public bool SetExistingPath(string existingPath)
        {
            try
            {
                if (string.IsNullOrEmpty(existingPath) || !Directory.Exists(existingPath))
                    return false;

                var adbName = Utils.CurrentOS == OSType.Windows ? "adb.exe" : "adb";
                var adbPath = Path.Combine(existingPath, adbName);

                if (!File.Exists(adbPath))
                    return false;

                _extractionPath = existingPath;
                _usingExistingPath = true;
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogImmediate($"Failed to set existing platform-tools path '{existingPath}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Extracts platform-tools from the specified zip file
        /// </summary>
        /// <param name="platformToolsZipPath">Path to the platform-tools zip file</param>
        /// <param name="progress">Optional progress callback</param>
        /// <returns>True if extraction was successful, false otherwise</returns>
        public async Task<bool> ExtractPlatformToolsAsync(string platformToolsZipPath, IProgress<string>? progress = null)
        {
            try
            {
                progress?.Report("Validating zip file...");

                if (!File.Exists(platformToolsZipPath))
                {
                    throw new FileNotFoundException("Platform-tools zip file not found", platformToolsZipPath);
                }

                progress?.Report("Cleaning previous extraction...");

                // Clean up any existing extraction
                if (Directory.Exists(_extractionPath))
                {
                    Directory.Delete(_extractionPath, true);
                }

                progress?.Report("Creating extraction directory...");
                Directory.CreateDirectory(_extractionPath);

                progress?.Report("Extracting platform-tools...");

                // Extract the zip file
                ZipFile.ExtractToDirectory(platformToolsZipPath, _extractionPath, true);
                progress?.Report("Extraction completed!");

                progress?.Report("Setting executable permissions...");

                // Set executable permissions on Unix systems
                if (Utils.CurrentOS != OSType.Windows)
                {
                    await SetExecutablePermissionsAsync();
                }

                progress?.Report("Verifying installation...");

                // Verify ADB is available
                if (!IsAdbAvailable())
                {
                    throw new InvalidOperationException("ADB executable not found after extraction");
                }

                progress?.Report("Platform-tools extraction completed successfully!");
                return true;
            }
            catch (Exception ex)
            {
                progress?.Report($"Error extracting platform-tools: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Sets executable permissions on Unix systems
        /// </summary>
        private async Task SetExecutablePermissionsAsync()
        {
            if (Utils.CurrentOS == OSType.Windows)
                return;

            try
            {
                var platformToolsDir = Path.Combine(_extractionPath, "platform-tools");
                if (!Directory.Exists(platformToolsDir))
                    return;

                var files = Directory.GetFiles(platformToolsDir);

                foreach (var file in files)
                {
                    var fileName = Path.GetFileName(file);

                    // Set executable permissions for common tools
                    if (fileName == "adb" || fileName == "fastboot" || fileName == "aapt" ||
                        fileName == "aidl" || fileName == "dexdump" || fileName == "split-select" ||
                        fileName.StartsWith("lib") || !Path.HasExtension(fileName))
                    {
                        using var process = new System.Diagnostics.Process
                        {
                            StartInfo = new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = "chmod",
                                Arguments = $"+x \"{file}\"",
                                UseShellExecute = false,
                                CreateNoWindow = true
                            }
                        };

                        process.Start();
                        await process.WaitForExitAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogImmediate($"Failed to set executable permissions: {ex.Message}");
                Console.WriteLine($"Failed to set executable permissions: {ex.Message}");
            }
        }

        /// <summary>
        /// Removes the extracted platform-tools
        /// </summary>
        public void CleanUp()
        {
            try
            {
                if (Directory.Exists(_extractionPath))
                {
                    Directory.Delete(_extractionPath, true);
                }
            }
            catch (Exception ex)
            {
                Logger.LogImmediate($"Failed to clean up platform-tools: {ex.Message}");
                Console.WriteLine($"Failed to clean up platform-tools: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the size of the platform-tools directory in bytes
        /// </summary>
        /// <returns>Size in bytes, or 0 if not available</returns>
        public long GetPlatformToolsSize()
        {
            try
            {
                if (!Directory.Exists(_extractionPath))
                    return 0;

                var directoryInfo = new DirectoryInfo(_extractionPath);
                return GetDirectorySize(directoryInfo);
            }
            catch (Exception ex)
            {
                Logger.LogImmediate($"Failed to get platform-tools size: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Recursively calculates directory size
        /// </summary>
        private static long GetDirectorySize(DirectoryInfo directoryInfo)
        {
            long size = 0;

            try
            {
                // Add file sizes
                foreach (var fileInfo in directoryInfo.GetFiles())
                {
                    size += fileInfo.Length;
                }

                // Add subdirectory sizes
                foreach (var subdirectoryInfo in directoryInfo.GetDirectories())
                {
                    size += GetDirectorySize(subdirectoryInfo);
                }
            }
            catch (Exception ex)
            {
                // Log access denied and other errors but continue
                Logger.LogImmediate($"Error accessing directory during size calculation: {ex.Message}");
            }

            return size;
        }

        /// <summary>
        /// Validates that the zip file contains platform-tools
        /// </summary>
        /// <param name="zipPath">Path to the zip file</param>
        /// <returns>True if valid platform-tools zip, false otherwise</returns>
        public static bool ValidatePlatformToolsZip(string zipPath)
        {
            try
            {
                if (!File.Exists(zipPath))
                    return false;

                using var archive = ZipFile.OpenRead(zipPath);

                // Look for required files
                bool hasAdb = false;
                bool hasPlatformToolsDir = false;

                foreach (var entry in archive.Entries)
                {
                    if (entry.FullName.Contains("platform-tools/"))
                    {
                        hasPlatformToolsDir = true;
                    }

                    if (entry.Name == "adb" || entry.Name == "adb.exe")
                    {
                        hasAdb = true;
                    }
                }

                return hasAdb && hasPlatformToolsDir;
            }
            catch (Exception ex)
            {
                Logger.LogImmediate($"Failed to validate platform-tools zip '{zipPath}': {ex.Message}");
                return false;
            }
        }
    }
}