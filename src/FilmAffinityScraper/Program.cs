using System.Text.Json;
using FilmAffinityScraper.Models;
using FilmAffinityScraper.Options;
using FilmAffinityScraper.Parsing;
using FilmAffinityScraper.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FilmAffinityScraper;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("==============================================");
        Console.WriteLine("        FilmAffinity Movie Scraper            ");
        Console.WriteLine("==============================================");

        // Parse CLI parameters
        string? inputExcelPath = null;
        string? outputExcelPath = null;
        int? maxRowsToProcess = null;
        int delayMs = 3000; // Default 3-second delay between requests
        bool singleUrlMode = false;
        string? singleUrl = null;

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg.Equals("--single", StringComparison.OrdinalIgnoreCase) || arg.Equals("-1", StringComparison.OrdinalIgnoreCase))
            {
                maxRowsToProcess = 1;
            }
            else if (arg.Equals("--limit", StringComparison.OrdinalIgnoreCase) || arg.Equals("-n", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length && int.TryParse(args[i + 1], out var limit))
                {
                    maxRowsToProcess = limit;
                    i++;
                }
            }
            else if (arg.Equals("--delay", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length && int.TryParse(args[i + 1], out var delay))
                {
                    delayMs = delay;
                    i++;
                }
            }
            else if (Uri.TryCreate(arg, UriKind.Absolute, out var uri) && uri.Host.Contains("filmaffinity.com"))
            {
                singleUrlMode = true;
                singleUrl = arg;
            }
            else if (int.TryParse(arg, out var numericLimit) && numericLimit > 0 && maxRowsToProcess == null)
            {
                maxRowsToProcess = numericLimit;
            }
            else if (inputExcelPath == null)
            {
                inputExcelPath = arg;
            }
            else if (outputExcelPath == null)
            {
                outputExcelPath = arg;
            }
        }

        var services = new ServiceCollection();
        ConfigureServices(services, delayMs);

        using var serviceProvider = services.BuildServiceProvider();
        var scraper = serviceProvider.GetRequiredService<IFilmAffinityScraper>();
        var excelProcessor = serviceProvider.GetRequiredService<IExcelProcessor>();
        var options = serviceProvider.GetRequiredService<IOptions<FilmAffinityOptions>>().Value;

        if (singleUrlMode && singleUrl != null)
        {
            return await HandleSingleUrlScrapeAsync(scraper, singleUrl);
        }

        // Determine input Excel path
        inputExcelPath ??= Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "movies.xlsx");
        if (!File.Exists(inputExcelPath))
        {
            inputExcelPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "data", "movies.xlsx"));
        }

        if (!File.Exists(inputExcelPath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: Could not find Excel file at path: '{inputExcelPath}'");
            Console.ResetColor();
            return 1;
        }

        // Save in-place or to output destination
        outputExcelPath ??= inputExcelPath;

        return await ProcessExcelFileAsync(scraper, excelProcessor, options, inputExcelPath, outputExcelPath, maxRowsToProcess);
    }

    private static void ConfigureServices(IServiceCollection services, int delayMs)
    {
        services.Configure<FilmAffinityOptions>(options =>
        {
            options.BaseUrl = "https://www.filmaffinity.com";
            options.Language = "es";
            options.Timeout = TimeSpan.FromSeconds(30);
            options.DelayMsBetweenRequests = delayMs;
        });

        services.AddSingleton<IMovieHtmlParser, MovieHtmlParser>();
        services.AddSingleton<IExcelProcessor, ExcelProcessor>();

        services.AddHttpClient<IFilmAffinityScraper, Services.FilmAffinityScraper>((sp, client) =>
        {
            var opt = sp.GetRequiredService<IOptions<FilmAffinityOptions>>().Value;
            client.Timeout = opt.Timeout;
        });
    }

    private static async Task<int> HandleSingleUrlScrapeAsync(IFilmAffinityScraper scraper, string url)
    {
        Console.WriteLine($"Scraping single URL: {url}");
        try
        {
            var movie = await scraper.ScrapeUrlAsync(url);
            var json = JsonSerializer.Serialize(movie, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            Console.WriteLine("\nScraped Movie Data (JSON):");
            Console.WriteLine(json);
            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error scraping URL: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    private static async Task<int> ProcessExcelFileAsync(
        IFilmAffinityScraper scraper,
        IExcelProcessor excelProcessor,
        FilmAffinityOptions options,
        string inputExcelPath,
        string outputExcelPath,
        int? maxRowsToProcess)
    {
        Console.WriteLine($"Loading movies Excel file: {inputExcelPath}");
        var rows = excelProcessor.ReadRows(inputExcelPath);
        Console.WriteLine($"Total rows in sheet: {rows.Count}");
        Console.WriteLine($"Configuration: Throttling Delay = {options.DelayMsBetweenRequests}ms (3 seconds)");
        if (maxRowsToProcess.HasValue)
        {
            Console.WriteLine($"Max rows limit enabled: Processing up to {maxRowsToProcess.Value} row(s).");
        }
        Console.WriteLine("----------------------------------------------\n");

        // Copy input to output file if different and output doesn't exist
        if (!inputExcelPath.Equals(outputExcelPath, StringComparison.OrdinalIgnoreCase) && !File.Exists(outputExcelPath))
        {
            File.Copy(inputExcelPath, outputExcelPath, true);
        }

        string targetSavePath = outputExcelPath;
        int processedCount = 0;
        int skippedCount = 0;
        int succeededCount = 0;
        int errorCount = 0;

        foreach (var row in rows)
        {
            if (maxRowsToProcess.HasValue && processedCount >= maxRowsToProcess.Value)
            {
                Console.WriteLine($"\n[Limit Reached] Processed target limit of {maxRowsToProcess.Value} row(s). Stopping.");
                break;
            }

            // IDEMPOTENCY CHECK: Skip if data is already populated or marked as Found
            if (IsRowFullyPopulated(row))
            {
                skippedCount++;
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"[Row {row.RowIndex}] Skipping '{row.Title}' - Already populated (Status: {row.Status ?? "Found"}).");
                Console.ResetColor();
                continue;
            }

            processedCount++;
            Console.WriteLine($"[{processedCount}/{(maxRowsToProcess ?? rows.Count)}] Searching row {row.RowIndex}: '{row.Title}' (Year: {row.Year?.ToString() ?? "N/A"})...");

            try
            {
                var movie = await scraper.SearchAndScrapeAsync(row.Title!, row.Year);
                
                // Validate if HTTP request retrieved expected values
                if (movie != null && HasExpectedValues(movie))
                {
                    row.Director = movie.Director ?? row.Director;
                    row.Genre = movie.Genre ?? row.Genre;
                    row.Country = movie.Country ?? row.Country;
                    row.OriginalTitle = movie.OriginalTitle ?? row.OriginalTitle;
                    row.Language = movie.Language ?? row.Language;
                    row.Synopsis = movie.Synopsis ?? row.Synopsis;
                    row.Status = "Found";

                    succeededCount++;

                    // Save row immediately to Excel file for idempotency
                    excelProcessor.UpdateSingleRow(targetSavePath, row);

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"   ✓ Found & Saved: '{movie.OriginalTitle ?? row.Title}' | Director: {movie.Director ?? "N/A"} | Country: {movie.Country ?? "N/A"}");
                    Console.ResetColor();
                }
                else
                {
                    errorCount++;
                    row.Status = "Error: Not Found";

                    // Save error status immediately to Excel file
                    excelProcessor.UpdateSingleRow(targetSavePath, row);

                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"   ⚠ Row {row.RowIndex} error: No valid matching movie returned from FilmAffinity. Marked as 'Error: Not Found'.");
                    Console.ResetColor();
                }
            }
            catch (Exception ex)
            {
                errorCount++;
                row.Status = $"Error: {ex.Message}";

                excelProcessor.UpdateSingleRow(targetSavePath, row);

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"   ✗ Row {row.RowIndex} exception: {ex.Message}. Marked as Error.");
                Console.ResetColor();
            }

            // Throttling: Wait 3 seconds between HTTP requests
            Console.WriteLine($"   ⏳ Waiting {options.DelayMsBetweenRequests}ms before next request...");
            await Task.Delay(options.DelayMsBetweenRequests);
        }

        Console.WriteLine("\n----------------------------------------------");
        Console.WriteLine($"Summary: Processed = {processedCount}, Skipped = {skippedCount}, Succeeded = {succeededCount}, Errors = {errorCount}");
        Console.WriteLine($"Updated Excel file saved at: {targetSavePath}");
        Console.WriteLine("----------------------------------------------");

        return 0;
    }

    private static bool IsRowFullyPopulated(ExcelMovieRow row)
    {
        if (string.Equals(row.Status, "Found", StringComparison.OrdinalIgnoreCase))
            return true;

        return !string.IsNullOrWhiteSpace(row.Director) &&
               !string.IsNullOrWhiteSpace(row.Genre) &&
               !string.IsNullOrWhiteSpace(row.Country) &&
               !string.IsNullOrWhiteSpace(row.Synopsis);
    }

    private static bool HasExpectedValues(Movie movie)
    {
        // Expect at least Director or Synopsis or Country/Genre to be present to consider the scrape valid
        return !string.IsNullOrWhiteSpace(movie.Director) ||
               !string.IsNullOrWhiteSpace(movie.Genre) ||
               !string.IsNullOrWhiteSpace(movie.Synopsis) ||
               !string.IsNullOrWhiteSpace(movie.Country);
    }
}
