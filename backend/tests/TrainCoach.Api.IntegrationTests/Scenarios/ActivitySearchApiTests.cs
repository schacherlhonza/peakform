using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TrainCoach.Api.IntegrationTests.Infrastructure;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Execution;
using TrainCoach.Infrastructure.Persistence;
using Xunit;

namespace TrainCoach.Api.IntegrationTests.Scenarios;

/// <summary>The paged activity-history endpoint behind the activities page.</summary>
public class ActivitySearchApiTests : IntegrationTestBase
{
    private record ItemDto(Guid Id, string Sport, string? Title, DateTime StartedAtUtc, string Source);
    private record SummaryDto(int Count, long TotalDurationSeconds, decimal TotalDistanceMeters, decimal TotalElevationGainMeters);
    private record PageDto(List<ItemDto> Items, int TotalCount, int Page, int PageSize, SummaryDto Summary);

    private async Task SeedAsync(Guid athleteUserId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TrainCoachDbContext>();
        // 30 runs on consecutive days in Jan 2024 + 5 rides in Feb 2024.
        for (var i = 0; i < 35; i++)
        {
            var isRun = i < 30;
            var record = new ActivitySourceRecord { Source = DataSource.Strava, ExternalId = $"{athleteUserId:N}-{i}", FetchedAtUtc = DateTime.UtcNow };
            db.CompletedActivities.Add(new CompletedActivity
            {
                AthleteUserId = athleteUserId,
                Sport = isRun ? SportType.Running : SportType.Cycling,
                Title = isRun ? $"Ranní běh {i}" : $"Vyjížďka {i}",
                StartedAtUtc = isRun ? new DateTime(2024, 1, 1, 7, 0, 0, DateTimeKind.Utc).AddDays(i) : new DateTime(2024, 2, 1, 9, 0, 0, DateTimeKind.Utc).AddDays(i - 30),
                DurationSeconds = 3600,
                DistanceMeters = isRun ? 10000 : 40000,
                ElevationGainMeters = 100,
                CreatedAtUtc = DateTime.UtcNow,
                PrimarySourceRecordId = record.Id,
                SourceRecords = { record },
            });
        }
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Pages_newest_first_with_totals_over_the_whole_filtered_set()
    {
        var auth = await RegisterAsync($"{Guid.NewGuid():N}@test.cz", "Athlete");
        var client = AuthenticatedClient(auth);
        await SeedAsync(Guid.Parse(auth.UserId));

        var first = (await client.GetFromJsonAsync<PageDto>($"/api/athletes/{auth.UserId}/activities/search?pageSize=10", JsonOptions))!;

        first.TotalCount.Should().Be(35);
        first.Items.Should().HaveCount(10);
        first.Items[0].Title.Should().Be("Vyjížďka 34");
        first.Items.Should().BeInDescendingOrder(i => i.StartedAtUtc);
        first.Items[0].Source.Should().Be("Strava");
        first.Summary.Count.Should().Be(35);
        first.Summary.TotalDurationSeconds.Should().Be(35 * 3600);
        first.Summary.TotalDistanceMeters.Should().Be(30 * 10000 + 5 * 40000);

        var last = (await client.GetFromJsonAsync<PageDto>($"/api/athletes/{auth.UserId}/activities/search?pageSize=10&page=4", JsonOptions))!;
        last.Items.Should().HaveCount(5);
        last.Items[^1].Title.Should().Be("Ranní běh 0");
    }

    [Fact]
    public async Task Filters_by_sport_date_range_and_title()
    {
        var auth = await RegisterAsync($"{Guid.NewGuid():N}@test.cz", "Athlete");
        var client = AuthenticatedClient(auth);
        await SeedAsync(Guid.Parse(auth.UserId));
        var baseUrl = $"/api/athletes/{auth.UserId}/activities/search";

        var rides = (await client.GetFromJsonAsync<PageDto>($"{baseUrl}?sports=Cycling", JsonOptions))!;
        rides.TotalCount.Should().Be(5);
        rides.Items.Should().OnlyContain(i => i.Sport == "Cycling");

        var both = (await client.GetFromJsonAsync<PageDto>($"{baseUrl}?sports=Cycling&sports=Running&pageSize=100", JsonOptions))!;
        both.TotalCount.Should().Be(35);

        var range = (await client.GetFromJsonAsync<PageDto>($"{baseUrl}?from=2024-01-10&to=2024-01-19", JsonOptions))!;
        range.TotalCount.Should().Be(10);
        range.Summary.TotalDistanceMeters.Should().Be(100000);

        var titled = (await client.GetFromJsonAsync<PageDto>($"{baseUrl}?search=VYJÍŽĎKA%203", JsonOptions))!;
        titled.Items.Select(i => i.Title).Should().BeEquivalentTo("Vyjížďka 30", "Vyjížďka 31", "Vyjížďka 32", "Vyjížďka 33", "Vyjížďka 34");

        var empty = (await client.GetFromJsonAsync<PageDto>($"{baseUrl}?search=plavání", JsonOptions))!;
        empty.TotalCount.Should().Be(0);
        empty.Summary.Count.Should().Be(0);
    }

    [Fact]
    public async Task Page_size_is_capped_and_other_athletes_history_is_forbidden()
    {
        var auth = await RegisterAsync($"{Guid.NewGuid():N}@test.cz", "Athlete");
        var other = await RegisterAsync($"{Guid.NewGuid():N}@test.cz", "Athlete");
        await SeedAsync(Guid.Parse(auth.UserId));

        var page = (await AuthenticatedClient(auth).GetFromJsonAsync<PageDto>($"/api/athletes/{auth.UserId}/activities/search?pageSize=1000", JsonOptions))!;
        page.PageSize.Should().Be(100);

        (await AuthenticatedClient(other).GetAsync($"/api/athletes/{auth.UserId}/activities/search")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Date_filters_use_the_athletes_time_zone_not_utc()
    {
        var auth = await RegisterAsync($"{Guid.NewGuid():N}@test.cz", "Athlete"); // Europe/Prague
        var client = AuthenticatedClient(auth);
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TrainCoachDbContext>();
            foreach (var (title, utc) in new[]
            {
                ("Neděle 23:30", new DateTime(2026, 9, 27, 21, 30, 0, DateTimeKind.Utc)), // Sunday in Prague
                ("Pondělí 0:30", new DateTime(2026, 9, 27, 22, 30, 0, DateTimeKind.Utc)), // Monday in Prague, still Sunday in UTC
                ("Neděle 22:30", new DateTime(2026, 10, 4, 20, 30, 0, DateTimeKind.Utc)),  // last day of that week
            })
            {
                var record = new ActivitySourceRecord { Source = DataSource.Strava, ExternalId = $"{auth.UserId}-{title}", FetchedAtUtc = DateTime.UtcNow };
                db.CompletedActivities.Add(new CompletedActivity
                {
                    AthleteUserId = Guid.Parse(auth.UserId), Sport = SportType.Running, Title = title, StartedAtUtc = utc, DurationSeconds = 600,
                    CreatedAtUtc = DateTime.UtcNow, PrimarySourceRecordId = record.Id, SourceRecords = { record },
                });
            }
            await db.SaveChangesAsync();
        }

        var week = (await client.GetFromJsonAsync<List<ItemDto>>($"/api/athletes/{auth.UserId}/activities?from=2026-09-28&to=2026-10-04", JsonOptions))!;
        week.Select(a => a.Title).Should().BeEquivalentTo(["Pondělí 0:30", "Neděle 22:30"]);

        var search = (await client.GetFromJsonAsync<PageDto>($"/api/athletes/{auth.UserId}/activities/search?from=2026-09-28&to=2026-10-04", JsonOptions))!;
        search.TotalCount.Should().Be(2);
    }
}
