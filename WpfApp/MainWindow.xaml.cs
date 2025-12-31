using System;
using System.Collections.ObjectModel;
using System.Windows;
using AutoBrowserDownloader.WpfApp.Core;
using AutoBrowserDownloader.WpfApp.Models;

namespace AutoBrowserDownloader.WpfApp
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<AutomationStep> Steps { get; set; }
        public ObservableCollection<ScrapeResult> Results { get; set; }

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

        private async void Start_Click(object sender, RoutedEventArgs e)
        {
            LogBox.Text = ""; 
            LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] Status: Starting scraping {UrlBox.Text}...\n");
            
            var url = UrlBox.Text;
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

                await runner.RunDeepScrapeAsync(url, config, headless: false);
                
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
    }
}
