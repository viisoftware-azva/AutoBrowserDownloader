using System;
using System.IO;
using System.Windows;

namespace AutoBrowserDownloader
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Global exception handling
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                LogFatalError(args.ExceptionObject as Exception);
            };

            DispatcherUnhandledException += (s, args) =>
            {
                LogFatalError(args.Exception, "Dispatcher");
                args.Handled = true; // Prevent crash if possible, or just log
            };

            // Optional: TaskScheduler.UnobservedTaskException
        }

        private void LogFatalError(Exception? ex, string source = "AppDomain")
        {
            if (ex == null) return;
            
            try 
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash_log.txt");
                string message = $"[{DateTime.Now}] FATAL ERROR ({source}): {ex.Message}\nStack Trace:\n{ex.StackTrace}\n\nInner Exception: {ex.InnerException?.Message}";
                File.AppendAllText(path, message);
                MessageBox.Show($"Application crashed: {ex.Message}\nSee crash_log.txt for details.", "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch 
            {
                // Last ditch effort
                MessageBox.Show($"Application crashed: {ex.Message}", "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
