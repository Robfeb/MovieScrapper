using System.Net;
using FilmAffinityScraper.Exceptions;
using FilmAffinityScraper.Models;
using FilmAffinityScraper.Options;
using FilmAffinityScraper.Parsing;
using Microsoft.Extensions.Options;

namespace FilmAffinityScraper.Services;

public sealed class FilmAffinityScraper : IFilmAffinityScraper
{
    private readonly HttpClient _httpClient;
    private readonly IMovieHtmlParser _parser;
    private readonly FilmAffinityOptions _options;

    public FilmAffinityScraper(
        HttpClient httpClient,
        IMovieHtmlParser parser,
        IOptions<FilmAffinityOptions>? options = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _options = options?.Value ?? new FilmAffinityOptions();
    }

    public async Task<Movie> ScrapeUrlAsync(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("URL cannot be empty.", nameof(url));

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !IsAllowedHost(uri))
            throw new ScrapingException($"Invalid or disallowed URL host: '{url}'.");

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.UserAgent.ParseAdd(_options.UserAgent);
            request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            request.Headers.AcceptLanguage.ParseAdd("en-US,en;q=0.9,es;q=0.8");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new ScrapingException(
                    $"HTTP request to '{url}' failed with status code {(int)response.StatusCode} ({response.ReasonPhrase}).");
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(html))
                throw new ScrapingException($"Received empty HTML from '{url}'.");

            return _parser.Parse(html);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ScrapingException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ScrapingException($"Failed to scrape URL '{url}': {ex.Message}", ex);
        }
    }

    public async Task<Movie?> SearchAndScrapeAsync(string title, int? year = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title))
            return null;

        var encodedTitle = WebUtility.UrlEncode(title.Trim());
        var searchUrl = $"{_options.BaseUrl.TrimEnd('/')}/{_options.Language}/search.php?stext={encodedTitle}";

          if (!Uri.TryCreate(searchUrl, UriKind.Absolute, out var uri) || !IsAllowedHost(uri))
            throw new ScrapingException($"Generated search URL host is invalid: '{searchUrl}'.");

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.UserAgent.ParseAdd(_options.UserAgent);
            request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            request.Headers.AcceptLanguage.ParseAdd("es-ES,en;q=0.9,es;q=0.8");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new ScrapingException(
                    $"Search request for '{title}' failed with status code {(int)response.StatusCode} ({response.ReasonPhrase}).");
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(html))
                return null;

            // Check if FilmAffinity redirected directly to a movie details page or returned search results
            var finalUrl = response.RequestMessage?.RequestUri?.ToString() ?? searchUrl;
            if (finalUrl.Contains("/film") && finalUrl.EndsWith(".html") && !_parser.IsSearchResultsPage(html))
            {
                return _parser.Parse(html);
            }

            if (_parser.IsSearchResultsPage(html))
            {
                var movieUrl = _parser.ExtractTopResultUrl(html, _options.BaseUrl, year);
                if (!string.IsNullOrWhiteSpace(movieUrl))
                {
                    await Task.Delay(_options.DelayMsBetweenRequests, cancellationToken);
                    return await ScrapeUrlAsync(movieUrl, cancellationToken);
                }
            }
            else
            {
                // Try parsing directly if it resembles a film page
                try
                {
                    return _parser.Parse(html);
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ScrapingException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ScrapingException($"Error executing search for movie '{title}': {ex.Message}", ex);
        }
    }

    private static bool IsAllowedHost(Uri uri)
    {
        var host = uri.Host.ToLowerInvariant();
        return host == "filmaffinity.com" || host.EndsWith(".filmaffinity.com");
    }
}
