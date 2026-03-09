using System;
using System.Collections.Generic;
using System.Text.Json;
using BazaChecker.Services;
using BazaChecker.Models;

namespace BazaChecker
{
    /// <summary>
    /// Hybrid scanner wrappers that use the native C++ DLL (SayurinCore.dll)
    /// for protected logic, while returning existing C# model objects.
    /// 
    /// To use: Replace direct calls to RegistryScanner/SteamScanner with these.
    /// Example: NativeScanners.ScanRegistry() instead of RegistryScanner.GetPrograms()
    /// </summary>
    public static class NativeScanners
    {
        #region VM Detection
        
        /// <summary>
        /// Check if running in a virtual machine (uses C++ DLL)
        /// </summary>
        public static bool IsVirtualMachine()
        {
            try
            {
                return NativeInterop.SC_IsVirtualMachine();
            }
            catch
            {
                // Fallback to C# implementation if DLL fails
                return VmDetector.IsVirtualMachine();
            }
        }

        /// <summary>
        /// Get VM detection log (uses C++ DLL)
        /// </summary>
        public static List<string> GetVmDetectionLog()
        {
            try
            {
                string json = NativeInterop.GetVmDetectionLog();
                return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            }
            catch
            {
                // Fallback to C# implementation - wrap string in list
                var log = VmDetector.GetDetectionLog();
                return new List<string> { log };
            }
        }

        #endregion

        #region System Checks

        /// <summary>
        /// Check if running as administrator (uses C++ DLL)
        /// </summary>
        public static bool IsAdmin()
        {
            try
            {
                return NativeInterop.SC_IsAdmin();
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get monitor count (uses C++ DLL)
        /// </summary>
        public static int GetMonitorCount()
        {
            try
            {
                return NativeInterop.SC_GetMonitorCount();
            }
            catch
            {
                return 1;
            }
        }

        #endregion

        #region Registry Scanner

        /// <summary>
        /// Scan registry and return ProgramInfo list (uses C++ DLL)
        /// </summary>
        public static List<ProgramInfo> ScanRegistry()
        {
            try
            {
                string json = NativeInterop.ScanRegistry();
                var nativePrograms = JsonSerializer.Deserialize<List<NativeProgramInfo>>(json) ?? new List<NativeProgramInfo>();
                
                var result = new List<ProgramInfo>();
                foreach (var np in nativePrograms)
                {
                    result.Add(new ProgramInfo
                    {
                        DisplayName = np.displayName ?? "",
                        InstallPath = np.installPath ?? "",
                        Status = np.status switch
                        {
                            "deleted" => ProgramStatus.Deleted,
                            "trace" => ProgramStatus.Trace,
                            _ => ProgramStatus.Installed
                        },
                        IsHistory = np.isHistory
                    });
                }
                return result;
            }
            catch
            {
                // Fallback to C# implementation
                return RegistryScanner.GetPrograms();
            }
        }

        // Internal class for JSON deserialization
        private class NativeProgramInfo
        {
            public string? displayName { get; set; }
            public string? installPath { get; set; }
            public string? installDate { get; set; }
            public string? status { get; set; }
            public bool isHistory { get; set; }
            public bool isCheat { get; set; }
        }

        #endregion

        #region Steam Scanner

        /// <summary>
        /// Get Steam accounts (uses C++ DLL)
        /// </summary>
        public static List<SteamAccount> GetSteamAccounts()
        {
            try
            {
                string json = NativeInterop.GetSteamAccounts();
                var nativeAccounts = JsonSerializer.Deserialize<List<NativeSteamAccount>>(json) ?? new List<NativeSteamAccount>();
                
                var result = new List<SteamAccount>();
                foreach (var na in nativeAccounts)
                {
                    result.Add(new SteamAccount
                    {
                        SteamId64 = na.steamId64 ?? "",
                        AccountName = na.accountName ?? "",
                        PersonaName = na.personaName ?? na.accountName ?? "",
                        IsActive = na.isActive
                    });
                }
                return result;
            }
            catch
            {
                // Fallback to C# implementation
                return SteamScanner.GetAccounts();
            }
        }

        // Internal class for JSON deserialization
        private class NativeSteamAccount
        {
            public string? steamId64 { get; set; }
            public string? accountName { get; set; }
            public string? personaName { get; set; }
            public bool isActive { get; set; }
        }

        #endregion

        #region USB Scanner

        /// <summary>
        /// Get USB device history (uses C++ DLL)
        /// </summary>
        public static List<UsbDeviceInfo> GetUsbHistory()
        {
            try
            {
                string json = NativeInterop.GetUsbHistory();
                var nativeDevices = JsonSerializer.Deserialize<List<NativeUsbDevice>>(json) ?? new List<NativeUsbDevice>();
                
                var result = new List<UsbDeviceInfo>();
                foreach (var nd in nativeDevices)
                {
                    result.Add(new UsbDeviceInfo
                    {
                        Name = nd.name ?? "Unknown Device",
                        SerialNumber = nd.serialNumber ?? ""
                    });
                }
                return result;
            }
            catch
            {
                // Fallback to C# implementation
                return UsbScanner.GetUsbHistory();
            }
        }

        // Internal class for JSON deserialization
        private class NativeUsbDevice
        {
            public string? name { get; set; }
            public string? serialNumber { get; set; }
        }

        #endregion

        #region System Info

        /// <summary>
        /// Get system summary using native DLL
        /// </summary>
        public static SystemSummary GetSystemSummary()
        {
            try
            {
                var summary = new SystemSummary
                {
                    CPU = NativeInterop.GetCPU(),
                    GPU = NativeInterop.GetGPU(),
                    RAM = NativeInterop.GetRAM(),
                    OSName = NativeInterop.GetOS(),
                    IsVirtualMachine = IsVirtualMachine(),
                    MonitorCount = GetMonitorCount()
                };
                return summary;
            }
            catch
            {
                // Fallback to C# implementation
                return SystemInfoProvider.GetSummary();
            }
        }

        #endregion
    }
}
