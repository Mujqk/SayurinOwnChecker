using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace BazaChecker
{
    public partial class MacroCheckWindow : Window
    {
        private bool _isDrawing;
        private Point _lastPoint;
        private Brush _currentBrush = Brushes.White;

        // P/Invoke
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        private const int SW_RESTORE = 9;
        private const int SW_MINIMIZE = 6;
        private const int KEYEVENTF_KEYUP = 0x0002;

        public MacroCheckWindow(bool autoStartCheck = false)
        {
            InitializeComponent();
            if (autoStartCheck)
            {
                PreCheckOverlay.Visibility = Visibility.Visible;
            }
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void MacroCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _isDrawing = true;
            _lastPoint = e.GetPosition(MacroCanvas);
            MacroCanvas.CaptureMouse();
        }

        private void MacroCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDrawing) return;

            var currentPoint = e.GetPosition(MacroCanvas);
            var line = new Line
            {
                X1 = _lastPoint.X,
                Y1 = _lastPoint.Y,
                X2 = currentPoint.X,
                Y2 = currentPoint.Y,
                Stroke = _currentBrush,
                StrokeThickness = 2,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            MacroCanvas.Children.Add(line);
            _lastPoint = currentPoint;
        }

        private void MacroCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _isDrawing = false;
            MacroCanvas.ReleaseMouseCapture();
        }

        private void Color_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string colorHex)
            {
                try
                {
                    _currentBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex));
                }
                catch { }
            }
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            MacroCanvas.Children.Clear();
        }

        private void BtnShowPreCheck_Click(object sender, RoutedEventArgs e)
        {
            PreCheckOverlay.Visibility = Visibility.Visible;
        }

        private void BtnCancelPreCheck_Click(object sender, RoutedEventArgs e)
        {
            PreCheckOverlay.Visibility = Visibility.Collapsed;
        }

        private void BtnOpenOSK_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Try TabTip first (Windows Touch Keyboard) - cleaner UI
                string tabTipPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles), @"Microsoft Shared\ink\TabTip.exe");
                if (System.IO.File.Exists(tabTipPath))
                {
                    System.Diagnostics.Process.Start(tabTipPath);
                    return;
                }
                
                // Fallback to classic OSK
                string oskPath = System.IO.Path.Combine(Environment.SystemDirectory, "osk.exe");
                System.Diagnostics.Process.Start(oskPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось запустить экранную клавиатуру:\n" + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnStartBruteForce_Click(object sender, RoutedEventArgs e)
        {
            var cs2 = System.Diagnostics.Process.GetProcessesByName("cs2").FirstOrDefault();
            if (cs2 == null)
            {
                MessageBox.Show("CS2 не запущен! Запустите игру перед проверкой.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            BtnConfirmStart.IsEnabled = false;
            BtnConfirmStart.Content = "ИДЕТ ПРОВЕРКА...";

            // Hide overlay to show game? No, keep focus management logic.
            // Actually, we want to hide the overlay so the user sees the "Check Finished" state locally later.
            // But main focus shifts to CS2.
            
            await System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    // Focus CS2
                    ShowWindow(cs2.MainWindowHandle, SW_RESTORE);
                    SetForegroundWindow(cs2.MainWindowHandle);
                    System.Threading.Thread.Sleep(1000); // Wait for focus

                    // Key Lists
                    byte[] functionKeys = { 0x70, 0x71, 0x72, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78, 0x79, 0x7A, 0x7B }; // F1-F12
                    byte[] specialKeys = { 0x2D, 0x2E, 0x24, 0x23, 0x21, 0x22 }; // Ins, Del, Home, End, PgUp, PgDn
                    byte[] mainRow = { 0xC0 /*~*/, 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39, 0x30, 0xBD /*-*/, 0xBB /*=*/ };
                    byte[] numpad = { 0x60, 0x61, 0x62, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68, 0x69, 0x6A, 0x6B, 0x6D, 0x6E, 0x6F };

                    // 1. Single Press (All Lists)
                    PressList(cs2.MainWindowHandle, functionKeys);
                    PressList(cs2.MainWindowHandle, specialKeys);
                    PressList(cs2.MainWindowHandle, numpad);
                    // Press custom range (A-Z)
                    for(byte k = 0x41; k <= 0x5A; k++) PressKeySafe(cs2.MainWindowHandle, k);
                    
                    // 2. Modifiers Combos (Popular cheat binds)
                    // Alt + ...
                    PressModifiedList(cs2.MainWindowHandle, 0x12, specialKeys); // Alt + Ins/Del/etc
                    PressModifiedList(cs2.MainWindowHandle, 0x12, functionKeys); // Alt + F1-F12
                    
                    // Shift + ...
                    PressModifiedList(cs2.MainWindowHandle, 0x10, specialKeys); // Shift + Ins/Del/etc
                    PressModifiedList(cs2.MainWindowHandle, 0x10, functionKeys); // Shift + F1-F12

                    // Ctrl + ...
                    PressModifiedList(cs2.MainWindowHandle, 0x11, specialKeys); 
                    PressModifiedList(cs2.MainWindowHandle, 0x11, functionKeys);

                    // Row keys (~ to =)
                    PressList(cs2.MainWindowHandle, mainRow);
                }
                catch { }
            });

            // Finish
            this.Dispatcher.Invoke(() =>
            {
                // Minimize CS2 to show checker (optional, user asked to minimize/show check finished)
                try { ShowWindow(cs2.MainWindowHandle, SW_MINIMIZE); } catch { }
                
                // Focus Checker
                this.Show();
                this.WindowState = WindowState.Normal;
                this.Topmost = true;
                this.Topmost = false;
                this.Activate();

                BtnConfirmStart.IsEnabled = true;
                BtnConfirmStart.Content = "Начать проверку 🚀";
                PreCheckOverlay.Visibility = Visibility.Collapsed;
                MessageBox.Show("Проверка завершена!\nЕсли меню не открылось — биндов нет.", "Результат", MessageBoxButton.OK, MessageBoxImage.Information);
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
                keybd_event(modifier, 0, 0, 0); // Mod Down
                System.Threading.Thread.Sleep(50);
                
                keybd_event(k, 0, 0, 0); // Key Down
                System.Threading.Thread.Sleep(50);
                keybd_event(k, 0, KEYEVENTF_KEYUP, 0); // Key Up
                
                System.Threading.Thread.Sleep(50);
                keybd_event(modifier, 0, KEYEVENTF_KEYUP, 0); // Mod Up
                System.Threading.Thread.Sleep(150); // Delay
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
    }
}
