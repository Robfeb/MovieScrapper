using FilmAffinityScraper.Options;
using FilmAffinityScraper.Parsing;
using Xunit;

namespace FilmAffinityScraper.Tests;

public class MovieHtmlParserTests
{
    private readonly MovieHtmlParser _parser;

    public MovieHtmlParserTests()
    {
        _parser = new MovieHtmlParser(new FilmAffinityOptions
        {
            BaseUrl = "https://www.filmaffinity.com",
            Language = "en"
        });
    }

    [Fact]
    public void Parse_NormalFixture_ExtractsAllMovieFieldsCorrectly()
    {
        var fixturePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures", "example.html");
        var html = File.ReadAllText(fixturePath);

        var movie = _parser.Parse(html);

        Assert.NotNull(movie);
        Assert.Equal("345698", movie.FilmAffinityId);
        Assert.Equal("https://www.filmaffinity.com/en/film345698.html", movie.Url);
        Assert.Equal("13 dies d'octubre", movie.OriginalTitle);
        Assert.Equal(2015, movie.Year);
        Assert.Equal("96 min.", movie.RunningTime);
        Assert.Equal(96, movie.RunningTimeMinutes);
        Assert.Equal("Spain", movie.Country);
        Assert.Equal("Carlos Marques-Marcet", movie.Director);
        Assert.Equal("Drama | Historical. Franquismo. Biography. TV Movie", movie.Genre);
        Assert.Equal(6.1m, movie.RatingValue);
        Assert.Contains("Drama", movie.Genres);
        Assert.Contains("Biography", movie.Genres);
        Assert.False(string.IsNullOrWhiteSpace(movie.Synopsis));
    }

    [Fact]
    public void Parse_MissingMovieId_ReturnsNullIdAndUrl()
    {
        var html = @"<html><body>
            <dl class='movie-info'>
                <dt>Original title</dt><dd>Test Movie</dd>
            </dl>
        </body></html>";

        var movie = _parser.Parse(html);

        Assert.Null(movie.FilmAffinityId);
        Assert.Null(movie.Url);
        Assert.Equal("Test Movie", movie.OriginalTitle);
    }

    [Fact]
    public void Parse_MissingFields_ReturnsNullPropertiesAndEmptyGenres()
    {
        var html = @"<html><body>
            <div class='item2item' id='item2item' data-movie-id='12345'></div>
        </body></html>";

        var movie = _parser.Parse(html);

        Assert.Equal("12345", movie.FilmAffinityId);
        Assert.Null(movie.Director);
        Assert.Null(movie.Genre);
        Assert.Null(movie.Synopsis);
        Assert.Empty(movie.Genres);
    }

    [Theory]
    [InlineData("96 min.", 96)]
    [InlineData("96 min", 96)]
    [InlineData("96 minutes", 96)]
    public void Parse_RunningTimeFormats_ParsesMinutesCorrectly(string input, int expectedMinutes)
    {
        var html = $@"<html><body>
            <dl class='movie-info'>
                <dt>Running time</dt><dd>{input}</dd>
            </dl>
        </body></html>";

        var movie = _parser.Parse(html);

        Assert.Equal(input, movie.RunningTime);
        Assert.Equal(expectedMinutes, movie.RunningTimeMinutes);
    }

    [Theory]
    [InlineData("6.1", 6.1)]
    [InlineData("6,1", 6.1)]
    public void Parse_RatingFormats_ParsesDecimalCorrectly(string ratingStr, decimal expectedRating)
    {
        var html = $@"<html><body>
            <div id='movie-rat-avg' itemprop='ratingValue' content='{ratingStr}'>{ratingStr}</div>
        </body></html>";

        var movie = _parser.Parse(html);

        Assert.Equal(expectedRating, movie.RatingValue);
    }

    [Fact]
    public void Parse_HtmlEntitiesAndWhitespace_CleansAndNormalizesText()
    {
        var html = @"<html><body>
            <dl class='movie-info'>
                <dt>Synopsis</dt>
                <dd>  During  &amp;   after &quot;13 days&quot;   </dd>
            </dl>
        </body></html>";

        var movie = _parser.Parse(html);

        Assert.Equal("During & after \"13 days\"", movie.Synopsis);
    }

    [Fact]
    public void Parse_CustomLanguage_BuildsUrlWithLanguage()
    {
        var esParser = new MovieHtmlParser(new FilmAffinityOptions
        {
            BaseUrl = "https://www.filmaffinity.com",
            Language = "es"
        });

        var html = @"<html><body>
            <div class='item2item' id='item2item' data-movie-id='345698'></div>
        </body></html>";

        var movie = esParser.Parse(html);

        Assert.Equal("https://www.filmaffinity.com/es/film345698.html", movie.Url);
    }

    [Fact]
    public void IsSearchResultsPage_WithSearchFixture_ReturnsTrue()
    {
        var fixturePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures", "search_results.html");
        var html = File.ReadAllText(fixturePath);

        Assert.True(_parser.IsSearchResultsPage(html));
    }

    [Fact]
    public void ExtractTopResultUrl_WithTargetYear_ReturnsMatchingUrl()
    {
        var fixturePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures", "search_results.html");
        var html = File.ReadAllText(fixturePath);

        var url = _parser.ExtractTopResultUrl(html, "https://www.filmaffinity.com", 2015);

        Assert.Equal("https://www.filmaffinity.com/en/film345698.html", url);
    }
}
