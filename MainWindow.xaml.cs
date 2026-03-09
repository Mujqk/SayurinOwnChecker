using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Diagnostics;
using BazaChecker.Models;
using BazaChecker.Services;
using System.Net;
using System.Net.Http;
using System.Windows.Media.Animation;
using System.Text.RegularExpressions;
using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Interop;

namespace BazaChecker
{
    public partial class MainWindow : Window
    {
        // P/Invoke for Macro Check
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        private const int SW_RESTORE = 9;
        private const int SW_MINIMIZE = 6;
        private const int KEYEVENTF_KEYUP = 0x0002;

        // Data caches
        private List<ProgramInfo> _allPrograms = new List<ProgramInfo>();
        private List<SteamAccount> _accounts = new List<SteamAccount>();
        private SystemSummary? _systemSummary;
        private List<DiskInfo> _disks = new List<DiskInfo>();

        // Scan cancellation
        private CancellationTokenSource? _scanCts;
        private bool _isScanning = false;

        // Notification system
        private ObservableCollection<NotificationItem> _notifications = new ObservableCollection<NotificationItem>();
        private System.Windows.Threading.DispatcherTimer? _monitorCheckTimer;
        private NotificationItem? _monitorWarning;
        private NotificationItem? _recordingWarning;
        
        // List of prohibited recording/streaming software
        private readonly HashSet<string> _prohibitedProcesses = new(StringComparer.OrdinalIgnoreCase)
        {
            "obs64", "obs32", "obs-browser-page", "streamlabs obs", "streamlabs obs-browser-page",
            "NVIDIA Share", 
            "gamebar", "GameBarFTServer",
            "bdcam", "Bandicam",
            "Fraps",
            "XSplit.Core", "XSplit.Gamecaster",
            "Medal", "MedalEncoder", "MedalRecorder",
            "Action_x64", "Action",
            // Capture Cards
            "CameraHub", "Elgato Camera Hub", "ControlCenter", // Elgato
            "RECentral", "RECentral 4", "AVerMedia RECentral",
            "PrismLiveStudio"
        };

        private async void ShowNotification(string message, string title = "Успешно", NotificationType type = NotificationType.Success, bool persistent = false)
        {
            var item = new NotificationItem 
            { 
                Title = title, 
                Message = message, 
                Icon = type switch 
                {
                    NotificationType.Error => "❌",
                    NotificationType.Warning => "⚠",
                    _ => "✅"
                },
                Type = type,
                IsPersistent = persistent
            };
            
            _notifications.Add(item);

            if (!persistent)
            {
                // Auto-remove after 3 seconds
                await Task.Delay(3000);
                if (_notifications.Contains(item))
                {
                    // Smooth fade out animation
                    await AnimateNotificationClose(item);
                    if (_notifications.Contains(item)) _notifications.Remove(item);
                }
            }
        }
        
        private async Task AnimateNotificationClose(NotificationItem item)
        {
            item.IsClosing = true;
            const int steps = 15;
            const int duration = 300; // 300ms total
            int delay = duration / steps;
            
            for (int i = 0; i < steps; i++)
            {
                double progress = (double)(i + 1) / steps;
                // Ease out quad
                double eased = 1 - (1 - progress) * (1 - progress);
                item.OpacityValue = 1.0 - eased;
                // No slide, just fade
                await Task.Delay(delay);
            }
        }

        private async void CloseNotification_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Guid id)
            {
                var item = _notifications.FirstOrDefault(n => n.Id == id);
                if (item != null && !item.IsClosing)
                {
                    await AnimateNotificationClose(item);
                    if (_notifications.Contains(item)) _notifications.Remove(item);
                }
            }
        }

        // Navigation buttons for styling
        private Button[] _navButtons = Array.Empty<Button>();
        private Grid[] _pages = Array.Empty<Grid>();

        public MainWindow()
        {
            InitializeComponent();
            NotificationStack.ItemsSource = _notifications;
            _currentPage = PagePCCheck;
            // Start with transparent window for fade-in effect
            this.Opacity = 0;
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
            PreviewKeyDown += MainWindow_PreviewKeyDown;
        }

        public void InjectPreloadedData(List<ProgramInfo> programs, List<SteamAccount> accounts)
        {
            _allPrograms = programs;
            _accounts = accounts;
        }

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Back)
            {
                // Check if mouse is over a notification
                var element = Mouse.DirectlyOver as FrameworkElement;
                if (element != null)
                {
                    // Walk up to find the DataContext
                    var current = element;
                    while (current != null)
                    {
                        if (current.DataContext is NotificationItem item)
                        {
                            // Found it! Close it force-fully.
                            DismissNotification(item);
                            e.Handled = true;
                            return;
                        }
                        current = VisualTreeHelper.GetParent(current) as FrameworkElement;
                        // Stop if we hit the ItemsControl or higher to prevent false positives? 
                        // Actually DataContext check is specific enough for NotificationItem type.
                    }
                }
            }
        }

        private async void DismissNotification(NotificationItem item)
        {
            if (item.IsClosing) return;
            await AnimateNotificationClose(item);
            if (_notifications.Contains(item)) _notifications.Remove(item);
            
            // Allow re-triggering warnings if dismissed manually
            if (item == _monitorWarning) _monitorWarning = null;
            if (item == _recordingWarning) _recordingWarning = null;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Adjust window size for smaller screens
            var screenWidth = SystemParameters.PrimaryScreenWidth;
            var screenHeight = SystemParameters.PrimaryScreenHeight;
            
            if (screenWidth < 1400 || screenHeight < 830)
            {
                // Scale down to fit screen with some margin
                this.Width = Math.Min(1400, screenWidth - 50);
                this.Height = Math.Min(830, screenHeight - 80);
                
                // Center on screen
                this.Left = (screenWidth - this.Width) / 2;
                this.Top = (screenHeight - this.Height) / 2;
            }

            // Fade-in animation
            var fadeIn = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };
            this.BeginAnimation(OpacityProperty, fadeIn);

            try
            {
                _navButtons = new[] { NavPCCheck, NavRegistry, NavAccounts, NavUSB, NavPrograms, NavFolders, NavSites, NavAdditional, NavExtra };
                _pages = new[] { PagePCCheck, PageRegistry, PageAccounts, PageUSB, PagePrograms, PageFolders, PageSites, PageAdditional, PageExtra };

                CheckAdminStatus();
                AppLauncher.ExtractEmbeddedTools();

                // Load available disks
                LoadDisks();

                await LoadSystemInfoAsync();

                // Initialize Discord RPC
                DiscordService.Initialize("1463981045555789825");

                // Monitor check timer (every 5 seconds)
                _monitorCheckTimer = new System.Windows.Threading.DispatcherTimer();
                _monitorCheckTimer.Interval = TimeSpan.FromSeconds(5);
                _monitorCheckTimer.Tick += (s, e) => 
                {
                    CheckMonitorStatus();
                    CheckRecordingStatus();
                };
                _monitorCheckTimer.Start();
                CheckMonitorStatus();
                CheckRecordingStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Startup Error: {ex.Message}\nStack: {ex.StackTrace}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            DiscordService.Deinitialize();
        }

        private void LoadDisks()
        {
            _disks = DiskScanner.GetAvailableDisks();
            DiskList.ItemsSource = _disks;
        }

        #region Admin Check

        private void CheckAdminStatus()
        {
            bool isAdmin = false;
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { }

            if (isAdmin)
            {
                AdminStatus.Text = "✓ ADMIN MODE";
                AdminStatus.Foreground = (System.Windows.Media.Brush)FindResource("SuccessBrush");
            }
            else
            {
                AdminStatus.Text = "⚠ LIMITED MODE";
                AdminStatus.Foreground = (System.Windows.Media.Brush)FindResource("DangerBrush");
            }
        }

        #endregion

        #region Monitor & Record Check

        private void CheckMonitorStatus()
        {
            try
            {
                bool isExtended = System.Windows.Forms.Screen.AllScreens.Length > 1;

                if (isExtended && _monitorWarning == null)
                {
                    AddMonitorWarning();
                }
                else if (!isExtended && _monitorWarning != null)
                {
                    DismissNotification(_monitorWarning);
                }
            }
            catch { }
        }

        private void AddMonitorWarning()
        {
            _monitorWarning = new NotificationItem
            {
                Title = "Второй монитор!",
                Message = "Обнаружен активный второй экран. Пожалуйста, выключите режим 'Расширить' для чистоты проверки.",
                Type = NotificationType.Warning,
                Icon = "⚠",
                IsPersistent = true
            };
            _notifications.Add(_monitorWarning);
        }

        private void CheckRecordingStatus()
        {
            try
            {
                var processes = Process.GetProcesses();
                var foundProcess = processes.FirstOrDefault(p => _prohibitedProcesses.Contains(p.ProcessName));

                if (foundProcess != null)
                {
                    if (_recordingWarning == null)
                    {
                        AddRecordingWarning(foundProcess.ProcessName);
                    }
                }
                else
                {
                    if (_recordingWarning != null)
                    {
                        DismissNotification(_recordingWarning);
                    }
                }
            }
            catch { }
        }

        private void AddRecordingWarning(string processName)
        {
             _recordingWarning = new NotificationItem
            {
                Title = "Идет запись экрана!",
                Message = $"Обнаружен процесс записи: {processName}. Выключите запись для продолжения.",
                Type = NotificationType.Error,
                Icon = "🔴", // Or some specific icon
                IsPersistent = true
            };
            _notifications.Add(_recordingWarning);
        }

        #endregion

        #region Window Controls

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private string _currentLanguage = "ru"; // Default: Russian

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            var langWindow = new LanguageWindow(_currentLanguage);
            langWindow.Owner = this;
            if (langWindow.ShowDialog() == true)
            {
                _currentLanguage = langWindow.SelectedLanguage;
                ApplyLanguage(_currentLanguage);
            }
        }

        private void ApplyLanguage(string lang)
        {
            var dictPath = lang == "en" ? "Resources/Lang.en.xaml" : "Resources/Lang.ru.xaml";
            var newDict = new ResourceDictionary { Source = new Uri(dictPath, UriKind.Relative) };
            
            // Find and remove existing language dictionary
            var mergedDicts = Application.Current.Resources.MergedDictionaries;
            ResourceDictionary? oldLangDict = null;
            foreach (var dict in mergedDicts)
            {
                if (dict.Source != null && dict.Source.OriginalString.Contains("Lang."))
                {
                    oldLangDict = dict;
                    break;
                }
            }
            if (oldLangDict != null)
                mergedDicts.Remove(oldLangDict);
            
            // Add new language dictionary
            mergedDicts.Insert(0, newDict);
        }

        #endregion

        #region Navigation

        private FrameworkElement? _currentPage;
        private bool _isChangingPage = false;

        private async Task SwitchPageAsync(FrameworkElement newPage)
        {
            if (_currentPage == newPage || _isChangingPage) return;
            _isChangingPage = true;

            if (_currentPage != null)
            {
                _currentPage.Visibility = Visibility.Collapsed;
            }

            _currentPage = newPage;
            _currentPage.Opacity = 1;
            _currentPage.Visibility = Visibility.Visible;

            // Ensure transforms are reset to 0
            if (_currentPage.RenderTransform is TranslateTransform tt) tt.Y = 0;

            _isChangingPage = false;
            await Task.CompletedTask;
        }

        private async void Nav_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            var tag = btn.Tag?.ToString();
            if (string.IsNullOrEmpty(tag)) return;

            var navStyle = (Style)FindResource("NavButtonStyle");
            var activeStyle = (Style)FindResource("NavButtonActiveStyle");

            foreach (var navBtn in _navButtons)
                navBtn.Style = navStyle;
            btn.Style = activeStyle;

            FrameworkElement targetPage = tag switch
            {
                "PCCheck" => PagePCCheck,
                "Registry" => PageRegistry,
                "Accounts" => PageAccounts,
                "USB" => PageUSB,
                "Programs" => PagePrograms,
                "Folders" => PageFolders,
                "Sites" => PageSites,
                "Additional" => PageAdditional,
                "Extra" => PageExtra,
                _ => PagePCCheck
            };

            await SwitchPageAsync(targetPage);

            // Update Discord Presence
            string pageName = tag switch
            {
                "PCCheck" => "📑 Проверка на читы",
                "Registry" => "🔍 Сканирование реестра",
                "Accounts" => "👤 Просмотр аккаунтов",
                "USB" => "🔌 История USB",
                "Programs" => "🛠️ Инструменты",
                "Folders" => "📂 Проверка папок",
                "Sites" => "🌐 Полезные ссылки",
                "Additional" => "✨ Дополнительно",
                "Extra" => "🖥️ Система",
                _ => "📑 Проверка на читы"
            };
            DiscordService.UpdatePresence(pageName);

            // Defer heavy loading until AFTER animation is done
            if (tag == "Registry") LoadRegistryAsync();
            if (tag == "Accounts") LoadAccountsAsync();
            if (tag == "USB") LoadUsbAsync();
        }

        #endregion

        #region PC Check / Modal Flow

        private void ShowDiskSelection_Click(object sender, RoutedEventArgs e)
        {
            // Reload disks in case they changed
            LoadDisks();
            
            // Show modal
            DiskSelectionModal.Visibility = Visibility.Visible;
        }

        private void CloseModal_Click(object sender, RoutedEventArgs e)
        {
            DiskSelectionModal.Visibility = Visibility.Collapsed;
        }

        private void ResetScan_Click(object sender, RoutedEventArgs e)
        {
            // Reset to initial view
            ResultsView.Visibility = Visibility.Collapsed;
            ScanInitialView.Visibility = Visibility.Visible;
            ThreatsListView.ItemsSource = null;
            ThreatCountText.Text = "0";
            ProcessCountText.Text = "0 files";
            EmptyState.Visibility = Visibility.Visible;
        }

        private async void StartScan_Click(object sender, RoutedEventArgs e)
        {
            if (_isScanning)
            {
                CancelScan();
                return;
            }

            var selectedDisks = _disks.Where(d => d.IsSelected).ToList();
            if (selectedDisks.Count == 0)
            {
                MessageBox.Show("Please select at least one drive to scan.", "No Drives Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _isScanning = true;
            _scanCts = new CancellationTokenSource();

            // Hide modal and initial view, show results view
            DiskSelectionModal.Visibility = Visibility.Collapsed;
            ScanInitialView.Visibility = Visibility.Collapsed;
            ResultsView.Visibility = Visibility.Visible;

            // Update UI
            ProgressPanel.Visibility = Visibility.Visible;
            EmptyState.Visibility = Visibility.Collapsed;
            ScanningIndicator.Visibility = Visibility.Visible;
            ThreatsListView.ItemsSource = null;
            ThreatCountText.Text = "0";
            ProcessCountText.Text = "Scanning...";
            ScanStatusText.Text = "Initializing scan...";

            var progress = new Progress<string>(status =>
            {
                ProgressText.Text = status;
                ScanStatusText.Text = status;
            });

            try
            {
                DiscordService.UpdatePresence("🔎 Идёт сканирование ПК...", "📋 Проверка на читы");
                var useIconScan = CheckIconScan.IsChecked == true;
                var useSignatureScan = CheckSignatureScan.IsChecked == true;
                var result = await DiskScanner.ScanDisksAsync(selectedDisks, useIconScan, useSignatureScan, progress, _scanCts.Token);

                // Update UI with results
                ThreatsListView.ItemsSource = result.Threats;
                ThreatCountText.Text = result.Threats.Count.ToString();
                ProcessCountText.Text = $"{result.TotalFilesScanned:N0} files";

                if (result.WasCancelled)
                {
                    ScanStatusText.Text = "Scan cancelled";
                    ScanningIndicator.Visibility = Visibility.Collapsed;
                }
                else if (result.Threats.Count > 0)
                {
                    ScanStatusText.Text = $"Found {result.Threats.Count} threats in {result.ScanDuration.TotalSeconds:F1}s";
                    EmptyState.Visibility = Visibility.Collapsed;
                    ScanningIndicator.Visibility = Visibility.Collapsed;
                }
                else
                {
                    ScanStatusText.Text = $"Clean! Scanned in {result.ScanDuration.TotalSeconds:F1}s";
                    ScanningIndicator.Visibility = Visibility.Collapsed;
                    EmptyState.Visibility = Visibility.Visible;
                }
                DiscordService.UpdatePresence("✅ Сканирование завершено", "📋 Проверка на читы");
            }
            catch (OperationCanceledException)
            {
                ScanStatusText.Text = "Scan cancelled";
                ScanningIndicator.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                ScanStatusText.Text = $"Error: {ex.Message}";
                MessageBox.Show($"Scan error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isScanning = false;
                _scanCts?.Dispose();
                _scanCts = null;
                ProgressPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void CancelScan_Click(object sender, RoutedEventArgs e)
        {
            CancelScan();
        }

        private void CancelScan()
        {
            _scanCts?.Cancel();
            ScanStatusText.Text = "Cancelling...";
        }

        private void ThreatsListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ThreatsListView.SelectedItem is ThreatInfo threat)
            {
                try
                {
                    string path = threat.Path;

                    // If it's a process info string like "Process: Name (PID: 123)", we can't open folder easily.
                    if (threat.Type == "Process" || path.StartsWith("Process:")) return;

                    // If file exists, open explorer with file selected.
                    if (System.IO.File.Exists(path))
                    {
                        Process.Start("explorer.exe", $"/select,\"{path}\"");
                    }
                    else if (System.IO.Directory.Exists(path))
                    {
                         Process.Start("explorer.exe", $"\"{path}\"");
                    }
                }
                catch { }
            }
        }


        #endregion

        #region Registry

        #region Registry
        
        private async void LoadRegistryAsync()
        {
            if (_allPrograms.Count > 0) { ApplyRegistryFilters(); return; }

            RegistryListView.ItemsSource = null;
            _allPrograms = await Task.Run(() => RegistryScanner.GetPrograms());
            ApplyRegistryFilters();
        }

        private void RefreshRegistry_Click(object sender, RoutedEventArgs e)
        {
            _allPrograms.Clear();
            LoadRegistryAsync();
        }

        private void FilterAll_Click(object sender, RoutedEventArgs e)
        {
            FilterAll.IsChecked = true;
            FilterInstalled.IsChecked = false;
            FilterDeleted.IsChecked = false;
            FilterHistory.IsChecked = false;
            ApplyRegistryFilters();
        }

        private void Filter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton tb)
            {
                if (tb == FilterInstalled) { FilterDeleted.IsChecked = false; FilterHistory.IsChecked = false; }
                if (tb == FilterDeleted) { FilterInstalled.IsChecked = false; FilterHistory.IsChecked = false; }
                if (tb == FilterHistory) { FilterInstalled.IsChecked = false; FilterDeleted.IsChecked = false; }
                
                if (FilterInstalled.IsChecked == false && FilterDeleted.IsChecked == false && FilterHistory.IsChecked == false)
                    FilterAll.IsChecked = true;
                else
                    FilterAll.IsChecked = false;
            }
            ApplyRegistryFilters();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyRegistryFilters();

        private void ApplyRegistryFilters()
        {
            var showAll = FilterAll.IsChecked == true;
            var showInstalled = FilterInstalled.IsChecked == true;
            var showDeleted = FilterDeleted.IsChecked == true;
            var showHistory = FilterHistory.IsChecked == true;
            var searchText = SearchBox.Text?.ToLowerInvariant() ?? "";

            var filtered = _allPrograms.Where(p =>
            {
                if (!showAll)
                {
                    if (showInstalled && (p.IsHistory || p.Status != ProgramStatus.Installed)) return false;
                    if (showDeleted && (p.IsHistory || p.Status != ProgramStatus.Deleted)) return false;
                    if (showHistory && !p.IsHistory) return false;
                }

                if (!string.IsNullOrEmpty(searchText))
                    return p.DisplayName.ToLowerInvariant().Contains(searchText) ||
                           p.InstallPath.ToLowerInvariant().Contains(searchText);

                return true;
            }).ToList();

            RegistryListView.ItemsSource = filtered;

            var counts = RegistryScanner.GetCounts(_allPrograms);
            var historyCount = _allPrograms.Count(p => p.IsHistory);
            var installedCount = _allPrograms.Count(p => !p.IsHistory && p.Status == ProgramStatus.Installed);
            var deletedCount = _allPrograms.Count(p => !p.IsHistory && p.Status == ProgramStatus.Deleted);

            FilterAll.Content = $"All ({_allPrograms.Count})";
            FilterInstalled.Content = $"Installed ({installedCount})";
            FilterDeleted.Content = $"Deleted ({deletedCount})";
            FilterHistory.Content = $"Traces ({historyCount})";
        }
        


        private void OpenRegistry_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag == null) return;
            string? path = btn.Tag.ToString();
            if (!string.IsNullOrEmpty(path)) OpenRegistryKey(path);
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            Folder_Click(sender, e);
        }

        private void OpenRegistryKey(string path)
        {
            try
            {
                // Expand common abbreviations
                path = path.Replace("HKCU", "HKEY_CURRENT_USER")
                           .Replace("HKLM", "HKEY_LOCAL_MACHINE");

                // Ensure proper prefix for Regedit (often requires localized name on RU systems)
                // We use "Компьютер\" as default because the user is on a Russian system.
                // However, let's strip any existing prefix first to be safe.
                if (path.StartsWith("Computer\\")) path = path.Substring(9);
                if (path.StartsWith("Компьютер\\")) path = path.Substring(10);
                
                path = "Компьютер\\" + path;

                using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Applets\Regedit"))
                {
                    if (key != null) key.SetValue("LastKey", path);
                }

                foreach (var proc in Process.GetProcessesByName("regedit"))
                {
                    proc.Kill();
                }

                Process.Start("regedit.exe");
            }
            catch (Exception ex)
            {
                ShowNotification($"Ошибка открытия реестра: {ex.Message}", "Ошибка", NotificationType.Error);
            }
        }

        #endregion




        #endregion

        #region Accounts

        private async void LoadAccountsAsync()
        {
            if (_accounts.Count > 0) { AccountsListView.ItemsSource = _accounts; return; }

            _accounts = await Task.Run(() => SteamScanner.GetAccounts());
            AccountsListView.ItemsSource = _accounts;

            if (_accounts.Count == 0)
            {
                MessageBox.Show("Steam аккаунты не найдены.\n\nУбедитесь, что Steam установлен.",
                    "Нет аккаунтов", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Fetch Steam API data (avatars, bans)
            await SteamScanner.EnrichAccountsWithSteamDataAsync(_accounts);
            AccountsListView.ItemsSource = null;
            AccountsListView.ItemsSource = _accounts;
        }

        private void RefreshAccounts_Click(object sender, RoutedEventArgs e)
        {
            _accounts.Clear();
            LoadAccountsAsync();
        }

        private void AccountCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is SteamAccount account)
            {
                account.IsExpanded = !account.IsExpanded;
            }
        }

        private void CopySteamID_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is string steamId)
            {
                try
                {
                    Clipboard.SetText(steamId);
                    ShowNotification("SteamID скопирован!", "Успешно", NotificationType.Success);
                }
                catch { }
            }
            e.Handled = true; // Prevent event bubbling to card click
        }

        private void ClearAccounts_Click(object sender, RoutedEventArgs e)
        {
            _accounts.Clear();
            AccountsListView.ItemsSource = null;
        }

        #endregion

        #region USB

        private List<UsbDeviceInfo> _usbDevices = new();
        private List<UsbDeviceInfo> _allUsbDevices = new();

        private async void LoadUsbAsync()
        {
            _allUsbDevices = await Task.Run(() => UsbScanner.GetUsbHistory());
            UpdateFilterVisuals(); // Set visual state based on default filter
            ApplyUsbSearch();      // Apply filter to data
        }

        private async void RefreshUSB_Click(object sender, RoutedEventArgs e)
        {
            _allUsbDevices = await Task.Run(() => UsbScanner.GetUsbHistory());
            ApplyUsbSearch();
            
            var connected = _allUsbDevices.Count(d => d.IsConnected);
            ShowNotification($"Список USB устройств обновлен. Подключено: {connected}");
        }
        
        private void UsbSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyUsbSearch();
        }
        
        private string _currentFilter = "Flash"; // Default to Carriers (Flash + Phones)

        private void UsbFilter_All_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            SetFilter("All");
        }

        private void UsbFilter_Flash_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            SetFilter("Flash");
        }

        private void UsbFilter_Keyboard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            SetFilter("Keyboard");
        }

        private void SetFilter(string filter)
        {
            _currentFilter = filter;
            UpdateFilterVisuals();
            ApplyUsbSearch();
        }

        private void UpdateFilterVisuals()
        {
            // Set Tags for XAML styling (Active/Inactive)
            UsbFilterAll.Tag = _currentFilter == "All" ? "Active" : "Inactive";
            UsbFilterFlash.Tag = _currentFilter == "Flash" ? "Active" : "Inactive";
            UsbFilterKeyboard.Tag = _currentFilter == "Keyboard" ? "Active" : "Inactive";
        }

        private void ApplyUsbSearch()
        {
            var query = UsbSearchBox.Text?.Trim().ToLower() ?? "";
            var filtered = _allUsbDevices.AsEnumerable();

            // 1. Search text
            if (!string.IsNullOrEmpty(query))
            {
                filtered = filtered.Where(d => d.Name.ToLower().Contains(query) || 
                                             d.SerialNumber.ToLower().Contains(query) ||
                                             d.VID.ToLower().Contains(query));
            }

            // 2. Filter type
            if (_currentFilter == "Flash" || _currentFilter == "Keyboard")
            {
                filtered = filtered.Where(d => 
                {
                    var name = d.Name.ToLower();
                    var desc = d.Description.ToLower();
                    
                    // Identify Flash Drives
                    bool isFlash = name.Contains("flash") || name.Contains("disk") || name.Contains("mass storage") || desc.Contains("storage");
                    
                    // Identify Phones
                    bool isPhone = name.Contains("mtp") || name.Contains("mobile") || name.Contains("android") || 
                                   name.Contains("iphone") || name.Contains("samsung") || name.Contains("xiaomi") || 
                                   name.Contains("redmi") || name.Contains("poco") || name.Contains("pixel");

                    if (_currentFilter == "Flash") // Acts as "Carriers" (Flash + Phones)
                        return isFlash || isPhone;
                    
                    if (_currentFilter == "Keyboard") // Acts as "Peripherals" (Everything else)
                        return !isFlash && !isPhone;
                        
                    return true;
                });
            }

            _usbDevices = filtered.ToList();

            // Re-index after filtering
            for (int i = 0; i < _usbDevices.Count; i++)
                _usbDevices[i].DeviceIndex = i + 1;

            UsbItemsControl.ItemsSource = _usbDevices;
            UpdateUsbCounter();
        }
        
        private void UpdateUsbCounter()
        {
            // Count total Carriers (Flash + Phones) for the tab label
            var carriersCount = _allUsbDevices.Count(d => 
            {
                var name = d.Name.ToLower();
                var desc = d.Description.ToLower();
                
                bool isFlash = name.Contains("flash") || name.Contains("disk") || name.Contains("mass storage") || desc.Contains("storage");
                bool isPhone = name.Contains("mtp") || name.Contains("mobile") || name.Contains("android") || 
                               name.Contains("iphone") || name.Contains("samsung") || name.Contains("xiaomi") || 
                               name.Contains("redmi") || name.Contains("poco") || name.Contains("pixel");
                               
                return isFlash || isPhone;
            });
                
            if (UsbDeviceCounter != null)
                UsbDeviceCounter.Text = carriersCount.ToString();
        }

        #endregion

        #region Tools

        private async void Tool_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            var exeName = btn.Tag?.ToString();
            if (!string.IsNullOrEmpty(exeName))
            {
                // Run in background to prevent UI freeze if the tool launch is slow
                await Task.Run(() => AppLauncher.RunTool(exeName));
            }
        }

        private void Support_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://baza-cs2.ru/tickets/",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                ShowNotification("Ошибка: " + ex.Message, "Ошибка", NotificationType.Error);
            }
        }

        private void NazeNick_Click(object sender, MouseButtonEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://discord.com/users/877811080867491880",
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private void BazaLink_Click(object sender, MouseButtonEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://discord.gg/2Npfvc3R",
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private void Banner_Click(object sender, MouseButtonEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://baza-cs2.ru/",
                    UseShellExecute = true
                });
            }
            catch { }
        }

        #endregion



        #region Folders

        private void Folder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            switch (btn.Tag?.ToString())
            {
                case "AppData": FolderLauncher.OpenAppData(); break;
                case "Prefetch": FolderLauncher.OpenPrefetch(); break;
                case "Recent": FolderLauncher.OpenRecent(); break;
                case "ProgramFiles": FolderLauncher.OpenProgramFiles(); break;
                case "ProgramData": FolderLauncher.OpenProgramData(); break;
                case "Temp": FolderLauncher.OpenTemp(); break;
                case "Documents": FolderLauncher.OpenDocuments(); break;
                case "Downloads": FolderLauncher.OpenDownloads(); break;
                case "Users": FolderLauncher.OpenUsers(); break;
                case "CrashDumps": FolderLauncher.OpenCrashDumps(); break;
                case "ReportArchive": FolderLauncher.OpenReportArchive(); break;
            }
        }

        #endregion

        #region Sites

        private void Site_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            var url = btn.Tag?.ToString();
            if (!string.IsNullOrEmpty(url))
                SiteLauncher.OpenUrl(url);
        }

        #endregion

        #region System Info

        private async Task LoadSystemInfoAsync()
        {
            _systemSummary = await Task.Run(() => SystemInfoProvider.GetSummary());
            UpdateSystemInfoUI();
        }

        private void UpdateSystemInfoUI()
        {
            if (_systemSummary == null) return;

            OSText.Text = _systemSummary.OSName;
            InstallDateText.Text = _systemSummary.InstallDateText;
            ScreenCountText.Text = _systemSummary.MonitorCount.ToString();
            LastBootText.Text = _systemSummary.LastBootText;
            RAMText.Text = _systemSummary.RAM;
            CPUText.Text = _systemSummary.CPU;
            GPUText.Text = _systemSummary.GPU;
            MotherboardText.Text = _systemSummary.Motherboard;
            MachineNameText.Text = _systemSummary.MachineName;
            UserNameText.Text = _systemSummary.UserName;
            VMText.Text = _systemSummary.IsVirtualMachineText;
        }

        private async void RefreshSystemInfo_Click(object sender, RoutedEventArgs e)
        {
            _systemSummary = null;
            await LoadSystemInfoAsync();
        }

        #endregion
        private void Buffer_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string text)
            {
                try
                {
                    Clipboard.SetText(text);
                    ShowNotification($"{btn.Content} скопирован!");
                }
                catch (Exception ex)
                {
                    ShowNotification($"Ошибка копирования: {ex.Message}", "❌");
                }
            }
        }

        #region System Tools

        private void OpenNvidia_Click(object sender, RoutedEventArgs e)
        {
            // Check if NVIDIA GPU is present
            bool hasNvidiaGpu = false;
            try
            {
                using var searcher = new System.Management.ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
                foreach (var obj in searcher.Get())
                {
                    var name = obj["Name"]?.ToString() ?? "";
                    if (name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase))
                    {
                        hasNvidiaGpu = true;
                        break;
                    }
                }
            }
            catch { }

            if (!hasNvidiaGpu)
            {
                ShowNotification("У вас видеокарта не от NVIDIA", "Ошибка", NotificationType.Error);
                return;
            }

            // Try multiple methods to open NVIDIA Control Panel
            var paths = new[]
            {
                // Modern NVIDIA Control Panel (Windows Store app)
                "shell:AppsFolder\\NVIDIACorp.NVIDIAControlPanel_56jybvy8sckqj!NVIDIACorp.NVIDIAControlPanel",
                // Legacy paths
                @"C:\Windows\System32\nvcplui.exe",
                @"C:\Program Files\NVIDIA Corporation\Control Panel Client\nvcplui.exe",
                @"C:\Program Files (x86)\NVIDIA Corporation\Control Panel Client\nvcplui.exe",
            };

            foreach (var path in paths)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = path.StartsWith("shell:") ? "explorer.exe" : path,
                        Arguments = path.StartsWith("shell:") ? path : "",
                        UseShellExecute = !path.StartsWith("shell:")
                    });
                    return;
                }
                catch { }
            }

            ShowNotification("NVIDIA Control Panel не установлен", "Ошибка", NotificationType.Error);
        }

        private void OpenNetwork_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "ms-settings:network",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenServices_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "services.msc",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Macro Check

        private void OpenMacroCheck_Click(object sender, RoutedEventArgs e)
        {
            var macroWindow = new MacroCheckWindow();
            macroWindow.Owner = this;
            macroWindow.ShowDialog();
        }

        private void BtnOpenOSK_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Try standard launch
                Process.Start(new ProcessStartInfo 
                { 
                    FileName = "osk", 
                    UseShellExecute = true 
                });
            }
            catch
            {
                try
                {
                    // Fallback for some system configurations (Sysnative virtual folder)
                    string sysNative = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Sysnative", "osk.exe");
                    if (System.IO.File.Exists(sysNative))
                    {
                         Process.Start(new ProcessStartInfo 
                         { 
                             FileName = sysNative, 
                             UseShellExecute = true 
                         });
                         return;
                    }

                    // Explicit System32 fallback
                    string sys32 = System.IO.Path.Combine(Environment.SystemDirectory, "osk.exe");
                    Process.Start(new ProcessStartInfo 
                    { 
                        FileName = sys32, 
                        UseShellExecute = true 
                    });
                }
                catch (Exception ex)
                {
                     MessageBox.Show("Не удалось запустить экранную клавиатуру:\n" + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void OpenMouseSoftware_Click(object sender, RoutedEventArgs e)
        {
            // All known mouse software paths
            var mouseSoftware = new (string Name, string[] Paths)[]
            {
                ("Bloody", new[] {
                    @"C:\Program Files (x86)\Bloody7\Bloody7\Bloody7.exe",
                    @"C:\Program Files\Bloody7\Bloody7\Bloody7.exe",
                    @"C:\Program Files (x86)\Bloody\Bloody.exe",
                    @"C:\Program Files\A4Tech\Bloody\Bloody.exe"
                }),
                ("Logitech G Hub", new[] {
                    @"C:\Program Files\LGHUB\lghub.exe",
                    @"C:\Program Files (x86)\LGHUB\lghub.exe",
                    @"C:\Program Files\Logitech Gaming Software\LCore.exe",
                    @"C:\Program Files (x86)\Logitech Gaming Software\LCore.exe"
                }),
                ("Razer Synapse", new[] {
                    // Synapse 3 Host
                    @"C:\Program Files (x86)\Razer\Synapse3\WPFUI\Framework\Razer Synapse 3 Host\Razer Synapse 3.exe",
                    @"C:\Program Files\Razer\Synapse3\WPFUI\Framework\Razer Synapse 3 Host\Razer Synapse 3.exe",
                    // Razer Central (Launcher)
                    @"C:\Program Files (x86)\Razer\Razer Services\Razer Central\Razer Central.exe",
                    @"C:\Program Files\Razer\Razer Services\Razer Central\Razer Central.exe",
                    // Synapse 2
                    @"C:\Program Files (x86)\Razer\Synapse\RzSynapse.exe",
                    @"C:\Program Files\Razer\Synapse\RzSynapse.exe",
                    // Generic fallback
                    @"C:\Program Files (x86)\Razer\Razer Central\Razer Central.exe"
                }),
                ("SteelSeries Engine", new[] {
                    @"C:\Program Files\SteelSeries\SteelSeries Engine 3\SteelSeriesEngine3.exe",
                    @"C:\Program Files (x86)\SteelSeries\SteelSeries Engine 3\SteelSeriesEngine3.exe",
                    @"C:\Program Files\SteelSeries\GG\SteelSeriesGG.exe", // Newer SteelSeries GG
                    @"C:\Program Files (x86)\SteelSeries\GG\SteelSeriesGG.exe"
                }),
                ("Corsair iCUE", new[] {
                    @"C:\Program Files\Corsair\CORSAIR iCUE 4 Software\iCUE.exe",
                    @"C:\Program Files (x86)\Corsair\CORSAIR iCUE Software\iCUE.exe",
                    @"C:\Program Files\Corsair\CORSAIR iCUE 5 Software\iCUE.exe"
                }),
                ("HyperX NGENUITY", new[] {
                    @"C:\Program Files\HyperX NGENUITY\HyperX NGENUITY.exe"
                }),
                ("Roccat Swarm", new[] {
                    @"C:\Program Files (x86)\ROCCAT\Swarm\ROCCAT_Swarm.exe",
                    @"C:\Program Files\ROCCAT\Swarm\ROCCAT_Swarm.exe"
                })
            };

            foreach (var (name, paths) in mouseSoftware)
            {
                foreach (var path in paths)
                {
                    if (System.IO.File.Exists(path))
                    {
                        try
                        {
                            var psi = new ProcessStartInfo(path) { UseShellExecute = true };
                            System.Diagnostics.Process.Start(psi);
                            return;
                        }
                        catch { }
                    }
                }
            }
            
            ShowNotification("ПО для управления мышью не найдено", "Ошибка", NotificationType.Error);
        }

        #endregion

        private void BtnVmReport_Click(object sender, RoutedEventArgs e)
        {
            var log = Services.VmDetector.GetDetectionLog();
            var reportWin = new VmReportWindow(log);
            reportWin.Owner = this;
            reportWin.ShowDialog();
        }

        #region Macro Check Logic (Integrated)

        private async void BtnStartBruteForce_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var cs2 = System.Diagnostics.Process.GetProcessesByName("cs2").FirstOrDefault();
            if (cs2 == null)
            {
                ShowNotification("CS2 не запущен! Запустите игру перед проверкой.", "Ошибка", NotificationType.Error);
                return;
            }

            if(btn != null)
            {
                btn.IsEnabled = false;
                btn.Content = "⏳ ИДЕТ ПРОВЕРКА...";
            }

            await System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    ShowWindow(cs2.MainWindowHandle, SW_RESTORE);
                    SetForegroundWindow(cs2.MainWindowHandle);
                    System.Threading.Thread.Sleep(1000);

                    byte[] functionKeys = { 0x70, 0x71, 0x72, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78, 0x79, 0x7A, 0x7B }; 
                    byte[] specialKeys = { 0x2D, 0x2E, 0x24, 0x23, 0x21, 0x22 }; 
                    byte[] mainRow = { 0xC0, 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39, 0x30, 0xBD, 0xBB };
                    byte[] numpad = { 0x60, 0x61, 0x62, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68, 0x69, 0x6A, 0x6B, 0x6D, 0x6E, 0x6F };

                    PressList(cs2.MainWindowHandle, functionKeys);
                    PressList(cs2.MainWindowHandle, specialKeys);
                    PressList(cs2.MainWindowHandle, numpad);
                    for(byte k = 0x41; k <= 0x5A; k++) PressKeySafe(cs2.MainWindowHandle, k);
                    
                    PressModifiedList(cs2.MainWindowHandle, 0x12, specialKeys); // Alt
                    // Skip Alt+F2 (NVIDIA Photo Mode) and Alt+F3 (NVIDIA Game Filter) - start from F4 (0x73)
                    byte[] altFunctionKeys = { 0x73, 0x74, 0x75, 0x76, 0x77, 0x78, 0x79, 0x7A, 0x7B }; // F4-F12
                    PressModifiedList(cs2.MainWindowHandle, 0x12, altFunctionKeys);
                    PressModifiedList(cs2.MainWindowHandle, 0x10, specialKeys); // Shift
                    PressModifiedList(cs2.MainWindowHandle, 0x10, functionKeys);

                    // Add Shift + A-Z
                    var alphabet = new System.Collections.Generic.List<byte>();
                    for(byte k = 0x41; k <= 0x5A; k++) alphabet.Add(k);
                    PressModifiedList(cs2.MainWindowHandle, 0x10, alphabet.ToArray());
                    PressModifiedList(cs2.MainWindowHandle, 0x11, specialKeys); // Ctrl
                    PressModifiedList(cs2.MainWindowHandle, 0x11, functionKeys);
                    PressList(cs2.MainWindowHandle, mainRow);
                }
                catch { }
            });

            this.Dispatcher.Invoke(() =>
            {
                try { ShowWindow(cs2.MainWindowHandle, SW_MINIMIZE); } catch { }
                
                if (this.WindowState == WindowState.Minimized) this.WindowState = WindowState.Normal;
                this.Activate();

                if(btn != null)
                {
                    btn.IsEnabled = true;
                    btn.Content = "🤖 НАЧАТЬ ПРОВЕРКУ";
                }
                ShowNotification("Проверка прошла успешно", "Успешно", NotificationType.Success);
            });
        }

        private void PressList(IntPtr hWnd, byte[] keys)
        {
            foreach (var k in keys) PressKeySafe(hWnd, k);
        }

        private void PressModifiedList(IntPtr hWnd, byte modifier, byte[] keys)
        {
            foreach (var k in keys)
            {
                if (k == 0x5B || k == 0x5C || modifier == 0x5B || modifier == 0x5C) continue;
                SetForegroundWindow(hWnd);
                keybd_event(modifier, 0, 0, 0);
                System.Threading.Thread.Sleep(50);
                keybd_event(k, 0, 0, 0);
                System.Threading.Thread.Sleep(50);
                keybd_event(k, 0, KEYEVENTF_KEYUP, 0);
                System.Threading.Thread.Sleep(50);
                keybd_event(modifier, 0, KEYEVENTF_KEYUP, 0);
                System.Threading.Thread.Sleep(150);
            }
        }

        private void PressKeySafe(IntPtr hWnd, byte key)
        {
             if (key == 0x5B || key == 0x5C) return;
             SetForegroundWindow(hWnd);
             keybd_event(key, 0, 0, 0);
             System.Threading.Thread.Sleep(50);
             keybd_event(key, 0, KEYEVENTF_KEYUP, 0);
             System.Threading.Thread.Sleep(150);
        }

        #endregion

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
                e.Handled = true;
            }
            catch { }
        }
    }
}
