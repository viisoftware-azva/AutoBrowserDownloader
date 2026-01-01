using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace AutoBrowserDownloader.WpfApp.Models
{
    public class AppSettings
    {
        public string LastUrl { get; set; } = "https://www.dafontfree.io/";
        public int LastPage { get; set; } = 0;
        public int PagesToScrape { get; set; } = 10;
        public int ScrapeDelay { get; set; } = 1000;
        public List<string> ScrapedUrls { get; set; } = new List<string>();
        public string DownloadPath { get; set; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Downloads");
        public ScraperConfig ScraperSettings { get; set; } = new ScraperConfig();

        private static string SettingsPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Settings.json");

        public static AppSettings Load()
        {
            if (File.Exists(SettingsPath))
            {
                try
                {
                    string json = File.ReadAllText(SettingsPath);
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
                catch { }
            }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, json);
            }
            catch { }
        }
    }
}
