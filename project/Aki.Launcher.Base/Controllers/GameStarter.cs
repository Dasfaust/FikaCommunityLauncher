/* GameStarter.cs
 * License: NCSA Open Source License
 * 
 * Copyright: Merijn Hendriks
 * AUTHORS:
 * waffle.lord
 * reider123
 * Merijn Hendriks
 */


using Aki.Launcher.Helpers;
using Aki.Launcher.MiniCommon;
using Aki.Launcher.Models.Launcher;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Aki.Launcher.Controllers;
using Aki.Launcher.Interfaces;
using System.Runtime.InteropServices;

namespace Aki.Launcher
{
    public class GameStarter
    {
        private readonly IGameStarterFrontend _frontend;
        private readonly bool _showOnly;
        private readonly string _originalGamePath;
        private readonly string[] _excludeFromCleanup;
        private const string registryInstall = @"Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\EscapeFromTarkov";

        private const string registrySettings = @"Software\Battlestate Games\EscapeFromTarkov";

        public GameStarter(IGameStarterFrontend frontend, string gamePath = null, string originalGamePath = null,
            bool showOnly = false, string[] excludeFromCleanup = null)
        {
            _frontend = frontend;
            _showOnly = showOnly;
            _originalGamePath = originalGamePath ??= DetectOriginalGamePath();
            _excludeFromCleanup = excludeFromCleanup ?? LauncherSettingsProvider.Instance.ExcludeFromCleanup;
        }

        private static string DetectOriginalGamePath()
        {
            // We can't detect the installed path on non-Windows
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return null;

            var installLocation = Registry.LocalMachine.OpenSubKey(registryInstall, false)
                ?.GetValue("InstallLocation");
            var info = (installLocation is string key) ? new DirectoryInfo(key) : null;
            return info?.FullName;
        }

        public async Task<GameStarterResult> LaunchGame(ServerInfo server, AccountInfo account, string gamePath)
        {
            LogManager.Instance.Info(">>> Launching Game");
            LogManager.Instance.Info($">>> Account: {account.username}");
            LogManager.Instance.Info($">>> Server : {server.backendUrl}");
            // setup directories
            if (IsInstalledInLive())
            {
                LogManager.Instance.Error("[LaunchGame] Installed in Live :: YES");
                return GameStarterResult.FromError(-1);
            }
            
            LogManager.Instance.Info("[LaunchGame] Installed in Live :: NO");
            
            LogManager.Instance.Info("[LaunchGame] Setup Game Files ...");
            SetupGameFiles(gamePath);

            if (!ValidationUtil.Validate())
            {
                LogManager.Instance.Error("[LaunchGame] Game Validation   :: FAILED");
                return GameStarterResult.FromError(-2);
            }
            
            LogManager.Instance.Info("[LaunchGame] Game Validation   :: OK");

            if (account.wipe)
            {
                LogManager.Instance.Info("[LaunchGame] Wipe profile requested");
                RemoveRegistryKeys();
                CleanTempFiles();
            }

            // check game path
            var clientExecutable = Path.Join(gamePath, "EscapeFromTarkov.exe");

            if (!File.Exists(clientExecutable))
            {
                LogManager.Instance.Error("[LaunchGame] Valid Game Path   :: FAILED");
                LogManager.Instance.Error($"Could not find {clientExecutable}");
                return GameStarterResult.FromError(-6);
            }
            
            LogManager.Instance.Info("[LaunchGame] Valid Game Path   :: OK");

            // apply patches
            ProgressReportingPatchRunner patchRunner = new ProgressReportingPatchRunner(gamePath);

            try
            {
                await _frontend.CompletePatchTask(patchRunner.PatchFiles());
            }
            catch (TaskCanceledException)
            {
                LogManager.Instance.Error("[LaunchGame] Applying Patch    :: FAILED");
                return GameStarterResult.FromError(-4);
            }
            
            LogManager.Instance.Info("[LaunchGame] Applying Patch    :: OK");
            
            //start game
            var args =
                $"-force-gfx-jobs native -token={account.id} -config={Json.Serialize(new ClientConfig(server.backendUrl))}";

            if (_showOnly)
            {
                Console.WriteLine($"{clientExecutable} {args}");
                LogManager.Instance.Info("[LaunchGame] NOOP :: show only");
            }
            else
            {
                var clientProcess = new ProcessStartInfo(clientExecutable)
                {
                    Arguments = args,
                    UseShellExecute = false,
                    WorkingDirectory = gamePath,
                };

                Process.Start(clientProcess);
                LogManager.Instance.Info("[LaunchGame] Game process started");
            }

            return GameStarterResult.FromSuccess();
        }

        bool IsInstalledInLive()
        {
            var isInstalledInLive = false;

            try
            {
                var files = new FileInfo[]
                {
                    // aki files
                    new FileInfo(Path.Combine(_originalGamePath, @"Aki.Launcher.exe")),
                    new FileInfo(Path.Combine(_originalGamePath, @"Aki.Server.exe")),
                    new FileInfo(Path.Combine(_originalGamePath, @"EscapeFromTarkov_Data\Managed\Aki.Build.dll")),
                    new FileInfo(Path.Combine(_originalGamePath, @"EscapeFromTarkov_Data\Managed\Aki.Common.dll")),
                    new FileInfo(Path.Combine(_originalGamePath, @"EscapeFromTarkov_Data\Managed\Aki.Reflection.dll")),

                    // bepinex files
                    new FileInfo(Path.Combine(_originalGamePath, @"doorstep_config.ini")),
                    new FileInfo(Path.Combine(_originalGamePath, @"winhttp.dll")),

                    // licenses
                    new FileInfo(Path.Combine(_originalGamePath, @"LICENSE-BEPINEX.txt")),
                    new FileInfo(Path.Combine(_originalGamePath, @"LICENSE-ConfigurationManager.txt")),                                    
                    new FileInfo(Path.Combine(_originalGamePath, @"LICENSE-Launcher.txt")),
                    new FileInfo(Path.Combine(_originalGamePath, @"LICENSE-Modules.txt")),
                    new FileInfo(Path.Combine(_originalGamePath, @"LICENSE-Server.txt"))
                };
                var directories = new DirectoryInfo[]
                {
                    new DirectoryInfo(Path.Combine(_originalGamePath, @"Aki_Data")),
                    new DirectoryInfo(Path.Combine(_originalGamePath, @"BepInEx"))
                };

                foreach (var file in files)
                {
                    if (File.Exists(file.FullName))
                    {
                        File.Delete(file.FullName);
                        LogManager.Instance.Warning($"File removed :: found in live dir: {file.FullName}");
                        isInstalledInLive = true;
                    }
                }

                foreach (var directory in directories)
                {
                    if (Directory.Exists(directory.FullName))
                    {
                        RemoveFilesRecurse(directory);
                        LogManager.Instance.Warning($"Directory removed :: found in live dir: {directory.FullName}");
                        isInstalledInLive = true;
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Instance.Exception(ex);
            }

            return isInstalledInLive;
        }

        void SetupGameFiles(string gamePath)
        {
            var files = new []
            {
                GetFileForCleanup("BattlEye", gamePath),
                GetFileForCleanup("Logs", gamePath),
                GetFileForCleanup("ConsistencyInfo", gamePath),
                GetFileForCleanup("EscapeFromTarkov_BE.exe", gamePath),
                GetFileForCleanup("Uninstall.exe", gamePath),
                GetFileForCleanup("UnityCrashHandler64.exe", gamePath),
                GetFileForCleanup("WinPixEventRuntime.dll", gamePath)
            };

            foreach (var file in files)
            {
                if (file == null)
                {
                    continue;
                }

                if (Directory.Exists(file))
                {
                    RemoveFilesRecurse(new DirectoryInfo(file));
                }

                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
        }

        private string GetFileForCleanup(string fileName, string gamePath)
        {
            if (_excludeFromCleanup.Contains(fileName))
            {
                LogManager.Instance.Info($"Excluded {fileName} from file cleanup");
                return null;
            }
            
            return Path.Combine(gamePath, fileName);
        }

        /// <summary>
        /// Remove the registry keys
        /// </summary>
        /// <returns>returns true if the keys were removed. returns false if an exception occured</returns>
		public bool RemoveRegistryKeys()
        {
            try
            {
                var key = Registry.CurrentUser.OpenSubKey(registrySettings, true);

                foreach (var value in key.GetValueNames())
                {
                    key.DeleteValue(value);
                    LogManager.Instance.Debug($"Removing reg key: {key.Name}");
                }
            }
            catch (Exception ex)
            {
                LogManager.Instance.Exception(ex);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Clean the temp folder
        /// </summary>
        /// <returns>returns true if the temp folder was cleaned succefully or doesn't exist. returns false if something went wrong.</returns>
		public bool CleanTempFiles()
        {
            var rootdir = new DirectoryInfo(Path.Combine(Path.GetTempPath(), @"Battlestate Games\EscapeFromTarkov"));

            if (!rootdir.Exists)
            {
                return true;
            }

            return RemoveFilesRecurse(rootdir);
        }

        bool RemoveFilesRecurse(DirectoryInfo basedir)
        {
            LogManager.Instance.Info($"Recursive Removal: {basedir}");
            
            if (!basedir.Exists)
            {
                return true;
            }

            try
            {
                // remove subdirectories
                foreach (var dir in basedir.EnumerateDirectories())
                {
                    RemoveFilesRecurse(dir);
                }

                // remove files
                var files = basedir.GetFiles();

                foreach (var file in files)
                {
                    file.IsReadOnly = false;
                    file.Delete();
                    LogManager.Instance.Debug($"  -> del file: {file.FullName}");
                }

                // remove directory
                basedir.Delete();
            }
            catch (Exception ex)
            {
                LogManager.Instance.Exception(ex);
                return false;
            }

            return true;
        }
    }
}