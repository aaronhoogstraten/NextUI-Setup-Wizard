using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;


namespace NextUI_Setup_Wizard.Resources
{
    public static class PartitionSchemeDetector
    {
        public enum PartitionScheme
        {
            Unknown,
            MBR,
            GPT,
            APM,
            Unpartitioned
        }

        public class PartitionInfo
        {
            public PartitionScheme Scheme { get; set; } = PartitionScheme.Unknown;
            public string FileSystem { get; set; } = "";
            public string Model { get; set; } = "";
            public long SizeBytes { get; set; }
            public bool IsRemovable { get; set; }
            public string Details { get; set; } = "";
            public string ErrorMessage { get; set; } = "";
        }

        /// <summary>
        /// Detects partition scheme using command line tools that don't require admin privileges
        /// </summary>
        public static async Task<PartitionInfo> DetectPartitionScheme(string drivePath)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return await DetectWindowsPartitionScheme(drivePath);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return await DetectMacOSPartitionScheme(drivePath);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return await DetectLinuxPartitionScheme(drivePath);
            }
            else
            {
                return new PartitionInfo { ErrorMessage = "Unsupported platform" };
            }
        }

        // ==========================================
        // Windows Implementation (No Admin Required)
        // ==========================================

        private static async Task<PartitionInfo> DetectWindowsPartitionScheme(string drivePath)
        {
            var info = new PartitionInfo();

            try
            {
                var driveLetter = Path.GetPathRoot(drivePath)?.TrimEnd('\\', '/') ?? "";

                // Method 1: Try PowerShell with Get-Disk (Windows 8+)
                var psResult = await TryPowerShellGetDisk(driveLetter);
                if (psResult.Scheme != PartitionScheme.Unknown)
                {
                    return psResult;
                }

                // Method 2: Use WMI through PowerShell (more compatible)
                var wmiResult = await TryPowerShellWMI(driveLetter);
                if (wmiResult.Scheme != PartitionScheme.Unknown)
                {
                    return wmiResult;
                }

                // Method 3: Use wmic command (legacy but widely available)
                var wmicResult = await TryWMIC(driveLetter);
                return wmicResult;
            }
            catch (Exception ex)
            {
                info.ErrorMessage = $"Windows detection failed: {ex.Message}";
                return info;
            }
        }

        private static async Task<PartitionInfo> TryPowerShellGetDisk(string driveLetter)
        {
            var info = new PartitionInfo();

            try
            {
                var psScript = $@"
    try {{
        $partition = Get-Partition -DriveLetter {driveLetter} -ErrorAction SilentlyContinue
        if ($partition) {{
            $disk = Get-Disk -Number $partition.DiskNumber -ErrorAction SilentlyContinue
            if ($disk) {{
                Write-Output ""PartitionStyle: $($disk.PartitionStyle)""
                Write-Output ""Model: $($disk.Model)""
                Write-Output ""Size: $($disk.Size)""
                Write-Output ""BusType: $($disk.BusType)""
                Write-Output ""OperationalStatus: $($disk.OperationalStatus)""
            }}
        }}
    }} catch {{
        Write-Output ""PowerShell Get-Disk not available""
    }}
    ";

                var result = await RunPowerShell(psScript);
                if (!string.IsNullOrEmpty(result) && !result.Contains("not available"))
                {
                    return ParsePowerShellOutput(result, info);
                }
            }
            catch
            {
                // Ignore errors, try next method
            }

            return info;
        }

        private static async Task<PartitionInfo> TryPowerShellWMI(string driveLetter)
        {
            var info = new PartitionInfo();

            try
            {
                var psScript = $@"
    try {{
        # Get logical disk
        $logicalDisk = Get-WmiObject -Class Win32_LogicalDisk -Filter ""DeviceID='{driveLetter}:'"" -ErrorAction SilentlyContinue
        if ($logicalDisk) {{
            Write-Output ""DriveType: $($logicalDisk.DriveType)""
            Write-Output ""FileSystem: $($logicalDisk.FileSystem)""
            Write-Output ""Size: $($logicalDisk.Size)""
        
            # Get physical disk through partition
            $partition = Get-WmiObject -Class Win32_LogicalDiskToPartition | Where-Object {{ $_.Dependent -match '{driveLetter}:' }}
            if ($partition) {{
                $diskPartition = Get-WmiObject -Class Win32_DiskPartition | Where-Object {{ $_.DeviceID -eq $partition.Antecedent.Split('""')[1] }}
                if ($diskPartition) {{
                    $diskToDisk = Get-WmiObject -Class Win32_DiskPartitionToDisk | Where-Object {{ $_.Dependent -match $diskPartition.DeviceID }}
                    if ($diskToDisk) {{
                        $physicalDisk = Get-WmiObject -Class Win32_DiskDrive | Where-Object {{ $_.DeviceID -eq $diskToDisk.Antecedent.Split('""')[1] }}
                        if ($physicalDisk) {{
                            Write-Output ""Model: $($physicalDisk.Model)""
                            Write-Output ""MediaType: $($physicalDisk.MediaType)""
                            Write-Output ""InterfaceType: $($physicalDisk.InterfaceType)""
                        
                            # Try to determine partition style from partition type
                            if ($diskPartition.Type -eq 'GPT: Basic data partition') {{
                                Write-Output ""PartitionStyle: GPT""
                            }} elseif ($diskPartition.Type -like '*EFI*') {{
                                Write-Output ""PartitionStyle: GPT""
                            }} else {{
                                Write-Output ""PartitionStyle: MBR""
                            }}
                        }}
                    }}
                }}
            }}
        }}
    }} catch {{
        Write-Output ""WMI query failed: $($_.Exception.Message)""
    }}
    ";

                var result = await RunPowerShell(psScript);
                if (!string.IsNullOrEmpty(result) && !result.Contains("failed:"))
                {
                    return ParsePowerShellWMIOutput(result, info);
                }
            }
            catch
            {
                // Ignore errors, try next method
            }

            return info;
        }

        private static async Task<PartitionInfo> TryWMIC(string driveLetter)
        {
            var info = new PartitionInfo();

            try
            {
                // Get logical disk info
                var logicalDiskCmd = $"wmic logicaldisk where \"DeviceID='{driveLetter}:'\" get Size,FileSystem,DriveType /format:csv";
                var logicalResult = await RunCommand("cmd", $"/c {logicalDiskCmd}");

                if (!string.IsNullOrEmpty(logicalResult))
                {
                    ParseWMICLogicalDisk(logicalResult, info);
                }

                // Get physical disk info (more complex with wmic)
                var diskDriveCmd = "wmic diskdrive get Model,Size,InterfaceType,MediaType /format:csv";
                var diskResult = await RunCommand("cmd", $"/c {diskDriveCmd}");

                if (!string.IsNullOrEmpty(diskResult))
                {
                    ParseWMICDiskDrive(diskResult, info);
                }

                info.Details = "Used WMIC (limited partition scheme detection)";
            }
            catch (Exception ex)
            {
                info.ErrorMessage = $"WMIC failed: {ex.Message}";
            }

            return info;
        }

        // ==========================================
        // macOS Implementation (No Admin Required)
        // ==========================================

        private static async Task<PartitionInfo> DetectMacOSPartitionScheme(string volumePath)
        {
            var info = new PartitionInfo();

            try
            {
                // Method 1: diskutil info (most reliable)
                var diskutilResult = await TryDiskutil(volumePath);
                if (diskutilResult.Scheme != PartitionScheme.Unknown)
                {
                    return diskutilResult;
                }

                // Method 2: df + system_profiler
                var alternativeResult = await TryMacOSAlternative(volumePath);
                return alternativeResult;
            }
            catch (Exception ex)
            {
                info.ErrorMessage = $"macOS detection failed: {ex.Message}";
                return info;
            }
        }

        private static async Task<PartitionInfo> TryDiskutil(string volumePath)
        {
            var info = new PartitionInfo();

            try
            {
                // Get volume info
                var volumeInfo = await RunCommand("/usr/sbin/diskutil", $"info \"{volumePath}\"");
                if (!string.IsNullOrEmpty(volumeInfo))
                {
                    ParseDiskutilVolumeInfo(volumeInfo, info);
                }

                // Get parent disk info for partition scheme
                if (!string.IsNullOrEmpty(info.Details))
                {
                    var bsdNameMatch = Regex.Match(info.Details, @"Device Identifier:\s*(\w+)");
                    if (bsdNameMatch.Success)
                    {
                        var bsdName = bsdNameMatch.Groups[1].Value;
                        var diskName = Regex.Replace(bsdName, @"s\d+$", ""); // Remove partition number

                        var diskInfo = await RunCommand("/usr/sbin/diskutil", $"info {diskName}");
                        if (!string.IsNullOrEmpty(diskInfo))
                        {
                            ParseDiskutilDiskInfo(diskInfo, info);
                        }
                    }
                }
            }
            catch
            {
                // Ignore errors, try alternative method
            }

            return info;
        }

        private static async Task<PartitionInfo> TryMacOSAlternative(string volumePath)
        {
            var info = new PartitionInfo();

            try
            {
                // Use df to get basic info
                var dfInfo = await RunCommand("/bin/df", $"-h \"{volumePath}\"");
                if (!string.IsNullOrEmpty(dfInfo))
                {
                    ParseDFOutput(dfInfo, info);
                }

                // Use system_profiler for hardware info
                var storageInfo = await RunCommand("/usr/sbin/system_profiler", "SPStorageDataType");
                if (!string.IsNullOrEmpty(storageInfo))
                {
                    ParseSystemProfilerOutput(storageInfo, info, volumePath);
                }
            }
            catch (Exception ex)
            {
                info.ErrorMessage = $"macOS alternative method failed: {ex.Message}";
            }

            return info;
        }

        // ==========================================
        // Linux Implementation (No Admin Required)
        // ==========================================

        private static async Task<PartitionInfo> DetectLinuxPartitionScheme(string mountPath)
        {
            var info = new PartitionInfo();

            try
            {
                // Method 1: lsblk (most modern)
                var lsblkResult = await TryLsblk(mountPath);
                if (lsblkResult.Scheme != PartitionScheme.Unknown)
                {
                    return lsblkResult;
                }

                // Method 2: /proc/partitions + /sys filesystem
                var procResult = await TryProcPartitions(mountPath);
                return procResult;
            }
            catch (Exception ex)
            {
                info.ErrorMessage = $"Linux detection failed: {ex.Message}";
                return info;
            }
        }

        private static async Task<PartitionInfo> TryLsblk(string mountPath)
        {
            var info = new PartitionInfo();

            try
            {
                var result = await RunCommand("/bin/lsblk", $"-f -o NAME,SIZE,FSTYPE,MOUNTPOINT,PTTYPE");
                if (!string.IsNullOrEmpty(result))
                {
                    ParseLsblkOutput(result, info, mountPath);
                }
            }
            catch
            {
                // lsblk might not be available
            }

            return info;
        }

        private static async Task<PartitionInfo> TryProcPartitions(string mountPath)
        {
            var info = new PartitionInfo();

            try
            {
                // Use df to find device
                var dfResult = await RunCommand("/bin/df", mountPath);
                if (!string.IsNullOrEmpty(dfResult))
                {
                    ParseDFOutput(dfResult, info);
                }

                // Read /proc/partitions for additional info
                if (File.Exists("/proc/partitions"))
                {
                    var partitions = await File.ReadAllTextAsync("/proc/partitions");
                    // Parse partition information
                    info.Details += $" | /proc/partitions available";
                }
            }
            catch (Exception ex)
            {
                info.ErrorMessage = $"Linux /proc method failed: {ex.Message}";
            }

            return info;
        }

        // ==========================================
        // Helper Methods
        // ==========================================

        private static async Task<string> RunCommand(string command, string arguments)
        {
#if IOS
            return "Command execution not supported on iOS";
#else
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    var output = await process.StandardOutput.ReadToEndAsync();
                    var error = await process.StandardError.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    return process.ExitCode == 0 ? output : $"Error: {error}";
                }
            }
            catch (Exception ex)
            {
                return $"Command failed: {ex.Message}";
            }

            return "";
#endif
        }

        private static async Task<string> RunPowerShell(string script)
        {
            return await RunCommand("powershell", $"-Command \"{script.Replace("\"", "\\\"")}\"");
        }

        // ==========================================
        // Output Parsers
        // ==========================================

        private static PartitionInfo ParsePowerShellOutput(string output, PartitionInfo info)
        {
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("PartitionStyle:"))
                {
                    var style = trimmed.Substring(15).Trim();
                    info.Scheme = style.ToUpperInvariant() switch
                    {
                        "GPT" => PartitionScheme.GPT,
                        "MBR" => PartitionScheme.MBR,
                        "RAW" => PartitionScheme.Unpartitioned,
                        _ => PartitionScheme.Unknown
                    };
                }
                else if (trimmed.StartsWith("Model:"))
                {
                    info.Model = trimmed.Substring(6).Trim();
                }
                else if (trimmed.StartsWith("Size:"))
                {
                    if (long.TryParse(trimmed.Substring(5).Trim(), out long size))
                    {
                        info.SizeBytes = size;
                    }
                }
                else if (trimmed.StartsWith("BusType:"))
                {
                    var busType = trimmed.Substring(8).Trim();
                    info.IsRemovable = busType.Contains("USB", StringComparison.OrdinalIgnoreCase);
                }
            }

            info.Details = "PowerShell Get-Disk";
            return info;
        }

        private static PartitionInfo ParsePowerShellWMIOutput(string output, PartitionInfo info)
        {
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("PartitionStyle:"))
                {
                    var style = trimmed.Substring(15).Trim();
                    info.Scheme = style.ToUpperInvariant() switch
                    {
                        "GPT" => PartitionScheme.GPT,
                        "MBR" => PartitionScheme.MBR,
                        _ => PartitionScheme.Unknown
                    };
                }
                else if (trimmed.StartsWith("DriveType:"))
                {
                    var driveType = trimmed.Substring(10).Trim();
                    info.IsRemovable = driveType == "2"; // Removable disk
                }
                else if (trimmed.StartsWith("FileSystem:"))
                {
                    info.FileSystem = trimmed.Substring(11).Trim();
                }
                else if (trimmed.StartsWith("Model:"))
                {
                    info.Model = trimmed.Substring(6).Trim();
                }
            }

            info.Details = "PowerShell WMI";
            return info;
        }

        private static void ParseWMICLogicalDisk(string output, PartitionInfo info)
        {
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line.Contains(",") && !line.Contains("Node"))
                {
                    var parts = line.Split(',');
                    if (parts.Length >= 4)
                    {
                        info.FileSystem = parts[2]?.Trim() ?? "";
                        if (long.TryParse(parts[3]?.Trim(), out long size))
                        {
                            info.SizeBytes = size;
                        }
                        if (int.TryParse(parts[1]?.Trim(), out int driveType))
                        {
                            info.IsRemovable = driveType == 2;
                        }
                    }
                }
            }
        }

        private static void ParseWMICDiskDrive(string output, PartitionInfo info)
        {
            // Basic parsing of disk drive info from WMIC
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line.Contains("USB") || line.Contains("Removable"))
                {
                    info.IsRemovable = true;
                    info.Model = line; // Simplified
                    break;
                }
            }
        }

        private static void ParseDiskutilVolumeInfo(string output, PartitionInfo info)
        {
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.Contains("File System Personality:"))
                {
                    info.FileSystem = trimmed.Split(':')[1].Trim();
                }
                else if (trimmed.Contains("Total Size:"))
                {
                    var sizeMatch = Regex.Match(trimmed, @"\((\d+)\s+Bytes\)");
                    if (sizeMatch.Success && long.TryParse(sizeMatch.Groups[1].Value, out long size))
                    {
                        info.SizeBytes = size;
                    }
                }
                else if (trimmed.Contains("Removable Media:"))
                {
                    info.IsRemovable = trimmed.Contains("Yes");
                }
            }
            info.Details = output;
        }

        private static void ParseDiskutilDiskInfo(string output, PartitionInfo info)
        {
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.Contains("Content (IOContent):"))
                {
                    if (trimmed.Contains("GUID_partition_scheme"))
                        info.Scheme = PartitionScheme.GPT;
                    else if (trimmed.Contains("FDisk_partition_scheme"))
                        info.Scheme = PartitionScheme.MBR;
                    else if (trimmed.Contains("Apple_partition_scheme"))
                        info.Scheme = PartitionScheme.APM;
                }
                else if (trimmed.Contains("Device / Media Name:"))
                {
                    info.Model = trimmed.Split(':')[1].Trim();
                }
            }
        }

        private static void ParseDFOutput(string output, PartitionInfo info)
        {
            // Basic df parsing for filesystem and device info
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length > 1)
            {
                var parts = lines[1].Split(new char[0], StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0)
                {
                    info.Details += $"Device: {parts[0]}";
                }
            }
        }

        private static void ParseSystemProfilerOutput(string output, PartitionInfo info, string volumePath)
        {
            // Parse system_profiler storage information
            // This would contain detailed storage device info
            info.Details += " | System Profiler data available";
        }

        private static void ParseLsblkOutput(string output, PartitionInfo info, string mountPath)
        {
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line.Contains(mountPath))
                {
                    var parts = line.Split(new char[0], StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 4)
                    {
                        var pttype = parts[4]; // Partition table type
                        info.Scheme = pttype.ToLowerInvariant() switch
                        {
                            "gpt" => PartitionScheme.GPT,
                            "dos" => PartitionScheme.MBR,
                            _ => PartitionScheme.Unknown
                        };
                    }
                }
            }
        }
    }
}