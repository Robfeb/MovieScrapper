using ClosedXML.Excel;
using FilmAffinityScraper.Models;

namespace FilmAffinityScraper.Services;

public sealed class ExcelProcessor : IExcelProcessor
{
    public List<ExcelMovieRow> ReadRows(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Excel file not found at path: {filePath}");

        using var workbook = new XLWorkbook(filePath);
        var worksheet = workbook.Worksheets.FirstOrDefault()
                        ?? throw new InvalidOperationException("Workbook contains no worksheets.");

        var rows = new List<ExcelMovieRow>();
        var headerRow = worksheet.Row(1);

        int colFullName = GetColumnIndex(headerRow, "full name");
        int colTitle = GetColumnIndex(headerRow, "title");
        int colYear = GetColumnIndex(headerRow, "year");
        int colDirector = GetColumnIndex(headerRow, "director");
        int colGenre = GetColumnIndex(headerRow, "genre");
        int colPais = GetColumnIndex(headerRow, "pais", "country");
        int colTitolOriginal = GetColumnIndex(headerRow, "titol original", "original title");
        int colIdioma = GetColumnIndex(headerRow, "idioma", "language");
        int colSinopsis = GetColumnIndex(headerRow, "sinopsis", "synopsis");
        int colStatus = GetColumnIndex(headerRow, "status", "estado");

        int lastRowIndex = worksheet.LastRowUsed()?.RowNumber() ?? 1;

        for (int r = 2; r <= lastRowIndex; r++)
        {
            var row = worksheet.Row(r);
            var titleCell = GetCellString(row, colTitle);
            if (string.IsNullOrWhiteSpace(titleCell))
                continue;

            var excelRow = new ExcelMovieRow
            {
                RowIndex = r,
                FullName = GetCellString(row, colFullName),
                Title = titleCell,
                Year = GetCellInt(row, colYear),
                Director = GetCellString(row, colDirector),
                Genre = GetCellString(row, colGenre),
                Country = GetCellString(row, colPais),
                OriginalTitle = GetCellString(row, colTitolOriginal),
                Language = GetCellString(row, colIdioma),
                Synopsis = GetCellString(row, colSinopsis),
                Status = GetCellString(row, colStatus)
            };

            rows.Add(excelRow);
        }

        return rows;
    }

    public void UpdateRows(string inputPath, IEnumerable<ExcelMovieRow> rows, string outputPath)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException($"Input Excel file not found: {inputPath}");

        using var workbook = new XLWorkbook(inputPath);
        var worksheet = workbook.Worksheets.FirstOrDefault()
                        ?? throw new InvalidOperationException("Workbook contains no worksheets.");

        var headerRow = worksheet.Row(1);
        EnsureHeaderColumns(headerRow, out var cols);

        foreach (var rowData in rows)
        {
            ApplyRowData(worksheet.Row(rowData.RowIndex), rowData, cols);
        }

        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        workbook.SaveAs(outputPath);
    }

    public void UpdateSingleRow(string excelPath, ExcelMovieRow rowData)
    {
        if (!File.Exists(excelPath))
            throw new FileNotFoundException($"Excel file not found: {excelPath}");

        using var workbook = new XLWorkbook(excelPath);
        var worksheet = workbook.Worksheets.FirstOrDefault()
                        ?? throw new InvalidOperationException("Workbook contains no worksheets.");

        var headerRow = worksheet.Row(1);
        EnsureHeaderColumns(headerRow, out var cols);

        ApplyRowData(worksheet.Row(rowData.RowIndex), rowData, cols);

        workbook.Save();
    }

    private struct ColumnMapping
    {
        public int Director;
        public int Genre;
        public int Pais;
        public int TitolOriginal;
        public int Idioma;
        public int Sinopsis;
        public int Status;
    }

    private static void EnsureHeaderColumns(IXLRow headerRow, out ColumnMapping cols)
    {
        cols.Director = GetColumnIndex(headerRow, "director");
        cols.Genre = GetColumnIndex(headerRow, "genre");
        cols.Pais = GetColumnIndex(headerRow, "pais", "country");
        cols.TitolOriginal = GetColumnIndex(headerRow, "titol original", "original title");
        cols.Idioma = GetColumnIndex(headerRow, "idioma", "language");
        cols.Sinopsis = GetColumnIndex(headerRow, "sinopsis", "synopsis");
        cols.Status = GetColumnIndex(headerRow, "status", "estado");

        // If Status column doesn't exist, create it in header
        if (cols.Status <= 0)
        {
            int nextCol = headerRow.LastCellUsed()?.Address.ColumnNumber + 1 ?? 10;
            headerRow.Cell(nextCol).Value = "Status";
            cols.Status = nextCol;
        }
    }

    private static void ApplyRowData(IXLRow row, ExcelMovieRow rowData, ColumnMapping cols)
    {
        if (cols.Director > 0 && rowData.Director != null)
            row.Cell(cols.Director).Value = rowData.Director;

        if (cols.Genre > 0 && rowData.Genre != null)
            row.Cell(cols.Genre).Value = rowData.Genre;

        if (cols.Pais > 0 && rowData.Country != null)
            row.Cell(cols.Pais).Value = rowData.Country;

        if (cols.TitolOriginal > 0 && rowData.OriginalTitle != null)
            row.Cell(cols.TitolOriginal).Value = rowData.OriginalTitle;

        if (cols.Idioma > 0 && rowData.Language != null)
            row.Cell(cols.Idioma).Value = rowData.Language;

        if (cols.Sinopsis > 0 && rowData.Synopsis != null)
            row.Cell(cols.Sinopsis).Value = rowData.Synopsis;

        if (cols.Status > 0 && rowData.Status != null)
            row.Cell(cols.Status).Value = rowData.Status;
    }

    private static int GetColumnIndex(IXLRow headerRow, params string[] headerNames)
    {
        foreach (var cell in headerRow.CellsUsed())
        {
            var cellValue = cell.GetString().Trim().ToLowerInvariant();
            foreach (var name in headerNames)
            {
                if (cellValue.Equals(name.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase))
                    return cell.Address.ColumnNumber;
            }
        }
        return -1;
    }

    private static string? GetCellString(IXLRow row, int columnIndex)
    {
        if (columnIndex <= 0) return null;
        var val = row.Cell(columnIndex).GetString();
        return string.IsNullOrWhiteSpace(val) ? null : val.Trim();
    }

    private static int? GetCellInt(IXLRow row, int columnIndex)
    {
        if (columnIndex <= 0) return null;
        var cell = row.Cell(columnIndex);
        if (cell.DataType == XLDataType.Number)
        {
            return Convert.ToInt32(cell.GetDouble());
        }

        var text = cell.GetString().Trim();
        return int.TryParse(text, out var result) ? result : null;
    }
}
