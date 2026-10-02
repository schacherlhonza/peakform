using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using Microsoft.EntityFrameworkCore;
using TrainCoach.Application.Common;
using TrainCoach.Application.Execution.Streams;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Account;

public interface IActivityStreamExportService
{
    /// <summary>Writes a ZIP of the user's stored activity streams — GPX for activities with a
    /// GPS route, CSV for the rest — to <paramref name="output"/>. Returns how many were written.</summary>
    Task<int> WriteZipAsync(Guid userId, Stream output, CancellationToken cancellationToken = default);
}

/// <summary>
/// GDPR data portability for the detail streams (docs/security.md §11), separate from the JSON
/// account export because it can run to tens of MB. GPX 1.1 with the Garmin TrackPointExtension
/// (hr, cad, atemp) plus a <c>power</c> element — the format Strava, Garmin and intervals.icu read
/// back; CSV (one row per sample) for streams without a position. These are the stored, downsampled
/// streams (≤ 2 000 points per activity).
/// </summary>
public class ActivityStreamExportService(IApplicationDbContext db) : IActivityStreamExportService
{
    private const int BatchSize = 100;

    public async Task<int> WriteZipAsync(Guid userId, Stream output, CancellationToken cancellationToken = default)
    {
        var activityIds = await db.CompletedActivities.AsNoTracking()
            .Where(a => a.AthleteUserId == userId && a.SourceRecords.Any(sr => sr.Stream != null))
            .OrderBy(a => a.StartedAtUtc)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        var written = 0;
        using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        foreach (var batch in activityIds.Chunk(BatchSize))
        {
            var activities = await db.CompletedActivities.AsNoTracking()
                .Where(a => batch.Contains(a.Id))
                .Select(a => new
                {
                    a.Id, a.Title, a.Sport, a.StartedAtUtc,
                    Streams = a.SourceRecords.Where(sr => sr.Stream != null)
                        .Select(sr => new { sr.Stream!.Payload, sr.Stream.FormatVersion, sr.Stream.OriginalSampleCount, sr.Stream.SampleCount, sr.Stream.Channels })
                        .ToList(),
                })
                .ToListAsync(cancellationToken);

            foreach (var a in activities.OrderBy(a => a.StartedAtUtc))
            {
                var stream = a.Streams.OrderByDescending(s => s.SampleCount).First();
                var data = ActivityStreamCodec.Decode(stream.Payload, stream.FormatVersion, stream.OriginalSampleCount);
                var hasRoute = stream.Channels.HasFlag(ActivityStreamChannels.Position);
                var entry = zip.CreateEntry($"{EntryName(a.StartedAtUtc, a.Title, a.Id)}.{(hasRoute ? "gpx" : "csv")}", CompressionLevel.Optimal);
                await using var entryStream = entry.Open();
                if (hasRoute)
                {
                    WriteGpx(entryStream, a.Title, a.Sport, a.StartedAtUtc, data);
                }
                else
                {
                    WriteCsv(entryStream, a.StartedAtUtc, data);
                }
                written++;
            }
        }
        return written;
    }

    private static string EntryName(DateTime startedAtUtc, string? title, Guid id)
    {
        var safe = new string((title ?? "aktivita").Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
        if (safe.Length > 40)
        {
            safe = safe[..40];
        }
        return $"{startedAtUtc:yyyy-MM-dd_HHmm}_{(safe.Length > 0 ? safe : "aktivita")}_{id.ToString("N")[..8]}";
    }

    internal static void WriteGpx(Stream output, string? title, SportType sport, DateTime startedAtUtc, ActivityStreamData d)
    {
        const string gpx = "http://www.topografix.com/GPX/1/1";
        const string tpx = "http://www.garmin.com/xmlschemas/TrackPointExtension/v1";
        using var w = XmlWriter.Create(output, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = false, Async = false });
        w.WriteStartDocument();
        w.WriteStartElement("gpx", gpx);
        w.WriteAttributeString("version", "1.1");
        w.WriteAttributeString("creator", "PeakForm");
        w.WriteAttributeString("xmlns", "gpxtpx", null, tpx);
        w.WriteStartElement("metadata", gpx);
        w.WriteElementString("time", gpx, startedAtUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
        w.WriteEndElement();
        w.WriteStartElement("trk", gpx);
        w.WriteElementString("name", gpx, title ?? sport.ToString());
        w.WriteElementString("type", gpx, sport.ToString().ToLowerInvariant());
        w.WriteStartElement("trkseg", gpx);
        for (var i = 0; i < d.Count; i++)
        {
            if (d.Latitude?[i] is not { } lat || d.Longitude?[i] is not { } lon)
            {
                continue;
            }
            w.WriteStartElement("trkpt", gpx);
            w.WriteAttributeString("lat", lat.ToString("F7", CultureInfo.InvariantCulture));
            w.WriteAttributeString("lon", lon.ToString("F7", CultureInfo.InvariantCulture));
            if (d.AltitudeMeters?[i] is { } ele)
            {
                w.WriteElementString("ele", gpx, ele.ToString("F1", CultureInfo.InvariantCulture));
            }
            w.WriteElementString("time", gpx, startedAtUtc.AddSeconds(d.TimeOffsetsSeconds[i]).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
            var hr = d.HeartRateBpm?[i];
            var cad = d.CadenceRpm?[i];
            var temp = d.TemperatureC?[i];
            var power = d.PowerWatts?[i];
            if (hr is not null || cad is not null || temp is not null || power is not null)
            {
                w.WriteStartElement("extensions", gpx);
                if (power is { } p)
                {
                    w.WriteElementString("power", gpx, p.ToString(CultureInfo.InvariantCulture));
                }
                if (hr is not null || cad is not null || temp is not null)
                {
                    w.WriteStartElement("gpxtpx", "TrackPointExtension", tpx);
                    if (temp is { } t)
                    {
                        w.WriteElementString("gpxtpx", "atemp", tpx, t.ToString("F1", CultureInfo.InvariantCulture));
                    }
                    if (hr is { } h)
                    {
                        w.WriteElementString("gpxtpx", "hr", tpx, h.ToString(CultureInfo.InvariantCulture));
                    }
                    if (cad is { } c)
                    {
                        w.WriteElementString("gpxtpx", "cad", tpx, c.ToString(CultureInfo.InvariantCulture));
                    }
                    w.WriteEndElement();
                }
                w.WriteEndElement();
            }
            w.WriteEndElement();
        }
        w.WriteEndElement();
        w.WriteEndElement();
        w.WriteEndElement();
        w.WriteEndDocument();
    }

    internal static void WriteCsv(Stream output, DateTime startedAtUtc, ActivityStreamData d)
    {
        using var w = new StreamWriter(output, new UTF8Encoding(false), leaveOpen: true);
        w.WriteLine("time_utc,offset_s,heart_rate_bpm,power_w,cadence_rpm,distance_m,altitude_m,speed_mps,temperature_c");
        string N(double? v, string format) => v is { } x ? x.ToString(format, CultureInfo.InvariantCulture) : "";
        for (var i = 0; i < d.Count; i++)
        {
            w.Write(startedAtUtc.AddSeconds(d.TimeOffsetsSeconds[i]).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
            w.Write(',');
            w.Write(d.TimeOffsetsSeconds[i].ToString(CultureInfo.InvariantCulture));
            w.WriteLine(
                $",{d.HeartRateBpm?[i]},{d.PowerWatts?[i]},{d.CadenceRpm?[i]},{N(d.DistanceMeters?[i], "F1")},{N(d.AltitudeMeters?[i], "F1")},{N(d.SpeedMetersPerSecond?[i], "F3")},{N(d.TemperatureC?[i], "F1")}");
        }
    }
}
