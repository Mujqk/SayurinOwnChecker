using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using BazaChecker.Models;
using Microsoft.Win32;
using System.ComponentModel;
using System.Net;
using System.Reflection;
using DiscordRPC;
using DiscordRPC.Logging;

namespace BazaChecker.Services
{
    /// <summary>
    /// Comprehensive cheat signature database
    /// </summary>
    public static class CheatDatabase
    {
        public static readonly HashSet<string> Signatures = new(StringComparer.OrdinalIgnoreCase)
        {
            // Cheats & Hacks (removed generic words: water, radar, nova, winx, shark, orbit, aurora, pandora, weave, klar)
            "xone", "exloader", "com.swiftsoft", "interium", "mason", "lunocs2", "neverlose",
            "midnight", "hellshack", "hells-hack", "blasthack", "nixware", "en1gma",
            "enigma", "sharhack", "sharkhack", "exhack", "uwuware", "espd2x", "wallhack", "skinchanger",
            "vredux", "neverkernel", "aquila", "luno", "fecurity", "tkazer", "pellix",
            "pussycat", "macros", "axios", "syncware", "onemacro", "softhub", "mvploader",
            "detorus", "proext", "sapphire", "interwebz", "spirthack", "haxcs", "plaguecheat",
            "vapehook", "smurfwrecker", "iniuia", "inuria", "memesense", "yeahnot", "leaguemode",
            "legendware", "eghack", "hauntedproject", "externalcrack", "rager9", "rager8",
            "phoenixhack", "obr", "onebyteradar", "ezinjector", "reborn", "onebytewallhack",
            "osiris", "multihack", "breakthrough", "rhcheats", "fatality", "onetap",
            "ev0lve", "bhop", "bunnyhop", "compkiller", "tripit", "rawetrip",
            "plague", "neoxahack", "fizzy", "expandera", "ekknod", "axion", "doomxtf",
            "jestkii", "wh-satano", "cheatcsgo", "r8cheats", "ezcheats", "cs-elect", "rf-cheats",
            "anyx", "hackvshack", "ezyhack", "unknowncheats", "cheater", "insanitycheats",
            "elitehacks", "novamacro", "securecheats", "ezcs", "dhjcheats",
            "nanogon", "extract_merc", "undetek", "millionware",
            "xy0", "aristois", "w1nner", "desync", "ragebot", "legitbot",
            
            // Tools & Debuggers
            "process hacker", "cheat engine", "x64dbg", "ida pro", "http debugger",
            
            // File signatures
            ".amc", ".ahk"
        };

        // System paths to skip (avoid false positives, but allow Windows/System32/Program Files to be scanned for hidden folders)
        public static readonly HashSet<string> SystemPathsToSkip = new(StringComparer.OrdinalIgnoreCase)
        {
            "winsxs", "driverstore", "syswow64", "assembly", "servicing",
            "microsoft.net", "fonts", "boot", "recovery"
        };
    }


    /// <summary>
    /// Disk information model
    /// </summary>
    public class DiskInfo
    {
        public string DriveLetter { get; set; } = "";
        public string Label { get; set; } = "";
        public string DisplayName => string.IsNullOrEmpty(Label) ? DriveLetter : $"{Label} ({DriveLetter})";
        public long TotalSize { get; set; }
        public long FreeSpace { get; set; }
        public bool IsSelected { get; set; } = true;
        public string SizeText => $"{FreeSpace / (1024 * 1024 * 1024):N0} GB free of {TotalSize / (1024 * 1024 * 1024):N0} GB";
    }

    /// <summary>
    /// Detected threat model
    /// </summary>
    public class ThreatInfo
    {
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        public string Type { get; set; } = ""; // "Cheat", "Injector", "Suspicious Folder", "Suspicious File"
        
        public string DisplayPath
        {
            get
            {
                if (string.IsNullOrEmpty(Path) || Path.Length <= 80) return Path;
                
                // Smart truncation: C:\Users\...\folder\file.exe
                try
                {
                    int keepStart = 30;
                    int keepEnd = 45;
                    return Path.Substring(0, keepStart) + "..." + Path.Substring(Path.Length - keepEnd);
                }
                catch { return Path; }
            }
        }

        public string TypeIcon => Type switch
        {
            "Cheat" => "🎮",
            "Injector" => "💉",
            "Suspicious Folder" => "📁",
            "Suspicious File" => "📄",
            "Process" => "⚡",
            _ => "⚠️"
        };
    }

    /// <summary>
    /// Extended scan result
    /// </summary>
    public class DiskScanResult
    {
        public int TotalFilesScanned { get; set; }
        public int TotalFoldersScanned { get; set; }
        public List<ThreatInfo> Threats { get; set; } = new();
        public TimeSpan ScanDuration { get; set; }
        public bool WasCancelled { get; set; }
    }

    /// <summary>
    /// Disk scanner with cheat detection
    /// </summary>
    public static class DiskScanner
    {
        public static List<DiskInfo> GetAvailableDisks()
        {
            var disks = new List<DiskInfo>();

            try
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (drive.IsReady && drive.DriveType == DriveType.Fixed)
                    {
                        disks.Add(new DiskInfo
                        {
                            DriveLetter = drive.Name.TrimEnd('\\'),
                            Label = drive.VolumeLabel,
                            TotalSize = drive.TotalSize,
                            FreeSpace = drive.AvailableFreeSpace,
                            IsSelected = true
                        });
                    }
                }
            }
            catch { }

            return disks;
        }

        public static async Task<DiskScanResult> ScanDisksAsync(
            List<DiskInfo> selectedDisks,
            bool useIconScan,
            bool useSignatureScan,
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var result = new DiskScanResult();
            var stopwatch = Stopwatch.StartNew();
            var logBuilder = new System.Text.StringBuilder();
            logBuilder.AppendLine($"DEBUG LOG START: {DateTime.Now}");
            logBuilder.AppendLine($"Scan Depth: 25");
            logBuilder.AppendLine("SystemPathsToSkip contents: " + string.Join(", ", CheatDatabase.SystemPathsToSkip));

            // First scan running processes
            progress?.Report("Scanning running processes...");
            await Task.Run(() => ScanProcesses(result), cancellationToken);

            // Scan each selected disk
            foreach (var disk in selectedDisks.Where(d => d.IsSelected))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    result.WasCancelled = true;
                    break;
                }

                progress?.Report($"Scanning {disk.DriveLetter}...");

                // Scan key locations on each disk
                var pathsToScan = GetScanPaths(disk.DriveLetter);

                foreach (var path in pathsToScan)
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    if (!Directory.Exists(path)) 
                    {
                        logBuilder.AppendLine($"Root path not found: {path}");
                        continue;
                    }

                    logBuilder.AppendLine($"Starting scan of root: {path}");
                    await Task.Run(() => ScanDirectory(path, result, progress, cancellationToken, 0, 25, useIconScan, useSignatureScan, logBuilder), cancellationToken);
                }
            }

            stopwatch.Stop();
            result.ScanDuration = stopwatch.Elapsed;
            logBuilder.AppendLine($"Scan finished in {stopwatch.Elapsed.TotalSeconds}s. Total files: {result.TotalFilesScanned}");
            
            try { File.WriteAllText("scan_log.txt", logBuilder.ToString()); } catch { }
            
            return result;
        }

        private static List<string> GetScanPaths(string driveLetter)
        {
            var paths = new List<string>();
            var drive = driveLetter.TrimEnd('\\', ':') + ":\\";
            
            // Full Deep Scan: Return the root drive to scan everything
            paths.Add(drive);

            return paths;
        }

        private static void ScanProcesses(DiskScanResult result)
        {
            try
            {
                var processes = Process.GetProcesses();
                foreach (var proc in processes)
                {
                    try
                    {
                        var procName = proc.ProcessName.ToLowerInvariant();
                        if (CheatDatabase.Signatures.Contains(procName))
                        {
                            result.Threats.Add(new ThreatInfo 
                            { 
                                Name = proc.ProcessName, 
                                Path = $"Process: {proc.ProcessName} (PID: {proc.Id})", 
                                Type = "Suspicious Process" 
                            });
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        private static void ScanDirectory(string path, DiskScanResult result, IProgress<string>? progress, CancellationToken ct, int depth, int maxDepth, bool useIconScan, bool useSignatureScan, System.Text.StringBuilder log)
        {
            if (ct.IsCancellationRequested) return;
            if (depth > maxDepth) 
            {
                // Only log depth limit reached for interesting paths
                if (path.Contains("swiftsoft", StringComparison.OrdinalIgnoreCase))
                    log.AppendLine($"Reached MAX DEPTH at: {path}");
                return;
            }

            try
            {
                var dirName = Path.GetFileName(path);
                var dirNameLower = dirName.ToLowerInvariant();

                // Exclusive Filter: Skip Faceit and Firefox folders
                if (dirNameLower.Contains("faceit") || dirNameLower.Contains("firefox")) return;

                // Check folders signatures if enabled
                if (useSignatureScan) 
                {
                    foreach (var sig in CheatDatabase.Signatures)
                    {
                        var sigLower = sig.ToLowerInvariant().Replace(" ", "");
                        if (dirNameLower.Contains(sigLower))
                        {
                            result.Threats.Add(new ThreatInfo { Name = dirName, Path = path, Type = "Suspicious Folder" });
                            log.AppendLine($"THREAT FOUND (FOLDER): {path} [Sig: {sig}]");
                            break;
                        }
                    }
                }

                result.TotalFoldersScanned++;

                // Scan files in this directory - ONLY check executable files
                try
                {
                    var enumOptions = new System.IO.EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = 0 }; // Don't skip hidden/system
                    
                    foreach (var file in System.IO.Directory.GetFiles(path, "*", enumOptions))
                    {
                        if (ct.IsCancellationRequested) return;

                        var fileName = Path.GetFileName(file);
                        var fileNameLower = fileName.ToLowerInvariant();
                        var ext = Path.GetExtension(fileNameLower);
                        
                        result.TotalFilesScanned++;

                        // Only check executable and script files
                        if (ext != ".exe" && ext != ".ahk" && ext != ".bat" && ext != ".cmd" && ext != ".ini")
                            continue;

                        // Check file signatures if enabled
                        if (useSignatureScan)
                        {
                            foreach (var sig in CheatDatabase.Signatures)
                            {
                                var sigLower = sig.ToLowerInvariant().Replace(" ", "");
                                if (fileNameLower.Contains(sigLower))
                                {
                                    result.Threats.Add(new ThreatInfo { Name = fileName, Path = file, Type = "Suspicious File" });
                                    log.AppendLine($"THREAT FOUND (FILE): {file} [Sig: {sig}]");
                                    break;
                                }
                            }
                        }
                        
                        // Placeholder for Icon Scan
                        if (useIconScan)
                        {
                             // TODO: Implement Icon Hash check here
                        }
                    }
                }
                catch (Exception ex) 
                {
                    if (path.Contains("Windows", StringComparison.OrdinalIgnoreCase))
                         log.AppendLine($"Error scanning files in {path}: {ex.Message}");
                }

                // Recurse into subdirectories
                try
                {
                    var enumOptions = new System.IO.EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = 0 };

                    foreach (var subDir in Directory.GetDirectories(path, "*", enumOptions))
                    {
                        if (ct.IsCancellationRequested) return;

                        var subDirName = Path.GetFileName(subDir).ToLowerInvariant();
                        var subDirPathLower = subDir.ToLowerInvariant();

                        // Skip technical/cache folders
                        if (subDirName.StartsWith(".") ||
                            subDirName == "node_modules" ||
                            subDirName == "cache" ||
                            subDirName == "caches" ||
                            subDirName == "__pycache__" ||
                            subDirName == "packages" ||
                            CheatDatabase.SystemPathsToSkip.Any(skip => subDirPathLower.Contains(skip)))
                        {
                             // Log why we skipped, but only for system paths to avoid spam
                             if (subDirPathLower.Contains("windows") || subDirPathLower.Contains("program files"))
                                 log.AppendLine($"Skipped: {subDir} [Filter Match]");
                             continue;
                        }

                        ScanDirectory(subDir, result, progress, ct, depth + 1, maxDepth, useIconScan, useSignatureScan, log);
                    }
                }
                catch (Exception ex)
                {
                    if (path.Contains("Windows", StringComparison.OrdinalIgnoreCase))
                        log.AppendLine($"Error scanning subdirs in {path}: {ex.Message}");
                }
            }
            catch (Exception ex) 
            {
                log.AppendLine($"Critical error in ScanDirectory {path}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Scans Windows Registry for installed/deleted programs
    /// </summary>
    public static class RegistryScanner
    {
        private static readonly string[] UninstallPaths = new[]
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
        };

        public static List<ProgramInfo> GetPrograms()
        {
            var programs = new List<ProgramInfo>();
            
            // 1. Standard Uninstall Keys
            ScanUninstallKeys(Registry.CurrentUser, programs);
            ScanUninstallKeys(Registry.LocalMachine, programs);

            // 2. MUICache (Shell File History)
            ScanMuiCache(programs);

            // 3. AppCompatFlags (Compatibility Assistant)
            ScanAppCompatFlags(programs);

            // 4. UserAssist (Execution History)
            ScanUserAssist(programs);

            return programs
                .GroupBy(p => p.DisplayName.ToLowerInvariant())
                .Select(g => g.First()) // Basic deduplication
                .OrderBy(p => p.DisplayName)
                .ToList();
        }

        private static void ScanUninstallKeys(RegistryKey rootKey, List<ProgramInfo> programs)
        {
            foreach (var path in UninstallPaths)
            {
                using var key = rootKey.OpenSubKey(path);
                if (key == null) continue;

                foreach (var subName in key.GetSubKeyNames())
                {
                    using var subKey = key.OpenSubKey(subName);
                    if (subKey == null) continue;

                    var name = subKey.GetValue("DisplayName")?.ToString();
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    var installLoc = subKey.GetValue("InstallLocation")?.ToString();
                    var uninstStr = subKey.GetValue("UninstallString")?.ToString();
                    var iconStr = subKey.GetValue("DisplayIcon")?.ToString();

                    var (verifiedPath, status) = VerifyProgramStatus(installLoc, uninstStr, iconStr);

                    programs.Add(new ProgramInfo
                    {
                        DisplayName = name,
                        InstallPath = verifiedPath,
                        Publisher = subKey.GetValue("Publisher")?.ToString() ?? "",
                        Status = status
                    });
                }
            }
        }

        private static void ScanMuiCache(List<ProgramInfo> programs)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\Local Settings\Software\Microsoft\Windows\Shell\MuiCache");
                if (key == null) return;

                foreach (var valueName in key.GetValueNames())
                {
                    if (valueName == "LangID" || string.IsNullOrEmpty(valueName)) continue;

                    var rawPath = valueName.Split('|')[0]; // Path is often path.exe|FriendlyName
                    if (!rawPath.Contains('\\')) continue;

                    var name = key.GetValue(valueName)?.ToString() ?? System.IO.Path.GetFileNameWithoutExtension(rawPath);
                    var (verifiedPath, status) = VerifyProgramStatus(rawPath, null, null);

                    // For MuiCache, if it doesn't exist, it's a TRACE
                    if (status == ProgramStatus.Deleted) status = ProgramStatus.Trace;

                    programs.Add(new ProgramInfo { DisplayName = name, InstallPath = verifiedPath, Status = status, IsHistory = true });
                }
            }
            catch { }
        }

        private static void ScanAppCompatFlags(List<ProgramInfo> programs)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Compatibility Assistant\Store");
                if (key == null) return;

                foreach (var valueName in key.GetValueNames())
                {
                    if (string.IsNullOrEmpty(valueName) || !valueName.Contains('\\')) continue;

                    var name = System.IO.Path.GetFileNameWithoutExtension(valueName);
                    var (verifiedPath, status) = VerifyProgramStatus(valueName, null, null);

                    if (status == ProgramStatus.Deleted) status = ProgramStatus.Trace;

                    programs.Add(new ProgramInfo { DisplayName = $"[Trace] {name}", InstallPath = verifiedPath, Status = status, IsHistory = true });
                }
            }
            catch { }
        }

        private static void ScanUserAssist(List<ProgramInfo> programs)
        {
            try
            {
                using var uaKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\UserAssist");
                if (uaKey == null) return;

                foreach (var guid in uaKey.GetSubKeyNames())
                {
                    using var countKey = uaKey.OpenSubKey($@"{guid}\Count");
                    if (countKey == null) continue;

                    foreach (var valName in countKey.GetValueNames())
                    {
                        var decoded = Rot13(valName);
                        if (!decoded.Contains('\\') || !decoded.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;

                        var name = System.IO.Path.GetFileNameWithoutExtension(decoded);
                        var (verifiedPath, status) = VerifyProgramStatus(decoded, null, null);

                        if (status == ProgramStatus.Deleted) status = ProgramStatus.Trace;

                        programs.Add(new ProgramInfo { DisplayName = $"[Run History] {name}", InstallPath = verifiedPath, Status = status, IsHistory = true });
                    }
                }
            }
            catch { }
        }

        private static (string path, ProgramStatus status) VerifyProgramStatus(string? installLoc, string? uninst, string? icon)
        {
            string? bestPath = null;

            // Priority 1: Install Location
            if (!string.IsNullOrWhiteSpace(installLoc))
            {
                bestPath = installLoc.Trim('\"', ' ');
                if (System.IO.Directory.Exists(bestPath)) return (bestPath, ProgramStatus.Installed);
            }

            // Priority 2: Extract from Icon (often the main exe)
            if (!string.IsNullOrWhiteSpace(icon))
            {
                var iconPath = icon.Split(',')[0].Trim('\"', ' ');
                if (System.IO.File.Exists(iconPath)) return (iconPath, ProgramStatus.Installed);
                if (bestPath == null) bestPath = iconPath;
            }

            // Priority 3: Extract from Uninstall String
            if (!string.IsNullOrWhiteSpace(uninst))
            {
                int exeIdx = uninst.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
                if (exeIdx != -1)
                {
                    var path = uninst.Substring(0, exeIdx + 4).Trim('\"', ' ');
                    if (System.IO.File.Exists(path)) return (path, ProgramStatus.Installed);
                    if (bestPath == null) bestPath = System.IO.Path.GetDirectoryName(path) ?? path;
                }
            }

            // If we found a path but it's not on disk
            if (!string.IsNullOrEmpty(bestPath)) return (bestPath, ProgramStatus.Deleted);

            return ("Unknown", ProgramStatus.Deleted);
        }

        private static string Rot13(string input)
        {
            return new string(input.Select(c =>
            {
                if (c >= 'a' && c <= 'z') return (char)((c - 'a' + 13) % 26 + 'a');
                if (c >= 'A' && c <= 'Z') return (char)((c - 'A' + 13) % 26 + 'A');
                return c;
            }).ToArray());
        }

        public static (int installed, int deleted, int trace) GetCounts(List<ProgramInfo> programs)
        {
            return (
                programs.Count(p => p.Status == ProgramStatus.Installed),
                programs.Count(p => p.Status == ProgramStatus.Deleted || p.Status == ProgramStatus.Trace), // Group deleted/trace for simple view or separate them
                programs.Count(p => p.Status == ProgramStatus.Trace)
            );
        }
    }

    /// <summary>
    /// Caches Steam avatars locally to avoid repeated downloads
    /// </summary>
    public static class AvatarCache
    {
        private static readonly string CacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SayurinChecker", "AvatarCache");
        
        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        private const int MaxCacheAgeDays = 7;

        static AvatarCache()
        {
            try { Directory.CreateDirectory(CacheDir); } catch { }
        }

        /// <summary>
        /// Gets avatar from cache or downloads it
        /// </summary>
        public static async Task<string?> GetOrFetchAvatarAsync(string steamId, string? url)
        {
            if (string.IsNullOrEmpty(url)) return null;
            
            try
            {
                string cacheFile = Path.Combine(CacheDir, $"{steamId}.jpg");
                
                // Check if cached and not expired
                if (File.Exists(cacheFile))
                {
                    var fileInfo = new FileInfo(cacheFile);
                    if ((DateTime.Now - fileInfo.LastWriteTime).TotalDays < MaxCacheAgeDays)
                    {
                        return cacheFile; // Use cached
                    }
                }
                
                // Download and cache
                var imageBytes = await _httpClient.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(cacheFile, imageBytes);
                return cacheFile;
            }
            catch
            {
                return null; // Fall back to URL
            }
        }

        /// <summary>
        /// Cleans old cache files
        /// </summary>
        public static void CleanOldCache()
        {
            try
            {
                if (!Directory.Exists(CacheDir)) return;
                
                foreach (var file in Directory.GetFiles(CacheDir))
                {
                    var fileInfo = new FileInfo(file);
                    if ((DateTime.Now - fileInfo.LastWriteTime).TotalDays > MaxCacheAgeDays)
                    {
                        File.Delete(file);
                    }
                }
            }
            catch { }
        }
    }

    /// <summary>
    /// Scans for Steam accounts on the PC and fetches data from csst.at
    /// </summary>
    public static class SteamScanner
    {
        private static readonly HttpClient _httpClient = new HttpClient(new HttpClientHandler 
        { 
            AutomaticDecompression = DecompressionMethods.All,
            ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
        })
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        // Compiled Regex patterns for better performance
        private static readonly Regex SteamIdPattern = new Regex(@"""(\d{17})""[^{]*\{([^}]+)\}", RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex AvatarMediumPattern = new Regex(@"<avatarMedium><!\[CDATA\[(.*?)\]\]></avatarMedium>", RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex AvatarFullPattern = new Regex(@"<avatarFull><!\[CDATA\[(.*?)\]\]></avatarFull>", RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex VacBannedPattern = new Regex(@"<vacBanned>(\d+)</vacBanned>", RegexOptions.Compiled);
        private static readonly Regex TradeBanPattern = new Regex(@"<tradeBanState><!\[CDATA\[(.*?)\]\]></tradeBanState>", RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex BadgeClassPattern = new Regex(@"user_badge\s+badge_(\w+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex BadgeTextPattern = new Regex(@"<div[^>]*class=""[^""]*user_badge[^""]*""[^>]*>(.*?)</div>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex HtmlTagPattern = new Regex(@"<[^>]+>", RegexOptions.Compiled);
        private static readonly Regex WhitespacePattern = new Regex(@"\s+", RegexOptions.Compiled);
        private static readonly Regex BansContentPattern = new Regex(@"class=""bans_comms_content""[^>]*>\s*<ul[^>]*>(.*?)</ul>", RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex ListItemPattern = new Regex(@"<li[^>]*>(.*?)</li>", RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex SpanPattern = new Regex(@"<span[^>]*>(.*?)</span>", RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex SvgPathPattern = new Regex(@"d=""([^""]+)""", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static List<SteamAccount> GetAccounts()
        {
            var accounts = new List<SteamAccount>();

            try
            {
                string? steamPath = null;
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                {
                    steamPath = key?.GetValue("SteamPath")?.ToString();
                }

                if (string.IsNullOrEmpty(steamPath))
                {
                    var defaultPaths = new[] { @"C:\Program Files (x86)\Steam", @"C:\Program Files\Steam" };
                    foreach (var path in defaultPaths)
                    {
                        if (Directory.Exists(path)) { steamPath = path; break; }
                    }
                }

                if (string.IsNullOrEmpty(steamPath)) return accounts;

                var loginUsersPath = Path.Combine(steamPath, "config", "loginusers.vdf");
                if (!File.Exists(loginUsersPath)) return accounts;

                var content = File.ReadAllText(loginUsersPath);
                var matches = SteamIdPattern.Matches(content);

                foreach (Match match in matches)
                {
                    var steamId = match.Groups[1].Value;
                    var block = match.Groups[2].Value;

                    var accountName = ExtractValue(block, "AccountName");
                    var personaName = ExtractValue(block, "PersonaName");
                    var mostRecent = ExtractValue(block, "MostRecent");

                    if (string.IsNullOrEmpty(accountName)) continue;

                    accounts.Add(new SteamAccount
                    {
                        SteamId64 = steamId,
                        AccountName = accountName,
                        PersonaName = !string.IsNullOrEmpty(personaName) ? personaName : accountName,
                        IsActive = mostRecent == "1",
                        IsVacBanned = false
                    });
                }
            }
            catch { }

            return accounts;
        }

        /// <summary>
        /// Fetches player data from csst.at (no API key required)
        /// </summary>
        public static async Task EnrichAccountsWithSteamDataAsync(List<SteamAccount> accounts)
        {
            if (accounts.Count == 0) return;

            // Ensure TLS 1.2/1.3 for Baza
#pragma warning disable SYSLIB0014
            try { ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13; } catch { }
#pragma warning restore SYSLIB0014

            var tasks = accounts.Select(async account => 
            {
                await Task.WhenAll(
                    FetchSteamProfileDataAsync(account),
                    FetchBazaDataAsync(account)
                );
            });
            await Task.WhenAll(tasks);
        }

        private static async Task FetchBazaDataAsync(SteamAccount account)
        {
            try
            {
                account.IsBazaLoading = true;
                string url = $"https://baza-cs2.ru/profiles/{account.SteamId64}/block/0/";
                
                // Set User-Agent
                if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
                    _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

                var response = await _httpClient.GetAsync(url);
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    account.BazaBanInfo = "Не зарегистрирован (Not Registered)";
                    account.BazaAdminStatus = "N/A";
                    return;
                }
                
                response.EnsureSuccessStatusCode();
                string html = await response.Content.ReadAsStringAsync();
                
                // --- ADMIN STATUS CHECK (Class-based) ---
                // Browser analysis suggests roles are in <div class="user_badge badge_TYPE">Text</div>
                string adminStatus = "Игрок (PLAYER)"; 

                // Regex to find the badge class. Expected: class="user_badge badge_root" or similar
                var badgeMatch = Regex.Match(html, @"user_badge\s+badge_(\w+)", RegexOptions.IgnoreCase);
                string badgeType = badgeMatch.Success ? badgeMatch.Groups[1].Value.ToLower() : "";
                
                // Fallback: Check text content inside the badge element if class isn't definitive
                string badgeText = "";
                var badgeTextMatch = Regex.Match(html, @"<div[^>]*class=""[^""]*user_badge[^""]*""[^>]*>(.*?)</div>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (badgeTextMatch.Success) badgeText = CleanBazaText(badgeTextMatch.Groups[1].Value).ToLower();

                // Mapping Logic (Priority: Class > Text)
                if (badgeType == "root" || badgeText.Contains("создатель") || badgeText.Contains("владелец")) 
                    adminStatus = "Создатель / Владелец (CREATOR)";
                else if (badgeType == "co_owner" || badgeText.Contains("совладелец")) 
                    adminStatus = "Совладелец (CO-OWNER)";
                else if (badgeText.Contains("зам") && (badgeText.Contains("создател") || badgeText.Contains("владельц"))) 
                    adminStatus = "Зам. Создателя (DEP. CREATOR)";
                else if (badgeType == "main_admin" || badgeText.Contains("главный") || badgeText.Contains("га")) 
                    adminStatus = "Главный Админ (HEAD ADMIN)";
                else if (badgeText.Contains("зам") && (badgeText.Contains("главного") || badgeText.Contains("зга"))) 
                    adminStatus = "Зам. Гл. Админа (DEP. HEAD ADMIN)";
                else if (badgeType == "curator" || badgeText.Contains("куратор")) 
                    adminStatus = "Куратор (CURATOR)";
                else if (badgeType == "spec_admin" || badgeText.Contains("спец")) 
                    adminStatus = "Спец. Админ (SPECIAL ADMIN)";
                else if (badgeType == "admin" || badgeText == "администратор" || badgeText == "admin") 
                    adminStatus = "Администратор (ADMIN)";
                else if (badgeType == "moder" || badgeText.Contains("модератор")) 
                    adminStatus = "Модератор (MODERATOR)";
                else if (badgeType == "vip" || badgeText.Contains("vip")) 
                    adminStatus = "VIP PLAYER";
                else if (badgeType == "player" || badgeText.Contains("игрок")) 
                    adminStatus = "Игрок (PLAYER)"; // Explicit player check
                
                account.BazaAdminStatus = adminStatus;

                // --- BAN/MUTE HISTORY CHECK ---
                account.BazaBans = new List<BazaBanEntry>();
                account.BazaMutes = new List<BazaBanEntry>();

                // Helper to clean text
                string CleanBazaText(string input)
                {
                    if (string.IsNullOrEmpty(input)) return "";
                    // Remove HTML tags
                    string text = Regex.Replace(input, "<[^>]+>", " ");
                    text = System.Net.WebUtility.HtmlDecode(text);
                    return Regex.Replace(text, @"\s+", " ").Trim();
                }

                // Helper to parse the DIV/UL/LI structure
                List<BazaBanEntry> ParseBazaList(string pageHtml, string headerText, bool isMuteSection, string? stopAt = null)
                {
                    var entries = new List<BazaBanEntry>();
                    
                    // 1. Find the header position (e.g. "Последние муты")
                    int headerIdx = pageHtml.IndexOf(headerText, StringComparison.OrdinalIgnoreCase);
                    if (headerIdx == -1) return entries;

                    // 2. Extract a chunk of HTML after the header
                    string searchChunk = pageHtml.Substring(headerIdx);
                    
                    // CRITICAL FIX: Limit the chunk to the NEXT section header to prevent bleeding
                    if (!string.IsNullOrEmpty(stopAt))
                    {
                        int stopIdx = searchChunk.IndexOf(stopAt, StringComparison.OrdinalIgnoreCase);
                        if (stopIdx != -1)
                        {
                            searchChunk = searchChunk.Substring(0, stopIdx);
                        }
                    }

                    // Check for "No data" indicators within this restricted chunk
                    if (searchChunk.Contains("Нет банов") || searchChunk.Contains("Нет мутов") || searchChunk.Contains("class=\"no_data\""))
                    {
                        return entries;
                    }

                    // Find the start of the content list WITHIN the restricted chunk
                    var contentMatch = Regex.Match(searchChunk, @"class=""bans_comms_content""[^>]*>\s*<ul[^>]*>(.*?)</ul>", RegexOptions.Singleline);
                    if (!contentMatch.Success) return entries;

                    string listHtml = contentMatch.Groups[1].Value;

                    // 3. Parse each LI item
                    var listItems = Regex.Matches(listHtml, @"<li[^>]*>(.*?)</li>", RegexOptions.Singleline);

                    foreach (Match li in listItems)
                    {
                        string innerHtml = li.Groups[1].Value;
                        // Find all Spans
                        // Structure: <span>Date</span> <span>Reason</span> <span>Admin</span> <span>Type</span> <span>Status</span>
                        var spans = Regex.Matches(innerHtml, @"<span[^>]*>(.*?)</span>", RegexOptions.Singleline);
                        
                        // We expect 5 spans based on browser analysis
                        if (spans.Count >= 5)
                        {
                            string date = CleanBazaText(spans[0].Groups[1].Value);
                            // Basic validation: Date should start with digit
                            if (!char.IsDigit(date.FirstOrDefault())) continue;

                            string reason = CleanBazaText(spans[1].Groups[1].Value);
                            string admin = CleanBazaText(spans[2].Groups[1].Value);
                            string type = "Ban"; 
                            string iconPath = "";
                            string iconColor = "#888888";
                            string duration = "—";
                            string status = "";

                            bool isImage = false;

                            if (isMuteSection)
                            {
                                // Mutes: Date | Reason | Admin | Type(Icon) | Status
                                string typeHtml = spans[3].Groups[1].Value;
                                status = CleanBazaText(spans[4].Groups[1].Value);
                                duration = "—"; // Mutes don't show duration in this table layout

                                // Extract SVG
                                var pathMatch = Regex.Match(typeHtml, "d=\"([^\"]+)\"", RegexOptions.IgnoreCase);
                                if (pathMatch.Success) iconPath = pathMatch.Groups[1].Value;
                                
                                if (typeHtml.Contains("M38.8") || typeHtml.Contains("microphone")) 
                                { 
                                    type = "Voice"; 
                                    iconColor = "#A855F7"; // Purple
                                    // Voice Mute SVG Path (microphone with slash)
                                    iconPath = "M38.8 5.1C28.4-3.1 13.3-1.2 5.1 9.2S-1.2 34.7 9.2 42.9l592 464c10.4 8.2 25.5 6.3 33.7-4.1s6.3-25.5-4.1-33.7L472.1 344.7c15.2-26 23.9-56.3 23.9-88.7V216c0-13.3-10.7-24-24-24s-24 10.7-24 24v40c0 21.2-5.1 41.1-14.2 58.7L416 300.8V96c0-53-43-96-96-96s-96 43-96 96v54.3L38.8 5.1zM344 430.4c20.4-2.8 39.7-9.1 57.3-18.2l-43.1-33.9C346.1 382 333.3 384 320 384c-70.7 0-128-57.3-128-128v-8.7L144.7 210c-.5 1.9-.7 3.9-.7 6v40c0 89.1 66.2 162.7 152 174.4V464H248c-13.3 0-24 10.7-24 24s10.7 24 24 24h72 72c13.3 0 24-10.7 24-24s-10.7-24-24-24H344V430.4z";
                                    isImage = false;
                                }
                                else if (typeHtml.Contains("M6.009") || typeHtml.Contains("M64.0") || typeHtml.Contains("comment") || typeHtml.Contains("gag")) 
                                { 
                                    type = "Chat"; 
                                    iconColor = "#A855F7"; // Purple
                                    // Chat Mute SVG Path (message bubble with slash)
                                    iconPath = "M64.03 239.1c0 49.59 21.38 94.1 56.97 130.7c-12.5 50.39-54.31 95.3-54.81 95.8c-2.187 2.297-2.781 5.703-1.5 8.703c1.312 3 4.125 4.797 7.312 4.797c66.31 0 116-31.8 140.6-51.41c32.72 12.31 69.02 19.41 107.4 19.41c37.39 0 72.78-6.663 104.8-18.36L82.93 161.7C70.81 185.9 64.03 212.3 64.03 239.1zM630.8 469.1l-118.1-92.59C551.1 340 576 292.4 576 240c0-114.9-114.6-207.1-255.1-207.1c-67.74 0-129.1 21.55-174.9 56.47L38.81 5.117C28.21-3.154 13.16-1.096 5.115 9.19C-3.072 19.63-1.249 34.72 9.188 42.89l591.1 463.1c10.5 8.203 25.57 6.333 33.7-4.073C643.1 492.4 641.2 477.3 630.8 469.1z";
                                    isImage = false;
                                }
                                else if (typeHtml.Contains("M367.2") || typeHtml.Contains("slash") || typeHtml.Contains("block")) 
                                { 
                                    type = "Block"; 
                                    iconColor = "#A855F7"; // Purple
                                    // All/Block Mute SVG Path (circle with slash)
                                    iconPath = "M367.2 412.5L99.5 144.8C77.1 176.1 64 214.5 64 256c0 106 86 192 192 192c41.5 0 79.9-13.1 111.2-35.5zm45.3-45.3C434.9 335.9 448 297.5 448 256c0-106-86-192-192-192c-41.5 0-79.9 13.1-111.2 35.5L412.5 367.2zM512 256c0 141.4-114.6 256-256 256S0 397.4 0 256S114.6 0 256 0S512 114.6 512 256z";
                                    isImage = false;
                                }
                            }
                            else
                            {
                                // Bans: Date | Reason | Admin | Duration | Status
                                duration = CleanBazaText(spans[3].Groups[1].Value);
                                status = CleanBazaText(spans[4].Groups[1].Value);
                                
                                type = "Ban";
                                iconColor = "#EF4444"; // Red
                                // Ban SVG Path (circle with slash - same as block but red)
                                iconPath = "M256 8C119.034 8 8 119.033 8 256s111.034 248 248 248 248-111.034 248-248S392.967 8 256 8zm130.108 117.892c65.448 65.448 70 165.481 20.677 235.637L150.47 105.216c70.204-49.356 170.226-44.735 235.638 20.676zM125.892 386.108c-65.448-65.448-70-165.481-20.677-235.637L361.53 406.784c-70.203 49.356-170.226 44.736-235.638-20.676z";
                                isImage = false;
                            }

                            entries.Add(new BazaBanEntry {
                                Type = type,
                                Date = date,
                                Reason = reason,
                                Admin = admin,
                                Duration = duration,
                                Status = status,
                                IconData = iconPath,
                                IconColor = iconColor,
                                IsImage = isImage
                            });
                        }
                    }
                    return entries;
                }

                // Execute Parsing
                // Try multiple header variations just in case
                // PASS "Муты" or "Mutes" as stop marker for BANS search to prevent over-reading
                var bans = ParseBazaList(html, "Блокировки", false, "Мут"); 
                if (bans.Count == 0) bans = ParseBazaList(html, "История банов", false, "Мут");
                if (bans.Count == 0) bans = ParseBazaList(html, "Последние баны", false, "Мут");

                var mutes = ParseBazaList(html, "Муты", true);
                if (mutes.Count == 0) mutes = ParseBazaList(html, "Последние муты", true);

                account.BazaBans = bans;
                account.BazaMutes = mutes;

                // Summary logic
                string banSummary = "Чист (Clean)";
                bool hasHistory = account.BazaBans.Count > 0 || account.BazaMutes.Count > 0;
                
                if (hasHistory)
                {
                    var summaryParts = new List<string>();
                    if (account.BazaBans.Count > 0) summaryParts.Add($"БАНЫ ({account.BazaBans.Count})");
                    if (account.BazaMutes.Count > 0) summaryParts.Add($"МУТЫ ({account.BazaMutes.Count})");
                    banSummary = string.Join(" | ", summaryParts);
                }
                
                // Check if currently active
                bool isAnyActive = account.BazaBans.Any(b => !b.Status.ToLower().Contains("ист") && !b.Status.ToLower().Contains("exp")) || 
                                   account.BazaMutes.Any(m => !m.Status.ToLower().Contains("ист") && !m.Status.ToLower().Contains("exp"));

                if (isAnyActive)
                {
                    banSummary = "АКТИВНОЕ НАКАЗАНИЕ";
                    if (account.BazaBans.Any(b => !b.Status.ToLower().Contains("ист"))) banSummary = "ЗАБАНЕН (ACTIVE BAN)";
                    else if (account.BazaMutes.Any(m => !m.Status.ToLower().Contains("ист"))) banSummary = "МУТ (ACTIVE MUTE)";
                }
                else if (hasHistory)
                {
                    if (!banSummary.Contains("ИСТЁК")) banSummary += " (ИСТЁК)";
                }

                account.BazaBanInfo = banSummary;
            }
            catch
            {
                account.BazaBanInfo = "Ошибка парсинга";
            }
            finally
            {
                account.IsBazaLoading = false;
            }
        }

        /// <summary>
        /// Fetches player data from Steam Community XML API (reliable, no Cloudflare)
        /// </summary>
        private static async Task FetchSteamProfileDataAsync(SteamAccount account)
        {
            try
            {
                // Use Steam Community XML API - reliable and no protection
                var url = $"https://steamcommunity.com/profiles/{account.SteamId64}/?xml=1";
                var xml = await _httpClient.GetStringAsync(url);

                // Extract avatar URL (medium size)
                string? avatarUrl = null;
                var avatarMatch = AvatarMediumPattern.Match(xml);
                if (avatarMatch.Success && !string.IsNullOrEmpty(avatarMatch.Groups[1].Value))
                {
                    avatarUrl = avatarMatch.Groups[1].Value;
                }
                else
                {
                    // Fallback to avatarFull
                    var avatarFullMatch = AvatarFullPattern.Match(xml);
                    if (avatarFullMatch.Success)
                    {
                        avatarUrl = avatarFullMatch.Groups[1].Value;
                    }
                }

                // Cache avatar locally for faster loading
                if (!string.IsNullOrEmpty(avatarUrl))
                {
                    var cachedPath = await AvatarCache.GetOrFetchAvatarAsync(account.SteamId64, avatarUrl);
                    account.AvatarUrl = cachedPath ?? avatarUrl; // Use cached path or fall back to URL
                }

                // Extract VAC ban status from XML
                var vacMatch = VacBannedPattern.Match(xml);
                if (vacMatch.Success)
                {
                    int vacStatus = int.Parse(vacMatch.Groups[1].Value);
                    account.IsVacBanned = vacStatus > 0;
                    if (account.IsVacBanned) account.NumberOfVACBans = 1;
                }

                // Check for trade ban state
                var tradeBanMatch = TradeBanPattern.Match(xml);
                if (tradeBanMatch.Success)
                {
                    string tradeBanState = tradeBanMatch.Groups[1].Value.ToLower();
                    account.EconomyBan = tradeBanState != "none" && !string.IsNullOrEmpty(tradeBanState);
                }
            }
            catch
            {
                // Silent fail - keep default values
            }
        }

        private static string ExtractValue(string block, string key)
        {
            var pattern = new Regex($@"""{key}""\s+""([^""]+)""", RegexOptions.IgnoreCase);
            var match = pattern.Match(block);
            return match.Success ? match.Groups[1].Value : "";
        }
    }

    /// <summary>
    /// Provides system information using WMI
    /// </summary>
    public static class SystemInfoProvider
    {
        public static SystemSummary GetSummary()
        {
            var summary = new SystemSummary();

            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Caption, InstallDate, LastBootUpTime, Version FROM Win32_OperatingSystem"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        summary.OSName = mo["Caption"]?.ToString() ?? "Windows";
                        summary.OSVersion = mo["Version"]?.ToString() ?? "";

                        var installDateStr = mo["InstallDate"]?.ToString();
                        if (!string.IsNullOrEmpty(installDateStr))
                        {
                            summary.InstallDate = ManagementDateTimeConverter.ToDateTime(installDateStr);
                        }

                        var lastBootStr = mo["LastBootUpTime"]?.ToString();
                        if (!string.IsNullOrEmpty(lastBootStr))
                        {
                            summary.LastBootTime = ManagementDateTimeConverter.ToDateTime(lastBootStr);
                            summary.SessionStartTime = ManagementDateTimeConverter.ToDateTime(lastBootStr);
                        }
                        break;
                    }
                }

                summary.MonitorCount = System.Windows.Forms.Screen.AllScreens.Length;

                // Get CPU info
                using (var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        summary.CPU = mo["Name"]?.ToString() ?? "Unknown";
                        break;
                    }
                }

                // Get GPU info
                using (var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController"))
                {
                    var gpus = new List<string>();
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        var name = mo["Name"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(name)) gpus.Add(name);
                    }
                    // Join all found GPUs
                    summary.GPU = gpus.Count > 0 ? string.Join(" + ", gpus) : "Unknown";
                }

                // Get RAM info
                using (var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        var totalBytes = Convert.ToInt64(mo["TotalPhysicalMemory"]);
                        // Round to nearest GB to show accurate value (e.g., 15.8 GB → 16 GB)
                        double totalGb = totalBytes / (1024.0 * 1024.0 * 1024.0);
                        int roundedGb = (int)Math.Round(totalGb);
                        summary.RAM = $"{roundedGb} GB";
                        break;
                    }
                }

                // Get Motherboard info
                using (var searcher = new ManagementObjectSearcher("SELECT Manufacturer, Product FROM Win32_BaseBoard"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        var manufacturer = mo["Manufacturer"]?.ToString() ?? "";
                        var product = mo["Product"]?.ToString() ?? "";
                        summary.Motherboard = $"{manufacturer} {product}".Trim();
                        if (string.IsNullOrEmpty(summary.Motherboard)) summary.Motherboard = "Unknown";
                        break;
                    }
                }

                // Advanced Pafish-like VM Detection
                if (!summary.IsVirtualMachine)
                {
                    summary.IsVirtualMachine = VmDetector.IsVirtualMachine();
                }

                if (summary.IsVirtualMachine) 
                {
                    summary.Motherboard = summary.Motherboard + " (Virtual)";
                }

                // Fallback for simple status
                if (string.IsNullOrEmpty(summary.OSName))
                {
                    summary.OSName = Environment.OSVersion.ToString();
                    summary.MonitorCount = 1;
                }
            }
            catch
            {
                summary.OSName = Environment.OSVersion.ToString();
                summary.MonitorCount = 1;
            }

            return summary;
        }
    }

    /// <summary>
    /// Advanced Virtual Machine Detection (inspired by Pafish) - Restored from NeocsChecker
    /// </summary>
    public static class VmDetector
    {
        private static StringBuilder _log = new StringBuilder();

        public static string GetDetectionLog() 
        {
            if (_log.Length == 0) return "Отчет еще не сформирован. Запустите проверку.";
            
            var sb = new StringBuilder();
            sb.AppendLine("[-] Sayurin Checker VM Scan Report");
            sb.AppendLine("[-] Started at: " + DateTime.Now);
            sb.AppendLine("--------------------------------------------------");
            sb.Append(_log.ToString());
            sb.AppendLine("--------------------------------------------------");
            sb.AppendLine("[-] End of report.");
            return sb.ToString();
        }

        public static bool IsVirtualMachine()
        {
            _log.Clear();
            bool isVm = false;

            _log.AppendLine("[-] Checking Registry keys...");
            if (CheckRegistry()) isVm = true;
            
            _log.AppendLine("\n[-] Checking Processes...");
            if (CheckProcesses()) isVm = true;

            _log.AppendLine("\n[-] Checking Driver Files...");
            if (CheckFiles()) isVm = true;

            _log.AppendLine("\n[-] Checking MAC Addresses...");
            if (CheckMacAddress()) isVm = true;

            _log.AppendLine("\n[-] Checking WMI Objects...");
            if (CheckWMI()) isVm = true;
            
            if (isVm)
                _log.AppendLine("\n[!] RESULT: VIRTUAL MACHINE DETECTED!");
            else
                _log.AppendLine("\n[*] RESULT: System appears to be CLEAN.");

            return isVm;
        }

        private static bool CheckRegistry()
        {
            bool found = false;
            string[] keys = {
                @"SOFTWARE\Oracle\VirtualBox Guest Additions",
                @"SOFTWARE\VMware, Inc.\VMware Tools",
                @"HARDWARE\DESCRIPTION\System\SystemBiosVersion"
            };

            foreach (var key in keys)
            {
                using var k = Registry.LocalMachine.OpenSubKey(key);
                if (k != null) 
                {
                    _log.AppendLine($"[!] Found Key: {key} ... DETECTED!");
                    found = true;
                }
                else
                {
                     _log.AppendLine($"[*] Key: {key} ... OK");
                }
            }
            
            // Value checks
            using (var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System"))
            {
                if (key != null)
                {
                    var sysBios = key.GetValue("SystemBiosVersion") as string[] ?? new[] { key.GetValue("SystemBiosVersion")?.ToString() ?? "" };
                    var videoBios = key.GetValue("VideoBiosVersion") as string[] ?? new[] { key.GetValue("VideoBiosVersion")?.ToString() ?? "" };
                    
                    bool biosClean = true;
                    foreach(var s in sysBios)
                    {
                        if (CheckString(s)) 
                        {
                            _log.AppendLine($"[!] SystemBiosVersion artifact: {s} ... DETECTED!");
                            found = true;
                            biosClean = false;
                        }
                    }
                    if(biosClean) _log.AppendLine($"[*] SystemBiosVersion check ... OK");

                    bool videoClean = true;
                     foreach(var s in videoBios)
                    {
                        if (CheckString(s)) 
                        {
                            _log.AppendLine($"[!] VideoBiosVersion artifact: {s} ... DETECTED!");
                            found = true;
                             videoClean = false;
                        }
                    }
                    if(videoClean) _log.AppendLine($"[*] VideoBiosVersion check ... OK");
                }
            }

            return found;
        }

        private static bool CheckString(string s)
        {
             var lower = s.ToLower();
             return lower.Contains("vbox") || lower.Contains("vmware") || lower.Contains("qemu") || lower.Contains("virt");
        }

        private static bool CheckProcesses()
        {
            bool found = false;
            string[] procNames = { 
                "vboxservice", "vboxtray", "vmtoolsd", "vmwaretray", "vmwareuser", 
                "vgauthservice", "vmacthlp", "vmsrvc", "vmusrvc", "prl_cc", "prl_tools", "xenservice" 
            };
            var procs = Process.GetProcesses();
            bool procFoundThisLoop = false;
            foreach (var p in procs)
            {
                if (procNames.Contains(p.ProcessName.ToLower()))
                {
                    _log.AppendLine($"[!] Found VM Process: {p.ProcessName} ... DETECTED!");
                    found = true;
                    procFoundThisLoop = true;
                }
            }
            
            if (!procFoundThisLoop) _log.AppendLine("[*] Checking known VM processes ... OK");

            return found;
        }

        private static bool CheckFiles()
        {
            bool found = false;
            string[] paths = {
                @"C:\windows\system32\drivers\VBoxMouse.sys",
                @"C:\windows\system32\drivers\VBoxGuest.sys",
                @"C:\windows\system32\drivers\VBoxSF.sys",
                @"C:\windows\system32\drivers\VBoxVideo.sys",
                @"C:\windows\system32\vboxdisp.dll",
                @"C:\windows\system32\vboxhook.dll",
                @"C:\windows\system32\vboxmrxnp.dll",
                @"C:\windows\system32\vboxogl.dll",
                @"C:\windows\system32\vboxoglarrayspu.dll",
                @"C:\windows\system32\vboxoglcrutil.dll",
                @"C:\windows\system32\vboxoglerrorspu.dll",
                @"C:\windows\system32\vboxoglfeedbackspu.dll",
                @"C:\windows\system32\vboxoglpackspu.dll",
                @"C:\windows\system32\vboxoglpassthroughspu.dll",
                @"C:\windows\system32\vboxservice.exe",
                @"C:\windows\system32\vboxtray.exe",
                @"C:\windows\system32\VBoxControl.exe",
                @"C:\windows\system32\drivers\vmmouse.sys",
                @"C:\windows\system32\drivers\vmnet.sys",
                @"C:\windows\system32\drivers\vmxnet.sys",
                @"C:\windows\system32\drivers\vmhgfs.sys",
                @"C:\windows\system32\drivers\vmtools.sys",
            };

            foreach (var path in paths)
            {
                if (File.Exists(path))
                {
                    _log.AppendLine($"[!] Found Driver/Tool: {path} ... DETECTED!");
                    found = true;
                }
                else
                {
                     // Typically pafish logs all checked files
                     _log.AppendLine($"[*] File: {path} ... OK");
                }
            }
            return found;
        }

        private static bool CheckMacAddress()
        {
            bool found = false;
            int checkedCount = 0;
            try
            {
                var nics = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
                foreach (var nic in nics)
                {
                    // Only check Ethernet/Wifi, skip Loopback/Tunnel
                    if (nic.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback) continue;

                    var mac = nic.GetPhysicalAddress().ToString().ToUpper();
                    string vendor = "";

                    if (mac.StartsWith("000569") || mac.StartsWith("000C29") || mac.StartsWith("001C14") || mac.StartsWith("005056")) vendor = "VMware";
                    else if (mac.StartsWith("080027")) vendor = "VirtualBox";
                    else if (mac.StartsWith("001C42")) vendor = "Parallels";
                    else if (mac.StartsWith("00163E")) vendor = "Xen";

                    if (!string.IsNullOrEmpty(vendor))
                    {
                        _log.AppendLine($"[!] MAC: {nic.Name} ({mac}) - {vendor} ... DETECTED!");
                        found = true;
                    }
                    else
                    {
                         _log.AppendLine($"[*] MAC: {nic.Name} ({mac}) ... OK");
                    }
                    checkedCount++;
                }
                if (checkedCount == 0) _log.AppendLine("[?] No network interfaces found to check.");
            }
            catch(Exception ex) { 
                _log.AppendLine($"[!] MAC Check Error: {ex.Message}");
            }
            return found;
        }

        private static bool CheckWMI()
        {
            bool found = false;
            try
            {
                 using (var searcher = new ManagementObjectSearcher("SELECT Manufacturer, Model FROM Win32_ComputerSystem"))
                 {
                     foreach (var item in searcher.Get())
                     {
                         string manufacturer = item["Manufacturer"]?.ToString()?.ToLower() ?? "";
                         string model = item["Model"]?.ToString()?.ToLower() ?? "";
                         
                         bool wmiClean = true;

                         if (manufacturer.Contains("microsoft corporation") && model.Contains("virtual")) 
                         {
                             _log.AppendLine($"[!] WMI Manufacturer: {manufacturer} ... DETECTED!");
                             found = true;
                             wmiClean = false;
                         }
                         if (manufacturer.Contains("vmware") || model.Contains("vmware")) 
                         {
                             _log.AppendLine($"[!] WMI Manufacturer/Model: VMware ... DETECTED!");
                             found = true;
                              wmiClean = false;
                         }
                         if (model.Contains("virtualbox"))
                         {
                             _log.AppendLine($"[!] WMI Model: VirtualBox ... DETECTED!");
                             found = true;
                              wmiClean = false;
                         }
                         if (model.Contains("kvm") || model.Contains("qemu") || model.Contains("bochs"))
                         {
                             _log.AppendLine($"[!] WMI Model: QEMU/KVM ... DETECTED!");
                             found = true;
                              wmiClean = false;
                         }

                         if (wmiClean)
                             _log.AppendLine($"[*] WMI System Info ... OK");
                     }
                 }
            }
            catch (Exception ex) {
                 _log.AppendLine($"[!] WMI Check Error: {ex.Message}");
            }
            return found;
        }
    }

    /// <summary>
    /// Launches forensic tools from the apps folder
    /// </summary>
    public static class AppLauncher
    {
        // Use 'apps' folder. Checks both adjacent to EXE and internal bundle extraction path
        public static readonly string AppsFolder = GetAppsFolder();

        private static string GetAppsFolder()
        {
            // 1. Check next to the outer .exe (user-provided assets)
            var outerPath = Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule?.FileName) ?? AppDomain.CurrentDomain.BaseDirectory, "apps");
            if (Directory.Exists(outerPath)) return outerPath;

            // 2. Check in the extraction directory (bundled assets)
            var bundlePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "apps");
            return bundlePath;
        }

        public static bool RunTool(string exeName)
        {
            try
            {
                var path = Path.Combine(AppsFolder, exeName);

                if (!File.Exists(path))
                {
                    MessageBox.Show($"Инструмент не найден:\n{path}\n\nУбедитесь, что папка 'apps' находится рядом с программой.",
                        "Ошибка запуска", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                if (exeName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    // Launch directly since we are Admin
                    var psi = new ProcessStartInfo
                    {
                        FileName = path,
                        UseShellExecute = true,
                        WorkingDirectory = AppsFolder,
                        Verb = "runas" // Request Admin for tools too
                    };
                    Process.Start(psi);
                }
                else
                {
                    Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true, WorkingDirectory = AppsFolder });
                }
                return true;
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to launch {exeName}:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        public static void EnsureAppsFolderExists() { } // No-op
        public static void ExtractEmbeddedTools() { } // No-op
    }

    /// <summary>
    /// Opens system folders
    /// </summary>
    public static class FolderLauncher
    {
        public static void OpenAppData() => OpenFolder(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
        public static void OpenPrefetch() => OpenFolder(@"C:\Windows\Prefetch");
        public static void OpenRecent() => OpenFolder(Environment.GetFolderPath(Environment.SpecialFolder.Recent));
        public static void OpenTemp() => OpenFolder(Path.GetTempPath());
        public static void OpenProgramFiles() => OpenFolder(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
        public static void OpenProgramData() => OpenFolder(@"C:\ProgramData");
        public static void OpenDocuments() => OpenFolder(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        public static void OpenDownloads() => OpenFolder(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"));
        public static void OpenUsers() => OpenFolder(@"C:\Users");
        public static void OpenCrashDumps() => OpenFolder(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrashDumps"));
        public static void OpenReportArchive() => OpenFolder(@"C:\ProgramData\Microsoft\Windows\WER\ReportArchive");

        public static void OpenFolder(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Process.Start("explorer.exe", path);
                else
                    MessageBox.Show($"Folder not found:\n{path}", "Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    /// <summary>
    /// Opens websites in default browser
    /// </summary>
    public static class SiteLauncher
    {
        public static void OpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true }); } catch { }
        }
    }

    /// <summary>
    /// Legacy process scanner (kept for compatibility)
    /// </summary>
    public static class ProcessScanner
    {
        public static ScanResult Scan()
        {
            var result = new ScanResult();

            try
            {
                var processes = Process.GetProcesses();
                result.TotalProcesses = processes.Length;

                foreach (var process in processes)
                {
                    try
                    {
                        var name = process.ProcessName.ToLowerInvariant();

                        foreach (var sig in CheatDatabase.Signatures)
                        {
                            var sigLower = sig.ToLowerInvariant().Replace(" ", "");
                            if (name.Contains(sigLower))
                            {
                                result.ThreatsFound++;
                                if (!result.DetectedThreats.Contains(process.ProcessName))
                                    result.DetectedThreats.Add(process.ProcessName);
                                break;
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }

            return result;
        }
    }

    /// <summary>
    /// Professional USB history scanner using external USBDeview tool
    /// </summary>
    public static class UsbScanner
    {
        // Device types/services to filter out (internal devices)
        private static readonly string[] ExcludedServices = new[]
        {
            "usbhub", "usbhub3", "usbccgp", 
            "iusb3hub", "usbehci", "usbxhci", "usbuhci", "usbohci",
            "RootHub", "pci", "ACPI", "volume", "partmgr", "disk" // System stuff
        };
        
        // Only exclude very obvious non-storage devices
        private static readonly string[] ExcludedDescPatterns = new[]
        {
            "root hub", "host controller"
        };

        public static List<UsbDeviceInfo> GetUsbHistory()
        {
            var devices = new List<UsbDeviceInfo>();
            
            // Try multiple paths for USBDeview
            string[] possiblePaths = new[]
            {
                Path.Combine(AppLauncher.AppsFolder, "USBDeview.exe"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "apps", "USBDeview.exe"),
                @"C:\Users\Naze\Downloads\Новая папка (3)\apps\USBDeview.exe"
            };
            
            string usbDeviewPath = possiblePaths.FirstOrDefault(File.Exists) ?? "";
            string tempXml = Path.Combine(Path.GetTempPath(), $"usb_history_{Guid.NewGuid():N}.xml");

            try
            {
                if (File.Exists(usbDeviewPath))
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = usbDeviewPath,
                        Arguments = $"/sxml \"{tempXml}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };

                    using (var process = Process.Start(startInfo))
                    {
                        process?.WaitForExit(10000); 
                    }

                    if (File.Exists(tempXml))
                    {
                        var xmlDoc = new System.Xml.XmlDocument();
                        xmlDoc.Load(tempXml);
                        var items = xmlDoc.SelectNodes("//item");

                        if (items != null)
                        {
                            foreach (System.Xml.XmlNode item in items)
                            {
                                string GetVal(params string[] tags) {
                                    foreach (var t in tags) {
                                        var v = item.SelectSingleNode(t)?.InnerText?.Trim();
                                        if (!string.IsNullOrEmpty(v)) return v;
                                    }
                                    return "";
                                }

                                var description = GetVal("description", "friendly_name", "device_name");
                                var technicalName = GetVal("device_name", "instance_id");
                                var drive = GetVal("drive_letter");
                                var connectedStr = GetVal("connected");
                                bool connected = connectedStr.Equals("Yes", StringComparison.OrdinalIgnoreCase);
                                
                                var installDate = GetVal("install_date", "install_time", "registry_time_1");
                                var disconnectDate = GetVal("disconnect_date", "disconnect_time", "registry_time_2", "last_plug_unplug_date");
                                var serialNumber = GetVal("serial_number", "serialnumber", "instance_id");

                                var vid = GetVal("vendorid", "vendor_id", "vid");
                                var pid = GetVal("productid", "product_id", "pid");
                                var service = GetVal("service_name");
                                var deviceType = GetVal("device_type");

                                // REMOVED RESTRICTIVE FILTER:
                                // Was: if (connected && string.IsNullOrEmpty(drive)) continue; 
                                // Now we embrace all devices.
                                
                                // Also filter out internal devices by description patterns
                                var descLower = description.ToLower() + " " + technicalName.ToLower() + " " + deviceType.ToLower();
                                if (ExcludedDescPatterns.Any(p => descLower.Contains(p)))
                                    continue;
                                
                                // Skip devices without VID/PID (usually internal)
                                if (string.IsNullOrEmpty(vid) && string.IsNullOrEmpty(pid))
                                    continue;

                                if (vid.Length > 4 && vid.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) vid = vid.Substring(2);
                                if (pid.Length > 4 && pid.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) pid = pid.Substring(2);
                                vid = vid.ToUpper().Trim();
                                pid = pid.ToUpper().Trim();

                                // Clean serial number
                                if (serialNumber.Contains("&")) 
                                    serialNumber = serialNumber.Split('&').LastOrDefault() ?? "";
                                if (serialNumber.Length > 20)
                                    serialNumber = serialNumber.Substring(0, 20) + "...";

                                var displayName = description;
                                if (!string.IsNullOrEmpty(drive)) displayName = $"({drive}) {displayName}";

                                devices.Add(new UsbDeviceInfo
                                {
                                    Name = displayName,
                                    Description = deviceType,
                                    DriveLetter = drive,
                                    VID = vid,
                                    PID = pid,
                                    SerialNumber = serialNumber,
                                    IsConnected = connected,
                                    InstallDate = installDate,
                                    DisconnectDate = connected ? "" : disconnectDate
                                });
                            }
                        }
                    }
                }
            }
            catch { }
            finally
            {
                if (File.Exists(tempXml)) try { File.Delete(tempXml); } catch { }
            }

            // Sort and assign index
            var sorted = devices
                .OrderByDescending(d => d.IsConnected)
                .ThenByDescending(d => !string.IsNullOrEmpty(d.InstallDate) ? d.InstallDate : "")
                .ToList();
            
            // Fallback to Native Scan if USBDeview failed or returned nothing
            if (sorted.Count == 0)
            {
                sorted = GetUsbHistoryNative();
            }
            
            for (int i = 0; i < sorted.Count; i++)
                sorted[i].DeviceIndex = i + 1;

            return sorted;
        }

        private static List<UsbDeviceInfo> GetUsbHistoryNative()
        {
            var list = new List<UsbDeviceInfo>();
            try
            {
                // Scan USBSTOR for generic storage devices
                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\USBSTOR");
                if (key != null)
                {
                    foreach (var deviceId in key.GetSubKeyNames())
                    {
                        using var deviceKey = key.OpenSubKey(deviceId);
                        if (deviceKey == null) continue;

                        foreach (var instanceId in deviceKey.GetSubKeyNames())
                        {
                            using var instanceKey = deviceKey.OpenSubKey(instanceId);
                            if (instanceKey == null) continue;

                            var friendlyName = instanceKey.GetValue("FriendlyName")?.ToString();
                            if (string.IsNullOrEmpty(friendlyName))
                            {
                                // Try DeviceDesc fallback
                                var desc = instanceKey.GetValue("DeviceDesc")?.ToString();
                                if (!string.IsNullOrEmpty(desc))
                                {
                                    // Often format is "@oemXX.inf,%ClassName%;Friendly Name"
                                    var parts = desc.Split(';');
                                    friendlyName = parts.Last();
                                }
                            }

                            if (string.IsNullOrEmpty(friendlyName)) friendlyName = deviceId;

                            list.Add(new UsbDeviceInfo
                            {
                                Name = friendlyName,
                                Description = "USB Storage",
                                SerialNumber = instanceId, // The instance ID is usually the serial + suffix
                                VID = "N/A", 
                                PID = "N/A",
                                IsConnected = false, // Cannot easily determine from registry alone without setupapi
                                InstallDate = "Unknown", 
                                DisconnectDate = ""
                            });
                        }
                    }
                }
            }
            catch { }
            return list;
        }
    }

    public static class DiscordService
    {
        private static DiscordRpcClient? _client;
        private static Timestamps? _startTime;

        public static void Initialize(string clientId)
        {
            if (_client != null) return;

            _client = new DiscordRpcClient(clientId);
            _startTime = Timestamps.Now;

            _client.Logger = new ConsoleLogger() { Level = LogLevel.Warning };

            _client.OnReady += (sender, e) =>
            {
                Debug.WriteLine($"Discord RPC Ready: {e.User.Username}");
            };

            _client.Initialize();

            // Set static presence immediately
            UpdatePresence();
        }

        public static void UpdatePresence(string? details = "📋 Проверка на читы", string? state = null)
        {
            if (_client == null || !_client.IsInitialized) return;

            _client.SetPresence(new RichPresence()
            {
                Details = details,
                State = state,
                Assets = new Assets()
                {
                    LargeImageKey = "logo",
                    LargeImageText = "🌀 Sayurin Checker", // Tooltip
                    SmallImageKey = "info",
                    SmallImageText = "Information"
                },
                Timestamps = _startTime
            });
        }

        public static void Deinitialize()
        {
            _client?.Dispose();
            _client = null;
        }
    }
}
