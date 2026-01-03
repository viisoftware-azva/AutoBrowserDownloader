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

        public async Task RunAsync(string url, List<AutomationStep> steps, bool headless, AppSettings settings, System.Threading.CancellationToken ct = default)
        {
            await RunDeepScrapeAsync(url, new ScraperConfig(), headless, settings, ct);
        }

        public async Task RunDeepScrapeAsync(string startUrl, ScraperConfig config, bool headless, AppSettings settings, System.Threading.CancellationToken ct = default)
        {
            try
            {
                // Ensure dependencies are installed
                await DependencyInstaller.EnsurePlaywrightInstalledAsync(Log);

                // Initialize high-level counter if needed
                int resultCounter = settings.ScrapedUrls.Count + 1;
                HashSet<string> seenUrls = new HashSet<string>(settings.ScrapedUrls);

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

                var basicResults = new List<ScrapeResult>();
                
                int startPage = settings.LastPage + 1;
                int endPage = startPage + (settings.PagesToScrape - 1); 

                for (int pageNum = startPage; pageNum <= endPage; pageNum++)
                {
                    if (ct.IsCancellationRequested) break;

                    string currentUrl = startUrl;
                    if (pageNum > 1)
                    {
                        currentUrl = startUrl.TrimEnd('/') + $"/page/{pageNum}/";
                    }

                    Log($"--- Processing Page {pageNum}: {currentUrl} ---");
                    
                    try 
                    {
                        await page.GotoAsync(currentUrl, new PageGotoOptions { Timeout = 60000 });
                        try { await page.WaitForLoadStateAsync(LoadState.NetworkIdle); } catch { }

                        var items = await page.QuerySelectorAllAsync(config.ItemContainer);
                        if (items.Count == 0)
                        {
                            Log($"No items found on page {pageNum}. Ending pagination.");
                            break;
                        }

                        Log($"Found {items.Count} items on page {pageNum}.");

                        foreach (var item in items)
                        {
                            try 
                            {
                                var urlEl = await item.QuerySelectorAsync(config.UrlSelector);
                                if (urlEl != null) 
                                {
                                    string fontUrl = await urlEl.GetAttributeAsync("href") ?? "";
                                    if (!string.IsNullOrEmpty(fontUrl))
                                    {
                                        if (!fontUrl.StartsWith("http"))
                                        {
                                            try {
                                                var uri = new Uri(new Uri(page.Url), fontUrl);
                                                fontUrl = uri.ToString();
                                            } catch { }
                                        }

                                        if (seenUrls.Contains(fontUrl))
                                        {
                                            // Skip already scraped
                                            continue;
                                        }

                                        var result = new ScrapeResult { No = resultCounter++ };
                                        result.FontUrl = fontUrl;

                                        var nameEl = await item.QuerySelectorAsync(config.NameSelector);
                                        if (nameEl != null) result.FontName = (await nameEl.InnerTextAsync()).Trim();

                                        var catEl = await item.QuerySelectorAsync(config.CategorySelector);
                                        if (catEl != null) result.Category = (await catEl.InnerTextAsync()).Trim();

                                        var imgEl = await item.QuerySelectorAsync(config.ImageSelector);
                                        if (imgEl != null) result.ImageUrl = await imgEl.GetAttributeAsync("src") ?? "";

                                        basicResults.Add(result);
                                        seenUrls.Add(fontUrl); // Prevent duplicates in the same batch
                                    }
                                }
                            }
                            catch (Exception ex) { Log($"  Error extracting item summary: {ex.Message}"); }
                        }

                        // Update current page in settings and save after each list page processed
                        settings.LastPage = pageNum;
                        settings.Save();
                    }
                    catch (Exception ex)
                    {
                        Log($"  Failed to load list page {pageNum}: {ex.Message}. Stopping pagination.");
                        break; 
                    }
                }

                // 3. Visit each URL to get details
                Log($"Collected {basicResults.Count} links. Starting detail scrape with {settings.MaxThreads} threads...");
                
                var semaphore = new System.Threading.SemaphoreSlim(settings.MaxThreads);
                var tasks = new List<Task>();

                foreach (var res in basicResults)
                {
                    if (ct.IsCancellationRequested) break;
                    
                    await semaphore.WaitAsync(ct);
                    
                    tasks.Add(Task.Run(async () => 
                    {
                        IPage? detailPage = null;
                        try 
                        {
                            if (string.IsNullOrEmpty(res.FontUrl)) return;

                            detailPage = await context.NewPageAsync();
                            Log($"Visiting: {res.FontUrl}...");
                            await detailPage.GotoAsync(res.FontUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 60000 });
                            try { await detailPage.WaitForTimeoutAsync(settings.ScrapeDelay); } catch { } 
                            
                            // --- 1. Extract Category from H2 ---
                            var h2El = await detailPage.QuerySelectorAsync("h2");
                            if (h2El != null)
                            {
                                var h2Text = (await h2El.InnerTextAsync()).Trim();
                                var cleanText = Regex.Replace(h2Text, @"\s+Font$", "", RegexOptions.IgnoreCase);
                                var parts = cleanText.Split(' ');
                                if (parts.Length > 0) res.Category = parts.Last();
                            }

                            // --- 2. Extract Author / Contact ---
                            var authorText = await detailPage.EvaluateAsync<string>(@"() => {
                                const h2 = document.querySelector('h2');
                                if (!h2) return '';
                                let prev = h2.previousElementSibling;
                                while(prev && prev.innerText.trim().length === 0) {
                                    prev = prev.previousElementSibling;
                                }
                                return prev ? prev.innerText.trim() : '';
                            }");
                            if (!string.IsNullOrEmpty(authorText)) res.Author = authorText;

                            // --- 3. Extract License ---
                            var licenseEl = await detailPage.QuerySelectorAsync("text=/License/i");
                            if (licenseEl != null)
                            {
                                var text = await licenseEl.InnerTextAsync();
                                res.License = text.Replace("License", "").Replace(":", "").Trim();
                            }

                            // --- 3.1 Extract FontImgUrl ---
                            var firstImg = await detailPage.QuerySelectorAsync(".entry-content img, article img");
                            if (firstImg != null) res.FontImgUrl = await firstImg.GetAttributeAsync("src") ?? "";

                            // --- 3.2 Extract LicenseUrl ---
                            var hereEl = await detailPage.QuerySelectorAsync("strong:has-text('HERE') a, a:has(strong:has-text('HERE'))");
                            if (hereEl != null) res.LicenseUrl = await hereEl.GetAttributeAsync("href") ?? "";
                            
                            // --- 4. Description ---
                            if (!string.IsNullOrEmpty(config.DescriptionSelector))
                            {
                                var descEl = await detailPage.QuerySelectorAsync(config.DescriptionSelector);
                                if (descEl != null) 
                                {
                                    var text = await descEl.InnerTextAsync();
                                    res.Description = text.Length > 150 ? text.Substring(0, 150).Replace("\n", " ") + "..." : text;
                                }
                            }

                            // --- 5. Real Download Link ---
                            string? preDownloadUrl = null;
                            if (!string.IsNullOrEmpty(config.DownloadButtonSelector))
                            {
                                var dlEl = await detailPage.QuerySelectorAsync(config.DownloadButtonSelector);
                                if (dlEl != null) preDownloadUrl = await dlEl.GetAttributeAsync("href");
                            }

                            if (!string.IsNullOrEmpty(preDownloadUrl))
                            {
                                if (!preDownloadUrl!.StartsWith("http"))
                                {
                                    var uri = new Uri(new Uri(res.FontUrl!), preDownloadUrl);
                                    preDownloadUrl = uri.ToString();
                                }

                                await detailPage.GotoAsync(preDownloadUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 60000 });
                                try { await detailPage.WaitForTimeoutAsync(settings.ScrapeDelay / 2); } catch { }
                                
                                var finalLink = await detailPage.QuerySelectorAsync("a[href$='.zip'], a[href$='.rar'], a[href$='.ttf'], a[href$='.otf']");
                                if (finalLink == null) finalLink = await detailPage.QuerySelectorAsync("a:has-text('Click here')");

                                if (finalLink != null) res.DownloadUrl = await finalLink.GetAttributeAsync("href") ?? "Download URL missing";
                                else res.DownloadUrl = "Not Found on Page 2";
                            }
                            else res.DownloadUrl = "No Initial Button";

                            lock (settings)
                            {
                                settings.ScrapedUrls.Add(res.FontUrl);
                                settings.Save();
                            }

                            OnResultFound?.Invoke(res);
                            await detailPage.WaitForTimeoutAsync(500); 
                        }
                        catch (Exception ex)
                        {
                            Log($"Failed detailed scrape for {res.FontName}: {ex.Message}");
                        }
                        finally
                        {
                            if (detailPage != null) await detailPage.CloseAsync();
                            semaphore.Release();
                        }
                    }, ct));
                }
                
                await Task.WhenAll(tasks);

                // 4. Save to CSV
                try 
                {
                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    string csvPath = System.IO.Path.Combine(baseDir, "output_result.csv");
                    
                    int fileCounter = 0;
                    bool saved = false;

                    while (!saved && fileCounter < 100)
                    {
                        try 
                        {
                            bool fileExists = System.IO.File.Exists(csvPath);
                            var csvLines = new List<string>();
                            
                            if (!fileExists)
                            {
                                csvLines.Add("No,Name,Category,Author,License,LicenseUrl,FontImgUrl,FontUrl,DownloadUrl,Description");
                            }

                             foreach(var r in basicResults)
                            {
                                // "Smart" filter: only save if we at least reached the detail page successfully 
                                // (If DownloadUrl is empty/null, it means detail scrape failed or was skipped)
                                if (string.IsNullOrEmpty(r.DownloadUrl)) continue;

                                string SafeCsv(string s) => s?.Replace("\"", "\"\"") ?? "";
                                csvLines.Add($"{r.No},\"{SafeCsv(r.FontName)}\",\"{SafeCsv(r.Category)}\",\"{SafeCsv(r.Author)}\",\"{SafeCsv(r.License)}\",\"{SafeCsv(r.LicenseUrl)}\",\"{SafeCsv(r.FontImgUrl)}\",\"{SafeCsv(r.FontUrl)}\",\"{SafeCsv(r.DownloadUrl)}\",\"{SafeCsv(r.Description)}\"");
                            }

                            if (csvLines.Count > 0)
                            {
                                await System.IO.File.AppendAllLinesAsync(csvPath, csvLines);
                                Log($"Saved/Appended {basicResults.Count} results to {System.IO.Path.GetFileName(csvPath)}");
                            }
                            saved = true;
                        }
                        catch (System.IO.IOException)
                        {
                            fileCounter++;
                            csvPath = System.IO.Path.Combine(baseDir, $"output_result_{fileCounter}.csv");
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
