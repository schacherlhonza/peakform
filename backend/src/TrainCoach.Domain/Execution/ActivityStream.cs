using TrainCoach.Domain.Common;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Domain.Execution;

/// <summary>
/// A stored, downsampled detail stream (time series of heart rate, power, cadence, distance,
/// altitude, speed, GPS, temperature) for one <see cref="ActivitySourceRecord"/>, so the activity
/// detail can draw charts and the route map without calling the provider. Only sources whose data
/// may be retained are stored here — the athlete's own Strava archive, not the live Strava API.
/// <see cref="Payload"/> is the compact columnar encoding produced by ActivityStreamCodec
/// (see docs/integrations/strava-archive-import.md, "Streamy").
/// </summary>
public class ActivityStream : Entity
{
    public Guid ActivitySourceRecordId { get; set; }
    public ActivitySourceRecord ActivitySourceRecord { get; set; } = null!;

    public ActivityStreamOrigin Origin { get; set; }
    public int SampleCount { get; set; }

    /// <summary>Samples in the source file before downsampling to at most ~2 000 points.</summary>
    public int OriginalSampleCount { get; set; }

    public ActivityStreamChannels Channels { get; set; }

    public double? StartLatitude { get; set; }
    public double? StartLongitude { get; set; }
    public double? MinLatitude { get; set; }
    public double? MinLongitude { get; set; }
    public double? MaxLatitude { get; set; }
    public double? MaxLongitude { get; set; }

    /// <summary>Version of BestEffortCalculator that last computed this activity's best efforts
    /// from this stream (0 = never) — lets the background pass find what still needs (re)computing.</summary>
    public int BestEffortsVersion { get; set; }

    /// <summary>The efforts came from the full-resolution file rather than this downsampled stream.</summary>
    public bool BestEffortsPrecise { get; set; }

    public byte FormatVersion { get; set; }
    public byte[] Payload { get; set; } = [];

    public DateTime CreatedAtUtc { get; set; }
}
