namespace FilmAffinityScraper.Models;

public sealed class ExcelMovieRow
{
    public int RowIndex { get; set; }
    public string? FullName { get; set; }
    public string? Title { get; set; }
    public int? Year { get; set; }
    public string? Director { get; set; }
    public string? Genre { get; set; }
    public string? Country { get; set; } // Pais
    public string? OriginalTitle { get; set; } // titol original
    public string? Language { get; set; } // Idioma
    public string? Synopsis { get; set; } // sinopsis
    public string? Status { get; set; } // Status / Estado
}
