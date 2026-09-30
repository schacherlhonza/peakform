using System.Globalization;
using System.Xml;
using Dynastream.Fit;
using TrainCoach.Application.Execution.Streams;
using TrainCoach.Application.Integrations.StravaArchive;
using DateTime = System.DateTime;

namespace TrainCoach.Integrations.StravaArchive;

/// <summary>
/// Pulls just the identity bits out of an activity file from a Strava archive: sport, start time
/// and — for FIT — a stable file identity (<c>file_id</c>: manufacturer + device serial + creation
/// time), which is the level-3 match key once another source reports the same FIT file.
/// <see cref="ReadStream"/> additionally reads every sample (FIT <c>record</c>, GPX <c>trkpt</c>,
/// TCX <c>Trackpoint</c>) for the stored detail stream.
/// </summary>
public class ActivityFileProbe : IActivityFileProbe
{
    private static readonly XmlReaderSettings XmlSettings = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        IgnoreComments = true,
        IgnoreWhitespace = true,
    };

    public ActivityFileInfo? Probe(Stream content, ActivityFileFormat format, bool identityOnly = false)
    {
        try
        {
            return format switch
            {
                ActivityFileFormat.Fit => ProbeFit(content, identityOnly),
                ActivityFileFormat.Gpx => ProbeGpx(content),
                ActivityFileFormat.Tcx => ProbeTcx(content),
                _ => null,
            };
        }
        catch (Exception ex) when (ex is FitException or XmlException or IOException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>Thrown from the decoder's own event to stop reading once file_id is in hand.</summary>
    private sealed class StopDecodingException : Exception;

    private static ActivityFileInfo? ProbeFit(Stream content, bool identityOnly)
    {
        var decoder = new Decode();
        if (!decoder.IsFIT(content))
        {
            return null;
        }
        content.Position = 0;

        var broadcaster = new MesgBroadcaster();
        FileIdMesg? fileId = null;
        SessionMesg? session = null;
        broadcaster.FileIdMesgEvent += (_, e) =>
        {
            fileId ??= (FileIdMesg)e.mesg;
            if (identityOnly)
            {
                throw new StopDecodingException();
            }
        };
        broadcaster.SessionMesgEvent += (_, e) => session ??= (SessionMesg)e.mesg;
        decoder.MesgEvent += broadcaster.OnMesg;
        try
        {
            decoder.Read(content);
        }
        catch (Exception ex) when (fileId is not null && (ex is StopDecodingException || ex.InnerException is StopDecodingException))
        {
            // Expected early exit — see identityOnly.
        }

        var startedAt = session?.GetStartTime()?.GetDateTime() ?? fileId?.GetTimeCreated()?.GetDateTime();

        string? uuid = null;
        if (fileId?.GetSerialNumber() is { } serial && fileId.GetTimeCreated() is { } created)
        {
            uuid = $"fit:{fileId.GetManufacturer() ?? 0}:{serial}:{created.GetTimeStamp()}";
        }

        return new ActivityFileInfo(
            session?.GetSport()?.ToString(),
            startedAt is { } s ? DateTime.SpecifyKind(s, DateTimeKind.Utc) : null,
            uuid);
    }

    private static ActivityFileInfo? ProbeGpx(Stream content)
    {
        string? sport = null;
        DateTime? start = null;
        using var reader = XmlReader.Create(content, XmlSettings);
        while (reader.Read() && (sport is null || start is null))
        {
            if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }
            if (reader.LocalName == "type" && sport is null)
            {
                sport = reader.ReadElementContentAsString();
            }
            else if (reader.LocalName == "time" && start is null)
            {
                start = ParseUtc(reader.ReadElementContentAsString());
            }
        }
        return new ActivityFileInfo(sport, start, null);
    }

    private static ActivityFileInfo? ProbeTcx(Stream content)
    {
        using var reader = XmlReader.Create(content, XmlSettings);
        string? sport = null;
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }
            if (reader.LocalName == "Activity" && sport is null)
            {
                sport = reader.GetAttribute("Sport");
            }
            else if (reader.LocalName == "Id" && sport is not null)
            {
                return new ActivityFileInfo(sport, ParseUtc(reader.ReadElementContentAsString()), null);
            }
        }
        return sport is null ? null : new ActivityFileInfo(sport, null, null);
    }

    public ActivityStreamData? ReadStream(Stream content, ActivityFileFormat format)
    {
        try
        {
            var builder = new ActivityStreamBuilder();
            switch (format)
            {
                case ActivityFileFormat.Fit:
                    ReadFitRecords(content, builder);
                    break;
                case ActivityFileFormat.Gpx:
                    ReadXmlPoints(content, builder, pointElement: "trkpt");
                    break;
                case ActivityFileFormat.Tcx:
                    ReadXmlPoints(content, builder, pointElement: "Trackpoint");
                    break;
            }
            return builder.Build();
        }
        catch (Exception ex) when (ex is FitException or XmlException or IOException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    // FIT stores positions as "semicircles": 2^31 units = 180 degrees.
    private const double SemicirclesToDegrees = 180.0 / 2147483648.0;

    private static void ReadFitRecords(Stream content, ActivityStreamBuilder builder)
    {
        var decoder = new Decode();
        if (!decoder.IsFIT(content))
        {
            return;
        }
        content.Position = 0;

        var broadcaster = new MesgBroadcaster();
        broadcaster.RecordMesgEvent += (_, e) =>
        {
            var r = (RecordMesg)e.mesg;
            if (r.GetTimestamp() is not { } timestamp)
            {
                return;
            }
            var lat = r.GetPositionLat();
            var lon = r.GetPositionLong();
            builder.Add(new ActivityStreamBuilder.Sample(
                DateTime.SpecifyKind(timestamp.GetDateTime(), DateTimeKind.Utc),
                HeartRate: r.GetHeartRate() is { } hr and > 0 and < 255 ? hr : null,
                Power: r.GetPower() is { } power and < 65535 ? power : null,
                Cadence: r.GetCadence() is { } cadence and < 255 ? cadence : null,
                Distance: Finite(r.GetDistance()),
                Altitude: Finite(r.GetEnhancedAltitude()) ?? Finite(r.GetAltitude()),
                Speed: Finite(r.GetEnhancedSpeed()) ?? Finite(r.GetSpeed()),
                Latitude: lat is { } la and not int.MaxValue ? la * SemicirclesToDegrees : null,
                Longitude: lon is { } lo and not int.MaxValue ? lo * SemicirclesToDegrees : null,
                Temperature: r.GetTemperature() is { } temp and > -127 ? temp : null));
        };
        decoder.MesgEvent += broadcaster.OnMesg;
        decoder.Read(content);
    }

    private static double? Finite(float? value) => value is { } v && float.IsFinite(v) ? v : null;

    /// <summary>GPX <c>trkpt</c> (lat/lon attributes, ele, time, Garmin TrackPointExtension hr/cad/atemp,
    /// power) or TCX <c>Trackpoint</c> (Time, Position, AltitudeMeters, DistanceMeters,
    /// HeartRateBpm/Value, Cadence, and the ActivityExtension Speed/Watts/RunCadence). Matched by
    /// local name only — exporters disagree on namespaces/prefixes.</summary>
    private static void ReadXmlPoints(Stream content, ActivityStreamBuilder builder, string pointElement)
    {
        using var reader = XmlReader.Create(content, XmlSettings);
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element || reader.LocalName != pointElement)
            {
                continue;
            }

            double? lat = ParseDouble(reader.GetAttribute("lat"));
            double? lon = ParseDouble(reader.GetAttribute("lon"));
            DateTime? time = null;
            double? altitude = null, distance = null, speed = null, temperature = null;
            int? heartRate = null, cadence = null, power = null;
            var inHeartRate = false;

            using (var point = reader.ReadSubtree())
            {
                point.Read(); // the point element itself
                while (!point.EOF)
                {
                    if (point.NodeType == XmlNodeType.EndElement && point.LocalName == "HeartRateBpm")
                    {
                        inHeartRate = false;
                    }
                    if (point.NodeType != XmlNodeType.Element)
                    {
                        point.Read();
                        continue;
                    }

                    // ReadElementContentAsString already advances past the element, so only
                    // call Read() for elements we don't consume.
                    switch (point.LocalName)
                    {
                        case "HeartRateBpm":
                            inHeartRate = !point.IsEmptyElement;
                            point.Read();
                            break;
                        case "Value" when inHeartRate:
                            heartRate = ParseInt(point.ReadElementContentAsString());
                            break;
                        case "time" or "Time":
                            time = ParseUtc(point.ReadElementContentAsString());
                            break;
                        case "ele" or "AltitudeMeters":
                            altitude = ParseDouble(point.ReadElementContentAsString());
                            break;
                        case "DistanceMeters":
                            distance = ParseDouble(point.ReadElementContentAsString());
                            break;
                        case "LatitudeDegrees":
                            lat = ParseDouble(point.ReadElementContentAsString());
                            break;
                        case "LongitudeDegrees":
                            lon = ParseDouble(point.ReadElementContentAsString());
                            break;
                        case "hr":
                            heartRate = ParseInt(point.ReadElementContentAsString());
                            break;
                        case "cad" or "Cadence" or "RunCadence":
                            cadence = ParseInt(point.ReadElementContentAsString());
                            break;
                        case "power" or "Watts":
                            power = ParseInt(point.ReadElementContentAsString());
                            break;
                        case "atemp":
                            temperature = ParseDouble(point.ReadElementContentAsString());
                            break;
                        case "Speed":
                            speed = ParseDouble(point.ReadElementContentAsString());
                            break;
                        default:
                            point.Read();
                            break;
                    }
                }
            }

            if (time is { } t)
            {
                builder.Add(new ActivityStreamBuilder.Sample(t, heartRate, power, cadence, distance, altitude, speed, lat, lon, temperature));
            }
        }
    }

    private static double? ParseDouble(string? value) =>
        double.TryParse(value?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && double.IsFinite(d) ? d : null;

    private static int? ParseInt(string? value) => ParseDouble(value) is { } d ? (int)Math.Round(d) : null;

    private static DateTime? ParseUtc(string value) =>
        DateTime.TryParse(value.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed)
            ? DateTime.SpecifyKind(parsed, DateTimeKind.Utc)
            : null;
}
