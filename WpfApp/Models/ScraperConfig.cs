namespace AutoBrowserDownloader.WpfApp.Models
{
    public class ScraperConfig
    {
        // List Page Selectors
        public string ItemContainer { get; set; } = "article"; // Example: article or .product
        public string NameSelector { get; set; } = "h2 a"; 
        public string CategorySelector { get; set; } = ".cat-links";
        public string ImageSelector { get; set; } = "img";
        public string UrlSelector { get; set; } = "h2 a"; // The link to click/visit

        // Detail Page Selectors
        public string DescriptionSelector { get; set; } = ".entry-content";
        public string DownloadButtonSelector { get; set; } = "a:has-text('Download')"; 
    }
}
