using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using FilmAffinityScraper.Models;
using FilmAffinityScraper.Options;
using HtmlAgilityPack;

namespace FilmAffinityScraper.Parsing;

public sealed class MovieHtmlParser : IMovieHtmlParser
{
    private readonly FilmAffinityOptions _options;

    public MovieHtmlParser(FilmAffinityOptions? options = null)
    {
        _options = options ?? new FilmAffinityOptions();
    }

    public Movie Parse(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            throw new ArgumentException("HTML content cannot be empty.", nameof(html));

        var document = new HtmlDocument();
        document.LoadHtml(html);

        var movieId = GetMovieId(document);
        var runningTime = GetLabeledValue(document, "Running time") 
                          ?? GetLabeledValue(document, "Duración");
        var genre = GetLabeledValue(document, "Genre") 
                    ?? GetLabeledValue(document, "Género");

        return new Movie
        {
            FilmAffinityId = movieId,
            Url = BuildFilmAffinityUrl(movieId),
            OriginalTitle = GetLabeledValue(document, "Original title") 
                           ?? GetLabeledValue(document, "Título original"),
            Year = ParseYear(GetLabeledValue(document, "Year") ?? GetLabeledValue(document, "Año")),
            RunningTime = runningTime,
            RunningTimeMinutes = ParseRunningTimeMinutes(runningTime),
            Country = GetLabeledValue(document, "Country") ?? GetLabeledValue(document, "País"),
            Director = GetLabeledValue(document, "Director") ?? GetLabeledValue(document, "Dirección"),
            Genre = genre,
            Genres = ParseGenres(genre),
            Synopsis = GetLabeledValue(document, "Synopsis") ?? GetLabeledValue(document, "Sinopsis"),
            RatingValue = ParseRating(document),
            Language = GetLabeledValue(document, "Language") ?? GetLabeledValue(document, "Idioma")
        };
    }

    public bool IsSearchResultsPage(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return false;

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        return doc.DocumentNode.SelectSingleNode("//div[contains(@class,'se-it')]") != null
               || doc.DocumentNode.SelectSingleNode("//div[@id='main-search-results']") != null
               || doc.DocumentNode.SelectSingleNode("//div[contains(@class,'adv-search-item')]") != null;
    }

    public string? ExtractTopResultUrl(string html, string baseUrl, int? targetYear = null)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var searchItems = doc.DocumentNode.SelectNodes("//div[contains(@class,'se-it')]")
                          ?? doc.DocumentNode.SelectNodes("//div[contains(@class,'movie-card')]");

        if (searchItems == null || searchItems.Count == 0)
        {
            // Fallback: search for any anchor link pointing to a film page
            var filmLink = doc.DocumentNode.SelectSingleNode("//a[contains(@href, '/film') and contains(@href, '.html')]");
            if (filmLink != null)
            {
                var href = filmLink.GetAttributeValue("href", string.Empty);
                return FormatUrl(href, baseUrl);
            }
            return null;
        }

        HtmlNode? selectedItem = null;

        if (targetYear.HasValue)
        {
            foreach (var item in searchItems)
            {
                var itemText = item.InnerText;
                var yearMatch = Regex.Match(itemText, @"\b(18|19|20)\d{2}\b");
                if (yearMatch.Success && int.TryParse(yearMatch.Value, out var itemYear))
                {
                    if (Math.Abs(itemYear - targetYear.Value) <= 1)
                    {
                        selectedItem = item;
                        break;
                    }
                }
            }
        }

        selectedItem ??= searchItems.FirstOrDefault();

        if (selectedItem != null)
        {
            var titleNode = selectedItem.SelectSingleNode(".//*[contains(@class,'mc-title')]//a")
                            ?? selectedItem.SelectSingleNode(".//a[contains(@href, '/film')]");

            if (titleNode != null)
            {
                var href = titleNode.GetAttributeValue("href", string.Empty);
                return FormatUrl(href, baseUrl);
            }
        }

        return null;
    }

    private static string? GetMovieId(HtmlDocument document)
    {
        var node = document.DocumentNode.SelectSingleNode("//*[@id='item2item' and @data-movie-id]")
                   ?? document.DocumentNode.SelectSingleNode("//*[@data-movie-id]");

        if (node != null)
        {
            var movieId = CleanText(node.GetAttributeValue("data-movie-id", string.Empty));
            if (!string.IsNullOrWhiteSpace(movieId) && Regex.IsMatch(movieId, "^[0-9]+$"))
                return movieId;
        }

        // Fallback: Extract from canonical link tag if present
        var canonicalNode = document.DocumentNode.SelectSingleNode("//link[@rel='canonical']");
        if (canonicalNode != null)
        {
            var href = canonicalNode.GetAttributeValue("href", string.Empty);
            if (!string.IsNullOrWhiteSpace(href))
            {
                var match = Regex.Match(href, @"film([0-9]+)\.html");
                if (match.Success)
                    return match.Groups[1].Value;
            }
        }

        return null;
    }

    private string? BuildFilmAffinityUrl(string? movieId)
    {
        if (string.IsNullOrWhiteSpace(movieId))
            return null;

        return $"{_options.BaseUrl.TrimEnd('/')}/{_options.Language}/film{movieId}.html";
    }

    private static string? GetLabeledValue(HtmlDocument document, string label)
    {
        var labelNode = document.DocumentNode.SelectSingleNode($"//*[normalize-space(text())='{label}']");

        if (labelNode == null)
        {
            // Case-insensitive fallback search
            labelNode = document.DocumentNode.SelectSingleNode($"//*[translate(normalize-space(text()), 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz')='{label.ToLowerInvariant()}']");
        }

        if (labelNode == null)
            return null;

        var candidates = new HtmlNode?[]
        {
            labelNode.SelectSingleNode("following-sibling::dd[1]"),
            labelNode.SelectSingleNode("following-sibling::*[1]"),
            labelNode.ParentNode?.SelectSingleNode(".//*[contains(@class,'value')][1]"),
            labelNode.ParentNode?.SelectSingleNode(".//*[contains(@class,'dato')][1]"),
            labelNode.ParentNode?.SelectSingleNode(".//*[contains(@class,'data')][1]")
        };

        foreach (var candidate in candidates)
        {
            var value = CleanText(candidate?.InnerText);

            if (!string.IsNullOrWhiteSpace(value) &&
                !value.Equals(label, StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        return null;
    }

    public static string? CleanText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        value = WebUtility.HtmlDecode(value);
        value = Regex.Replace(value, @"\s+", " ");

        return value.Trim();
    }

    private static int? ParseYear(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var match = Regex.Match(value, @"\b(18|19|20)\d{2}\b");

        return match.Success && int.TryParse(match.Value, out var year)
            ? year
            : null;
    }

    private static int? ParseRunningTimeMinutes(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var match = Regex.Match(
            value,
            @"(?<minutes>\d+)\s*(?:min\.?|minutes?|m\b)",
            RegexOptions.IgnoreCase);

        return match.Success && int.TryParse(match.Groups["minutes"].Value, out var minutes)
            ? minutes
            : null;
    }

    private static List<string> ParseGenres(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return [];

        return Regex.Split(value, @"\s*\|\s*|\.\s+")
            .Select(CleanText)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static decimal? ParseRating(HtmlDocument document)
    {
        var xpaths = new[]
        {
            "//*[@itemprop='ratingValue'][1]",
            "//*[contains(@class,'rating')][1]",
            "//*[contains(@class,'score')][1]",
            "//*[contains(@class,'avgrat')][1]",
            "//*[@id='movie-rat-avg']"
        };

        foreach (var xpath in xpaths)
        {
            var node = document.DocumentNode.SelectSingleNode(xpath);

            if (node == null)
                continue;

            var value = CleanText(node.GetAttributeValue("content", string.Empty))
                        ?? CleanText(node.InnerText);

            if (TryParseDecimal(value, out var rating))
                return rating;
        }

        return null;
    }

    private static bool TryParseDecimal(string? value, out decimal result)
    {
        result = 0;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        value = value.Replace(',', '.');

        return decimal.TryParse(
            value,
            NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out result);
    }

    private static string? FormatUrl(string? href, string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(href))
            return null;

        if (Uri.TryCreate(href, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return uri.ToString();
        }

        return new Uri(new Uri(baseUrl), href).ToString();
    }
}
