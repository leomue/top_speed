using System;
using System.Diagnostics;
using System.IO;

namespace TopSpeed.Game
{
    internal sealed partial class Game
    {
        private void LaunchUpdaterAndExit()
        {
            var root = Directory.GetCurrentDirectory();
            var updaterPath = Path.Combine(root, _updateConfig.UpdaterExeName);
            if (!File.Exists(updaterPath))
            {
                ShowMessageDialog(
                    "Updater not found",
                    "The update could not be installed automatically.",
                    new[] { $"Missing file: {_updateConfig.UpdaterExeName}" });
                return;
            }

            if (string.IsNullOrWhiteSpace(_updateZipPath) || !File.Exists(_updateZipPath))
            {
                ShowMessageDialog(
                    "Update package missing",
                    "The update package file was not found.",
                    new[] { "You can download the update again or install manually." });
                return;
            }

            try
            {
                var currentProcess = Process.GetCurrentProcess();
                var args =
                    $"--pid {currentProcess.Id} --zip \"{_updateZipPath}\" --dir \"{root}\" --game \"{_updateConfig.GameExeName}\" --skip \"{_updateConfig.UpdaterExeName}\"";
                var startInfo = new ProcessStartInfo
                {
                    FileName = updaterPath,
                    Arguments = args,
                    WorkingDirectory = root,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process.Start(startInfo);
                ExitRequested?.Invoke();
            }
            catch (Exception ex)
            {
                ShowMessageDialog(
                    "Updater launch failed",
                    "The updater could not be started.",
                    new[] { ex.Message });
            }
        }
    }
}
