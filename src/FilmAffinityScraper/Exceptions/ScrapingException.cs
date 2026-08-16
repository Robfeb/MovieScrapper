namespace FilmAffinityScraper.Exceptions;

public sealed class ScrapingException : Exception
{
    public ScrapingException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }
}
