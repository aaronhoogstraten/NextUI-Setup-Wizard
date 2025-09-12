using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NextUI_Setup_Wizard.Resources
{
    public static class DiskUtilMac
    {
        public class DiskInfo
        {
            public string DiskIdentifier { get; set; } = "";
            public string PartitionType { get; set; } = "";
            public string PartitionScheme { get; set; } = "";
            public string FileSystem { get; set; } = "";
            public string VolumeLabel { get; set; } = "";
            public long SizeBytes { get; set; }
            public bool IsRemovable { get; set; }
            public string ErrorMessage { get; set; } = "";
            public string Details { get; set; } = "";
        }

        /// <summary>
        /// Detects disk information for the given volume path using diskutil (macOS only)
        /// </summary>
        /// <param name="volumePath">Path to the volume (e.g., "/Volumes/SETUPWIZ")</param>
        /// <returns>DiskInfo containing partition and disk details</returns>
        public static async Task<DiskInfo> DetectDiskInfo(string volumePath)
        {
            var info = new DiskInfo();

            try
            {
                // Get volume information using diskutil info
                var volumeInfo = await RunDiskUtilInfo(volumePath);
                if (string.IsNullOrEmpty(volumeInfo))
                {
                    info.ErrorMessage = "Failed to get volume information from diskutil";
                    return info;
                }

                // Parse volume information
                ParseVolumeInfo(volumeInfo, info);

                // If we found a disk identifier, get the parent disk information
                if (!string.IsNullOrEmpty(info.DiskIdentifier))
                {
                    // Extract disk number (e.g., "disk2" from "disk2s1")
                    var diskMatch = Regex.Match(info.DiskIdentifier, @"(disk\d+)");
                    if (diskMatch.Success)
                    {
                        var diskIdentifier = diskMatch.Groups[1].Value;
                        var diskInfo = await RunDiskUtilInfo($"/dev/{diskIdentifier}");

                        if (!string.IsNullOrEmpty(diskInfo))
                        {
                            ParseDiskInfo(diskInfo, info);
                        }
                    }
                }

                info.Details = volumeInfo;
            }
            catch (Exception ex)
            {
                info.ErrorMessage = $"Error detecting disk info: {ex.Message}";
            }

            return info;
        }

        /// <summary>
        /// Run diskutil info command and return output
        /// </summary>
        private static async Task<string> RunDiskUtilInfo(string target)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "/usr/sbin/diskutil",
                    Arguments = $"info \"{target}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                    return "";

                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    return $"Error: {error}";
                }

                return output;
            }
            catch (Exception ex)
            {
                return $"Command failed: {ex.Message}";
            }
        }

        /// <summary>
        /// Parse volume information from diskutil info output
        /// </summary>
        private static void ParseVolumeInfo(string output, DiskInfo info)
        {
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                // Extract device identifier (Part of Whole)
                if (trimmed.Contains("Part of Whole:"))
                {
                    var match = Regex.Match(trimmed, @"Part of Whole:\s*(.+)");
                    if (match.Success)
                    {
                        info.DiskIdentifier = match.Groups[1].Value.Trim();
                    }
                }
                // Extract volume name
                else if (trimmed.Contains("Volume Name:"))
                {
                    var parts = trimmed.Split(':', 2);
                    if (parts.Length > 1)
                    {
                        info.VolumeLabel = parts[1].Trim();
                    }
                }
                // Extract file system
                else if (trimmed.Contains("File System Personality:"))
                {
                    var parts = trimmed.Split(':', 2);
                    if (parts.Length > 1)
                    {
                        info.FileSystem = parts[1].Trim();
                    }
                }
                // Extract partition type from Type (Bundle)
                else if (trimmed.Contains("Type (Bundle):"))
                {
                    var parts = trimmed.Split(':', 2);
                    if (parts.Length > 1)
                    {
                        info.PartitionType = parts[1].Trim();
                    }
                }
                // Extract size
                else if (trimmed.Contains("Disk Size:") || trimmed.Contains("Total Size:"))
                {
                    var sizeMatch = Regex.Match(trimmed, @"\((\d+)\s+Bytes\)");
                    if (sizeMatch.Success && long.TryParse(sizeMatch.Groups[1].Value, out long size))
                    {
                        info.SizeBytes = size;
                    }
                }
                // Check if removable
                else if (trimmed.Contains("Removable Media:"))
                {
                    info.IsRemovable = trimmed.Contains("Yes", StringComparison.OrdinalIgnoreCase);
                }
            }

            // If PartitionType is empty, try File System Personality
            if (string.IsNullOrEmpty(info.PartitionType) && !string.IsNullOrEmpty(info.FileSystem))
            {
                info.PartitionType = info.FileSystem;
            }
        }

        /// <summary>
        /// Parse disk information from diskutil info output for the whole disk
        /// </summary>
        private static void ParseDiskInfo(string output, DiskInfo info)
        {
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                // Extract partition scheme
                if (trimmed.Contains("Content (IOContent):"))
                {
                    var parts = trimmed.Split(':', 2);
                    if (parts.Length > 1)
                    {
                        var content = parts[1].Trim();
                        if (content.Contains("GUID_partition_scheme"))
                            info.PartitionScheme = "GPT";
                        else if (content.Contains("FDisk_partition_scheme"))
                            info.PartitionScheme = "MBR";
                        else if (content.Contains("Apple_partition_scheme"))
                            info.PartitionScheme = "APM";
                        else
                            info.PartitionScheme = content;
                    }
                }
                // Extract device model/name
                else if (trimmed.Contains("Device / Media Name:"))
                {
                    var parts = trimmed.Split(':', 2);
                    if (parts.Length > 1)
                    {
                        // Store model info in Details since DiskInfo doesn't have a Model property
                        var model = parts[1].Trim();
                        info.Details += $"\nModel: {model}";
                    }
                }
                // Check removable status at disk level
                else if (trimmed.Contains("Removable Media:"))
                {
                    info.IsRemovable = trimmed.Contains("Yes", StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        /// <summary>
        /// Simple method to get basic disk info similar to the bash script
        /// </summary>
        /// <param name="volumePath">Volume path like "/Volumes/SETUPWIZ"</param>
        /// <returns>Formatted string with disk, partition type, and scheme</returns>
        public static async Task<string> GetBasicDiskInfo(string volumePath)
        {
            var info = await DetectDiskInfo(volumePath);

            if (!string.IsNullOrEmpty(info.ErrorMessage))
            {
                return $"Error: {info.ErrorMessage}";
            }

            // Extract disk identifier (e.g., "disk2" from "disk2s1")
            var disk = "";
            if (!string.IsNullOrEmpty(info.DiskIdentifier))
            {
                var diskMatch = Regex.Match(info.DiskIdentifier, @"(disk\d+)");
                if (diskMatch.Success)
                {
                    disk = diskMatch.Groups[1].Value;
                }
            }

            return $"Disk: {disk}\n" +
                   $"Partition Type: {info.PartitionType}\n" +
                   $"Scheme: {info.PartitionScheme}";
        }
    }
}