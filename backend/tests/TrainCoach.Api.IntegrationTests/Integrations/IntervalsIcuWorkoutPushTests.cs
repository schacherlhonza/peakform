using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrainCoach.Application.Integrations;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Planning;
using TrainCoach.Integrations.IntervalsIcu;
using Xunit;

namespace TrainCoach.Api.IntegrationTests.Integrations;

/// <summary>Planned workout push to the intervals.icu calendar against a fake HTTP handler — no network.</summary>
public class IntervalsIcuWorkoutPushTests
{
    private sealed class RecordingHandler(HttpStatusCode status, string responseBody = "[]") : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, string? Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request, request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken)));
            return new HttpResponseMessage(status) { Content = new StringContent(responseBody) };
        }
    }

    private sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private static IntervalsIcuIntegrationProvider Provider(HttpMessageHandler handler) =>
        new(new SingleClientFactory(handler), Options.Create(new IntervalsIcuOptions()), NullLogger<IntervalsIcuIntegrationProvider>.Instance);

    private static PlannedWorkout Workout(bool isRestDay = false)
    {
        var workout = new PlannedWorkout { Date = new DateOnly(2026, 10, 7), Sport = SportType.Running, Title = "Lehký běh", IsRestDay = isRestDay };
        workout.Segments.Add(new WorkoutSegment { Order = 1, Type = WorkoutSegmentType.Main, DurationSeconds = 2700, IntensityTargetType = IntensityTargetType.HeartRateZone, TargetHeartRateZoneNumber = 2 });
        return workout;
    }

    [Fact]
    public async Task Upserts_one_workout_event_keyed_by_the_peakform_id()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, """[{"id": 98765, "name": "Lehký běh"}]""");
        var workout = Workout();

        var result = await Provider(handler).UpsertWorkoutAsync("token", workout);

        result!.ExternalEventId.Should().Be("98765");
        var (request, body) = handler.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Post);
        request.RequestUri!.ToString().Should().Be("https://intervals.icu/api/v1/athlete/0/events/bulk?upsert=true&upsertOnUid=false&updatePlanApplied=false");
        using var json = JsonDocument.Parse(body!);
        var ev = json.RootElement.EnumerateArray().Should().ContainSingle().Subject;
        ev.GetProperty("category").GetString().Should().Be("WORKOUT");
        ev.GetProperty("start_date_local").GetString().Should().Be("2026-10-07T00:00:00");
        ev.GetProperty("type").GetString().Should().Be("Run");
        ev.GetProperty("name").GetString().Should().Be("Lehký běh");
        ev.GetProperty("description").GetString().Should().Be("- 45m Z2 HR intensity=active");
        ev.GetProperty("target").GetString().Should().Be("HR");
        ev.GetProperty("external_id").GetString().Should().Be(workout.Id.ToString());
        ev.TryGetProperty("moving_time", out _).Should().BeFalse("intervals.icu computes it from the steps");
    }

    [Fact]
    public async Task Rest_day_is_not_sent()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);

        (await Provider(handler).UpsertWorkoutAsync("token", Workout(isRestDay: true))).Should().BeNull();
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Removes_by_external_id()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, """{"eventsDeleted": 1}""");
        var id = Guid.NewGuid();

        await Provider(handler).RemoveWorkoutAsync("token", id);

        var (request, body) = handler.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Put);
        request.RequestUri!.ToString().Should().Be("https://intervals.icu/api/v1/athlete/0/events/bulk-delete");
        body.Should().Be($$"""[{"external_id":"{{id}}"}]""");
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, "znovu připojit")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "HTTP 422")]
    public async Task Rejection_becomes_a_user_facing_message(HttpStatusCode status, string expected)
    {
        var act = () => Provider(new RecordingHandler(status)).UpsertWorkoutAsync("token", Workout());

        (await act.Should().ThrowAsync<WorkoutPushException>()).Which.Message.Should().Contain(expected);
    }
}
