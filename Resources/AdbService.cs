using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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

        /// <summary>
        /// Event fired when an ADB command is executed for real-time logging
        /// </summary>
        public event EventHandler<AdbCommandLogEventArgs>? CommandExecuted;

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
                // Escape special characters in the path for shell
                var escapedPath = remotePath.Replace("(", "\\(").Replace(")", "\\)");
                var command = $"{deviceArg} shell test -e \"{escapedPath}\" && echo \"EXISTS\" || echo \"NOT_EXISTS\"".Trim();

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
                var command = $"{deviceArg} shell df {AdbFileOperations.NEXTUI_BASE_PATH}".Trim();

                var result = await ExecuteAdbCommandAsync(command);
                if (!result.IsSuccess)
                    return null;

                // Parse df output (format varies by device)
                var lines = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length < 2)
                    return null;

                var dataLine = lines.LastOrDefault(l => l.Contains(AdbFileOperations.NEXTUI_BASE_PATH) || l.Contains("storage") || !l.StartsWith("Filesystem"));
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
            var commandStartTime = DateTime.Now;
            var fullCommand = $"adb {arguments}";

            // Log command start
            Logger.LogImmediate($"ADB Command Starting: {fullCommand}");

            // Fire event for real-time UI updates
            CommandExecuted?.Invoke(this, new AdbCommandLogEventArgs
            {
                Command = fullCommand,
                StartTime = commandStartTime,
                Status = AdbCommandStatus.Starting
            });

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

                var executionTime = DateTime.Now - commandStartTime;

                if (!completed)
                {
                    process.Kill();
                    var timeoutResult = new AdbResult { IsSuccess = false, Error = "Command timed out" };

                    // Log timeout
                    Logger.LogImmediate($"ADB Command Timeout: {fullCommand} (after {executionTime.TotalSeconds:F2}s)");

                    // Fire timeout event
                    CommandExecuted?.Invoke(this, new AdbCommandLogEventArgs
                    {
                        Command = fullCommand,
                        StartTime = commandStartTime,
                        EndTime = DateTime.Now,
                        ExecutionTime = executionTime,
                        Status = AdbCommandStatus.Timeout,
                        Error = "Command timed out"
                    });

                    return Task.FromResult(timeoutResult);
                }

                var output = outputBuilder.ToString().Trim();
                var error = errorBuilder.ToString().Trim();
                var isSuccess = process.ExitCode == 0;

                var result = new AdbResult
                {
                    IsSuccess = isSuccess,
                    Output = output,
                    Error = string.IsNullOrEmpty(error) ? null : error,
                    ExitCode = process.ExitCode
                };

                // Log command completion
                var logMessage = isSuccess
                    ? $"ADB Command Success: {fullCommand} (completed in {executionTime.TotalSeconds:F2}s){(!string.IsNullOrEmpty(output) ? $" - Output: {output}" : "")}"
                    : $"ADB Command Failed: {fullCommand} (failed in {executionTime.TotalSeconds:F2}s) - Exit Code: {process.ExitCode}, Error: {error}";

                Logger.LogImmediate(logMessage);

                // Fire completion event
                CommandExecuted?.Invoke(this, new AdbCommandLogEventArgs
                {
                    Command = fullCommand,
                    StartTime = commandStartTime,
                    EndTime = DateTime.Now,
                    ExecutionTime = executionTime,
                    Status = isSuccess ? AdbCommandStatus.Success : AdbCommandStatus.Failed,
                    Output = output,
                    Error = error,
                    ExitCode = process.ExitCode
                });

                return Task.FromResult(result);
            }
            catch (Exception ex)
            {
                var executionTime = DateTime.Now - commandStartTime;
                var exceptionResult = new AdbResult { IsSuccess = false, Error = ex.Message };

                // Log exception
                Logger.LogImmediate($"ADB Command Exception: {fullCommand} (failed in {executionTime.TotalSeconds:F2}s) - {ex.Message}");

                // Fire exception event
                CommandExecuted?.Invoke(this, new AdbCommandLogEventArgs
                {
                    Command = fullCommand,
                    StartTime = commandStartTime,
                    EndTime = DateTime.Now,
                    ExecutionTime = executionTime,
                    Status = AdbCommandStatus.Exception,
                    Error = ex.Message
                });

                return Task.FromResult(exceptionResult);
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

        /// <summary>
        /// Gets SHA1 hash by temporarily pulling the remote file and computing hash locally
        /// </summary>
        /// <param name="remotePath">Remote path to the file</param>
        /// <param name="deviceId">Optional device ID if multiple devices</param>
        /// <returns>SHA1 hash string or null if failed</returns>
        public async Task<string?> GetRemoteFileSha1ByPullAsync(string remotePath, string? deviceId = null)
        {
            string? tempFilePath = null;
            try
            {
                // Create temporary file
                tempFilePath = Path.Combine(Path.GetTempPath(), $"bios_hash_check_{Guid.NewGuid():N}.tmp");

                // Pull file from device
                var deviceArg = !string.IsNullOrEmpty(deviceId) ? $"-s {deviceId}" : "";
                var pullCommand = $"{deviceArg} pull \"{remotePath}\" \"{tempFilePath}\"".Trim();
                var pullResult = await ExecuteAdbCommandAsync(pullCommand);

                if (pullResult.IsSuccess && File.Exists(tempFilePath))
                {
                    // Compute hash of temporary file
                    return await GetLocalFileSha1Async(tempFilePath);
                }

                return null;
            }
            catch
            {
                return null;
            }
            finally
            {
                // Clean up temporary file
                if (!string.IsNullOrEmpty(tempFilePath) && File.Exists(tempFilePath))
                {
                    try
                    {
                        File.Delete(tempFilePath);
                    }
                    catch
                    {
                        // Ignore cleanup failures
                    }
                }
            }
        }

        /// <summary>
        /// Computes SHA1 hash for a local file
        /// </summary>
        /// <param name="filePath">Path to local file</param>
        /// <returns>SHA1 hash string or null if failed</returns>
        public static async Task<string?> GetLocalFileSha1Async(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return null;

                using var sha1 = SHA1.Create();
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
                var hashBytes = await Task.Run(() => sha1.ComputeHash(fileStream));
                return Convert.ToHexString(hashBytes).ToLowerInvariant();
            }
            catch
            {
                return null;
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

    /// <summary>
    /// Event arguments for ADB command logging
    /// </summary>
    public class AdbCommandLogEventArgs : EventArgs
    {
        public string Command { get; set; } = "";
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public TimeSpan? ExecutionTime { get; set; }
        public AdbCommandStatus Status { get; set; }
        public string? Output { get; set; }
        public string? Error { get; set; }
        public int? ExitCode { get; set; }
    }

    /// <summary>
    /// Status of an ADB command execution
    /// </summary>
    public enum AdbCommandStatus
    {
        Starting,
        Success,
        Failed,
        Timeout,
        Exception
    }
}