namespace AutoBrowserDownloader.WpfApp.Models
{
    public enum ActionType
    {
        Navigate,
        Click,
        Wait,
        Input
    }

    public class AutomationStep
    {
        public ActionType Type { get; set; }
        public string Selector { get; set; } = "";
        public string FallbackSelector { get; set; } = ""; // Anti-break strategy
        public string Value { get; set; } = "";
        public string Description { get; set; } = "";
        public bool IsOptional { get; set; } = false;
    }
}
