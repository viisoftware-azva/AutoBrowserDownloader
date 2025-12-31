using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
                        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

                        // --- 1. Extract Category from H2 ---
                        // User Logic: "Adelora Serif Font" -> "Serif" (word before Font)
                        var h2El = await page.QuerySelectorAsync("h2");
                        if (h2El != null)
                        {
                            var h2Text = (await h2El.InnerTextAsync()).Trim();
                            // Logic: Remove " Font" from end, take the last word
                            var cleanText = Regex.Replace(h2Text, @"\s+Font$", "", RegexOptions.IgnoreCase);
                            var parts = cleanText.Split(' ');
                            if (parts.Length > 0)
                            {
                                res.Category = parts.Last();
                            }
                        }

                        // --- 2. Extract Author / Contact ---
                        // User Logic: "before <h2> there is link or email"
                        // We'll try to get the text of the element immediately preceding the H2
                        var authorText = await page.EvaluateAsync<string>(@"() => {
                            const h2 = document.querySelector('h2');
                            if (!h2) return '';
                            let prev = h2.previousElementSibling;
                            // Traverse back until we find something substantial
                            while(prev && prev.innerText.trim().length === 0) {
                                prev = prev.previousElementSibling;
                            }
                            return prev ? prev.innerText.trim() : '';
                        }");
                        if (!string.IsNullOrEmpty(authorText))
                        {
                            res.Author = authorText;
                        }

                        // --- 3. Extract License ---
                        // Look for element containing "License"
                        var licenseEl = await page.QuerySelectorAsync("text=/License/i");
                        if (licenseEl != null)
                        {
                            // Try to get the full text of that element or its parent
                            var text = await licenseEl.InnerTextAsync();
                            // If just 'License', maybe next sibling? For now, grab the text line.
                            // Cleaner: split by newline/colon
                            res.License = text.Replace("License", "").Replace(":", "").Trim();
                        }
                        
                        // --- 4. Description ---
                        if (!string.IsNullOrEmpty(config.DescriptionSelector))
                        {
                            var descEl = await page.QuerySelectorAsync(config.DescriptionSelector);
                            if (descEl != null) 
                            {
                                var text = await descEl.InnerTextAsync();
                                res.Description = text.Length > 150 ? text.Substring(0, 150).Replace("\n", " ") + "..." : text;
                            }
                        }

                        // --- 5. Real Download Link ---
                        // User Logic: Button leads to a page, real link is on that page.
                        string preDownloadUrl = null;
                        if (!string.IsNullOrEmpty(config.DownloadButtonSelector))
                        {
                            var dlEl = await page.QuerySelectorAsync(config.DownloadButtonSelector);
                            if (dlEl != null) preDownloadUrl = await dlEl.GetAttributeAsync("href");
                        }

                        if (!string.IsNullOrEmpty(preDownloadUrl))
                        {
                            Log($"  > Follow download link: {preDownloadUrl}...");
                            // Check if it's absolute or relative
                            if (!preDownloadUrl.StartsWith("http"))
                            {
                                var uri = new Uri(new Uri(res.FontUrl), preDownloadUrl);
                                preDownloadUrl = uri.ToString();
                            }

                            await page.GotoAsync(preDownloadUrl);
                            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
                            
                            // Now find the REAL download link (usually .zip, .rar, .ttf, .otf)
                            // Or a link that contains "download" but is NOT the one we just clicked (heuristic)
                            
                            // 1. Try to find a link ending in common font extensions
                            var finalLink = await page.QuerySelectorAsync("a[href$='.zip'], a[href$='.rar'], a[href$='.ttf'], a[href$='.otf']");
                            
                            // 2. Fallback: Try a link text "Click here to download" or similar
                            if (finalLink == null)
                            {
                                finalLink = await page.QuerySelectorAsync("a:has-text('Click here')");
                            }

                            if (finalLink != null)
                            {
                                res.DownloadUrl = await finalLink.GetAttributeAsync("href");
                            }
                            else
                            {
                                // Maybe the previous button was the direct link after all? 
                                // Or we failed to find it. Just use current URL or mark not found.
                                res.DownloadUrl = "Not Found on Page 2";
                            }
                        }
                        else
                        {
                            res.DownloadUrl = "No Initial Button";
                        }

                        OnResultFound?.Invoke(res);
                        // Be nice to the server
                        await page.WaitForTimeoutAsync(500); 
                    }
                    catch (Exception ex)
                    {
                        Log($"Failed detailed scrape for {res.FontName}: {ex.Message}");
                    }
                }

                // 4. Save to CSV
                try 
                {
                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    string csvPath = System.IO.Path.Combine(baseDir, "scraped_fonts.csv");
                    
                    int fileCounter = 0;
                    bool saved = false;

                    while (!saved && fileCounter < 100)
                    {
                        try 
                        {
                            var csvLines = new List<string> { "No,Name,Category,Author,License,FontUrl,DownloadUrl,Description" };
                            foreach(var r in basicResults)
                            {
                                string SafeCsv(string s) => s?.Replace("\"", "\"\"") ?? "";
                                csvLines.Add($"{r.No},\"{SafeCsv(r.FontName)}\",\"{SafeCsv(r.Category)}\",\"{SafeCsv(r.Author)}\",\"{SafeCsv(r.License)}\",\"{SafeCsv(r.FontUrl)}\",\"{SafeCsv(r.DownloadUrl)}\",\"{SafeCsv(r.Description)}\"");
                            }
                            await System.IO.File.WriteAllLinesAsync(csvPath, csvLines);
                            Log($"Saved results to {System.IO.Path.GetFileName(csvPath)}");
                            saved = true;
                        }
                        catch (System.IO.IOException)
                        {
                            fileCounter++;
                            csvPath = System.IO.Path.Combine(baseDir, $"scraped_fonts_{fileCounter}.csv");
                        }
                    }

                    if (!saved) Log("Failed to save CSV after multiple attempts.");
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
