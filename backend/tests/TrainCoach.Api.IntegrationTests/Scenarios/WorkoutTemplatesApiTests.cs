using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using TrainCoach.Api.IntegrationTests.Infrastructure;
using Xunit;

namespace TrainCoach.Api.IntegrationTests.Scenarios;

public class WorkoutTemplatesApiTests : IntegrationTestBase
{
    private record SegmentDto(int Order, string Type, int? RepeatCount, int? DurationSeconds, string IntensityTargetType, int? TargetHeartRateZoneNumber, int? TargetPaceSecondsPerKmMin, List<SegmentDto>? Steps);
    private record TemplateDto(Guid Id, string Name, List<SegmentDto> Segments);

    private static object Segment(int order, string type, int seconds, int? zone = null, int? repeat = null) => new
    {
        order, type, durationSeconds = seconds, repeatCount = repeat,
        intensityTargetType = zone is null ? "Free" : "HeartRateZone", targetHeartRateZoneNumber = zone,
    };

    [Fact]
    public async Task Template_keeps_its_structure_with_athlete_independent_zone_targets()
    {
        var coach = AuthenticatedClient(await RegisterAsync($"coach-tpl-{Guid.NewGuid():N}@example.com", "Coach"));

        var created = await coach.PostAsJsonAsync("/api/workout-templates", new
        {
            name = "Intervaly 6×5 min", sport = "Running", description = "",
            segments = new[] { Segment(1, "WarmUp", 900, zone: 2), Segment(2, "Interval", 300, zone: 4, repeat: 6), Segment(3, "CoolDown", 600) },
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await created.Content.ReadFromJsonAsync<TemplateDto>(JsonOptions))!.Id;

        var updated = await coach.PutAsJsonAsync($"/api/workout-templates/{id}", new
        {
            name = "Intervaly 5×5 min", sport = "Running", description = "",
            segments = new[] { Segment(1, "WarmUp", 900, zone: 2), Segment(2, "Interval", 300, zone: 4, repeat: 5) },
        });
        updated.EnsureSuccessStatusCode();

        var templates = await coach.GetFromJsonAsync<List<TemplateDto>>("/api/workout-templates", JsonOptions);
        var template = templates!.Single(t => t.Id == id);
        template.Name.Should().Be("Intervaly 5×5 min");
        template.Segments.Select(s => (s.Order, s.Type, s.RepeatCount, s.TargetHeartRateZoneNumber))
            .Should().Equal((1, "WarmUp", null, 2), (2, "Interval", 5, 4));
    }

    private static object Block(int order, int repeat, params object[] steps) => new
    {
        order, type = "Repeat", repeatCount = repeat, intensityTargetType = "Free", steps,
    };

    [Fact]
    public async Task Repeat_blocks_are_stored_and_replaced_with_their_steps()
    {
        var coach = AuthenticatedClient(await RegisterAsync($"coach-tpl-{Guid.NewGuid():N}@example.com", "Coach"));

        var created = await coach.PostAsJsonAsync("/api/workout-templates", new
        {
            name = "6×(5 min + 2 min klus)", sport = "Running", description = "",
            segments = new[]
            {
                Segment(1, "WarmUp", 900, zone: 2),
                Block(2, 6, Segment(1, "Interval", 300, zone: 4), Segment(2, "Recovery", 120, zone: 1)),
                Segment(3, "CoolDown", 600),
            },
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await created.Content.ReadFromJsonAsync<TemplateDto>(JsonOptions))!.Id;

        // Replacing the structure deletes the old block together with its steps.
        var updated = await coach.PutAsJsonAsync($"/api/workout-templates/{id}", new
        {
            name = "4×(3 min + 1 min klus)", sport = "Running", description = "",
            segments = new[] { Block(1, 4, Segment(1, "Interval", 180, zone: 5), Segment(2, "Recovery", 60)) },
        });
        updated.EnsureSuccessStatusCode();

        var template = (await coach.GetFromJsonAsync<List<TemplateDto>>("/api/workout-templates", JsonOptions))!.Single(t => t.Id == id);
        var block = template.Segments.Should().ContainSingle().Subject;
        block.Type.Should().Be("Repeat");
        block.RepeatCount.Should().Be(4);
        block.Steps!.Select(s => (s.Order, s.Type, s.DurationSeconds)).Should().Equal((1, "Interval", 180), (2, "Recovery", 60));
    }

    [Fact]
    public async Task Template_rejects_a_nested_repeat_block()
    {
        var coach = AuthenticatedClient(await RegisterAsync($"coach-tpl-{Guid.NewGuid():N}@example.com", "Coach"));

        var response = await coach.PostAsJsonAsync("/api/workout-templates", new
        {
            name = "Vnořené", sport = "Running", description = "",
            segments = new[] { Block(1, 3, Block(1, 2, Segment(1, "Interval", 60))) },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Template_rejects_a_zone_outside_1_to_7()
    {
        var coach = AuthenticatedClient(await RegisterAsync($"coach-tpl-{Guid.NewGuid():N}@example.com", "Coach"));

        var response = await coach.PostAsJsonAsync("/api/workout-templates", new
        {
            name = "Špatná zóna", sport = "Running", description = "",
            segments = new[] { Segment(1, "Main", 600, zone: 8) },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
