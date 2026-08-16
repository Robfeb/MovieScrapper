using ClosedXML.Excel;
using FilmAffinityScraper.Models;
using FilmAffinityScraper.Services;
using Xunit;

namespace FilmAffinityScraper.Tests;

public class ExcelProcessorTests
{
    private readonly ExcelProcessor _excelProcessor = new();

    [Fact]
    public void ReadRows_And_UpdateRows_ProcessesExcelFileSuccessfully()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var inputPath = Path.Combine(tempDirectory, "test_input.xlsx");
            var outputPath = Path.Combine(tempDirectory, "test_output.xlsx");

            // Create a test Excel workbook
            using (var wb = new XLWorkbook())
            {
                var ws = wb.Worksheets.Add("Movies");
                ws.Cell(1, 1).Value = "Full name";
                ws.Cell(1, 2).Value = "Title";
                ws.Cell(1, 3).Value = "year";
                ws.Cell(1, 4).Value = "Director";
                ws.Cell(1, 5).Value = "Genre";
                ws.Cell(1, 6).Value = "Pais";
                ws.Cell(1, 7).Value = "titol original";
                ws.Cell(1, 8).Value = "Idioma";
                ws.Cell(1, 9).Value = "sinopsis";

                ws.Cell(2, 1).Value = "13 dies d'octubre (2015)";
                ws.Cell(2, 2).Value = "13 dies d'octubre";
                ws.Cell(2, 3).Value = 2015;

                wb.SaveAs(inputPath);
            }

            // Test ReadRows
            var rows = _excelProcessor.ReadRows(inputPath);
            Assert.Single(rows);
            Assert.Equal("13 dies d'octubre", rows[0].Title);
            Assert.Equal(2015, rows[0].Year);

            // Mutate data
            rows[0].Director = "Carlos Marques-Marcet";
            rows[0].Country = "Spain";
            rows[0].Genre = "Drama";
            rows[0].Status = "Found";

            // Test UpdateRows
            _excelProcessor.UpdateRows(inputPath, rows, outputPath);
            Assert.True(File.Exists(outputPath));

            // Re-read updated output file to verify persistence of data and Status column
            var updatedRows = _excelProcessor.ReadRows(outputPath);
            Assert.Single(updatedRows);
            Assert.Equal("Carlos Marques-Marcet", updatedRows[0].Director);
            Assert.Equal("Spain", updatedRows[0].Country);
            Assert.Equal("Drama", updatedRows[0].Genre);
            Assert.Equal("Found", updatedRows[0].Status);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, true);
            }
        }
    }

    [Fact]
    public void UpdateSingleRow_UpdatesSpecificRowInPlace()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var excelPath = Path.Combine(tempDirectory, "test_single.xlsx");

            using (var wb = new XLWorkbook())
            {
                var ws = wb.Worksheets.Add("Movies");
                ws.Cell(1, 1).Value = "Title";
                ws.Cell(1, 2).Value = "year";
                ws.Cell(1, 3).Value = "Director";

                ws.Cell(2, 1).Value = "Movie 1";
                ws.Cell(2, 2).Value = 2020;

                ws.Cell(3, 1).Value = "Movie 2";
                ws.Cell(3, 2).Value = 2021;

                wb.SaveAs(excelPath);
            }

            var rowToUpdate = new ExcelMovieRow
            {
                RowIndex = 3,
                Title = "Movie 2",
                Year = 2021,
                Director = "Test Director",
                Status = "Found"
            };

            _excelProcessor.UpdateSingleRow(excelPath, rowToUpdate);

            var rows = _excelProcessor.ReadRows(excelPath);
            Assert.Equal(2, rows.Count);
            Assert.Null(rows[0].Director);
            Assert.Equal("Test Director", rows[1].Director);
            Assert.Equal("Found", rows[1].Status);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, true);
            }
        }
    }
}
