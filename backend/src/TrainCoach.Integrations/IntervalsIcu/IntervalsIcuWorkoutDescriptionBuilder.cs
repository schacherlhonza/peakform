using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Planning;

namespace TrainCoach.Integrations.IntervalsIcu;

/// <summary>Something in the workout that won't reach the watch the way the coach planned it.</summary>
public enum IntervalsIcuExportWarning
{
    /// <summary>Garmin has no RPE target — sent as step text, the step runs without a target.</summary>
    RpeSentAsText,

    /// <summary>More steps than Garmin Connect accepts (<see cref="IntervalsIcuWorkoutDescriptionBuilder.GarminMaxSteps"/>).</summary>
    TooManySteps,

    /// <summary>Swim structure needs a pool length we don't store; intervals.icu's swim export is best effort anyway.</summary>
    SwimPoolLengthMissing,

    /// <summary>Strength/cross-training: only timed steps survive the Garmin export (no reps, exercises, weights).</summary>
    OnlyTimedStepsSurvive,

    /// <summary>A power target on a run — only watches measuring running power show it (not shown in the 2026-10-06 test).</summary>
    RunPowerMayNotShow,
}

/// <summary>The fields of an intervals.icu calendar event (<c>category: WORKOUT</c>) derived from a planned workout.</summary>
/// <param name="Type">intervals.icu activity type (<c>Run</c>, <c>Ride</c>, ...).</param>
/// <param name="Description">The coach's text plus the structure in intervals.icu's workout text format.</param>
/// <param name="Target">What the device steers by: <c>HR</c>, <c>PACE</c>, <c>POWER</c> or <c>AUTO</c>.</param>
/// <param name="MovingTimeSeconds">Only for workouts without structure — with steps intervals.icu computes it.</param>
public record IntervalsIcuWorkoutEvent(
    string Name,
    string Type,
    string Description,
    string Target,
    int? MovingTimeSeconds,
    IReadOnlyList<IntervalsIcuExportWarning> Warnings);

/// <summary>
/// Turns a <see cref="PlannedWorkout"/> into intervals.icu's workout text format, which intervals.icu
/// parses into steps and uploads to Garmin Connect. Text is the only reliable input — posting
/// <c>workout_doc</c> JSON or files loses structure. Syntax and its Garmin behaviour are documented in
/// docs/integrations/garmin-workout-model.md §3.
/// </summary>
public static partial class IntervalsIcuWorkoutDescriptionBuilder
{
    public const int GarminMaxSteps = 50;

    /// <summary>Nominal length of an open "until lap press" step — intervals.icu needs one to keep the step.</summary>
    private const string LapStepEstimate = "1m";

    /// <summary>Null for a rest day — nothing to put on the watch.</summary>
    public static IntervalsIcuWorkoutEvent? Build(PlannedWorkout workout)
    {
        if (workout.IsRestDay || workout.Sport == SportType.Rest)
        {
            return null;
        }

        var tree = Tree(workout.Segments);
        var warnings = new List<IntervalsIcuExportWarning>();
        var description = new StringBuilder(SanitizeText(workout.CoachDescription));

        if (tree.Count > 0)
        {
            if (description.Length > 0)
            {
                description.Append("\n\n");
            }
            description.Append(Structure(tree));

            if (StepCount(tree) > GarminMaxSteps) warnings.Add(IntervalsIcuExportWarning.TooManySteps);
            if (AllSteps(tree).Any(s => s.IntensityTargetType == IntensityTargetType.Rpe && s.TargetRpe.HasValue)) warnings.Add(IntervalsIcuExportWarning.RpeSentAsText);
            if (workout.Sport == SportType.Swimming) warnings.Add(IntervalsIcuExportWarning.SwimPoolLengthMissing);
            if (workout.Sport is SportType.Strength or SportType.CrossTraining or SportType.Other) warnings.Add(IntervalsIcuExportWarning.OnlyTimedStepsSurvive);
            if (workout.Sport == SportType.Running && AllSteps(tree).Any(s => s.IntensityTargetType == IntensityTargetType.Power && s.TargetPowerWatts > 0)) warnings.Add(IntervalsIcuExportWarning.RunPowerMayNotShow);
        }

        return new IntervalsIcuWorkoutEvent(
            Name: string.IsNullOrWhiteSpace(workout.Title) ? "Trénink" : workout.Title.Trim(),
            Type: MapType(workout.Sport),
            Description: description.ToString(),
            Target: SteeringTarget(AllSteps(tree)),
            MovingTimeSeconds: tree.Count == 0 ? workout.PlannedDurationSeconds : null,
            Warnings: warnings);
    }

    /// <summary>The inverse of <c>IntervalsIcuIntegrationProvider.MapSport</c>. Rest days are never pushed.</summary>
    public static string MapType(SportType sport) => sport switch
    {
        SportType.Running => "Run",
        SportType.Cycling => "Ride",
        SportType.Swimming => "Swim",
        SportType.Strength => "WeightTraining",
        _ => "Workout",
    };

    private sealed record Node(WorkoutSegment Segment, IReadOnlyList<WorkoutSegment> Steps);

    private static List<Node> Tree(IEnumerable<WorkoutSegment> segments)
    {
        var all = segments.ToList();
        Guid? ParentOf(WorkoutSegment s) => s.ParentSegmentId ?? s.ParentSegment?.Id;
        return all.Where(s => ParentOf(s) is null).OrderBy(s => s.Order)
            .Select(s => new Node(s, all.Where(c => ParentOf(c) == s.Id).OrderBy(c => c.Order).ToList()))
            .Where(n => n.Segment.Type != WorkoutSegmentType.Repeat || n.Steps.Count > 0)
            .ToList();
    }

    private static IEnumerable<WorkoutSegment> AllSteps(IEnumerable<Node> tree) =>
        tree.SelectMany(n => n.Segment.Type == WorkoutSegmentType.Repeat ? n.Steps : [n.Segment]);

    /// <summary>As Garmin counts: a repeat is one step plus its children, not unrolled.</summary>
    private static int StepCount(IEnumerable<Node> tree) => tree.Sum(n =>
        n.Segment.Type == WorkoutSegmentType.Repeat ? 1 + n.Steps.Count : n.Segment.RepeatCount > 1 ? 2 : 1);

    /// <summary>
    /// Plain steps one per line; a repeat (a block, or one step with <c>RepeatCount</c>) as an <c>Nx</c>
    /// line with its steps, surrounded by blank lines — intervals.icu ends a repeat at the next blank line.
    /// </summary>
    private static string Structure(IReadOnlyList<Node> tree)
    {
        var chunks = new List<(bool IsRepeat, string Text)>();
        foreach (var node in tree)
        {
            var s = node.Segment;
            if (s.Type == WorkoutSegmentType.Repeat)
            {
                chunks.Add((true, $"{Math.Max(2, s.RepeatCount ?? 2)}x\n" + string.Join('\n', node.Steps.Select(StepLine))));
            }
            else if (s.RepeatCount > 1)
            {
                chunks.Add((true, $"{s.RepeatCount}x\n{StepLine(s)}"));
            }
            else
            {
                chunks.Add((false, StepLine(s)));
            }
        }

        var text = new StringBuilder();
        for (var i = 0; i < chunks.Count; i++)
        {
            if (i > 0)
            {
                // Blank line around every repeat; consecutive plain steps stay on adjacent lines.
                text.Append(chunks[i].IsRepeat || chunks[i - 1].IsRepeat ? "\n\n" : "\n");
            }
            text.Append(chunks[i].Text);
        }
        return text.ToString();
    }

    /// <summary><c>- [cue] {length} [target] intensity={type}</c>.</summary>
    private static string StepLine(WorkoutSegment s)
    {
        var parts = new List<string> { "-" };
        var cue = Cue(s);
        if (cue.Length > 0) parts.Add(cue);

        if (s.DurationSeconds is > 0)
        {
            parts.Add(FormatDuration(s.DurationSeconds.Value));
        }
        else if (s.DistanceMeters is > 0)
        {
            parts.Add(FormatDistance(s.DistanceMeters.Value));
        }
        else
        {
            // Verified live 2026-10-06: a "Press lap" step without a length is silently dropped by the
            // parser; with one it's kept as until_lap_press and the length is only an estimate.
            parts.Add($"Press lap {LapStepEstimate}");
        }

        if (Target(s) is { } target) parts.Add(target);
        parts.Add($"intensity={Intensity(s.Type)}");
        return string.Join(' ', parts);
    }

    /// <summary>Step text shown on the watch: the coach's note, the label of types Garmin has no step type for, and RPE (no Garmin target).</summary>
    private static string Cue(WorkoutSegment s)
    {
        var cue = new List<string>();
        if (s.Type == WorkoutSegmentType.Drill) cue.Add("Abeceda");
        if (s.Type == WorkoutSegmentType.Strides) cue.Add("Stupňované úseky");
        if (!string.IsNullOrWhiteSpace(s.Notes)) cue.Add(SanitizeText(s.Notes));
        if (s.IntensityTargetType == IntensityTargetType.Rpe && s.TargetRpe is { } rpe) cue.Add($"RPE {rpe}");
        return string.Join(", ", cue.Where(c => c.Length > 0));
    }

    private static string? Target(WorkoutSegment s) => s.IntensityTargetType switch
    {
        IntensityTargetType.HeartRateZone when s.TargetHeartRateZoneNumber is >= 1 and <= 7 => $"Z{s.TargetHeartRateZoneNumber} HR",
        IntensityTargetType.Pace when PaceRange(s) is { } pace => pace,
        IntensityTargetType.Power when s.TargetPowerWatts is > 0 => $"{s.TargetPowerWatts}w",
        _ => null,
    };

    private static string? PaceRange(WorkoutSegment s)
    {
        var paces = new[] { s.TargetPaceSecondsPerKmMin, s.TargetPaceSecondsPerKmMax }.OfType<int>().Where(p => p > 0).Distinct().Order().ToList();
        return paces.Count switch
        {
            0 => null,
            1 => $"{FormatPace(paces[0])}/km Pace",
            _ => $"{FormatPace(paces[0])}-{FormatPace(paces[1])}/km Pace",
        };
    }

    /// <summary>
    /// intervals.icu step intensity — what Garmin shows as the step type. Always explicit: without it
    /// intervals.icu guesses, and a "Warmup" header alone doesn't set it.
    /// </summary>
    private static string Intensity(WorkoutSegmentType type) => type switch
    {
        WorkoutSegmentType.WarmUp => "warmup",
        WorkoutSegmentType.CoolDown => "cooldown",
        WorkoutSegmentType.Interval or WorkoutSegmentType.Strides => "interval",
        WorkoutSegmentType.Rest => "rest",
        WorkoutSegmentType.Recovery => "recovery",
        _ => "active",
    };

    /// <summary>The target type most steps use; the device steers by one of them.</summary>
    private static string SteeringTarget(IEnumerable<WorkoutSegment> steps)
    {
        var counts = steps.Select(s => Target(s) is null ? null : s.IntensityTargetType switch
            {
                IntensityTargetType.HeartRateZone => "HR",
                IntensityTargetType.Pace => "PACE",
                IntensityTargetType.Power => "POWER",
                _ => null,
            })
            .OfType<string>()
            .GroupBy(t => t)
            .Select(g => (Target: g.Key, Count: g.Count()))
            .OrderByDescending(g => g.Count)
            .ToList();
        // A tie leaves the choice to the athlete's sport settings in intervals.icu.
        return counts.Count == 0 || (counts.Count > 1 && counts[0].Count == counts[1].Count) ? "AUTO" : counts[0].Target;
    }

    private static string FormatDuration(int seconds)
    {
        var h = seconds / 3600;
        var m = seconds % 3600 / 60;
        var s = seconds % 60;
        var text = new StringBuilder();
        if (h > 0) text.Append(h).Append('h');
        if (m > 0) text.Append(m).Append('m');
        if (s > 0 || text.Length == 0) text.Append(s).Append('s');
        return text.ToString();
    }

    /// <summary>Whole kilometres as <c>km</c>, the rest as <c>mtr</c> — a bare <c>m</c> means minutes in this format.</summary>
    private static string FormatDistance(decimal meters) =>
        meters % 1000 == 0 || meters >= 10000
            ? (meters / 1000).ToString("0.###", CultureInfo.InvariantCulture) + "km"
            : Math.Round(meters).ToString(CultureInfo.InvariantCulture) + "mtr";

    private static string FormatPace(int secondsPerKm) => $"{secondsPerKm / 60}:{secondsPerKm % 60:00}";

    /// <summary>
    /// Free text (coach description, step notes) must not read as workout syntax: a leading <c>-</c> makes
    /// a step, <c>6x</c> a repeat, and tokens like <c>2m</c>, <c>1km</c>, <c>Z4</c>, <c>80%</c>, <c>250w</c>
    /// become a step's length or target. The unit is only split from its number (in a coach's text
    /// "1000m" means metres, in the syntax minutes), so the meaning stays the reader's.
    /// </summary>
    internal static string SanitizeText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var lines = text.Replace("\r\n", "\n").Split('\n').Select(line =>
        {
            var l = LeadingDash().Replace(line, "• ");
            l = RepeatToken().Replace(l, "$1×");
            l = ZoneToken().Replace(l, "zóna $1");
            l = UnitToken().Replace(l, "$1 $2");
            return l.TrimEnd();
        });
        return string.Join('\n', lines).Trim();
    }

    [GeneratedRegex(@"^\s*[-–]\s*")]
    private static partial Regex LeadingDash();

    [GeneratedRegex(@"(\d+)\s*[xX](?=\s|\d|$)")]
    private static partial Regex RepeatToken();

    [GeneratedRegex(@"\b[zZ](\d)\b")]
    private static partial Regex ZoneToken();

    // A number glued to a unit the parser understands.
    [GeneratedRegex(@"(\d+(?:[.,]\d+)?)(mtr|km|mi|rpm|bpm|s|m|h|w|%|'|"")(?![\p{L}\d])", RegexOptions.IgnoreCase)]
    private static partial Regex UnitToken();
}
