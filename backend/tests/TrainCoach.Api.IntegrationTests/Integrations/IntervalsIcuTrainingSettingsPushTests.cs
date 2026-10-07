using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrainCoach.Application.Integrations;
using TrainCoach.Integrations.IntervalsIcu;
using Xunit;

namespace TrainCoach.Api.IntegrationTests.Integrations;

/// <summary>The intervals.icu zone write against a fake HTTP handler — no network.</summary>
public class IntervalsIcuTrainingSettingsPushTests
{
    private sealed class RecordingHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent("{}") };
        }
    }

    private sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private static readonly HeartRateZoneBound[] Zones =
    [
        new(1, "Z1 Regenerace", 144), new(2, "Z2 Vytrvalost", 155), new(3, "", 161), new(4, "Z4 Práh", 167), new(5, "Z5 VO2max", 190),
    ];

    private static IntervalsIcuIntegrationProvider Provider(HttpMessageHandler handler) =>
        new(new SingleClientFactory(handler), Options.Create(new IntervalsIcuOptions()), NullLogger<IntervalsIcuIntegrationProvider>.Instance);

    [Fact]
    public async Task Writes_upper_bounds_names_and_max_hr_to_run_settings_without_recalculation()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);

        await Provider(handler).PushTrainingSettingsAsync("token", new TrainingSettingsUpdate(Zones, null));

        handler.Request!.Method.Should().Be(HttpMethod.Put);
        handler.Request.RequestUri!.ToString().Should().Be("https://intervals.icu/api/v1/athlete/0/sport-settings/Run?recalcHrZones=false");
        handler.Request.Headers.Authorization!.ToString().Should().Be("Bearer token");
        using var body = JsonDocument.Parse(handler.Body!);
        body.RootElement.GetProperty("hr_zones").EnumerateArray().Select(e => e.GetInt32()).Should().Equal(144, 155, 161, 167, 190);
        body.RootElement.GetProperty("hr_zone_names").EnumerateArray().Select(e => e.GetString()).Should()
            .Equal("Z1 Regenerace", "Z2 Vytrvalost", "Z3", "Z4 Práh", "Z5 VO2max");
        body.RootElement.GetProperty("max_hr").GetInt32().Should().Be(190, "intervals.icu overwrites max_hr unless it equals the last bound");
        body.RootElement.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(["hr_zones", "hr_zone_names", "max_hr"], "the PUT is partial — nothing else may change");
    }

    [Fact]
    public async Task Threshold_pace_is_sent_as_meters_per_second_and_alone_leaves_the_zones_alone()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);

        await Provider(handler).PushTrainingSettingsAsync("token", new TrainingSettingsUpdate([], 270));

        using var body = JsonDocument.Parse(handler.Body!);
        body.RootElement.EnumerateObject().Select(p => p.Name).Should().Equal("threshold_pace");
        body.RootElement.GetProperty("threshold_pace").GetDouble().Should().BeApproximately(3.7037, 0.0001, "4:30/km = 1000 m / 270 s");
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, "znovu připojit")]
    [InlineData(HttpStatusCode.NotFound, "nastavení pro běh")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "HTTP 422")]
    public async Task Rejection_becomes_a_user_facing_message(HttpStatusCode status, string expected)
    {
        var act = () => Provider(new RecordingHandler(status)).PushTrainingSettingsAsync("token", new TrainingSettingsUpdate(Zones, null));

        (await act.Should().ThrowAsync<TrainingSettingsSyncException>()).Which.Message.Should().Contain(expected);
    }
}
