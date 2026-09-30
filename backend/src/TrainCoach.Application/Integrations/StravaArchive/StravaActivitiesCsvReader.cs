using System.Globalization;
using System.Text;

namespace TrainCoach.Application.Integrations.StravaArchive;

/// <summary>One row of the archive's <c>activities.csv</c>, already converted to invariant units
/// (seconds, meters, m/s). <see cref="Error"/> is set when the row can't be used at all.</summary>
public record StravaCsvActivity(
    int RowNumber,
    string ActivityId,
    DateTime? StartedAtUtc,
    string? Name,
    string? TypeLabel,
    string? FileName,
    decimal? ElapsedSeconds,
    decimal? MovingSeconds,
    decimal? DistanceMeters,
    decimal? AverageSpeedMetersPerSecond,
    decimal? ElevationGainMeters,
    decimal? MaxHeartRate,
    decimal? AverageHeartRate,
    decimal? AverageWatts,
    decimal? WeightedAverageWatts,
    decimal? Calories,
    IReadOnlyDictionary<string, string> RawValues,
    string? Error);

/// <summary>
/// Reads the Strava export's <c>activities.csv</c>. Two quirks drive the design (both verified on
/// a real Czech-locale export, see docs/integrations/strava-archive-import.md):
/// <list type="bullet">
/// <item>Header names — and the date and activity-type values — are localized to the account's
/// language, so columns are resolved by a cs/en alias table and, failing that, by their fixed
/// position in the export layout.</item>
/// <item>Several headers appear twice (e.g. Elapsed Time, Distance, Max Heart Rate): the first
/// block (columns 0-14) holds display values in the account's units/locale ("33,22" km), the
/// second the machine values ("33227.2" m). The <b>last</b> occurrence is always the one used.</item>
/// </list>
/// </summary>
/// <summary>The archive (or its activities.csv) isn't recognizably a Strava export. The message is
/// user-facing.</summary>
public class StravaArchiveFormatException(string message) : Exception(message);

public static class StravaActivitiesCsvReader
{
    private enum Field
    {
        Id, Date, Name, Type, FileName, Elapsed, Moving, Distance, AverageSpeed, ElevationGain,
        MaxHeartRate, AverageHeartRate, AverageWatts, WeightedAverageWatts, Calories,
    }

    private static readonly Dictionary<Field, (string[] Aliases, int FallbackIndex)> Columns = new()
    {
        [Field.Id] = (["ID aktivity", "Activity ID"], 0),
        [Field.Date] = (["Datum aktivity", "Activity Date"], 1),
        [Field.Name] = (["Název aktivity", "Activity Name"], 2),
        [Field.Type] = (["Typ aktivity", "Activity Type"], 3),
        [Field.FileName] = (["Název souboru", "Filename"], 12),
        [Field.Elapsed] = (["Uplynulý čas", "Elapsed Time"], 15),
        [Field.Moving] = (["Aktivní čas", "Moving Time"], 16),
        [Field.Distance] = (["Vzdálenost", "Distance"], 17),
        [Field.AverageSpeed] = (["Průměrná rychlost", "Average Speed"], 19),
        [Field.ElevationGain] = (["Nastoupaná výška", "Elevation Gain"], 20),
        [Field.MaxHeartRate] = (["Maximální tepová frekvence", "Max Heart Rate"], 30),
        [Field.AverageHeartRate] = (["Průměrná tepová frekvence", "Average Heart Rate"], 31),
        [Field.AverageWatts] = (["Průměrný výkon ve wattech", "Average Watts"], 33),
        [Field.WeightedAverageWatts] = (["Vážený průměrný výkon", "Weighted Average Power"], 46),
        [Field.Calories] = (["Kalorie", "Calories"], 34),
    };

    // cs: "6. 9. 2026 11:18:40"; en: "Sep 6, 2026, 11:18:40 AM" (older exports drop the second comma).
    private static readonly (CultureInfo Culture, string[] Formats)[] DateFormats =
    [
        (CultureInfo.GetCultureInfo("cs-CZ"), ["d. M. yyyy H:mm:ss", "d.M.yyyy H:mm:ss"]),
        (CultureInfo.GetCultureInfo("en-US"), ["MMM d, yyyy, h:mm:ss tt", "MMM d, yyyy h:mm:ss tt"]),
        (CultureInfo.InvariantCulture, ["yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm:ssZ"]),
    ];

    public static IEnumerable<StravaCsvActivity> Read(TextReader reader)
    {
        using var records = ReadRecords(reader).GetEnumerator();
        if (!records.MoveNext())
        {
            throw new StravaArchiveFormatException("Soubor activities.csv je prázdný.");
        }

        var header = records.Current.Select(h => h.Trim().Normalize(NormalizationForm.FormC)).ToArray();
        var index = ResolveColumns(header);
        var rawKeys = UniqueRawKeys(header);

        var rowNumber = 0;
        while (records.MoveNext())
        {
            var cells = records.Current;
            rowNumber++;
            if (cells.Count == 1 && string.IsNullOrWhiteSpace(cells[0]))
            {
                continue;
            }
            yield return ParseRow(rowNumber, cells, index, rawKeys);
        }
    }

    private static Dictionary<Field, int> ResolveColumns(string[] header)
    {
        var result = new Dictionary<Field, int>();
        foreach (var (field, (aliases, fallback)) in Columns)
        {
            var found = -1;
            for (var i = 0; i < header.Length; i++)
            {
                if (aliases.Any(a => string.Equals(a, header[i], StringComparison.OrdinalIgnoreCase)))
                {
                    found = i; // keep scanning: the last occurrence is the machine-units one
                }
            }
            if (found < 0 && fallback < header.Length)
            {
                found = fallback;
            }
            if (found >= 0)
            {
                result[field] = found;
            }
        }

        if (!result.ContainsKey(Field.Id) || !result.ContainsKey(Field.Date) || header.Length < 13)
        {
            throw new StravaArchiveFormatException("Soubor activities.csv nemá očekávané sloupce exportu ze Stravy.");
        }
        return result;
    }

    private static string[] UniqueRawKeys(string[] header)
    {
        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        return header.Select(h =>
        {
            var count = seen[h] = seen.GetValueOrDefault(h) + 1;
            return count == 1 ? h : $"{h} #{count}";
        }).ToArray();
    }

    private static StravaCsvActivity ParseRow(int rowNumber, IReadOnlyList<string> cells, Dictionary<Field, int> index, string[] rawKeys)
    {
        string? Text(Field f) => index.TryGetValue(f, out var i) && i < cells.Count && !string.IsNullOrWhiteSpace(cells[i]) ? cells[i].Trim() : null;
        decimal? Number(Field f) => Text(f) is { } t && decimal.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;

        var raw = new Dictionary<string, string>();
        for (var i = 0; i < cells.Count && i < rawKeys.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(cells[i]))
            {
                raw[rawKeys[i]] = cells[i];
            }
        }

        var id = Text(Field.Id) ?? string.Empty;
        var date = ParseDate(Text(Field.Date));
        string? error = null;
        if (!long.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out _))
        {
            error = "Chybí nebo je neplatné ID aktivity.";
        }

        return new StravaCsvActivity(
            rowNumber, id, date, Text(Field.Name), Text(Field.Type), Text(Field.FileName),
            Number(Field.Elapsed), Number(Field.Moving), Number(Field.Distance), Number(Field.AverageSpeed),
            Number(Field.ElevationGain), Number(Field.MaxHeartRate), Number(Field.AverageHeartRate),
            Number(Field.AverageWatts), Number(Field.WeightedAverageWatts), Number(Field.Calories),
            raw, error);
    }

    /// <summary>The export's dates are UTC (they equal the FIT session start_time), just formatted
    /// in the account's locale.</summary>
    internal static DateTime? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        // Some locales emit non-breaking/narrow spaces (e.g. before AM/PM) — normalize them.
        var normalized = value.Replace(' ', ' ').Replace(' ', ' ').Trim();
        foreach (var (culture, formats) in DateFormats)
        {
            if (DateTime.TryParseExact(normalized, formats, culture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
            {
                return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
            }
        }
        return null;
    }

    /// <summary>RFC 4180: quoted fields may contain commas, doubled quotes and newlines (activity
    /// descriptions do).</summary>
    internal static IEnumerable<List<string>> ReadRecords(TextReader reader)
    {
        var record = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var any = false;
        int c;
        while ((c = reader.Read()) != -1)
        {
            any = true;
            var ch = (char)c;
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (reader.Peek() == '"')
                    {
                        field.Append('"');
                        reader.Read();
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(ch);
                }
                continue;
            }

            switch (ch)
            {
                case '"':
                    inQuotes = true;
                    break;
                case ',':
                    record.Add(field.ToString());
                    field.Clear();
                    break;
                case '\r':
                    break;
                case '\n':
                    record.Add(field.ToString());
                    field.Clear();
                    yield return record;
                    record = [];
                    any = false;
                    break;
                case '﻿' when record.Count == 0 && field.Length == 0:
                    break;
                default:
                    field.Append(ch);
                    break;
            }
        }

        if (any)
        {
            record.Add(field.ToString());
            yield return record;
        }
    }
}
