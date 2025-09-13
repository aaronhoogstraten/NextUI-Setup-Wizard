using System;
using System.Collections.Generic;
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
#if WINDOWS
            return await DetectWindowsPartitionScheme(drivePath);
#elif MACCATALYST
            return await DetectMacOSPartitionScheme(drivePath);
#else
            return new PartitionInfo { ErrorMessage = "Unsupported platform" };
#endif
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
                info.ErrorMessage = $"Windows detection failed: {ex}";
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
                Logger.LogImmediate("DetectMacOSPartitionScheme");

                var result = await DiskUtilMac.DetectDiskInfo(volumePath);

                info.Scheme = result.PartitionScheme == "MBR" ? PartitionScheme.MBR 
                    : result.PartitionScheme == "GPT" ? PartitionScheme.GPT
                    : result.PartitionScheme == "APM" ? PartitionScheme.APM
                    : PartitionScheme.Unknown;
                info.FileSystem = result.FileSystem;
                info.ErrorMessage= result.ErrorMessage;
                info.Details= result.Details;
                info.SizeBytes = result.SizeBytes;
                info.IsRemovable = result.IsRemovable;

                return info;
            }
            catch (Exception ex)
            {
                info.ErrorMessage = $"macOS detection failed: {ex}";
                return info;
            }
        }

        private static async Task<PartitionInfo> TryDiskutil(string volumePath, string? testVolumeInfo = null, string? testDiskInfo = null)
        {
            Logger.LogImmediate($"TryDiskutil started for volume path: {volumePath}");

            using var logger = new Logger();

            var info = new PartitionInfo();

            try
            {
                logger.Log("Getting volume info using diskutil command");
                // Get volume info using diskutil info (equivalent to diskutil info "$VOLUME")
                var volumeInfo = testVolumeInfo ?? await RunCommand("/usr/sbin/diskutil", $"info \"{volumePath}\"");
                if (!string.IsNullOrEmpty(volumeInfo))
                {
                    logger.Log("Volume info retrieved successfully, parsing data");
                    // Parse volume info and extract disk identifier
                    ParseDiskutilVolumeInfo(volumeInfo, info);
                    
                    // Extract disk part (e.g., disk2s1) from "Part of Whole" field
                    var diskPartMatch = Regex.Match(volumeInfo, @"Part of Whole:\s*(\w+)");
                    if (diskPartMatch.Success)
                    {
                        var diskPart = diskPartMatch.Groups[1].Value; // e.g., disk2s1
                        logger.Log($"Extracted disk part: {diskPart}");
                        
                        // Extract just the disk number (e.g., disk2 from disk2s1)
                        var diskMatch = Regex.Match(diskPart, @"^(disk\d+)");
                        if (diskMatch.Success)
                        {
                            var disk = diskMatch.Groups[1].Value; // e.g., disk2
                            logger.Log($"Extracted disk identifier: {disk}");
                            
                            // Get partition type - look for "Type (Bundle)" first, then "File System Personality"
                            var partitionType = "";
                            var typeBundleMatch = Regex.Match(volumeInfo, @"Type \(Bundle\):\s*(.+)");
                            if (typeBundleMatch.Success)
                            {
                                partitionType = typeBundleMatch.Groups[1].Value.Trim();
                                logger.Log($"Found Type (Bundle): {partitionType}");
                            }
                            else
                            {
                                logger.Log("Type (Bundle) not found, trying File System Personality");
                                var fsPersonalityMatch = Regex.Match(volumeInfo, @"File System Personality:\s*(.+)");
                                if (fsPersonalityMatch.Success)
                                {
                                    partitionType = fsPersonalityMatch.Groups[1].Value.Trim();
                                    logger.Log($"Found File System Personality: {partitionType}");
                                }
                                else
                                {
                                    logger.Log("No partition type found in either field");
                                }
                            }
                            
                            // Validate partition type (FAT32 = "msdos" or contains "fat32", exFAT = "exfat" or contains "exfat")
                            var isFAT32 = partitionType.Equals("msdos", StringComparison.OrdinalIgnoreCase) || 
                                         partitionType.Contains("fat32", StringComparison.OrdinalIgnoreCase);
                            var isExFAT = partitionType.Contains("exfat", StringComparison.OrdinalIgnoreCase);
                            var isValidFileSystem = isFAT32 || isExFAT;
                            logger.Log($"File system validation - isFAT32: {isFAT32}, isExFAT: {isExFAT}, isValid: {isValidFileSystem}");
                            
                            if (isValidFileSystem)
                            {
                                info.FileSystem = isFAT32 ? "FAT32" : "exFAT";
                                logger.Log($"Set file system to: {info.FileSystem}");
                            }
                            
                            logger.Log($"Getting disk info for: /dev/{disk}");
                            // Get partition scheme from the whole disk (equivalent to diskutil info "/dev/$DISK")
                            var diskInfo = testDiskInfo ?? await RunCommand("/usr/sbin/diskutil", $"info /dev/{disk}");
                            if (!string.IsNullOrEmpty(diskInfo))
                            {
                                logger.Log("Disk info retrieved, parsing partition scheme");
                                // Look for "Content (IOContent)" field for partition scheme
                                var schemeMatch = Regex.Match(diskInfo, @"Content \(IOContent\):\s*(.+)");
                                if (schemeMatch.Success)
                                {
                                    var scheme = schemeMatch.Groups[1].Value.Trim();
                                    logger.Log($"Found partition scheme: {scheme}");
                                    
                                    // Validate scheme (MBR = "FDisk_partition_scheme")
                                    if (scheme.Equals("FDisk_partition_scheme", StringComparison.OrdinalIgnoreCase))
                                    {
                                        info.Scheme = PartitionScheme.MBR;
                                        logger.Log("Identified as MBR partition scheme");
                                    }
                                    else if (scheme.Contains("GUID_partition_scheme"))
                                    {
                                        info.Scheme = PartitionScheme.GPT;
                                        logger.Log("Identified as GPT partition scheme");
                                    }
                                    else if (scheme.Contains("Apple_partition_scheme"))
                                    {
                                        info.Scheme = PartitionScheme.APM;
                                        logger.Log("Identified as APM partition scheme");
                                    }
                                    else
                                    {
                                        logger.Log($"Unknown partition scheme: {scheme}");
                                    }
                                }
                                else
                                {
                                    logger.Log("No Content (IOContent) field found in disk info");
                                }
                                
                                // Extract model information
                                var modelMatch = Regex.Match(diskInfo, @"Device / Media Name:\s*(.+)");
                                if (modelMatch.Success)
                                {
                                    info.Model = modelMatch.Groups[1].Value.Trim();
                                    logger.Log($"Extracted model: {info.Model}");
                                }
                            }
                            else
                            {
                                logger.Log("Failed to retrieve disk info");
                            }
                            
                            // Set validation result based on both file system and partition scheme
                            info.Details = $"Disk: {disk}, Partition Type: {partitionType}, Scheme: {info.Scheme}";
                            logger.Log($"Validation details: {info.Details}");
                            
                            // Mark as valid if both file system and scheme are correct
                            if (isValidFileSystem && info.Scheme == PartitionScheme.MBR)
                            {
                                info.Details += " [VALID: SD card is properly formatted]";
                                logger.Log("SD card validation PASSED - properly formatted");
                            }
                            else
                            {
                                var issues = new List<string>();
                                if (!isValidFileSystem)
                                    issues.Add($"Invalid file system: {partitionType} (expected: msdos for FAT32 or exfat for exFAT)");
                                if (info.Scheme != PartitionScheme.MBR)
                                    issues.Add($"Invalid partition scheme: {info.Scheme} (expected: MBR/FDisk_partition_scheme)");
                                
                                info.ErrorMessage = $"SD card validation failed: {string.Join(", ", issues)}";
                                logger.Log($"SD card validation FAILED: {info.ErrorMessage}");
                            }
                        }
                        else
                        {
                            logger.Log("Failed to extract disk number from disk part");
                        }
                    }
                    else
                    {
                        logger.Log("Failed to find 'Part of Whole' field in volume info");
                    }
                }
                else
                {
                    logger.Log("Failed to retrieve volume info or volume info was empty");
                }
            }
            catch (Exception ex)
            {
                info.ErrorMessage = $"diskutil command failed: {ex.Message}";
                logger.Log($"Exception occurred: {ex.Message}");
            }

            logger.Log($"TryDiskutil completed with result - FileSystem: {info.FileSystem}, Scheme: {info.Scheme}, Error: {info.ErrorMessage}");
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

        /// <summary>
        /// Test method using real-world diskutil output data
        /// </summary>
        public static async Task TestRealWorldDiskutilAsync()
        {
            Console.WriteLine("Testing real-world diskutil output parsing...");

            var testVolumeInfo = @"      Device Identifier:         disk6s1
   Device Node:               /dev/disk6s1
   Whole:                     No
   Part of Whole:             disk6

   Volume Name:               SETUPWIZ
   Mounted:                   Yes
   Mount Point:               /Volumes/SETUPWIZ

   Partition Type:            Windows_NTFS
   File System Personality:   ExFAT
   Type (Bundle):             exfat
   Name (User Visible):       ExFAT

   OS Can Be Installed:       No
   Media Type:                Generic
   Protocol:                  USB
   SMART Status:              Not Supported
   Volume UUID:               E3E8A367-42AD-3283-A77E-5CF409ED1742
   Partition Offset:          1048576 Bytes (2048 512-Byte-Device-Blocks)

   Disk Size:                 62.5 GB (62533926912 Bytes) (exactly 122136576 512-Byte-Units)
   Device Block Size:         512 Bytes

   Volume Total Space:        62.5 GB (62530912256 Bytes) (exactly 122130688 512-Byte-Units)
   Volume Used Space:         13.2 MB (13238272 Bytes) (exactly 25856 512-Byte-Units) (0.0%)
   Volume Free Space:         62.5 GB (62517673984 Bytes) (exactly 122104832 512-Byte-Units) (100.0%)
   Allocation Block Size:     512 Bytes

   Media OS Use Only:         No
   Media Read-Only:           No
   Volume Read-Only:          No

   Device Location:           External
   Removable Media:           Removable
   Media Removal:             Software-Activated

   Solid State:               Info not available";

            var testDiskInfo = @"Content (IOContent): FDisk_partition_scheme";

            var result = await TryDiskutil("/Volumes/SETUPWIZ", testVolumeInfo, testDiskInfo);

            Console.WriteLine($"Result:");
            Console.WriteLine($"  File System: {result.FileSystem}");
            Console.WriteLine($"  Scheme: {result.Scheme}");
            Console.WriteLine($"  Error: {result.ErrorMessage}");
            Console.WriteLine($"  Details: {result.Details}");

            var isValid = result.FileSystem == "exFAT" && result.Scheme == PartitionScheme.MBR && string.IsNullOrEmpty(result.ErrorMessage);
            Console.WriteLine($"  Valid SD Card: {isValid}");

            if (isValid)
            {
                Console.WriteLine("  ✅ Test PASSED - SD card is properly formatted (exFAT + MBR)");
            }
            else
            {
                Console.WriteLine("  ❌ Test FAILED");
            }
        }
    }
}