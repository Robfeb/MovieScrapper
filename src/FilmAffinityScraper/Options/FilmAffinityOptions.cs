namespace FilmAffinityScraper.Options;

public sealed class FilmAffinityOptions
{
    public string BaseUrl { get; set; } = "https://www.filmaffinity.com";
    public string Language { get; set; } = "es";
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
    public string UserAgent { get; set; } =
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) " +
        "AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36";
    public int DelayMsBetweenRequests { get; set; } = 3000;
}
