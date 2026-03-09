using System;
using System.Runtime.InteropServices;

namespace BazaChecker.Services
{
    /// <summary>
    /// Native interop wrapper for SayurinCore.dll
    /// Provides protected C++ implementations of all scanner functions
    /// </summary>
    public static class NativeInterop
    {
        private const string DllName = "SayurinCore.dll";

        #region Memory Management
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void SC_FreeString(IntPtr str);
        #endregion

        #region VM Detection
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool SC_IsVirtualMachine();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr SC_GetVmDetectionLog();

        public static string GetVmDetectionLog()
        {
            IntPtr ptr = SC_GetVmDetectionLog();
            string result = Marshal.PtrToStringAnsi(ptr) ?? "[]";
            SC_FreeString(ptr);
            return result;
        }
        #endregion

        #region System Checks
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool SC_IsAdmin();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int SC_GetMonitorCount();
        #endregion

        #region System Information
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr SC_GetCPU();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr SC_GetGPU();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr SC_GetRAM();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr SC_GetOS();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr SC_GetSystemSummary();

        public static string GetCPU()
        {
            IntPtr ptr = SC_GetCPU();
            string result = Marshal.PtrToStringAnsi(ptr) ?? "";
            SC_FreeString(ptr);
            return result;
        }

        public static string GetGPU()
        {
            IntPtr ptr = SC_GetGPU();
            string result = Marshal.PtrToStringAnsi(ptr) ?? "";
            SC_FreeString(ptr);
            return result;
        }

        public static string GetRAM()
        {
            IntPtr ptr = SC_GetRAM();
            string result = Marshal.PtrToStringAnsi(ptr) ?? "";
            SC_FreeString(ptr);
            return result;
        }

        public static string GetOS()
        {
            IntPtr ptr = SC_GetOS();
            string result = Marshal.PtrToStringAnsi(ptr) ?? "";
            SC_FreeString(ptr);
            return result;
        }

        public static string GetSystemSummary()
        {
            IntPtr ptr = SC_GetSystemSummary();
            string result = Marshal.PtrToStringAnsi(ptr) ?? "{}";
            SC_FreeString(ptr);
            return result;
        }
        #endregion

        #region Registry Scanner
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr SC_ScanRegistry();

        /// <summary>
        /// Scans Windows Registry for installed/deleted programs
        /// Returns JSON array of program info
        /// </summary>
        public static string ScanRegistry()
        {
            IntPtr ptr = SC_ScanRegistry();
            string result = Marshal.PtrToStringAnsi(ptr) ?? "[]";
            SC_FreeString(ptr);
            return result;
        }
        #endregion

        #region Steam Scanner
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr SC_GetSteamAccounts();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr SC_EnrichSteamAccount([MarshalAs(UnmanagedType.LPStr)] string steamId64);

        /// <summary>
        /// Gets Steam accounts from loginusers.vdf
        /// Returns JSON array
        /// </summary>
        public static string GetSteamAccounts()
        {
            IntPtr ptr = SC_GetSteamAccounts();
            string result = Marshal.PtrToStringAnsi(ptr) ?? "[]";
            SC_FreeString(ptr);
            return result;
        }

        /// <summary>
        /// Enriches a Steam account with data from csst.at
        /// Returns JSON object
        /// </summary>
        public static string EnrichSteamAccount(string steamId64)
        {
            IntPtr ptr = SC_EnrichSteamAccount(steamId64);
            string result = Marshal.PtrToStringAnsi(ptr) ?? "{}";
            SC_FreeString(ptr);
            return result;
        }
        #endregion

        #region USB Scanner
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr SC_GetUsbHistory();

        /// <summary>
        /// Gets USB device history from registry
        /// Returns JSON array
        /// </summary>
        public static string GetUsbHistory()
        {
            IntPtr ptr = SC_GetUsbHistory();
            string result = Marshal.PtrToStringAnsi(ptr) ?? "[]";
            SC_FreeString(ptr);
            return result;
        }
        #endregion

        #region Disk Scanner
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr SC_ScanDisks();

        /// <summary>
        /// Scans user directories for cheat files
        /// Returns JSON array of threats
        /// </summary>
        public static string ScanDisks()
        {
            IntPtr ptr = SC_ScanDisks();
            string result = Marshal.PtrToStringAnsi(ptr) ?? "[]";
            SC_FreeString(ptr);
            return result;
        }
        #endregion

        #region Cheat Database
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr SC_GetCheatSignatures();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool SC_IsCheatSignature([MarshalAs(UnmanagedType.LPStr)] string name);

        public static string GetCheatSignatures()
        {
            IntPtr ptr = SC_GetCheatSignatures();
            string result = Marshal.PtrToStringAnsi(ptr) ?? "[]";
            SC_FreeString(ptr);
            return result;
        }

        public static bool IsCheatSignature(string name)
        {
            return SC_IsCheatSignature(name);
        }
        #endregion
    }
}
