using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NextUI_Setup_Wizard.Resources
{
    /// <summary>
    /// Service for executing ADB commands and managing device connections
    /// </summary>
    public class AdbService
    {
        private readonly string _adbExecutablePath;
        private readonly int _defaultTimeoutMs = 30000; // 30 seconds

        public AdbService(string adbExecutablePath)
        {
            _adbExecutablePath = adbExecutablePath ?? throw new ArgumentNullException(nameof(adbExecutablePath));
        }

        /// <summary>
        /// Checks if ADB is available and working
        /// </summary>
        public async Task<bool> IsAdbWorkingAsync()
        {
            try
            {
                var result = await ExecuteAdbCommandAsync("version", timeout: 5000);
                return result.IsSuccess && result.Output.Contains("Android Debug Bridge");
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets list of connected devices
        /// </summary>
        public async Task<List<AdbDevice>> GetDevicesAsync()
        {
            var devices = new List<AdbDevice>();

            try
            {
                var result = await ExecuteAdbCommandAsync("devices -l");
                if (!result.IsSuccess)
                    return devices;

                var lines = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                foreach (var line in lines.Skip(1)) // Skip "List of devices attached"
                {
                    var trimmedLine = line.Trim();
                    if (string.IsNullOrEmpty(trimmedLine))
                        continue;

                    // Split on whitespace (spaces and tabs) to get device ID and status
                    var parts = trimmedLine.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        var deviceId = parts[0];
                        var status = parts[1];

                        var device = new AdbDevice
                        {
                            Id = deviceId,
                            Status = status,
                            IsOnline = status == "device"
                        };

                        // Parse additional properties (everything after device ID and status)
                        if (parts.Length > 2)
                        {
                            var properties = string.Join(" ", parts.Skip(2));
                            ParseDeviceProperties(device, properties);
                        }

                        devices.Add(device);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to get ADB devices: {ex.Message}");
            }

            return devices;
        }

        /// <summary>
        /// Pushes a file to the device
        /// </summary>
        /// <param name="localPath">Local file path</param>
        /// <param name="remotePath">Remote path on device</param>
        /// <param name="deviceId">Optional device ID if multiple devices</param>
        /// <param name="progress">Optional progress callback</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public async Task<AdbResult> PushFileAsync(
            string localPath,
            string remotePath,
            string? deviceId = null,
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (!File.Exists(localPath))
                {
                    return new AdbResult { IsSuccess = false, Error = $"Local file not found: {localPath}" };
                }

                var fileName = Path.GetFileName(localPath);
                progress?.Report($"Pushing {fileName} to device...");

                var deviceArg = !string.IsNullOrEmpty(deviceId) ? $"-s {deviceId}" : "";
                var command = $"{deviceArg} push \"{localPath}\" \"{remotePath}\"".Trim();

                var result = await ExecuteAdbCommandAsync(command, progress, cancellationToken);

                if (result.IsSuccess)
                {
                    progress?.Report($"Successfully pushed {fileName}");
                }
                else
                {
                    progress?.Report($"Failed to push {fileName}: {result.Error}");
                }

                return result;
            }
            catch (Exception ex)
            {
                return new AdbResult { IsSuccess = false, Error = ex.Message };
            }
        }


        /// <summary>
        /// Lists files in a directory on the device
        /// </summary>
        /// <param name="remotePath">Remote directory path</param>
        /// <param name="deviceId">Optional device ID if multiple devices</param>
        public async Task<List<string>> ListFilesAsync(string remotePath, string? deviceId = null)
        {
            var files = new List<string>();

            try
            {
                var deviceArg = !string.IsNullOrEmpty(deviceId) ? $"-s {deviceId}" : "";
                var command = $"{deviceArg} shell ls \"{remotePath}\"".Trim();

                var result = await ExecuteAdbCommandAsync(command);
                if (result.IsSuccess)
                {
                    var lines = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    files.AddRange(lines.Select(line => line.Trim()).Where(line => !string.IsNullOrEmpty(line)));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to list files: {ex.Message}");
            }

            return files;
        }

        /// <summary>
        /// Checks if a file or directory exists on the device
        /// </summary>
        /// <param name="remotePath">Remote path to check</param>
        /// <param name="deviceId">Optional device ID if multiple devices</param>
        public async Task<bool> PathExistsAsync(string remotePath, string? deviceId = null)
        {
            try
            {
                var deviceArg = !string.IsNullOrEmpty(deviceId) ? $"-s {deviceId}" : "";
                var command = $"{deviceArg} shell test -e \"{remotePath}\" && echo \"EXISTS\" || echo \"NOT_EXISTS\"".Trim();

                var result = await ExecuteAdbCommandAsync(command);
                return result.IsSuccess && result.Output.Contains("EXISTS");
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets device storage information
        /// </summary>
        /// <param name="deviceId">Optional device ID if multiple devices</param>
        public async Task<DeviceStorageInfo?> GetStorageInfoAsync(string? deviceId = null)
        {
            try
            {
                var deviceArg = !string.IsNullOrEmpty(deviceId) ? $"-s {deviceId}" : "";
                var command = $"{deviceArg} shell df /sdcard".Trim();

                var result = await ExecuteAdbCommandAsync(command);
                if (!result.IsSuccess)
                    return null;

                // Parse df output (format varies by device)
                var lines = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length < 2)
                    return null;

                var dataLine = lines.LastOrDefault(l => l.Contains("/sdcard") || l.Contains("storage") || !l.StartsWith("Filesystem"));
                if (string.IsNullOrEmpty(dataLine))
                    return null;

                var parts = dataLine.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 4)
                {
                    if (long.TryParse(parts[1], out var totalKb) &&
                        long.TryParse(parts[2], out var usedKb) &&
                        long.TryParse(parts[3], out var availableKb))
                    {
                        return new DeviceStorageInfo
                        {
                            TotalBytes = totalKb * 1024,
                            UsedBytes = usedKb * 1024,
                            AvailableBytes = availableKb * 1024
                        };
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to get storage info: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Executes an ADB command
        /// </summary>
        private Task<AdbResult> ExecuteAdbCommandAsync(
            string arguments,
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default,
            int? timeout = null)
        {
            try
            {
                using var process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = _adbExecutablePath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();

                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        outputBuilder.AppendLine(e.Data);
                        progress?.Report(e.Data);
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        errorBuilder.AppendLine(e.Data);
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                var timeoutMs = timeout ?? _defaultTimeoutMs;
                process.WaitForExit(timeoutMs);
                var completed = process.HasExited;

                if (!completed)
                {
                    process.Kill();
                    return Task.FromResult(new AdbResult { IsSuccess = false, Error = "Command timed out" });
                }

                var output = outputBuilder.ToString().Trim();
                var error = errorBuilder.ToString().Trim();

                return Task.FromResult(new AdbResult
                {
                    IsSuccess = process.ExitCode == 0,
                    Output = output,
                    Error = string.IsNullOrEmpty(error) ? null : error,
                    ExitCode = process.ExitCode
                });
            }
            catch (Exception ex)
            {
                return Task.FromResult(new AdbResult { IsSuccess = false, Error = ex.Message });
            }
        }

        /// <summary>
        /// Parses device properties from ADB devices output
        /// </summary>
        private static void ParseDeviceProperties(AdbDevice device, string properties)
        {
            var pairs = properties.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var pair in pairs)
            {
                var colonIndex = pair.IndexOf(':');
                if (colonIndex > 0 && colonIndex < pair.Length - 1)
                {
                    var key = pair.Substring(0, colonIndex);
                    var value = pair.Substring(colonIndex + 1);

                    switch (key.ToLowerInvariant())
                    {
                        case "model":
                            device.Model = value;
                            break;
                        case "device":
                            device.DeviceName = value;
                            break;
                        case "product":
                            device.Product = value;
                            break;
                        case "transport_id":
                            device.TransportId = value;
                            break;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Represents the result of an ADB command execution
    /// </summary>
    public class AdbResult
    {
        public bool IsSuccess { get; set; }
        public string Output { get; set; } = "";
        public string? Error { get; set; }
        public int ExitCode { get; set; }
    }

    /// <summary>
    /// Represents an ADB device
    /// </summary>
    public class AdbDevice
    {
        public string Id { get; set; } = "";
        public string Status { get; set; } = "";
        public bool IsOnline { get; set; }
        public string? Model { get; set; }
        public string? DeviceName { get; set; }
        public string? Product { get; set; }
        public string? TransportId { get; set; }

        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrEmpty(Model))
                    return $"{Model} ({Id})";
                if (!string.IsNullOrEmpty(DeviceName))
                    return $"{DeviceName} ({Id})";
                return Id;
            }
        }
    }

    /// <summary>
    /// Represents device storage information
    /// </summary>
    public class DeviceStorageInfo
    {
        public long TotalBytes { get; set; }
        public long UsedBytes { get; set; }
        public long AvailableBytes { get; set; }

        public double TotalGB => TotalBytes / (1024.0 * 1024.0 * 1024.0);
        public double UsedGB => UsedBytes / (1024.0 * 1024.0 * 1024.0);
        public double AvailableGB => AvailableBytes / (1024.0 * 1024.0 * 1024.0);

        public double UsedPercentage => TotalBytes > 0 ? (UsedBytes * 100.0) / TotalBytes : 0;
    }
}