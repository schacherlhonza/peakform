using System.Globalization;
using System.Text;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Integrations;

/// <summary>
/// Header-based CSV parser used for both the standardized import template AND a CSV export of
/// the coach's Google Sheets spreadsheet. Deliberately NOT coordinate-based (no "column C is
/// distance") — it matches header text against a small alias list per field (Czech and English,
/// with diacritics/spacing normalized), so a reasonably-named spreadsheet export works without
/// per-file configuration. Ambiguous or unparseable values are flagged, never silently guessed
/// into something a coach would need to notice was wrong.
/// </summary>
public static class CsvImportParser
{
    private static readonly Dictionary<string, string[]> FieldAliases = new()
    {
        ["date"] = ["datum", "date", "den"],
        ["sport"] = ["sport", "typ", "typaktivity", "aktivita", "activity", "disciplina"],
        ["distancekm"] = ["vzdalenostkm", "vzdalenost", "distancekm", "distance", "km"],
        ["durationminutes"] = ["dobaminuty", "doba", "cas", "durationminutes", "duration", "minuty", "min"],
        ["elevationm"] = ["prevysenim", "prevyseni", "elevationm", "elevation", "vyskovemetry"],
        ["avghr"] = ["prumernytep", "tep", "avghr", "heartrate", "hr", "tf"],
        ["notes"] = ["poznamka", "poznamky", "notes", "note", "napln", "popis", "komentar"],
    };

    private static readonly Dictionary<string, SportType> SportAliases = new()
    {
        ["beh"] = SportType.Running,
        ["behani"] = SportType.Running,
        ["running"] = SportType.Running,
        ["run"] = SportType.Running,
        ["kolo"] = SportType.Cycling,
        ["cyklistika"] = SportType.Cycling,
        ["cycling"] = SportType.Cycling,
        ["bike"] = SportType.Cycling,
        ["plavani"] = SportType.Swimming,
        ["swimming"] = SportType.Swimming,
        ["posilovna"] = SportType.Strength,
        ["sila"] = SportType.Strength,
        ["strength"] = SportType.Strength,
        ["kruhovytrenink"] = SportType.CrossTraining,
        ["crosstraining"] = SportType.CrossTraining,
        ["odpocinek"] = SportType.Rest,
        ["rest"] = SportType.Rest,
        ["volno"] = SportType.Rest,
    };

    public static IReadOnlyList<ImportPreviewRowDto> Parse(string content)
    {
        var lines = content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n')
            .Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

        if (lines.Count == 0)
        {
            return [];
        }

        // Detect the delimiter once from the header row and reuse it for every data row — a
        // per-row heuristic would misfire on a semicolon-separated file whose data contains a
        // comma decimal separator (e.g. "7,5"), splitting a single value into two columns.
        var separator = lines[0].Contains(';') ? ';' : ',';

        var headers = SplitCsvLine(lines[0], separator).Select(Normalize).ToList();
        var fieldIndex = new Dictionary<string, int>();
        foreach (var (field, aliases) in FieldAliases)
        {
            var index = headers.FindIndex(h => aliases.Contains(h));
            if (index >= 0)
            {
                fieldIndex[field] = index;
            }
        }

        var rows = new List<ImportPreviewRowDto>();
        for (var i = 1; i < lines.Count; i++)
        {
            var cells = SplitCsvLine(lines[i], separator);
            rows.Add(ParseRow(i + 1, cells, fieldIndex));
        }

        return rows;
    }

    private static ImportPreviewRowDto ParseRow(int rowNumber, IReadOnlyList<string> cells, Dictionary<string, int> fieldIndex)
    {
        var messages = new List<string>();
        var status = ImportRowStatus.Valid;

        string? Get(string field) => fieldIndex.TryGetValue(field, out var idx) && idx < cells.Count ? cells[idx].Trim() : null;

        DateOnly? date = null;
        var rawDate = Get("date");
        if (string.IsNullOrWhiteSpace(rawDate))
        {
            messages.Add("Chybí datum.");
            status = ImportRowStatus.Error;
        }
        else if (DateOnly.TryParse(rawDate, CultureInfo.GetCultureInfo("cs-CZ"), DateTimeStyles.None, out var parsedDate)
                 || DateOnly.TryParse(rawDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedDate))
        {
            date = parsedDate;
        }
        else
        {
            messages.Add($"Datum \"{rawDate}\" se nepodařilo rozpoznat.");
            status = ImportRowStatus.Error;
        }

        SportType? sport = null;
        var rawSport = Get("sport");
        if (!string.IsNullOrWhiteSpace(rawSport))
        {
            var normalized = Normalize(rawSport);
            if (SportAliases.TryGetValue(normalized, out var matched))
            {
                sport = matched;
            }
            else
            {
                sport = SportType.Running;
                messages.Add($"Sport \"{rawSport}\" nebyl rozpoznán, nastaveno na Běh — zkontrolujte.");
                status = Max(status, ImportRowStatus.Warning);
            }
        }
        else
        {
            sport = SportType.Running;
        }

        var distanceKm = ParseDecimal(Get("distancekm"));
        var durationMinutes = ParseInt(Get("durationminutes"));
        var elevationM = ParseDecimal(Get("elevationm"));
        var avgHr = ParseInt(Get("avghr"));
        var notes = Get("notes");

        if (sport != SportType.Rest && distanceKm is null && durationMinutes is null)
        {
            messages.Add("Chybí vzdálenost i doba trvání — řádek nelze bez alespoň jedné z hodnot naimportovat.");
            status = ImportRowStatus.Error;
        }

        if (durationMinutes is > 600)
        {
            messages.Add("Doba trvání je nezvykle vysoká (> 10 hodin), zkontrolujte prosím.");
            status = Max(status, ImportRowStatus.Warning);
        }

        return new ImportPreviewRowDto(rowNumber, date, sport, distanceKm, durationMinutes, elevationM, avgHr, notes, status, messages);
    }

    private static ImportRowStatus Max(ImportRowStatus a, ImportRowStatus b) => (ImportRowStatus)Math.Max((int)a, (int)b);

    private static decimal? ParseDecimal(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var normalized = raw.Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    private static int? ParseInt(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return int.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    private static string Normalize(string value)
    {
        var formD = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in formD)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(c))
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    private static List<string> SplitCsvLine(string line, char separator)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == separator && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        result.Add(current.ToString());
        return result;
    }
}
