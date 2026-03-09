using System;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;
using System.Windows;
using System.Diagnostics;

namespace BazaChecker.Services
{
    public static class UpdateService
    {
        private const string GithubUrl = "https://github.com/Mujqk/SayurinOwnChecker";

        public static async Task<bool> CheckAndApplyUpdatesAsync(Action<int> progressCallback, Action<string> statusCallback)
        {
            try
            {
                // 1. Initial configuration
                // Velopack App Update
                VelopackApp.Build().Run();

                var mgr = new UpdateManager(new GithubSource(GithubUrl, null, false));

                // 2. Check for updates
                statusCallback?.Invoke("Проверка обновлений...");
                var newVersion = await mgr.CheckForUpdatesAsync();
                
                if (newVersion == null)
                {
                    statusCallback?.Invoke("Обновлений нет.");
                    return false; // No update available
                }

                // 3. Download updates
                statusCallback?.Invoke($"Загрузка обновления ({newVersion.TargetFullRelease.Version})...");
                await mgr.DownloadUpdatesAsync(newVersion, p => progressCallback?.Invoke(p));

                // 4. Apply and Restart
                statusCallback?.Invoke("Установка... Перезапуск...");
                
                // Allow user to see the "Restarting" status
                await Task.Delay(1000);

                // This will quit the app and start the new version
                mgr.ApplyUpdatesAndRestart(newVersion);
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Update Error: {ex.Message}");
                statusCallback?.Invoke("Ошибка обновления. Пропуск.");
                return false;
            }
        }

        /// <summary>
        /// This should be called as early as possible in App.xaml.cs 
        /// to handle Velopack setup events (like --veloapp-install)
        /// </summary>
        public static void HandleStartup()
        {
            try
            {
                VelopackApp.Build().Run();
            }
            catch { }
        }
    }
}
