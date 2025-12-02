using System.Globalization;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using Loto.Domain;

namespace Loto.Ingestion.Worker;

public class NewLotoZipCsvParser : IFdjParser
{
    private readonly ILogger<NewLotoZipCsvParser> _logger;

    public NewLotoZipCsvParser(ILogger<NewLotoZipCsvParser> logger)
    {
        _logger = logger;
    }

    public async IAsyncEnumerable<Draw> ParseNewLotoArchiveAsync(Stream zipStream, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // TODO: Adapter au format réel du fichier FDJ (colonnes, encodage, timezone). Actuellement basé sur:
        // annee_numero_de_tirage;jour_de_tirage;date_de_tirage;date_de_forclusion;boule_1;boule_2;boule_3;boule_4;boule_5;numero_chance;...

        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);
        var entry = archive.Entries.FirstOrDefault(e =>
            e.Name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) ||
            e.Name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase));

        if (entry is null)
        {
            _logger.LogWarning("No CSV/TXT entry found in FDJ archive.");
            yield break;
        }

        await using var entryStream = entry.Open();
        using var reader = new StreamReader(entryStream);

        var headerLine = await reader.ReadLineAsync(cancellationToken);
        if (headerLine is null)
        {
            _logger.LogWarning("FDJ archive: header line is null");
            yield break;
        }

        var headers = headerLine.Split(';');
        var columnIndexByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headers.Length; i++)
        {
            var name = headers[i].Trim();
            if (!string.IsNullOrEmpty(name))
            {
                columnIndexByName[name] = i;
            }
        }

        _logger.LogInformation("FDJ header columns: {Header}", string.Join(" | ", headers));

        var officialDrawIdIndex = RequireColumn(columnIndexByName, "annee_numero_de_tirage");
        var dayNameIndex = columnIndexByName.TryGetValue("jour_de_tirage", out var tmpDay) ? tmpDay : -1;
        var dateIndex = RequireColumn(columnIndexByName, "date_de_tirage");
        var b1Index = RequireColumn(columnIndexByName, "boule_1");
        var b2Index = RequireColumn(columnIndexByName, "boule_2");
        var b3Index = RequireColumn(columnIndexByName, "boule_3");
        var b4Index = RequireColumn(columnIndexByName, "boule_4");
        var b5Index = RequireColumn(columnIndexByName, "boule_5");
        var chanceIndex = RequireColumn(columnIndexByName, "numero_chance");

        var parsedCount = 0;
        var skippedCount = 0;
        var cultureFr = CultureInfo.GetCultureInfo("fr-FR");

        string? line;
        var lineNumber = 1; // already consumed header

        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var values = line.Split(';');
            if (values.Length <= chanceIndex)
            {
                skippedCount++;
                _logger.LogWarning("Skipping line {LineNumber}: not enough columns ({Count})", lineNumber, values.Length);
                continue;
            }

            var officialId = values[officialDrawIdIndex].Trim();
            var dayName = dayNameIndex >= 0 && dayNameIndex < values.Length ? values[dayNameIndex].Trim() : null;
            var dateString = values[dateIndex].Trim();

            var b1Str = values[b1Index].Trim();
            var b2Str = values[b2Index].Trim();
            var b3Str = values[b3Index].Trim();
            var b4Str = values[b4Index].Trim();
            var b5Str = values[b5Index].Trim();
            var chanceStr = values[chanceIndex].Trim();

            if (!DateTime.TryParseExact(dateString, "dd/MM/yyyy", cultureFr, DateTimeStyles.None, out var parsedDate))
            {
                skippedCount++;
                _logger.LogWarning("Skipping line {LineNumber}: invalid date '{DateValue}'", lineNumber, dateString);
                continue;
            }

            if (!int.TryParse(b1Str, out var n1) ||
                !int.TryParse(b2Str, out var n2) ||
                !int.TryParse(b3Str, out var n3) ||
                !int.TryParse(b4Str, out var n4) ||
                !int.TryParse(b5Str, out var n5) ||
                !int.TryParse(chanceStr, out var lucky))
            {
                skippedCount++;
                _logger.LogWarning("Skipping line {LineNumber}: invalid numbers (b1={B1}, b2={B2}, b3={B3}, b4={B4}, b5={B5}, chance={Chance})",
                    lineNumber, b1Str, b2Str, b3Str, b4Str, b5Str, chanceStr);
                continue;
            }

            parsedCount++;
            yield return new Draw
            {
                OfficialDrawId = string.IsNullOrWhiteSpace(officialId) ? null : officialId,
                DrawDayName = string.IsNullOrWhiteSpace(dayName) ? null : dayName,
                DrawDate = DateTime.SpecifyKind(parsedDate, DateTimeKind.Utc),
                Number1 = n1,
                Number2 = n2,
                Number3 = n3,
                Number4 = n4,
                Number5 = n5,
                LuckyNumber = lucky,
                Source = "FDJ-official",
                CreatedAt = DateTime.UtcNow
            };
        }

        _logger.LogInformation("FDJ new-loto parsing finished. Parsed {Parsed} draws, skipped {Skipped} lines.", parsedCount, skippedCount);
    }

    private int RequireColumn(Dictionary<string, int> columns, string name)
    {
        if (!columns.TryGetValue(name, out var index))
        {
            throw new InvalidOperationException($"FDJ CSV: required column '{name}' not found. Available: {string.Join(", ", columns.Keys)}");
        }
        return index;
    }
}
