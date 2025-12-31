using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace AutoBrowserDownloader.WpfApp.Core
{
    public static class DependencyInstaller
    {
        public static async Task EnsurePlaywrightInstalledAsync(Action<string> logger)
        {
            var exitCode = Microsoft.Playwright.Program.Main(new[] { "install" });
            if (exitCode != 0)
            {
                logger($"WARNING: Playwright install returned code {exitCode}");
                // Fallback to powershell script if the direct Main call doesn't work (common in some envs)
                await InstallViaScript(logger);
            }
            else
            {
                logger("Playwright binaries verified.");
            }
        }

        private static async Task InstallViaScript(Action<string> logger)
        {
            var psScript = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "playwright.ps1");
            if (File.Exists(psScript))
            {
                logger("Running playwright.ps1 install...");
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = $"-ExecutionPolicy Bypass -File \"{psScript}\" install",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    process.OutputDataReceived += (s, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) logger(e.Data); };
                    process.ErrorDataReceived += (s, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) logger("ERR: " + e.Data); };
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    await process.WaitForExitAsync();
                    logger("Install script completed.");
                }
            }
            else
            {
                logger("Playwright installation script not found.");
            }
        }
    }
}
