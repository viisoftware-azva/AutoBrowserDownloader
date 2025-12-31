# 🚀 AzvaScraper

**AzvaScraper** is a professional-grade, automated web scraping and downloading suite designed for efficiency and precision. Built with C# and powered by Playwright, it provides a seamless experience for extracting data and managing large-scale downloads from complex web architectures.

---

## ✨ Features

-   **Deep Web Scraping**: Advanced multi-page crawling logic designed specifically for font archival and asset discovery.
-   **Smart Downloader**: Dynamic CSV processing with automatic URL column detection and intelligent fallback mechanisms.
-   **Real-time Progress Tracking**: Live status logs, progress bars, and execution counters for both scraping and downloading tasks.
-   **Elegant UI/UX**: A modern, sidebar-driven interface with a glassmorphic aesthetic and intuitive workflow.
-   **Robust Error Handling**: Comprehensive validation for network failures, file I/O errors, and data inconsistencies.
-   **Stop & Resume**: Integrated cancellation tokens allow users to pause operations at any moment gracefully.

---

## 🛠️ Technology Stack

-   **Core Platform**: .NET 8.0 (WPF)
-   **Automation Engine**: [Playwright for .NET](https://playwright.dev/dotnet/)
-   **UI Framework**: XAML with Modern Styling
-   **Data Management**: JSON-based persistent settings and CSV export/import
-   **Networking**: Optimized `HttpClient` with custom header handling (Content-Disposition support)

---

## 🚀 Getting Started

### Prerequisites

-   [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
-   Windows OS (for WPF runtime)

### Installation

1.  **Clone the Repository**
    ```bash
    git clone https://github.com/yourusername/AzvaScraper.git
    cd AzvaScraper
    ```

2.  **Build the Project**
    ```bash
    dotnet build
    ```

3.  **Run the Application**
    Navigate to `bin/Debug/net8.0-windows/` and launch `AutoBrowserDownloader.exe` or use:
    ```bash
    dotnet run
    ```

---

## 📖 Usage Guide

### 1. Web Scraper
-   Enter the target URL in the sidebar.
-   The scraper is pre-configured for high-authority download sites but can be adapted via `ScraperConfig.cs`.
-   Monitor live results in the **Dashboard** and logs in the bottom panel.
-   Results are automatically saved to `scraped_fonts.csv`.

### 2. Smart Downloader
-   Navigate to the **Downloader** tab.
-   Drag and drop your CSV file or click the zone to browse.
-   **Smart Detection**: AzvaScraper automatically finds the URL column regardless of headers (it looks for common keywords or actual URL patterns).
-   Select your preferred **Download Subdirectory**.
-   Click **Start Downloading** and watch the progress bar.

---

## 📁 Project Structure

```text
├── WpfApp/
│   ├── Assets/             # Visual resources & Branding
│   ├── Core/               # Automation & Scraper Engines
│   ├── Models/             # Data structures and Settings logic
│   ├── MainWindow.xaml     # Primary UI Layout
│   └── AboutWindow.xaml    # Application Information
├── Downloads/              # Default download directory
└── Settings.json           # User preferences and session data
```

---

## 🤝 Contributing

Contributions are welcome! If you'd like to improve AzvaScraper, please fork the repository and create a pull request.

1. Fork the Project
2. Create your Feature Branch (`git checkout -b feature/AmazingFeature`)
3. Commit your Changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the Branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

---

## 📄 License

Distributed under the MIT License. See `LICENSE` for more information.

---

## 📬 Contact

Developed with ❤️ by **ViiSoftware**  
*Empowering automation for the modern web.*
