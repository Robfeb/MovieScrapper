# FilmAffinity Movie Scraper & Excel Processor (.NET 8)

A robust .NET 8 console solution designed to extract movie metadata (Director, Genre, Country, Original Title, Language, Synopsis, Rating) from [FilmAffinity](https://www.filmaffinity.com/) and automatically populate Excel spreadsheets (`data/movies.xlsx`).

---

## 🚀 Features

- **Throttling & Sequential Processing**: Processes movies 1-by-1 with a **3-second delay** (`3000ms`) between HTTP requests to prevent rate limiting or throttling from FilmAffinity.
- **Idempotency**: Reads the dataset and skips any row that already has populated metadata or status `Found`. HTTP requests are only made when data is missing.
- **Immediate Status & Error Tracking**: Updates `data/movies.xlsx` row by row. If expected data is retrieved, populates fields and sets `Status` to `Found`. If no values are retrieved, sets `Status` to `Error: Not Found` (or error details).
- **Testing Mode**: Supports limiting execution to a specific number of rows (e.g. `--limit 1` or `--single`) for quick testing.
- **Excel Dataset Integration**: Loads movie titles and production years from `data/movies.xlsx` using **ClosedXML**, queries FilmAffinity, and populates missing fields.
- **Resilient Search & Direct Redirect Handling**: Handles both FilmAffinity direct redirects to movie detail pages and search list results pages, matching candidate movies by title and year.
- **Robust HTML Parsing**: Uses **HtmlAgilityPack** and flexible XPath queries with text cleaning, HTML entity decoding, year normalization, running time conversion, and genre splitting.
- **Comprehensive Test Suite**: Automated unit tests using **xUnit** covering normal HTML fixtures, missing attributes, bad formatting, running time conversions, rating values, idempotency, and Excel I/O.

---

## 📋 Requirements

- **.NET 8 SDK** (or later)

---

## 🛠️ Build & Installation

Restore dependencies and build the solution:

```bash
dotnet restore
dotnet build
```

---

## 🧪 Running Unit Tests

Execute the unit test suite:

```bash
dotnet test
```

---

## 💻 Usage

### 1. Test Run with 1 Movie Row (`--limit 1` / `--single`)

To test the application on just 1 row with a 3-second delay:

```bash
dotnet run --project src/FilmAffinityScraper/FilmAffinityScraper.csproj -- --limit 1
```

### 2. Full Idempotent Batch Processing

To run sequentially on all rows in `data/movies.xlsx` (skipping already processed rows):

```bash
dotnet run --project src/FilmAffinityScraper/FilmAffinityScraper.csproj
```

### 3. Custom Delay & Limit

To customize delay (e.g. 3000 ms) and limit to N rows:

```bash
dotnet run --project src/FilmAffinityScraper/FilmAffinityScraper.csproj -- --limit 5 --delay 3000
```

Or pass custom input and output Excel file paths:

```bash
dotnet run --project src/FilmAffinityScraper/FilmAffinityScraper.csproj -- "data/movies.xlsx" "data/movies_updated.xlsx"
```

### 2. Single Movie URL Scraping Mode

To scrape a single FilmAffinity movie URL and output JSON to `stdout`:

```bash
dotnet run --project src/FilmAffinityScraper/FilmAffinityScraper.csproj -- "https://www.filmaffinity.com/en/film345698.html"
```

#### Sample JSON Output

```json
{
  "filmAffinityId": "345698",
  "url": "https://www.filmaffinity.com/en/film345698.html",
  "originalTitle": "13 dies d'octubre",
  "year": 2015,
  "runningTime": "96 min.",
  "runningTimeMinutes": 96,
  "country": "Spain",
  "director": "Carlos Marques-Marcet",
  "genre": "Drama | Historical. Franquismo. Biography. TV Movie",
  "genres": [
    "Drama",
    "Historical",
    "Franquismo",
    "Biography",
    "TV Movie"
  ],
  "synopsis": "During 13 days of October 1940, Ramona Companys, Lluís Companys sister; Ramón de Colubi, his defense attorney; and Luis Orgaz, Captain General of Catalunya, try to avoid, in different ways and for different reasons, that the President of the Generalitat is executed and thus becomes a martyr.",
  "ratingValue": 6.1
}
```

---

## 🛡️ Responsible Scraping & Politeness

- **Rate Limits**: The application introduces a default delay of 1,500 ms between HTTP requests to avoid overloading FilmAffinity servers or triggering HTTP 429 / 403 blocks.
- **User Agent**: Uses a browser `User-Agent` header.
- **Robots & Restrictions**: Respect site terms of service and avoid aggressive parallel scraping.

---

## ⚠️ Known Limitations

- Content dynamically generated exclusively via client-side JavaScript is not executed.
- FilmAffinity search results rely on structural HTML elements; major changes to FilmAffinity layout may require updated XPath strategies in `MovieHtmlParser.cs`.
