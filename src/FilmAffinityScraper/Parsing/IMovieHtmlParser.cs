using FilmAffinityScraper.Models;

namespace FilmAffinityScraper.Parsing;

public interface IMovieHtmlParser
{
    Movie Parse(string html);
    bool IsSearchResultsPage(string html);
    string? ExtractTopResultUrl(string html, string baseUrl, int? targetYear = null);
}
