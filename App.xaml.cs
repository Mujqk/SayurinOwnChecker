using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Collections.Generic;
using Application = System.Windows.Application;

namespace BazaChecker
{
    public partial class App : Application
    {
        public App()
        {
            // Catch exceptions on the UI thread
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            
            // Catch exceptions on background threads
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            // Initial Velopack setup (handles installs/uninstalls/short-circuiting)
            BazaChecker.Services.UpdateService.HandleStartup();

            base.OnStartup(e);

            // Show splash screen
            var splash = new SplashScreen();
            splash.Show();

            // Give UI time to render
            await Task.Delay(50);

            // Stage 1: Update Check
            splash.UpdateStatus("Проверка обновлений...");
            splash.UpdateProgress(5);
            
            bool isUpdating = await BazaChecker.Services.UpdateService.CheckAndApplyUpdatesAsync(
                progress => splash.UpdateProgress(5 + (int)(progress * 0.2)), // Use 20% of progress for download
                status => splash.UpdateStatus(status)
            );

            if (isUpdating)
            {
                // The app will restart, no need to continue loading
                return;
            }

            // Stage 2: Initialize services
            splash.UpdateStatus("Инициализация сервисов...");
            splash.UpdateProgress(25);
            
            // Create MainWindow hidden (so it loads resources)
            MainWindow mainWindow = null!;
            await Task.Run(() =>
            {
                Dispatcher.Invoke(() =>
                {
                    mainWindow = new MainWindow();
                });
            });

            // Stage 2: Parallel Data Loading
            splash.UpdateStatus("Сканирование системы...");
            splash.UpdateProgress(20);

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // We will run heavy tasks in parallel
            List<BazaChecker.Models.ProgramInfo> loadedPrograms = new();
            List<BazaChecker.Models.SteamAccount> loadedAccounts = new();

            try 
            {
                var registryTask = Task.Run(() => BazaChecker.Services.RegistryScanner.GetPrograms());
                var accountsTask = Task.Run(() => BazaChecker.Services.SteamScanner.GetAccounts());
                
                // Optional: You could add more tasks here (e.g. initial disk check or USB history if implemented)

                // Wait for all whilst updating splash if needed? 
                // Since we can't easily track progress of individual tasks without change, we just await them.
                // But for better UX let's pretend progress moves a bit.
                
                splash.UpdateStatus("Чтение реестра и аккаунтов...");
                splash.UpdateProgress(40);
                
                await Task.WhenAll(registryTask, accountsTask);

                loadedPrograms = await registryTask;
                loadedAccounts = await accountsTask;

                splash.UpdateStatus("Кэширование данных...");
                splash.UpdateProgress(80);

                // Enrich accounts (fetch avatars/bans) if any
                if (loadedAccounts.Count > 0)
                {
                    // This can be slow, so maybe only do it partially or async?
                    // Let's do it fully for "Real Loading" experience as requested.
                    splash.UpdateStatus($"Проверка {loadedAccounts.Count} аккаунтов Steam...");
                    await BazaChecker.Services.SteamScanner.EnrichAccountsWithSteamDataAsync(loadedAccounts);
                }
                
                // Inject data
                mainWindow.InjectPreloadedData(loadedPrograms, loadedAccounts);
            }
            catch (Exception ex)
            {
                LogException(ex, "Startup Data Loading");
            }

            splash.UpdateProgress(90);
            
            // Artificial delay if too fast (so user sees the splash at least briefly)
            if (stopwatch.ElapsedMilliseconds < 1000)
            {
                await Task.Delay(500);
            }
            
            stopwatch.Stop();

            // Complete
            splash.UpdateStatus("Готово!");
            splash.UpdateProgress(100);
            await Task.Delay(100);

            // Show main window and close splash
            mainWindow.Show();
            mainWindow.Activate();
            mainWindow.Focus();
            splash.Close();
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            LogException(e.Exception, "UI Thread Exception");
            e.Handled = true; // Prevent immediate crash if possible, though usually we want to shutdown gracefully
            Shutdown();
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            LogException(e.ExceptionObject as Exception, "Background Thread Exception");
        }

        private void LogException(Exception? ex, string source)
        {
            if (ex == null) return;

            string message = $"[{DateTime.Now}] CRITICAL ERROR ({source}):\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}\n\nExisting Inner Exception:\n{ex.InnerException?.Message}\n--------------------------------------------------\n";
            
            try
            {
                File.AppendAllText("crash_log.txt", message);
                MessageBox.Show($"Application crashed!\n\nError: {ex.Message}\n\nCheck crash_log.txt for details.", "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch
            {
                // Fallback if writing fails
                MessageBox.Show($"Crashed: {ex.Message}", "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
