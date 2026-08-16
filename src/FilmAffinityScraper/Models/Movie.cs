namespace FilmAffinityScraper.Models;

public sealed class Movie
{
    public string? FilmAffinityId { get; init; }
    public string? Url { get; init; }
    public string? OriginalTitle { get; init; }
    public int? Year { get; init; }
    public string? RunningTime { get; init; }
    public int? RunningTimeMinutes { get; init; }
    public string? Country { get; init; }
    public string? Director { get; init; }
    public string? Genre { get; init; }
    public List<string> Genres { get; init; } = [];
    public string? Synopsis { get; init; }
    public decimal? RatingValue { get; init; }
    public string? Language { get; init; }
}
