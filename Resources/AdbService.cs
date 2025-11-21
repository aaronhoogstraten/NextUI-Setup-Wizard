using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace NextUI_Setup_Wizard.Resources
{
    /// <summary>
    /// Service for executing ADB commands and managing device connections
    /// </summary>
    public partial class AdbService
    {
        private readonly string _adbExecutablePath;
        private readonly int _defaultTimeoutMs = 30000; // 30 seconds

        /// <summary>
        /// Regex source generator for stripping ANSI color codes
        /// </summary>
        [GeneratedRegex(@"\x1B\[[0-9;]*[a-zA-Z]")]
        private static partial Regex AnsiCodesRegex();

        /// <summary>
        /// Regex source generator for validating MD5 hash format
        /// </summary>
        [GeneratedRegex("^[0-9a-f]{32}$")]
        private static partial Regex Md5HashRegex();

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
            catch (Exception ex)
            {
                Logger.LogImmediate($"ADB availability check failed: {ex.Message}");
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
        /// Pulls a file from the device
        /// </summary>
        /// <param name="remotePath">Remote file path on device</param>
        /// <param name="localPath">Local destination path</param>
        /// <param name="deviceId">Optional device ID if multiple devices</param>
        /// <param name="progress">Optional progress callback</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public async Task<AdbResult> PullFileAsync(
            string remotePath,
            string localPath,
            string? deviceId = null,
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var fileName = Path.GetFileName(remotePath);
                progress?.Report($"Pulling {fileName} from device...");

                var deviceArg = !string.IsNullOrEmpty(deviceId) ? $"-s {deviceId}" : "";
                var command = $"{deviceArg} pull \"{remotePath}\" \"{localPath}\"".Trim();

                var result = await ExecuteAdbCommandAsync(command, progress, cancellationToken);

                if (result.IsSuccess)
                {
                    progress?.Report($"Successfully pulled {fileName}");
                }
                else
                {
                    progress?.Report($"Failed to pull {fileName}: {result.Error}");
                }

                return result;
            }
            catch (Exception ex)
            {
                return new AdbResult { IsSuccess = false, Error = ex.Message };
            }
        }

        /// <summary>
        /// Recursively pulls a directory from the device
        /// </summary>
        /// <param name="remotePath">Remote directory path on device</param>
        /// <param name="localPath">Local destination directory</param>
        /// <param name="deviceId">Optional device ID if multiple devices</param>
        /// <param name="progress">Optional progress callback</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public async Task<AdbResult> PullDirectoryAsync(
            string remotePath,
            string localPath,
            string? deviceId = null,
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Ensure local directory exists
                Directory.CreateDirectory(localPath);

                progress?.Report($"Pulling directory {remotePath}...");

                var deviceArg = !string.IsNullOrEmpty(deviceId) ? $"-s {deviceId}" : "";
                var command = $"{deviceArg} pull \"{remotePath}\" \"{localPath}\"".Trim();

                var result = await ExecuteAdbCommandAsync(command, progress, cancellationToken, timeout: 120000); // 2 minute timeout for directories

                if (result.IsSuccess)
                {
                    progress?.Report($"Successfully pulled directory {remotePath}");
                }
                else
                {
                    progress?.Report($"Failed to pull directory: {result.Error}");
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
        /// <param name="directoriesOnly">If true, only list directories (not files)</param>
        public async Task<List<string>> ListFilesAsync(string remotePath, string? deviceId = null, bool directoriesOnly = false)
        {
            var files = new List<string>();

            try
            {
                var deviceArg = !string.IsNullOrEmpty(deviceId) ? $"-s {deviceId}" : "";

                // Use ls with -1 to force one entry per line and --color=never to prevent ANSI color codes
                // If directoriesOnly is true, use -d */ to list only directories
                var listCommand = directoriesOnly ? "ls -1 -d --color=never */" : "ls -1 --color=never";
                var escapedPath = EscapeShellArgument(remotePath);
                var command = $"{deviceArg} shell \"cd {escapedPath} && {listCommand}\"".Trim();

                var result = await ExecuteAdbCommandAsync(command);

                var output = result.Output;
                var error = result.Error ?? "";

                // Check for common error messages in both output and error streams
                var combinedOutput = output + " " + error;
                if (combinedOutput.Contains("No such file or directory") ||
                    combinedOutput.Contains("cannot access") ||
                    combinedOutput.Contains("not found") ||
                    combinedOutput.Contains("can't cd to") ||
                    combinedOutput.Contains("does not exist"))
                {
                    return files; // Return empty list
                }

                if (result.IsSuccess && !string.IsNullOrEmpty(output))
                {
                    var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    files.AddRange(lines.Select(line => {
                        var cleaned = StripAnsiCodes(line.Trim());
                        // Remove trailing slash if present (from directory listing)
                        return cleaned.TrimEnd('/');
                    }).Where(line => !string.IsNullOrEmpty(line) &&
                                     !line.Contains("can't cd") &&
                                     !line.Contains("No such file")));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to list files: {ex.Message}");
            }

            return files;
        }

        /// <summary>
        /// Strips ANSI color codes and escape sequences from a string
        /// </summary>
        private static string StripAnsiCodes(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            // Remove ANSI escape sequences (color codes, cursor movements, etc.)
            // Uses compiled regex source generator for better performance
            return AnsiCodesRegex().Replace(input, string.Empty);
        }

        /// <summary>
        /// Escapes a string for safe use in shell commands.
        /// Uses single quotes and escapes any single quotes within the string.
        /// This prevents shell injection attacks.
        /// </summary>
        /// <param name="input">The string to escape</param>
        /// <returns>A shell-safe escaped string</returns>
        private static string EscapeShellArgument(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "''";

            // Replace single quotes with '\'' (end quote, escaped quote, start quote)
            // Then wrap the entire string in single quotes
            // This is the safest method for POSIX shell escaping
            return "'" + input.Replace("'", "'\\''") + "'";
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
                // Properly escape the path for shell to prevent injection attacks
                var escapedPath = EscapeShellArgument(remotePath);
                var command = $"{deviceArg} shell test -e {escapedPath} && echo \"EXISTS\" || echo \"NOT_EXISTS\"".Trim();

                var result = await ExecuteAdbCommandAsync(command);
                return result.IsSuccess && result.Output.Contains("EXISTS");
            }
            catch (Exception ex)
            {
                Logger.LogImmediate($"PathExists check failed for '{remotePath}': {ex.Message}");
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

                // Find the data line (not the header)
                var dataLine = lines.LastOrDefault(l => l != null && (l.Contains(AdbFileOperations.NEXTUI_BASE_PATH) || l.Contains("storage") || !l.StartsWith("Filesystem")));
                if (string.IsNullOrEmpty(dataLine))
                    return null;

                var parts = dataLine.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts == null || parts.Length < 4)
                    return null;

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
        private async Task<AdbResult> ExecuteAdbCommandAsync(
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

                // Use async wait with cancellation token for proper async/await pattern
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(timeoutMs);

                bool completed;
                try
                {
                    await process.WaitForExitAsync(cts.Token);
                    completed = true;
                }
                catch (OperationCanceledException)
                {
                    completed = false;
                }

                var executionTime = DateTime.Now - commandStartTime;

                if (!completed)
                {
                    try
                    {
                        process.Kill();

                        // Wait for process cleanup after kill (max 1 second)
                        // This ensures proper resource disposal
                        using var cleanupCts = new CancellationTokenSource(1000);
                        try
                        {
                            await process.WaitForExitAsync(cleanupCts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            // Process didn't exit cleanly within timeout
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        // Process already exited
                    }

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

                    return timeoutResult;
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

                return result;
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

                return exceptionResult;
            }
        }

        /// <summary>
        /// Parses device properties from ADB devices output
        /// </summary>
        private static void ParseDeviceProperties(AdbDevice device, string properties)
        {
            if (string.IsNullOrEmpty(properties))
                return;

            var pairs = properties.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (pairs == null)
                return;

            foreach (var pair in pairs)
            {
                if (string.IsNullOrEmpty(pair))
                    continue;

                var colonIndex = pair.IndexOf(':');
                // Ensure there's content before and after the colon
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
        /// Gets MD5 hash of a remote file on the device.
        /// First attempts to use md5sum command on device (via BusyBox).
        /// Falls back to pulling the file and computing hash locally if md5sum fails.
        /// </summary>
        /// <param name="remotePath">Remote path to the file</param>
        /// <param name="deviceId">Optional device ID if multiple devices</param>
        /// <returns>MD5 hash string or null if failed</returns>
        public async Task<string?> GetRemoteFileMd5Async(string remotePath, string? deviceId = null)
        {
            var deviceArg = !string.IsNullOrEmpty(deviceId) ? $"-s {deviceId}" : "";

            // Try to use busybox md5sum on the device
            try
            {
                var escapedPath = EscapeShellArgument(remotePath);
                var md5Command = $"{deviceArg} shell \"md5sum {escapedPath}\"".Trim();
                var md5Result = await ExecuteAdbCommandAsync(md5Command, timeout: 10000);

                if (md5Result.IsSuccess && !string.IsNullOrEmpty(md5Result.Output))
                {
                    // md5sum output format is typically: "hash  filename"
                    // Extract the hash (first part before whitespace)
                    var parts = md5Result.Output.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts != null && parts.Length > 0)
                    {
                        var hash = parts[0].Trim().ToLowerInvariant();
                        // Validate that it looks like an MD5 hash (32 hex characters)
                        // Uses compiled regex source generator for better performance
                        if (hash.Length == 32 && Md5HashRegex().IsMatch(hash))
                        {
                            return hash;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // If busybox md5sum fails, fall through to the pull method
                Logger.LogImmediate($"Device-side MD5 calculation failed for '{remotePath}', falling back to pull method: {ex.Message}");
            }

            // Fallback: Pull file and compute hash locally
            string? tempFilePath = null;
            try
            {
                // Create temporary file
                tempFilePath = Path.Combine(Path.GetTempPath(), $"bios_hash_check_{Guid.NewGuid():N}.tmp");

                // Pull file from device
                var pullCommand = $"{deviceArg} pull \"{remotePath}\" \"{tempFilePath}\"".Trim();
                var pullResult = await ExecuteAdbCommandAsync(pullCommand);

                if (pullResult.IsSuccess && File.Exists(tempFilePath))
                {
                    // Compute hash of temporary file
                    return await GetLocalFileMd5Async(tempFilePath);
                }

                return null;
            }
            catch (Exception ex)
            {
                Logger.LogImmediate($"Failed to pull file for MD5 hash calculation '{remotePath}': {ex.Message}");
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
                    catch (Exception ex)
                    {
                        // Log but don't fail on cleanup errors
                        Logger.LogImmediate($"Failed to delete temporary file '{tempFilePath}': {ex.Message}");
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
                // Use async hash computation for proper async/await pattern
                var hashBytes = await sha1.ComputeHashAsync(fileStream);
                return Convert.ToHexString(hashBytes).ToLowerInvariant();
            }
            catch (Exception ex)
            {
                Logger.LogImmediate($"Failed to compute SHA1 hash for '{filePath}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Computes MD5 hash for a local file
        /// </summary>
        /// <param name="filePath">Path to local file</param>
        /// <returns>MD5 hash string or null if failed</returns>
        public static async Task<string?> GetLocalFileMd5Async(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return null;

                using var md5 = System.Security.Cryptography.MD5.Create();
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
                // Use async hash computation for proper async/await pattern
                var hashBytes = await md5.ComputeHashAsync(fileStream);
                return Convert.ToHexString(hashBytes).ToLowerInvariant();
            }
            catch (Exception ex)
            {
                Logger.LogImmediate($"Failed to compute MD5 hash for '{filePath}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets SHA1 hash of a remote file on the device by pulling it locally.
        /// This method always pulls the file and computes hash locally.
        /// </summary>
        /// <param name="remotePath">Remote path to the file</param>
        /// <param name="deviceId">Optional device ID if multiple devices</param>
        /// <returns>SHA1 hash string or null if failed</returns>
        public async Task<string?> GetRemoteFileSha1ByPullAsync(string remotePath, string? deviceId = null)
        {
            var deviceArg = !string.IsNullOrEmpty(deviceId) ? $"-s {deviceId}" : "";
            string? tempFilePath = null;
            try
            {
                // Create temporary file
                tempFilePath = Path.Combine(Path.GetTempPath(), $"bios_hash_check_{Guid.NewGuid():N}.tmp");

                // Pull file from device
                var pullCommand = $"{deviceArg} pull \"{remotePath}\" \"{tempFilePath}\"".Trim();
                var pullResult = await ExecuteAdbCommandAsync(pullCommand);

                if (pullResult.IsSuccess && File.Exists(tempFilePath))
                {
                    // Compute hash of temporary file
                    return await GetLocalFileSha1Async(tempFilePath);
                }

                return null;
            }
            catch (Exception ex)
            {
                Logger.LogImmediate($"Failed to pull file for SHA1 hash calculation '{remotePath}': {ex.Message}");
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
                    catch (Exception ex)
                    {
                        // Log but don't fail on cleanup errors
                        Logger.LogImmediate($"Failed to delete temporary file '{tempFilePath}': {ex.Message}");
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