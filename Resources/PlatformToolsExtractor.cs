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
        private readonly string _extractionPath;

        public PlatformToolsExtractor()
        {
            _extractionPath = Path.Combine(FileSystem.AppDataDirectory, "platform-tools");
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
                return Path.Combine(_extractionPath, "platform-tools", adbName);
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
                        var process = new System.Diagnostics.Process
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
            catch
            {
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
            catch
            {
                // Ignore access denied errors
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
            catch
            {
                return false;
            }
        }
    }
}