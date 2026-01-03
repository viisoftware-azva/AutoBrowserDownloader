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
        private AppSettings? _settings;
        private string _selectedCsvPath = "";
        private System.Threading.CancellationTokenSource? _ctsDownload;
        private System.Threading.CancellationTokenSource? _ctsScrape;



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

            // Initialize Download Progress
            DownloadLogMessages = new ObservableCollection<string>();
            DownloadLogMessages.CollectionChanged += (s, e) =>
            {
                if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && e.NewItems != null)
                {
                    Dispatcher.Invoke(() => {
                        foreach (object? item in e.NewItems) {
                            if (item is string msg) {
                                DownloadLogBox.AppendText(msg + "\n");
                            }
                        }
                        DownloadLogBox.ScrollToEnd();
                    });
                }
            };

            // Load settings
            _settings = AppSettings.Load() ?? new AppSettings();
            UrlBox.Text = _settings.LastUrl;
            DownloadPathBox.Text = _settings.DownloadPath;
            PagesBox.Text = _settings.PagesToScrape.ToString();
            DelayBox.Text = _settings.ScrapeDelay.ToString();

            // Load Configuration UI
            CfgItemContainer.Text = _settings.ScraperSettings.ItemContainer;
            CfgNameSelector.Text = _settings.ScraperSettings.NameSelector;
            CfgCategorySelector.Text = _settings.ScraperSettings.CategorySelector;
            CfgImageSelector.Text = _settings.ScraperSettings.ImageSelector;
            CfgUrlSelector.Text = _settings.ScraperSettings.UrlSelector;
            CfgDescSelector.Text = _settings.ScraperSettings.DescriptionSelector;
            CfgDownloadSelector.Text = _settings.ScraperSettings.DownloadButtonSelector;

            // Update Resume Info
            ResumeCheck.Content = $"Resume from Last Page (Saved: Page {_settings.LastPage})";
            ResumeCheck.IsChecked = _settings.LastPage > 0;
        }


        private void SaveConfig_Click(object sender, RoutedEventArgs e)
        {
            if (_settings == null) _settings = new AppSettings();
            
            _settings.ScraperSettings.ItemContainer = CfgItemContainer.Text;
            _settings.ScraperSettings.NameSelector = CfgNameSelector.Text;
            _settings.ScraperSettings.CategorySelector = CfgCategorySelector.Text;
            _settings.ScraperSettings.ImageSelector = CfgImageSelector.Text;
            _settings.ScraperSettings.UrlSelector = CfgUrlSelector.Text;
            _settings.ScraperSettings.DescriptionSelector = CfgDescSelector.Text;
            _settings.ScraperSettings.DownloadButtonSelector = CfgDownloadSelector.Text;

            _settings.Save();
            MessageBox.Show("Configuration saved successfully!");
        }

        private void AddStep_Click(object sender, RoutedEventArgs e)
        {
            Steps.Add(new AutomationStep { Type = ActionType.Wait, Value = "1000", Description = "New Step" });
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            // Trigger Sidebar_Click for the AboutBtn
            AboutBtn.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        }

        private void Sidebar_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                // Reset styles
                var grayBrush = (Brush)new BrushConverter().ConvertFrom("#CBD5E0")!;
                ScraperBtn.Background = Brushes.Transparent;
                ScraperBtn.BorderThickness = new Thickness(0);
                ScraperBtn.Foreground = grayBrush;

                DownloaderBtn.Background = Brushes.Transparent;
                DownloaderBtn.BorderThickness = new Thickness(0);
                DownloaderBtn.Foreground = grayBrush;

                SettingsBtn.Background = Brushes.Transparent;
                SettingsBtn.BorderThickness = new Thickness(0);
                SettingsBtn.Foreground = grayBrush;

                AboutBtn.Background = Brushes.Transparent;
                AboutBtn.BorderThickness = new Thickness(0);
                AboutBtn.Foreground = grayBrush;

                // Set Active Style
                btn.Background = (Brush)new BrushConverter().ConvertFrom("#2D3748")!;
                btn.BorderThickness = new Thickness(4, 0, 0, 0);
                btn.BorderBrush = (Brush)new BrushConverter().ConvertFrom("#3182CE")!;
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
                else if (btn == AboutBtn) {
                    MainTabs.SelectedIndex = 3;
                    ScraperControls.Visibility = Visibility.Collapsed;
                }
            }
        }


        private void StopScrape_Click(object sender, RoutedEventArgs e)
        {
            _ctsScrape?.Cancel();
            LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] Status: Stopping scraper... Please wait for current task to finish.\n");
        }

        private async void Start_Click(object sender, RoutedEventArgs e)
        {
            LogBox.Text = ""; 
            LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] Status: Starting scraping {UrlBox.Text}...\n");
            
            var url = UrlBox.Text;

            // Save last URL used
            if (_settings == null) _settings = new AppSettings();
            _settings.LastUrl = url;
            if (int.TryParse(PagesBox.Text, out int pc)) _settings.PagesToScrape = pc;
            if (int.TryParse(DelayBox.Text, out int dc)) _settings.ScrapeDelay = dc;
            _settings.Save();

            // Handle Reset or Resume
            if (ResumeCheck.IsChecked == false)
            {
                _settings.LastPage = 0;
                _settings.ScrapedUrls.Clear();
                _settings.Save();
                LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] Status: Resetting progress, starting from Page 1.\n");
            }
            else
            {
                LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] Status: Resuming from Page {_settings.LastPage + 1}.\n");
            }

            // Set UI State
            StartScrapeBtn.IsEnabled = false;
            StopScrapeBtn.IsEnabled = true;
            UrlBox.IsEnabled = false;
            SidebarControlsEnabled(false);

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
                _ctsScrape = new System.Threading.CancellationTokenSource();

                // Use Dynamic Config from Settings
                var config = _settings!.ScraperSettings;

                await runner.RunDeepScrapeAsync(url, config, false, _settings, _ctsScrape.Token);
                
                LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] Status: All operations completed.\n");
                
                // Update Resume UI Info
                Dispatcher.Invoke(() => {
                    ResumeCheck.Content = $"Resume from Last Page (Saved: Page {_settings.LastPage})";
                });
            }
            catch (OperationCanceledException)
            {
                LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] Status: Scrape stopped by user.\n");
            }
            catch (Exception ex)
            {
                LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] ERROR: {ex.Message}\n");
            }
            finally
            {
                StartScrapeBtn.IsEnabled = true;
                StopScrapeBtn.IsEnabled = false;
                UrlBox.IsEnabled = true;
                SidebarControlsEnabled(true);
                
                _ctsScrape?.Dispose();
                _ctsScrape = null;
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
                    ClearFileBtn.Visibility = Visibility.Visible;
                    DownloadLogMessages.Add($"[System] Selected file: {FileNameText.Text}");
                }
                else
                {
                    MessageBox.Show("Please drop a valid CSV file.");
                }
            }
        }

        private void DropZone_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.Filter = "CSV files (*.csv)|*.csv";
            if (dialog.ShowDialog() == true)
            {
                _selectedCsvPath = dialog.FileName;
                FileNameText.Text = Path.GetFileName(_selectedCsvPath);
                ClearFileBtn.Visibility = Visibility.Visible;
                DownloadLogMessages.Add($"[System] Selected file: {FileNameText.Text}");
            }
        }

        private void ClearFile_Click(object sender, RoutedEventArgs e)
        {
            _selectedCsvPath = "";
            FileNameText.Text = "";
            ClearFileBtn.Visibility = Visibility.Collapsed;
            DownloadLogMessages.Add("[System] File selection cleared.");
        }

        private void StopDownload_Click(object sender, RoutedEventArgs e)
        {
            _ctsDownload?.Cancel();
            DownloadLogMessages.Add("[System] Stopping... Please wait for the current file to finish.");
            StopDownloadBtn.IsEnabled = false;
        }

        private void BrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            // OpenFolderDialog is available in .NET 8.0+
            var dialog = new Microsoft.Win32.OpenFolderDialog();
            dialog.InitialDirectory = DownloadPathBox.Text;
            if (dialog.ShowDialog() == true)
            {
                DownloadPathBox.Text = dialog.FolderName;
                if (_settings != null)
                {
                    _settings.DownloadPath = dialog.FolderName;
                    _settings.Save();
                }
            }
        }

        private async void StartDownload_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedCsvPath) || !File.Exists(_selectedCsvPath))
            {
                MessageBox.Show("Please drag and drop or browse for a CSV file first!");
                return;
            }

            try
            {
                DownloadLogMessages.Clear();
                DownloadLogBox.Clear();
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

                // Get download directory from UI and save it
                var downloadDir = DownloadPathBox.Text;
                if (string.IsNullOrWhiteSpace(downloadDir)) downloadDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Downloads");
                
                if (_settings != null)
                {
                    _settings.DownloadPath = downloadDir;
                    _settings.Save();
                }

                if (!Directory.Exists(downloadDir)) Directory.CreateDirectory(downloadDir);

                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromMinutes(5);

                _ctsDownload = new System.Threading.CancellationTokenSource();
                
                // Set UI State
                StartDownloadBtn.IsEnabled = false;
                StopDownloadBtn.IsEnabled = true;
                BrowseFolderBtn.IsEnabled = false;
                DropZone.AllowDrop = false;
                SidebarControlsEnabled(false);

                int count = 0;
                var rows = lines.Skip(1).ToList();
                int totalRows = rows.Count;
                DownloadProgress.Maximum = totalRows;
                DownloadProgress.Value = 0;
                ProgressText.Text = $"0/{totalRows}";

                int consecutiveEmptyRows = 0;

                for (int i = 0; i < rows.Count; i++)
                {
                    if (_ctsDownload.Token.IsCancellationRequested)
                    {
                        DownloadLogMessages.Add("[System] Download process stopped by user.");
                        break;
                    }

                    var line = rows[i];
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

                        if (string.IsNullOrEmpty(url) || url.Contains("Not Found")) 
                        {
                            consecutiveEmptyRows++;
                            if (consecutiveEmptyRows >= 5)
                            {
                                DownloadLogMessages.Add($"[{DateTime.Now:HH:mm:ss}] ⚠️ 5 consecutive empty rows found. Stopping process.");
                                break;
                            }

                            DownloadLogMessages.Add($"[{DateTime.Now:HH:mm:ss}] ℹ️ Row {i + 2} is empty. Checking next... (Attempt {consecutiveEmptyRows}/5)");
                            
                            DownloadProgress.Value = i + 1;
                            ProgressText.Text = $"{i + 1}/{totalRows}";
                            continue;
                        }

                        // Reset counter if we found a valid URL
                        consecutiveEmptyRows = 0;

                        url = url.Trim().TrimEnd(';'); // Remove trailing semicolon and spaces

                        try
                        {
                            var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, _ctsDownload.Token);
                            
                            // Try to get filename from Content-Disposition header
                            string? fileName = response.Content.Headers.ContentDisposition?.FileName?.Trim('\"', ' ');
                            
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
                                
                                // Log to CSV
                                await AppendToDownloadLog(downloadDir, fileName);

                                DownloadLogMessages.Add($"[{DateTime.Now:HH:mm:ss}] ✅ Success: {fileName}");
                                count++;
                            }
                            else
                            {
                                DownloadLogMessages.Add($"[{DateTime.Now:HH:mm:ss}] ❌ Failed: {fileName} (HTTP {response.StatusCode})");
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            DownloadLogMessages.Add($"[{DateTime.Now:HH:mm:ss}] ⏹️ Stopped.");
                            break;
                        }
                        catch (Exception ex)
                        {
                            DownloadLogMessages.Add($"[{DateTime.Now:HH:mm:ss}] ⚠️ Error: {ex.Message}");
                        }
                    }

                    DownloadProgress.Value = i + 1;
                    ProgressText.Text = $"{i + 1}/{totalRows}";
                }

                DownloadLogMessages.Add($"[System] Completed. {count} files downloaded to 'Downloads' folder.");
                MessageBox.Show($"Download process finished! {count} files saved to the Downloads folder.");
            }
            catch (Exception ex)
            {
                DownloadLogMessages.Add($"[System] Fatal Error: {ex.Message}");
            }
            finally
            {
                StartDownloadBtn.IsEnabled = true;
                StopDownloadBtn.IsEnabled = false;
                BrowseFolderBtn.IsEnabled = true;
                DropZone.AllowDrop = true;
                SidebarControlsEnabled(true);

                _ctsDownload?.Dispose();
                _ctsDownload = null;
            }
        }

        private void SidebarControlsEnabled(bool enabled)
        {
            ScraperBtn.IsEnabled = enabled;
            DownloaderBtn.IsEnabled = enabled;
            SettingsBtn.IsEnabled = enabled;
            AboutBtn.IsEnabled = enabled;
        }

        private void ResultsGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            if (_settings == null) return;

            string header = e.PropertyName;
            var cfg = _settings.ScraperSettings;

            // Mapping property names to user-friendly headers and checking if they should be visible
            bool isVisible = true;
            string newHeader = header;

            switch (header)
            {
                case "No":
                    newHeader = "No";
                    break;
                case "FontName":
                    newHeader = "Name";
                    isVisible = !string.IsNullOrWhiteSpace(cfg.NameSelector);
                    break;
                case "Category":
                    newHeader = "Category";
                    isVisible = !string.IsNullOrWhiteSpace(cfg.CategorySelector);
                    break;
                case "ImageUrl":
                    newHeader = "List Image";
                    isVisible = !string.IsNullOrWhiteSpace(cfg.ImageSelector);
                    break;
                case "FontUrl":
                    newHeader = "Page URL";
                    isVisible = !string.IsNullOrWhiteSpace(cfg.UrlSelector);
                    break;
                case "Description":
                    newHeader = "Description";
                    isVisible = !string.IsNullOrWhiteSpace(cfg.DescriptionSelector);
                    break;
                case "DownloadUrl":
                    newHeader = "Direct DL URL";
                    isVisible = !string.IsNullOrWhiteSpace(cfg.DownloadButtonSelector);
                    break;
                case "Author":
                    newHeader = "Author";
                    // Author extraction is currently heuristic, we'll keep it visible if name is visible
                    isVisible = !string.IsNullOrWhiteSpace(cfg.NameSelector);
                    break;
                case "License":
                    newHeader = "License";
                    isVisible = !string.IsNullOrWhiteSpace(cfg.NameSelector);
                    break;
                case "FontImgUrl":
                    newHeader = "Detail Image";
                    isVisible = !string.IsNullOrWhiteSpace(cfg.NameSelector);
                    break;
                case "LicenseUrl":
                    newHeader = "License URL";
                    isVisible = !string.IsNullOrWhiteSpace(cfg.NameSelector);
                    break;
            }

            if (!isVisible)
            {
                e.Cancel = true; // Don't create the column
            }
            else
            {
                e.Column.Header = newHeader;
            }
        }

        private bool IsUrl(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            return Uri.TryCreate(text, UriKind.Absolute, out var uriResult)
                && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        }


        private async Task AppendToDownloadLog(string downloadDir, string fileName)
        {
            try
            {
                string logPath = Path.Combine(downloadDir, "nama-download.csv");
                string line = $"\"{fileName}\"\n";
                
                // If file doesn't exist, add header
                if (!File.Exists(logPath))
                {
                    await File.WriteAllTextAsync(logPath, "FileName\n");
                }
                
                await File.AppendAllTextAsync(logPath, line);
            }
            catch (Exception ex)
            {
                // Silently fail or log to console for background logging tool
                System.Diagnostics.Debug.WriteLine($"Error logging download: {ex.Message}");
            }
        }

    }
}
