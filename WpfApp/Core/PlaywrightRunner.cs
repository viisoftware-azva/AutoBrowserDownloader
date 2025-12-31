using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Playwright;
using AutoBrowserDownloader.WpfApp.Models;

namespace AutoBrowserDownloader.WpfApp.Core
{
    public class PlaywrightRunner
    {
        public event Action<string>? OnLog;
        public event Action<ScrapeResult>? OnResultFound;

        public async Task RunAsync(string url, List<AutomationStep> steps, bool headless = false)
        {
            await RunDeepScrapeAsync(url, new ScraperConfig(), headless);
        }

        public async Task RunDeepScrapeAsync(string startUrl, ScraperConfig config, bool headless)
        {
            try
            {
                // Ensure dependencies are installed
                await DependencyInstaller.EnsurePlaywrightInstalledAsync(Log);

                // Launch Browser
                using var playwright = await Playwright.CreateAsync();
                IBrowser browser;
                try
                {
                    browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions 
                    { 
                        Headless = headless, 
                        Channel = "chrome", 
                        Args = new[] { "--start-maximized" } 
                    });
                }
                catch
                {
                    Log("Chrome not found, using Edge...");
                    browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions 
                    { 
                        Headless = headless, 
                        Channel = "msedge", 
                        Args = new[] { "--start-maximized" } 
                    });
                }

                var context = await browser.NewContextAsync(new BrowserNewContextOptions { ViewportSize = ViewportSize.NoViewport });
                var page = await context.NewPageAsync();

                Log($"Navigating to {startUrl}...");
                await page.GotoAsync(startUrl, new PageGotoOptions { Timeout = 60000 });
                try { await page.WaitForLoadStateAsync(LoadState.NetworkIdle); } catch { Log("Network idle timeout, continuing..."); }

                // 1. Find all items in the list
                Log($"Scanning for items using selector: '{config.ItemContainer}'...");
                var items = await page.QuerySelectorAllAsync(config.ItemContainer);
                
                if (items.Count == 0)
                {
                    Log("No items found! Check selectors.");
                }
                else
                {
                    Log($"Found {items.Count} items. Extracting basic info...");
                }

                var basicResults = new List<ScrapeResult>();
                int counter = 1;

                // 2. Extract basic info and URLs from Listing Page
                foreach (var item in items)
                {
                    var result = new ScrapeResult { No = counter++ };
                    
                    try 
                    {
                        var nameEl = await item.QuerySelectorAsync(config.NameSelector);
                        if (nameEl != null) result.FontName = (await nameEl.InnerTextAsync()).Trim();

                        var catEl = await item.QuerySelectorAsync(config.CategorySelector);
                        if (catEl != null) result.Category = (await catEl.InnerTextAsync()).Trim();

                        var imgEl = await item.QuerySelectorAsync(config.ImageSelector);
                        if (imgEl != null) result.ImageUrl = await imgEl.GetAttributeAsync("src");

                        var urlEl = await item.QuerySelectorAsync(config.UrlSelector);
                        if (urlEl != null) result.FontUrl = await urlEl.GetAttributeAsync("href");

                        if (!string.IsNullOrEmpty(result.FontUrl))
                        {
                            basicResults.Add(result);
                            Log($"Found: {result.FontName}");
                        }
                    }
                    catch (Exception ex) 
                    { 
                        Log($"Error extracting item: {ex.Message}"); 
                    }
                }

                // 3. Visit each URL to get details
                Log($"Collected {basicResults.Count} links. Starting detail scrape...");
                
                foreach (var res in basicResults)
                {
                    try 
                    {
                        if (string.IsNullOrEmpty(res.FontUrl)) continue;

                        Log($"Visiting: {res.FontUrl}...");
                        await page.GotoAsync(res.FontUrl);
                        
                        if (!string.IsNullOrEmpty(config.DescriptionSelector))
                        {
                            var descEl = await page.QuerySelectorAsync(config.DescriptionSelector);
                            if (descEl != null) 
                            {
                                var text = await descEl.InnerTextAsync();
                                res.Description = text.Length > 100 ? text.Substring(0, 100).Replace("\n", " ") + "..." : text;
                            }
                        }

                        if (!string.IsNullOrEmpty(config.DownloadButtonSelector))
                        {
                            var dlEl = await page.QuerySelectorAsync(config.DownloadButtonSelector);
                            if (dlEl != null) res.DownloadUrl = await dlEl.GetAttributeAsync("href");
                        }
                        
                        if (string.IsNullOrEmpty(res.DownloadUrl)) res.DownloadUrl = "Not Found";

                        OnResultFound?.Invoke(res);
                        await page.WaitForTimeoutAsync(1000); 
                    }
                    catch (Exception ex)
                    {
                        Log($"Failed detailed scrape for {res.FontName}: {ex.Message}");
                    }
                }

                // 4. Save to CSV
                try 
                {
                    var csvPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scraped_fonts.csv");
                    var csvLines = new List<string> { "No,Name,Category,FontUrl,DownloadUrl,Description" };
                    foreach(var r in basicResults)
                    {
                        string SafeCsv(string s) => s?.Replace("\"", "\"\"") ?? "";
                        csvLines.Add($"{r.No},\"{SafeCsv(r.FontName)}\",\"{SafeCsv(r.Category)}\",\"{SafeCsv(r.FontUrl)}\",\"{SafeCsv(r.DownloadUrl)}\",\"{SafeCsv(r.Description)}\"");
                    }
                    await System.IO.File.WriteAllLinesAsync(csvPath, csvLines);
                    Log($"Saved results to {csvPath}");
                }
                catch(Exception ex)
                {
                    Log("Error saving CSV: " + ex.Message);
                }

                Log("Scraping Completed.");
            }
            catch (Exception ex)
            {
                Log($"FATAL ERROR: {ex.Message}");
            }
        }

        private void Log(string message)
        {
            OnLog?.Invoke(message);
        }
    }
}
