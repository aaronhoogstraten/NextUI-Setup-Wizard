using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;

namespace NextUI_Setup_Wizard.Resources
{
    /// <summary>
    /// Service for performing BIOS and ROM file operations via ADB
    /// </summary>
    public class AdbFileOperations
    {
        private readonly AdbService _adbService;
        private readonly string? _selectedDeviceId;

        /// <summary>
        /// Base path for NextUI on TrimUI devices
        /// </summary>
        public const string NEXTUI_BASE_PATH = "/mnt/SDCARD";

        public AdbFileOperations(AdbService adbService, string? deviceId = null)
        {
            _adbService = adbService ?? throw new ArgumentNullException(nameof(adbService));
            _selectedDeviceId = deviceId;
        }

        /// <summary>
        /// Gets the base path for NextUI on the device
        /// Standard location for TrimUI devices: /mnt/SDCARD
        /// </summary>
        public string GetNextUIBasePath()
        {
            return NEXTUI_BASE_PATH;
        }

        /// <summary>
        /// Verifies that NextUI directories exist on the device
        /// </summary>
        public async Task<AdbResult> VerifyNextUIDirectoriesAsync(IProgress<string>? progress = null)
        {
            try
            {
                var basePath = GetNextUIBasePath();
                progress?.Report("Verifying NextUI directories on device...");

                // Check if NextUI base directory exists
                var baseExists = await _adbService.PathExistsAsync(basePath, _selectedDeviceId);
                if (!baseExists)
                {
                    return new AdbResult { IsSuccess = false, Error = $"NextUI installation not found at {basePath}. Please ensure NextUI is properly installed on your device." };
                }

                // Check BIOS and ROM directories
                var biosPath = Path.Combine(basePath, "Bios").Replace('\\', '/');
                var romsPath = Path.Combine(basePath, "Roms").Replace('\\', '/');

                var biosExists = await _adbService.PathExistsAsync(biosPath, _selectedDeviceId);
                var romsExists = await _adbService.PathExistsAsync(romsPath, _selectedDeviceId);

                if (!biosExists)
                {
                    return new AdbResult { IsSuccess = false, Error = $"BIOS directory not found at {biosPath}. Please ensure NextUI is properly installed." };
                }

                if (!romsExists)
                {
                    return new AdbResult { IsSuccess = false, Error = $"ROMs directory not found at {romsPath}. Please ensure NextUI is properly installed." };
                }

                // Check for NextUI version file
                progress?.Report("Verifying NextUI version file...");
                var versionFileExists = await VerifyNextUIVersionFileAsync(basePath);
                if (!versionFileExists)
                {
                    return new AdbResult { IsSuccess = false, Error = "NextUI version file not found. Please ensure NextUI is properly installed on your device." };
                }

                progress?.Report("NextUI installation verified successfully!");
                return new AdbResult { IsSuccess = true, Output = "NextUI installation verified" };
            }
            catch (Exception ex)
            {
                return new AdbResult { IsSuccess = false, Error = ex.Message };
            }
        }

        /// <summary>
        /// Verifies that NextUI version file exists on the device
        /// </summary>
        private async Task<bool> VerifyNextUIVersionFileAsync(string basePath)
        {
            try
            {
                // Check for MinUI.zip first
                var minUIZipPath = Path.Combine(basePath, "MinUI.zip").Replace('\\', '/');
                if (await _adbService.PathExistsAsync(minUIZipPath, _selectedDeviceId))
                {
                    // If MinUI.zip exists, we assume NextUI is installed
                    return true;
                }

                // Check for .system/version.txt directly on filesystem
                var versionFilePath = Path.Combine(basePath, ".system", "version.txt").Replace('\\', '/');
                if (await _adbService.PathExistsAsync(versionFilePath, _selectedDeviceId))
                {
                    return true;
                }

                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Copies BIOS files to the device using ADB
        /// </summary>
        public async Task<AdbResult> CopyBiosFilesAsync(
            List<BiosFileCopy> biosFiles,
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var totalFiles = biosFiles.Count;
                var processedFiles = 0;

                progress?.Report($"Starting BIOS file transfer to device ({totalFiles} files)...");

                foreach (var biosFile in biosFiles)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return new AdbResult { IsSuccess = false, Error = "Transfer cancelled by user" };
                    }

                    processedFiles++;
                    var fileName = Path.GetFileName(biosFile.SourcePath);
                    progress?.Report($"Copying {fileName} ({processedFiles}/{totalFiles})...");

                    // Get the target path on device
                    var remotePath = GetBiosRemotePath(biosFile.SystemCode, biosFile.FileName);

                    // Copy the file
                    var copyResult = await _adbService.PushFileAsync(
                        biosFile.SourcePath,
                        remotePath,
                        _selectedDeviceId,
                        progress: new Progress<string>(msg => progress?.Report($"{fileName}: {msg}")),
                        cancellationToken: cancellationToken);

                    if (!copyResult.IsSuccess)
                    {
                        progress?.Report($"Failed to copy {fileName}: {copyResult.Error}");
                        return copyResult;
                    }

                    // Handle special case for neogeo.zip (arcade BIOS)
                    if (biosFile.FileName == "neogeo.zip" && biosFile.SystemCode == "FBN")
                    {
                        await CreateArcadeMapFileAsync(biosFile.SystemCode, progress);
                    }
                }

                progress?.Report($"Successfully transferred {totalFiles} BIOS file(s) to device!");
                return new AdbResult { IsSuccess = true, Output = $"Transferred {totalFiles} files" };
            }
            catch (Exception ex)
            {
                return new AdbResult { IsSuccess = false, Error = ex.Message };
            }
        }

        /// <summary>
        /// Copies ROM files to the device using ADB
        /// </summary>
        public async Task<AdbResult> CopyRomFilesAsync(
            List<RomFileCopy> romFiles,
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var totalFiles = romFiles.Count;
                var processedFiles = 0;

                progress?.Report($"Starting ROM file transfer to device ({totalFiles} files)...");

                foreach (var romFile in romFiles)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return new AdbResult { IsSuccess = false, Error = "Transfer cancelled by user" };
                    }

                    processedFiles++;
                    var fileName = Path.GetFileName(romFile.SourcePath);
                    progress?.Report($"Copying {fileName} ({processedFiles}/{totalFiles})...");

                    // Get the target path on device
                    var remotePath = GetRomRemotePath(romFile.SystemCode, romFile.SystemName, fileName);

                    // Copy the file
                    var copyResult = await _adbService.PushFileAsync(
                        romFile.SourcePath,
                        remotePath,
                        _selectedDeviceId,
                        progress: new Progress<string>(msg => progress?.Report($"{fileName}: {msg}")),
                        cancellationToken: cancellationToken);

                    if (!copyResult.IsSuccess)
                    {
                        progress?.Report($"Failed to copy {fileName}: {copyResult.Error}");
                        return copyResult;
                    }
                }

                progress?.Report($"Successfully transferred {totalFiles} ROM file(s) to device!");
                return new AdbResult { IsSuccess = true, Output = $"Transferred {totalFiles} files" };
            }
            catch (Exception ex)
            {
                return new AdbResult { IsSuccess = false, Error = ex.Message };
            }
        }

        /// <summary>
        /// Gets the remote path for a BIOS file on the device
        /// </summary>
        private string GetBiosRemotePath(string systemCode, string fileName)
        {
            var basePath = GetNextUIBasePath();
            return Path.Combine(basePath, "Bios", systemCode, fileName).Replace('\\', '/');
        }

        /// <summary>
        /// Gets the remote path for a ROM file on the device
        /// </summary>
        private string GetRomRemotePath(string systemCode, string systemName, string fileName)
        {
            var basePath = GetNextUIBasePath();
            var romDirName = GetRomDirectoryName(systemCode, systemName);
            return Path.Combine(basePath, "Roms", romDirName, fileName).Replace('\\', '/');
        }

        /// <summary>
        /// Gets the ROM directory name based on system code and name
        /// </summary>
        private string GetRomDirectoryName(string systemCode, string systemName)
        {
            // Handle special cases where ROM path system name might be different
            var pathSystemName = systemCode switch
            {
                "FC" => "Nintendo Entertainment System",
                "MD" => "Sega Genesis",
                _ when !string.IsNullOrEmpty(systemName) => systemName,
                _ => systemCode
            };

            return $"{pathSystemName} ({systemCode})";
        }

        /// <summary>
        /// Creates the arcade map.txt file for FBN system
        /// </summary>
        private async Task CreateArcadeMapFileAsync(string systemCode, IProgress<string>? progress = null)
        {
            try
            {
                progress?.Report("Creating arcade map.txt file...");

                // Create a temporary map.txt file
                var tempMapPath = Path.Combine(FileSystem.CacheDirectory, "map.txt");
                var mapContent = "neogeo.zip\t.Neo Geo Bios\n"; // Hide Neo Geo BIOS from ROM list

                // Try to download full map.txt file
                try
                {
                    using var httpClient = new System.Net.Http.HttpClient();
                    httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

                    var response = await httpClient.GetAsync("https://raw.githubusercontent.com/ryanmsartor/TrimUI-Brick-and-Smart-Pro-Custom-MinUI-Paks/refs/heads/main/Roms/Arcade%20(FBN)/map.txt");

                    if (response.IsSuccessStatusCode)
                    {
                        mapContent = await response.Content.ReadAsStringAsync();
                        mapContent = mapContent.Replace("neogeo.zip\tNeo Geo Bios", "neogeo.zip\t.Neo Geo Bios");
                        progress?.Report("Downloaded full arcade map.txt");
                    }
                    else
                    {
                        progress?.Report("Using basic map.txt (full download failed)");
                    }
                }
                catch
                {
                    progress?.Report("Using basic map.txt (network error)");
                }

                // Write temporary file
                await File.WriteAllTextAsync(tempMapPath, mapContent);

                // Push to device
                var remotePath = GetBiosRemotePath(systemCode, "map.txt");
                var result = await _adbService.PushFileAsync(tempMapPath, remotePath, _selectedDeviceId);

                // Clean up temporary file
                if (File.Exists(tempMapPath))
                {
                    File.Delete(tempMapPath);
                }

                if (result.IsSuccess)
                {
                    progress?.Report("Arcade map.txt created successfully");
                }
                else
                {
                    progress?.Report($"Failed to create map.txt: {result.Error}");
                }
            }
            catch (Exception ex)
            {
                progress?.Report($"Error creating map.txt: {ex.Message}");
            }
        }

        /// <summary>
        /// Verifies that files were successfully copied to the device
        /// </summary>
        public async Task<List<string>> VerifyFilesAsync(List<string> remotePaths)
        {
            var missingFiles = new List<string>();

            foreach (var remotePath in remotePaths)
            {
                var exists = await _adbService.PathExistsAsync(remotePath, _selectedDeviceId);
                if (!exists)
                {
                    missingFiles.Add(remotePath);
                }
            }

            return missingFiles;
        }

    }

    /// <summary>
    /// Represents a BIOS file to be copied via ADB
    /// </summary>
    public class BiosFileCopy
    {
        public string SourcePath { get; set; } = "";
        public string FileName { get; set; } = "";
        public string SystemCode { get; set; } = "";
    }

    /// <summary>
    /// Represents a ROM file to be copied via ADB
    /// </summary>
    public class RomFileCopy
    {
        public string SourcePath { get; set; } = "";
        public string FileName { get; set; } = "";
        public string SystemCode { get; set; } = "";
        public string SystemName { get; set; } = "";
    }
}