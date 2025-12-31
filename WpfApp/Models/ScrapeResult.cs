using System;

namespace AutoBrowserDownloader.WpfApp.Models
{
    public class ScrapeResult
    {
        public int No { get; set; }
        public string FontName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string FontUrl { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string License { get; set; } = string.Empty;
        public string FontImgUrl { get; set; } = string.Empty;
        public string LicenseUrl { get; set; } = string.Empty;
    }
}
