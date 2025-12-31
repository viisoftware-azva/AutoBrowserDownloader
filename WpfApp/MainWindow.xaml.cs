using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Windows.Media;
using System.Windows.Controls;
using AutoBrowserDownloader.WpfApp.Core;
using AutoBrowserDownloader.WpfApp.Models;
using System.Threading.Tasks;

namespace AutoBrowserDownloader.WpfApp
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<AutomationStep> Steps { get; set; }
        public ObservableCollection<ScrapeResult> Results { get; set; }
        public ObservableCollection<string> DownloadLogMessages { get; set; }
        private AppSettings _settings;
        private string _selectedCsvPath = "";



        public MainWindow()
        {
            InitializeComponent();

            // Initialize Steps (still used for manual config if needed later)
            Steps = new ObservableCollection<AutomationStep>
            {
                new AutomationStep { Type = ActionType.Wait, Value = "1000", Description = "Wait for load" }, 
                new AutomationStep { Type = ActionType.Click, Selector = "text=Download", Description = "Click Download Button", IsOptional = true }
            };
            StepsGrid.ItemsSource = Steps;

            // Initialize Results
            Results = new ObservableCollection<ScrapeResult>();
            ResultsGrid.ItemsSource = Results;

            // Initialize Download Log
            DownloadLogMessages = new ObservableCollection<string>();
            DownloadLog.ItemsSource = DownloadLogMessages;

            // Load settings
            _settings = AppSettings.Load();
            UrlBox.Text = _settings.LastUrl;
        }


        private void AddStep_Click(object sender, RoutedEventArgs e)
        {
            Steps.Add(new AutomationStep { Type = ActionType.Wait, Value = "1000", Description = "New Step" });
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            var about = new AboutWindow();
            about.Owner = this;
            about.ShowDialog();
        }

        private void Sidebar_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                // Reset styles
                var grayBrush = (Brush)new BrushConverter().ConvertFrom("#CBD5E0");
                ScraperBtn.Background = Brushes.Transparent;
                ScraperBtn.BorderThickness = new Thickness(0);
                ScraperBtn.Foreground = grayBrush;

                DownloaderBtn.Background = Brushes.Transparent;
                DownloaderBtn.BorderThickness = new Thickness(0);
                DownloaderBtn.Foreground = grayBrush;

                SettingsBtn.Background = Brushes.Transparent;
                SettingsBtn.BorderThickness = new Thickness(0);
                SettingsBtn.Foreground = grayBrush;

                // Set Active Style
                btn.Background = (Brush)new BrushConverter().ConvertFrom("#2D3748");
                btn.BorderThickness = new Thickness(4, 0, 0, 0);
                btn.BorderBrush = (Brush)new BrushConverter().ConvertFrom("#3182CE");
                btn.Foreground = Brushes.White;

                // Navigate
                if (btn == ScraperBtn) {
                    MainTabs.SelectedIndex = 0;
                    ScraperControls.Visibility = Visibility.Visible;
                }
                else if (btn == DownloaderBtn) {
                    MainTabs.SelectedIndex = 2;
                    ScraperControls.Visibility = Visibility.Collapsed;
                }
                else if (btn == SettingsBtn) {
                    MainTabs.SelectedIndex = 1;
                    ScraperControls.Visibility = Visibility.Collapsed;
                }
            }
        }


        private async void Start_Click(object sender, RoutedEventArgs e)
        {
            LogBox.Text = ""; 
            LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] Status: Starting scraping {UrlBox.Text}...\n");
            
            var url = UrlBox.Text;

            // Save last URL used
            _settings.LastUrl = url;
            _settings.Save();

            var runner = new PlaywrightRunner();


            // Wire up logging
            runner.OnLog += (msg) =>
            {
                Dispatcher.Invoke(() => 
                {
                    LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\n");
                    LogBox.ScrollToEnd();
                });
            };

            // Wire up results
            Results.Clear();
            runner.OnResultFound += (result) =>
            {
                Dispatcher.Invoke(() => Results.Add(result));
            };

            try
            {
                this.IsEnabled = false;

                // Config specific for dafontfree.io (as requested)
                var config = new ScraperConfig
                {
                    ItemContainer = "article",        
                    NameSelector = "h2 a",
                    CategorySelector = ".cat-links a", 
                    ImageSelector = "img",
                    UrlSelector = "h2 a",
                    
                    // Detail page
                    DescriptionSelector = ".entry-content p",
                    DownloadButtonSelector = "a:has-text('Download')" 
                };

                await runner.RunDeepScrapeAsync(url, config, false, _settings);

                
                LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] Status: All operations completed.\n");
            }
            catch (Exception ex)
            {
                LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] ERROR: {ex.Message}\n");
            }
            finally
            {
                this.IsEnabled = true;
            }
        }

        private void DropZone_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }

        private void DropZone_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0 && files[0].ToLower().EndsWith(".csv"))
                {
                    _selectedCsvPath = files[0];
                    FileNameText.Text = Path.GetFileName(_selectedCsvPath);
                    DownloadLogMessages.Add($"[System] Selected file: {FileNameText.Text}");
                }
                else
                {
                    MessageBox.Show("Please drop a valid CSV file.");
                }
            }
        }

        private async void StartDownload_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedCsvPath) || !File.Exists(_selectedCsvPath))
            {
                MessageBox.Show("Please drag and drop a CSV file first!");
                return;
            }

            try
            {
                DownloadLogMessages.Add("[System] Reading CSV...");
                var lines = await File.ReadAllLinesAsync(_selectedCsvPath);
                if (lines.Length <= 1)
                {
                    DownloadLogMessages.Add("[System] Error: CSV is empty or has no data.");
                    return;
                }

                // Header mapping - SMART DETECTION
                var headerLine = lines[0];
                var headers = System.Text.RegularExpressions.Regex.Split(headerLine, ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)")
                                    .Select(h => h.Trim('\"')).ToList();
                
                // Try to find the best column index
                int dlIdx = headers.FindIndex(h => h.Equals("DownloadUrl", StringComparison.OrdinalIgnoreCase));
                
                if (dlIdx == -1)
                {
                    // Look for common keywords
                    string[] keywords = { "download", "url", "link", "file", "href", "src" };
                    foreach (var kw in keywords)
                    {
                        dlIdx = headers.FindIndex(h => h.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0);
                        if (dlIdx != -1) break;
                    }
                }

                if (dlIdx == -1)
                {
                    // Still not found? Look for the first column that actually contains a URL in the first few rows
                    for (int i = 1; i < Math.Min(lines.Length, 6); i++)
                    {
                        var sampleParts = System.Text.RegularExpressions.Regex.Split(lines[i], ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)")
                                            .Select(p => p.Trim('\"')).ToArray();
                        for (int j = 0; j < sampleParts.Length; j++)
                        {
                            if (IsUrl(sampleParts[j]))
                            {
                                dlIdx = j;
                                break;
                            }
                        }
                        if (dlIdx != -1) break;
                    }
                }

                if (dlIdx != -1)
                {
                    DownloadLogMessages.Add($"[System] Smart Detection: Using column '{headers[dlIdx]}' for downloads.");
                }
                else
                {
                    DownloadLogMessages.Add("[System] Warning: No specific URL column detected. Searching each row for URLs...");
                }

                var downloadDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Downloads");
                if (!Directory.Exists(downloadDir)) Directory.CreateDirectory(downloadDir);

                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromMinutes(5);

                this.IsEnabled = false;
                int count = 0;

                foreach (var line in lines.Skip(1))
                {
                    // Basic split (considering quotes)
                    var parts = System.Text.RegularExpressions.Regex.Split(line, ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)")
                                    .Select(p => p.Trim('\"')).ToArray();

                    if (parts.Length > 0)
                    {
                        // Smartly find the URL in this row
                        string url = "";
                        if (dlIdx != -1 && parts.Length > dlIdx && IsUrl(parts[dlIdx]))
                        {
                            url = parts[dlIdx];
                        }
                        else
                        {
                            // Fallback: look for ANY column that has a valid URL
                            url = parts.FirstOrDefault(p => IsUrl(p)) ?? "";
                        }

                        if (string.IsNullOrEmpty(url) || url.Contains("Not Found")) continue;
                        url = url.Trim().TrimEnd(';'); // Remove trailing semicolon and spaces

                        try
                        {
                            var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                            
                            // Try to get filename from Content-Disposition header
                            string fileName = response.Content.Headers.ContentDisposition?.FileName?.Trim('\"', ' ');
                            
                            // Fallback to URL path
                            if (string.IsNullOrEmpty(fileName))
                            {
                                try {
                                    fileName = Path.GetFileName(new Uri(url).LocalPath);
                                } catch { fileName = ""; }
                            }

                            // Still empty or has weird characters? 
                            if (string.IsNullOrEmpty(fileName)) 
                            {
                                fileName = $"file_{Guid.NewGuid().ToString().Substring(0, 8)}.zip";
                            }
                            else
                            {
                                // Clean filename of invalid chars and trailing junk like ;
                                fileName = string.Join("_", fileName.Split(Path.GetInvalidFileNameChars()));
                                fileName = fileName.Trim().TrimEnd(';', '.'); // Clean trailing semicolon/dots
                                
                                // Ensure it has an extension if it looks like a web resource
                                if (!Path.HasExtension(fileName) || fileName.EndsWith(";"))
                                {
                                    // Try to guess extension from content type if possible, or default to zip
                                    fileName += ".zip";
                                }
                            }

                            DownloadLogMessages.Add($"[{DateTime.Now:HH:mm:ss}] Starting: {fileName}");
                            
                            if (response.IsSuccessStatusCode)
                            {
                                var bytes = await response.Content.ReadAsByteArrayAsync();
                                await File.WriteAllBytesAsync(Path.Combine(downloadDir, fileName), bytes);
                                DownloadLogMessages.Add($"[{DateTime.Now:HH:mm:ss}] ✅ Success: {fileName}");
                                count++;
                            }
                            else
                            {
                                DownloadLogMessages.Add($"[{DateTime.Now:HH:mm:ss}] ❌ Failed: {fileName} (HTTP {response.StatusCode})");
                            }
                        }
                        catch (Exception ex)
                        {
                            DownloadLogMessages.Add($"[{DateTime.Now:HH:mm:ss}] ⚠️ Error: {ex.Message}");
                        }
                    }
                }

                DownloadLogMessages.Add($"[System] Completed. {count} files downloaded to 'Downloads' folder.");
                MessageBox.Show($"Download completed! {count} files saved to the Downloads folder.");
            }
            catch (Exception ex)
            {
                DownloadLogMessages.Add($"[System] Fatal Error: {ex.Message}");
            }
            finally
            {
                this.IsEnabled = true;
            }
        }

        private bool IsUrl(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            return Uri.TryCreate(text, UriKind.Absolute, out var uriResult)
                && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        }

    }
}
