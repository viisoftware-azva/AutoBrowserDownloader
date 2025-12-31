using System;

namespace AutoBrowserDownloader.WpfApp.Models
{
    public class ScrapeResult
    {
        public int No { get; set; }
        public string FontName { get; set; }
        public string Category { get; set; }
        public string ImageUrl { get; set; }
        public string FontUrl { get; set; }
        public string Description { get; set; }
        public string DownloadUrl { get; set; }
    }
}
