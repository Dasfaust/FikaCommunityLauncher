using Aki.Launcher.Controllers;
using Aki.Launcher.Helpers;
using Aki.Launcher.Models;
using Aki.Launcher.Models.Launcher;
using Aki.Launcher.ViewModels.Dialogs;
using Avalonia;
using Avalonia.Controls;
using ReactiveUI;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace Aki.Launcher.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        public LocaleCollection Locales { get; set; } = new LocaleCollection();

        private GameStarter gameStarter = new GameStarter(new GameStarterFrontend());

        public SettingsViewModel(IScreen Host) : base(Host)
        {
            if(Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow.Closing += MainWindow_Closing;
            }
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            LauncherSettingsProvider.Instance.SaveSettings();
        }

        public void GoBackCommand()
        {
            if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow.Closing -= MainWindow_Closing;
            }

            LauncherSettingsProvider.Instance.AllowSettings = true;
            LauncherSettingsProvider.Instance.SaveSettings();

            NavigateBack();
        }

        public void CleanTempFilesCommand()
        {
            bool filesCleared = gameStarter.CleanTempFiles();

            if (filesCleared)
            {
                SendNotification("", LocalizationProvider.Instance.clean_temp_files_succeeded, Avalonia.Controls.Notifications.NotificationType.Success);
            }
            else
            {
                SendNotification("", LocalizationProvider.Instance.clean_temp_files_failed, Avalonia.Controls.Notifications.NotificationType.Error);
            }
        }

        public void RemoveRegistryKeysCommand()
        {
            bool regKeysRemoved = gameStarter.RemoveRegistryKeys();

            if (regKeysRemoved)
            {
                SendNotification("", LocalizationProvider.Instance.remove_registry_keys_succeeded, Avalonia.Controls.Notifications.NotificationType.Success);
            }
            else
            {
                SendNotification("", LocalizationProvider.Instance.remove_registry_keys_failed, Avalonia.Controls.Notifications.NotificationType.Error);
            }
        }

        public async Task ResetGameSettingsCommand()
        {
            string EFTSettingsFolder = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Battlestate Games", "Escape from Tarkov", "Settings");
            string SPTSettingsFolder = Path.Join(LauncherSettingsProvider.Instance.GamePath, "user", "sptsettings");

            if (!Directory.Exists(EFTSettingsFolder))
            {
                LogManager.Instance.Warning($"EFT settings folder not found, can't reset :: Path: {EFTSettingsFolder}");
                SendNotification("", LocalizationProvider.Instance.load_live_settings_failed, Avalonia.Controls.Notifications.NotificationType.Error);
                return;
            }

            try
            {
                Directory.CreateDirectory(SPTSettingsFolder);

                foreach (string dirPath in Directory.GetDirectories(EFTSettingsFolder, "*", SearchOption.AllDirectories))
                {
                    Directory.CreateDirectory(dirPath.Replace(EFTSettingsFolder, SPTSettingsFolder));
                }

                //Copy all the files & Replaces any files with the same name
                foreach (string newPath in Directory.GetFiles(EFTSettingsFolder, "*.*", SearchOption.AllDirectories))
                {
                    File.Copy(newPath, newPath.Replace(EFTSettingsFolder, SPTSettingsFolder), true);
                }
            }
            catch (Exception ex)
            {
                LogManager.Instance.Exception(ex);
                SendNotification("", LocalizationProvider.Instance.load_live_settings_failed, Avalonia.Controls.Notifications.NotificationType.Error);
                return;
            }

            SendNotification("", LocalizationProvider.Instance.load_live_settings_succeeded, Avalonia.Controls.Notifications.NotificationType.Success);
        }

        public async Task ClearGameSettingsCommand()
        {
            var SPTSettingsDir = new DirectoryInfo(Path.Join(LauncherSettingsProvider.Instance.GamePath, "user", "sptsettings"));

            try
            {
                SPTSettingsDir.Delete(true);

                Directory.CreateDirectory(SPTSettingsDir.FullName);
            }
            catch(Exception ex)
            {
                LogManager.Instance.Exception(ex);
                SendNotification("", LocalizationProvider.Instance.clear_game_settings_failed, Avalonia.Controls.Notifications.NotificationType.Error);
                return;
            }

            SendNotification("", LocalizationProvider.Instance.clear_game_settings_succeeded, Avalonia.Controls.Notifications.NotificationType.Success);
        }

        public void OpenGameFolderCommand()
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = Path.EndsInDirectorySeparator(LauncherSettingsProvider.Instance.GamePath) ? LauncherSettingsProvider.Instance.GamePath : LauncherSettingsProvider.Instance.GamePath + Path.DirectorySeparatorChar,
                UseShellExecute = true,
                Verb = "open"
            });
        }

        public async Task SelectGameFolderCommand()
        {
            OpenFolderDialog dialog = new OpenFolderDialog();

            dialog.Directory = Assembly.GetExecutingAssembly().Location;

            if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                string? result = await dialog.ShowAsync(desktop.MainWindow);

                if (result != null)
                {
                    LauncherSettingsProvider.Instance.GamePath = result;
                }
            }
        }
    }
}
