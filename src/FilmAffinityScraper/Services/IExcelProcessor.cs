using FilmAffinityScraper.Models;

namespace FilmAffinityScraper.Services;

public interface IExcelProcessor
{
    List<ExcelMovieRow> ReadRows(string filePath);
    void UpdateRows(string inputPath, IEnumerable<ExcelMovieRow> rows, string outputPath);
    void UpdateSingleRow(string excelPath, ExcelMovieRow row);
}
