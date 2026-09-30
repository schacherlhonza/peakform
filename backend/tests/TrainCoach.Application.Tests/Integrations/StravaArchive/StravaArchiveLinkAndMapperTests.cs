using FluentAssertions;
using TrainCoach.Application.Integrations.StravaArchive;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Tests.Integrations.StravaArchive;

public class StravaArchiveLinkAndMapperTests
{
    [Theory]
    [InlineData("https://email.strava.com/ls/click?upn=u001.abc")]
    [InlineData("https://s3.amazonaws.com/strava.portability/athlete/123/export/live/export_123.zip?X-Amz-Signature=x")]
    [InlineData("https://s3.eu-west-1.amazonaws.com/strava.portability/athlete/123/export/export_123.zip")]
    public void Accepts_strava_email_and_archive_links(string url)
    {
        StravaArchiveLink.TryParse(url, out _).Should().BeTrue();
    }

    [Theory]
    [InlineData("http://email.strava.com/ls/click?upn=x")] // not https
    [InlineData("https://email.strava.com/other")]
    [InlineData("https://s3.amazonaws.com/some-other-bucket/athlete/1/export_1.zip")]
    [InlineData("https://s3.amazonaws.com/strava.portability/athlete/1/export_1.exe")]
    [InlineData("https://evil.example/strava.portability/athlete/1/export_1.zip")]
    [InlineData("https://s3.amazonaws.com.evil.example/strava.portability/athlete/1/export_1.zip")]
    [InlineData("https://s3.amazonaws.com:8443/strava.portability/athlete/1/export_1.zip")]
    [InlineData("https://169.254.169.254/latest/meta-data")]
    [InlineData("not a url")]
    public void Rejects_everything_else(string url)
    {
        StravaArchiveLink.TryParse(url, out _).Should().BeFalse();
    }

    [Fact]
    public void Extracts_athlete_id_from_archive_url_and_file_name()
    {
        StravaArchiveLink.AthleteIdFromArchiveUrl(new Uri("https://s3.amazonaws.com/strava.portability/athlete/22305381/export/live/export_22305381.zip?a=b"))
            .Should().Be("22305381");
        StravaArchiveLink.AthleteIdFromFileName(@"C:\Downloads\export_22305381.zip").Should().Be("22305381");
        StravaArchiveLink.AthleteIdFromFileName("moje-data.zip").Should().BeNull();
    }

    private static StravaCsvActivity Row(
        string? type = "Běh", DateTime? start = null, decimal? moving = 1800, decimal? distance = 5000, decimal? speed = null) =>
        new(1, "987", start ?? new DateTime(2024, 5, 1, 6, 0, 0, DateTimeKind.Utc), "Ranní běh", type, "activities/1.fit.gz",
            ElapsedSeconds: 1900, MovingSeconds: moving, DistanceMeters: distance, AverageSpeedMetersPerSecond: speed,
            ElevationGainMeters: 40, MaxHeartRate: 170.0m, AverageHeartRate: 150.4m, AverageWatts: null, WeightedAverageWatts: 250,
            Calories: 400, RawValues: new Dictionary<string, string> { ["ID aktivity"] = "987" }, Error: null);

    [Fact]
    public void Maps_row_like_the_strava_api_adapter()
    {
        var activity = StravaArchiveActivityMapper.ToExternalActivity(Row(), new ActivityFileInfo("running", null, "fit:1:2:3"))!;

        activity.ExternalId.Should().Be("987");
        activity.Sport.Should().Be(SportType.Running);
        activity.DurationSeconds.Should().Be(1800, "moving time, same as the API adapter");
        activity.AveragePaceSecondsPerKm.Should().Be(360, "derived from distance/moving time when average speed is missing");
        activity.AverageHeartRateBpm.Should().Be(150);
        activity.FitFileUuid.Should().Be("fit:1:2:3");
        activity.AdditionalMetrics.Should().Contain(m => m.Type == ActivityMetricType.ElapsedTimeSeconds && m.Value == 1900)
            .And.Contain(m => m.Type == ActivityMetricType.WeightedAveragePowerWatts && m.Value == 250);
        activity.RawPayloadJson.Should().Contain("987");
    }

    [Fact]
    public void Unknown_label_uses_file_sport_and_missing_csv_date_uses_file_start()
    {
        var fileStart = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var row = Row(type: "Kitesurfing") with { StartedAtUtc = null };

        var activity = StravaArchiveActivityMapper.ToExternalActivity(row, new ActivityFileInfo("cycling", fileStart, null))!;

        activity.Sport.Should().Be(SportType.Cycling);
        activity.StartedAtUtc.Should().Be(fileStart);
    }

    [Fact]
    public void No_start_time_anywhere_is_unmappable()
    {
        StravaArchiveActivityMapper.ToExternalActivity(Row() with { StartedAtUtc = null }, null).Should().BeNull();
    }
}
