namespace TrainCoach.Application.Common;

/// <summary>
/// The athlete's calendar dates (what the UI sends as from/to) converted to UTC instants for
/// filtering <c>StartedAtUtc</c>: a date runs from local midnight to the next local midnight in
/// the athlete's time zone (<c>UserProfile.TimeZoneId</c>), DST included. Comparing a date with
/// UTC directly dropped e.g. a Monday 00:30 Prague-time activity (Sunday 22:30 UTC) from "this
/// week".
/// </summary>
public static class AthleteLocalDates
{
    private const string DefaultTimeZoneId = "Europe/Prague";

    public static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        foreach (var id in new[] { timeZoneId, DefaultTimeZoneId })
        {
            if (!string.IsNullOrWhiteSpace(id) && TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone))
            {
                return zone;
            }
        }
        return TimeZoneInfo.Utc;
    }

    /// <summary>UTC start of the local <paramref name="date"/>.</summary>
    public static DateTime StartOfDayUtc(DateOnly date, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified), zone);

    /// <returns>Inclusive UTC lower bound for <paramref name="from"/>, exclusive UTC upper bound for <paramref name="to"/>.</returns>
    public static (DateTime? FromUtc, DateTime? ToUtcExclusive) ToUtcRange(DateOnly? from, DateOnly? to, TimeZoneInfo zone) =>
        (from is { } f ? StartOfDayUtc(f, zone) : null, to is { } t ? StartOfDayUtc(t.AddDays(1), zone) : null);
}
