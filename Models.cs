using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Media;

namespace BazaChecker.Models
{
    public enum NotificationType
    {
        Success,
        Error,
        Warning
    }

    public class NotificationItem : INotifyPropertyChanged
    {
        private bool _isClosing;
        private double _opacityValue = 1.0;
        private double _slideX = 0;
        
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Icon { get; set; } = "✅";
        public NotificationType Type { get; set; } = NotificationType.Success;
        public bool IsPersistent { get; set; } = false;

        public bool IsClosing
        {
            get => _isClosing;
            set
            {
                _isClosing = value;
                OnPropertyChanged(nameof(IsClosing));
            }
        }
        
        public double OpacityValue
        {
            get => _opacityValue;
            set
            {
                _opacityValue = value;
                OnPropertyChanged(nameof(OpacityValue));
            }
        }
        
        public double SlideX
        {
            get => _slideX;
            set
            {
                _slideX = value;
                OnPropertyChanged(nameof(SlideX));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    /// <summary>
    /// Status of a program in the registry
    /// </summary>
    public enum ProgramStatus
    {
        Installed,
        Deleted,
        Trace
    }

    /// <summary>
    /// Represents a program found in the Windows Registry
    /// </summary>
    public class ProgramInfo : INotifyPropertyChanged
    {
        public string DisplayName { get; set; } = string.Empty;
        public string InstallPath { get; set; } = string.Empty;
        public string Publisher { get; set; } = string.Empty;
        public ProgramStatus Status { get; set; }
        public bool IsHistory { get; set; }

        // UI Helpers
        public string StatusText => Status switch
        {
            ProgramStatus.Installed => "INSTALLED",
            ProgramStatus.Deleted => "DELETED",
            ProgramStatus.Trace => "TRACE",
            _ => "UNKNOWN"
        };

        public SolidColorBrush StatusColor => Status switch
        {
            ProgramStatus.Installed => new SolidColorBrush(Color.FromRgb(34, 197, 94)),   // Green
            ProgramStatus.Deleted => new SolidColorBrush(Color.FromRgb(239, 68, 68)),     // Red
            ProgramStatus.Trace => new SolidColorBrush(Color.FromRgb(249, 115, 22)),      // Orange
            _ => Brushes.Gray
        };

        public SolidColorBrush StatusBackground => Status switch
        {
            ProgramStatus.Installed => new SolidColorBrush(Color.FromArgb(38, 34, 197, 94)),
            ProgramStatus.Deleted => new SolidColorBrush(Color.FromArgb(38, 239, 68, 68)),
            ProgramStatus.Trace => new SolidColorBrush(Color.FromArgb(38, 249, 115, 22)),
            _ => new SolidColorBrush(Color.FromArgb(38, 128, 128, 128))
        };

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Represents a Steam account found on the PC
    /// </summary>
    public class SteamAccount : INotifyPropertyChanged
    {
        public string SteamId64 { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string PersonaName { get; set; } = string.Empty;
        public string AvatarUrl { get; set; } = "https://avatars.steamstatic.com/fef49e7fa7e1997310d705b2a6158ff8dc1cdfeb_medium.jpg";
        public bool IsVacBanned { get; set; }
        public bool IsActive { get; set; }
        public long LastLogin { get; set; }

        // Ban history from Steam API
        public int NumberOfVACBans { get; set; }
        public int NumberOfGameBans { get; set; }
        public int DaysSinceLastBan { get; set; }
        public bool CommunityBanned { get; set; }
        public bool EconomyBan { get; set; }
        public List<BanEntry> BanHistory { get; set; } = new();

        // Baza Integration
        private bool _isBazaLoading;
        public bool IsBazaLoading
        {
            get => _isBazaLoading;
            set { _isBazaLoading = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsBazaLoading))); }
        }

        private string _bazaAdminStatus = "Unknown";
        public string BazaAdminStatus
        {
            get => _bazaAdminStatus;
            set { _bazaAdminStatus = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BazaAdminStatus))); }
        }

        private string _bazaBanInfo = "Not checked";
        public string BazaBanInfo
        {
            get => _bazaBanInfo;
            set { _bazaBanInfo = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BazaBanInfo))); }
        }

        private List<BazaBanEntry> _bazaBans = new();
        public List<BazaBanEntry> BazaBans
        {
            get => _bazaBans;
            set 
            { 
                _bazaBans = value; 
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BazaBans))); 
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasBans)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BazaDetailVisibility)));
            }
        }

        private List<BazaBanEntry> _bazaMutes = new();
        public List<BazaBanEntry> BazaMutes
        {
            get => _bazaMutes;
            set 
            { 
                _bazaMutes = value; 
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BazaMutes))); 
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasMutes)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BazaDetailVisibility)));
            }
        }

        public bool HasBans => BazaBans != null && BazaBans.Count > 0;
        public bool HasMutes => BazaMutes != null && BazaMutes.Count > 0;

        public System.Windows.Visibility BazaDetailVisibility => 
            (HasBans || HasMutes) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        // UI state
        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                _isExpanded = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BanPanelVisibility)));
            }
        }

        // UI Helpers
        public string VacStatusText => IsVacBanned ? "VAC BAN" : "✓ NO VAC";
        public string ActiveStatusText => IsActive ? "Активен" : "Неактивен";

        public SolidColorBrush VacColor => IsVacBanned
            ? new SolidColorBrush(Color.FromRgb(239, 68, 68))   // Red
            : new SolidColorBrush(Color.FromRgb(34, 197, 94));  // Green

        public SolidColorBrush ActiveColor => IsActive
            ? new SolidColorBrush(Color.FromRgb(168, 85, 247))   // Purple
            : new SolidColorBrush(Color.FromRgb(107, 114, 128)); // Gray

        public System.Windows.Visibility BanPanelVisibility => 
            IsExpanded ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        public string BanSummary
        {
            get
            {
                if (NumberOfVACBans == 0 && NumberOfGameBans == 0)
                    return "Нет банов";
                var parts = new List<string>();
                if (NumberOfVACBans > 0) parts.Add($"{NumberOfVACBans} VAC бан(ов)");
                if (NumberOfGameBans > 0) parts.Add($"{NumberOfGameBans} игровых бан(ов)");
                if (DaysSinceLastBan > 0) parts.Add($"{DaysSinceLastBan} дней назад");
                return string.Join(" | ", parts);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    /// <summary>
    /// Represents a single punishment entry from Baza-CS2
    /// </summary>
    public class BazaBanEntry
    {
        public string Type { get; set; } = "Unknown"; // Chat, Voice, Ban
        public string Date { get; set; } = "";
        public string Reason { get; set; } = "";
        public string Admin { get; set; } = "";
        public string Duration { get; set; } = "";
        public string Status { get; set; } = "";

        // UI Properties
        public string IconData { get; set; } = ""; // SVG Path Data
        public string IconColor { get; set; } = "#888888"; // Hex Color
        public bool IsImage { get; set; } = false; // True if using PNG/Image instead of SVG
    }

    /// <summary>
    /// Represents a single ban entry in history
    /// </summary>
    public class BanEntry
    {
        public string Type { get; set; } = "VAC"; // VAC, Game Ban, Community
        public DateTime Date { get; set; }
        public string Game { get; set; } = "Unknown";
        public int DaysAgo => (DateTime.Now - Date).Days;

        public SolidColorBrush TypeColor => Type switch
        {
            "VAC" => new SolidColorBrush(Color.FromRgb(239, 68, 68)),
            "Game Ban" => new SolidColorBrush(Color.FromRgb(249, 115, 22)),
            _ => new SolidColorBrush(Color.FromRgb(107, 114, 128))
        };
    }

    /// <summary>
    /// System information summary
    /// </summary>
    public class SystemSummary
    {
        public string OSName { get; set; } = "Unknown";
        public string OSVersion { get; set; } = string.Empty;
        public DateTime InstallDate { get; set; }
        public int MonitorCount { get; set; } = 1;
        public DateTime LastBootTime { get; set; }
        public string MachineName { get; set; } = Environment.MachineName;
        public string UserName { get; set; } = Environment.UserName;
        
        // Extended hardware info
        public string CPU { get; set; } = "Unknown";
        public string GPU { get; set; } = "Unknown";
        public string RAM { get; set; } = "Unknown";
        public string Motherboard { get; set; } = "Unknown";
        public DateTime SessionStartTime { get; set; } = DateTime.Now;

        // UI Helpers
        public string InstallDateText => InstallDate.ToString("dd.MM.yyyy");
        public string LastBootText => LastBootTime.ToString("dd.MM.yyyy HH:mm");
        public string SessionTimeText => SessionStartTime.ToString("dd.MM.yyyy HH:mm");
        public bool IsVirtualMachine { get; set; } = false;
        public string IsVirtualMachineText => IsVirtualMachine ? "Да" : "Нет";
    }

    /// <summary>
    /// Represents a forensic tool in the apps folder
    /// </summary>
    public class ToolInfo
    {
        public string Name { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Icon { get; set; } = "🔧";
        public bool IsAvailable { get; set; }
    }

    /// <summary>
    /// Represents a quick link site
    /// </summary>
    public class SiteLink
    {
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Icon { get; set; } = "🌐";
    }

    /// <summary>
    /// Scan result summary
    /// </summary>
    public class ScanResult
    {
        public int TotalProcesses { get; set; }
        public int ThreatsFound { get; set; }
        public List<string> DetectedThreats { get; set; } = new();
        public DateTime ScanTime { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Represents a USB device from the registry history
    /// </summary>
    public class UsbDeviceInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string DriveLetter { get; set; } = string.Empty;
        public string InstallDate { get; set; } = string.Empty;
        public string LastConnected { get; set; } = string.Empty;
        public string DisconnectDate { get; set; } = string.Empty;
        public string VID { get; set; } = string.Empty;
        public string PID { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public int DeviceIndex { get; set; }
        public bool IsConnected { get; set; }

        // UI Helpers
        public SolidColorBrush StatusColor => IsConnected
            ? new SolidColorBrush(Color.FromRgb(0, 255, 102))   // Bright Neon Green
            : new SolidColorBrush(Color.FromRgb(100, 100, 110)); // Visible Light Gray
        
        public SolidColorBrush StatusBackground => IsConnected
            ? new SolidColorBrush(Color.FromRgb(34, 197, 94))   // Green
            : new SolidColorBrush(Color.FromRgb(168, 85, 247)); // Purple
        
        public string StatusText => IsConnected ? "Подключено" : "Отключено";
        
        public SolidColorBrush CardBorderBrush => new SolidColorBrush(Color.FromRgb(168, 85, 247)); // Purple left border
    }
}
