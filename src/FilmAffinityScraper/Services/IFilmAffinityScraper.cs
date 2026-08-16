using FilmAffinityScraper.Models;

namespace FilmAffinityScraper.Services;

public interface IFilmAffinityScraper
{
    Task<Movie> ScrapeUrlAsync(string url, CancellationToken cancellationToken = default);
    Task<Movie?> SearchAndScrapeAsync(string title, int? year = null, CancellationToken cancellationToken = default);
}
