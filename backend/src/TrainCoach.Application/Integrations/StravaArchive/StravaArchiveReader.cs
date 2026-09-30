using System.IO.Compression;
using System.Text;
using System.Text.Json;
using TrainCoach.Application.Execution.Streams;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Integrations.StravaArchive;

public enum ActivityFileFormat
{
    Fit,
    Gpx,
    Tcx,
}

/// <summary>What the activity file itself says — used only as a fallback/extra signal next to
/// the CSV row, which is the primary source of the summary values.</summary>
public record ActivityFileInfo(string? Sport, DateTime? StartedAtUtc, string? FitFileUuid);

/// <summary>Reads identity/sport/start out of a (decompressed) FIT, GPX or TCX stream. Implemented
/// in TrainCoach.Integrations (the FIT half needs the Garmin FIT SDK).</summary>
public interface IActivityFileProbe
{
    /// <summary>Null when the file can't be decoded — never throws for a malformed file. With
    /// <paramref name="identityOnly"/> a FIT file is only read up to its file_id message (the
    /// first one), skipping the full decode — sport/start then stay null.</summary>
    ActivityFileInfo? Probe(Stream content, ActivityFileFormat format, bool identityOnly = false);

    /// <summary>The full sample stream (not yet downsampled), or null when the file has no usable
    /// samples or can't be decoded — never throws for a malformed file.</summary>
    ActivityStreamData? ReadStream(Stream content, ActivityFileFormat format);
}

/// <param name="Stream">Downsampled detail stream — only read when the caller asked for streams.</param>
public record StravaArchiveActivity(StravaCsvActivity Row, ActivityFileInfo? File, ExternalActivity? Activity, string? Error, ActivityStreamData? Stream = null);

/// <summary>
/// Streams the activities out of a Strava export ZIP without extracting anything to disk: only
/// <c>activities.csv</c> and the files its rows reference are ever opened, each through a
/// size-capped decompression stream (zip-bomb guard). Everything else in the archive — messages,
/// followers, media, logins — is never read.
/// </summary>
public static class StravaArchiveReader
{
    public const string ActivitiesCsvEntry = "activities.csv";
    private const long MaxCsvBytes = 200L * 1024 * 1024;
    private const long MaxActivityFileBytes = 64L * 1024 * 1024;

    /// <param name="includeStreams">Also fully decode each activity file into a downsampled stream
    /// (much slower — the import phase only; the preview never needs it).</param>
    public static IEnumerable<StravaArchiveActivity> ReadActivities(
        string zipPath, IActivityFileProbe probe, CancellationToken cancellationToken = default, bool includeStreams = false)
    {
        using var zip = OpenZip(zipPath);
        var csvEntry = zip.GetEntry(ActivitiesCsvEntry)
            ?? throw new StravaArchiveFormatException("Archiv neobsahuje activities.csv – je to opravdu export dat ze Stravy?");

        using var csvStream = new LimitedReadStream(csvEntry.Open(), MaxCsvBytes);
        using var csvReader = new StreamReader(csvStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        foreach (var row in StravaActivitiesCsvReader.Read(csvReader))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (row.Error is not null)
            {
                yield return new StravaArchiveActivity(row, null, null, row.Error);
                continue;
            }

            // The CSV already has sport and start for almost every row; the file is then only
            // needed for its FIT identity, which is far cheaper than a full decode.
            var identityOnly = row.StartedAtUtc is not null && StravaSportTypeMapper.FromLabel(row.TypeLabel) is not null;
            var (file, stream) = ProbeFile(zip, row.FileName, probe, identityOnly, includeStreams);
            var activity = StravaArchiveActivityMapper.ToExternalActivity(row, file);
            yield return activity is null
                ? new StravaArchiveActivity(row, file, null, "Nelze určit čas začátku aktivity.")
                : new StravaArchiveActivity(row, file, activity, null, stream);
        }
    }

    /// <summary>Counts the rows without probing any activity file — cheap first pass for progress.</summary>
    public static int CountActivities(string zipPath)
    {
        using var zip = OpenZip(zipPath);
        var csvEntry = zip.GetEntry(ActivitiesCsvEntry)
            ?? throw new StravaArchiveFormatException("Archiv neobsahuje activities.csv – je to opravdu export dat ze Stravy?");
        using var reader = new StreamReader(new LimitedReadStream(csvEntry.Open(), MaxCsvBytes), Encoding.UTF8);
        return StravaActivitiesCsvReader.ReadRecords(reader).Count() - 1;
    }

    private static ZipArchive OpenZip(string zipPath)
    {
        try
        {
            return ZipFile.OpenRead(zipPath);
        }
        catch (InvalidDataException)
        {
            throw new StravaArchiveFormatException("Soubor není platný ZIP archiv.");
        }
    }

    private static (ActivityFileInfo? File, ActivityStreamData? Stream) ProbeFile(
        ZipArchive zip, string? fileName, IActivityFileProbe probe, bool identityOnly, bool includeStream)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return (null, null);
        }

        // Names come from the CSV, so look them up as ZIP entry names only — never used as a
        // filesystem path, which is what makes zip-slip a non-issue here.
        var entry = zip.GetEntry(fileName.Replace('\\', '/'));
        if (entry is null)
        {
            return (null, null);
        }

        var name = entry.FullName.ToLowerInvariant();
        var gzip = name.EndsWith(".gz", StringComparison.Ordinal);
        var bare = gzip ? name[..^3] : name;
        ActivityFileFormat? format = Path.GetExtension(bare) switch
        {
            ".fit" => ActivityFileFormat.Fit,
            ".gpx" => ActivityFileFormat.Gpx,
            ".tcx" => ActivityFileFormat.Tcx,
            _ => null,
        };
        if (format is null)
        {
            return (null, null);
        }

        try
        {
            using var entryStream = entry.Open();
            using Stream decompressed = gzip ? new GZipStream(entryStream, CompressionMode.Decompress) : entryStream;
            using var limited = new LimitedReadStream(decompressed, MaxActivityFileBytes);
            // FIT decoding needs a seekable stream; activity files are small, so buffer in memory.
            using var buffer = new MemoryStream();
            limited.CopyTo(buffer);
            buffer.Position = 0;
            var info = probe.Probe(buffer, format.Value, identityOnly);
            if (!includeStream)
            {
                return (info, null);
            }
            buffer.Position = 0;
            var stream = probe.ReadStream(buffer, format.Value);
            return (info, stream is null ? null : ActivityStreamDownsampler.Downsample(stream));
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or StravaArchiveFormatException)
        {
            return (null, null);
        }
    }

    /// <summary>Read-only wrapper that throws once more than <c>maxBytes</c> have been read.</summary>
    private sealed class LimitedReadStream(Stream inner, long maxBytes) : Stream
    {
        private long _read;

        public override int Read(byte[] buffer, int offset, int count)
        {
            var n = inner.Read(buffer, offset, count);
            _read += n;
            if (_read > maxBytes)
            {
                throw new StravaArchiveFormatException("Soubor v archivu je po rozbalení nepřiměřeně velký.");
            }
            return n;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => _read; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

public static class StravaArchiveActivityMapper
{
    /// <summary>
    /// Maps like StravaIntegrationProvider.FetchRecentActivitiesAsync does for the API — same
    /// ExternalId (the Strava activity id, not the upload id in the file name), moving time as the
    /// duration, pace from average speed — so the archive and API copies of one activity are
    /// interchangeable for level-1 dedup and fingerprinting. Null when no start time is known.
    /// </summary>
    public static ExternalActivity? ToExternalActivity(StravaCsvActivity row, ActivityFileInfo? file)
    {
        var startedAt = row.StartedAtUtc ?? file?.StartedAtUtc;
        if (startedAt is null)
        {
            return null;
        }

        var sport = StravaSportTypeMapper.FromLabel(row.TypeLabel) ?? StravaSportTypeMapper.FromFileSport(file?.Sport);
        var moving = row.MovingSeconds ?? row.ElapsedSeconds ?? 0;
        var distance = row.DistanceMeters is > 0 ? row.DistanceMeters : null;
        var speed = row.AverageSpeedMetersPerSecond is > 0
            ? row.AverageSpeedMetersPerSecond
            : distance is not null && moving > 0 ? distance / moving : null;

        var metrics = new List<ExternalActivityMetric>();
        if (row.ElapsedSeconds is { } elapsed)
        {
            metrics.Add(new ExternalActivityMetric(ActivityMetricType.ElapsedTimeSeconds, Math.Round(elapsed), "s"));
        }
        if (row.WeightedAverageWatts is { } weighted)
        {
            metrics.Add(new ExternalActivityMetric(ActivityMetricType.WeightedAveragePowerWatts, Math.Round(weighted), "W"));
        }

        return new ExternalActivity(
            ExternalId: row.ActivityId,
            Sport: sport,
            Title: row.Name,
            StartedAtUtc: DateTime.SpecifyKind(startedAt.Value, DateTimeKind.Utc),
            DurationSeconds: (int)Math.Round(moving),
            DistanceMeters: distance,
            ElevationGainMeters: row.ElevationGainMeters,
            AverageHeartRateBpm: Round(row.AverageHeartRate),
            MaxHeartRateBpm: Round(row.MaxHeartRate),
            AveragePaceSecondsPerKm: speed is > 0 ? (int)Math.Round(1000m / speed.Value) : null,
            AveragePowerWatts: Round(row.AverageWatts),
            Calories: Round(row.Calories),
            AdditionalMetrics: metrics,
            // The archive is the athlete's own data export, not Strava API data, so the API
            // agreement's no-retention rule doesn't apply — keep the full row (description, gear,
            // relative effort, ...) for later use.
            RawPayloadJson: JsonSerializer.Serialize(row.RawValues),
            FitFileUuid: file?.FitFileUuid);
    }

    private static int? Round(decimal? value) => value is { } v ? (int)Math.Round(v) : null;
}
