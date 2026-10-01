using System.Net;
using System.Text;
using System.Web;
using FluentAssertions;
using Microsoft.Extensions.Options;
using TrainCoach.Integrations.Strava;
using Xunit;

namespace TrainCoach.Api.IntegrationTests.Integrations;

/// <summary>The Strava adapter against a fake HTTP handler — no network.</summary>
public class StravaIntegrationProviderPagingTests
{
    /// <summary>Serves <paramref name="total"/> activities, <c>per_page</c> at a time.</summary>
    private sealed class PagedActivitiesHandler(int total) : HttpMessageHandler
    {
        public List<int> RequestedPages { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var query = HttpUtility.ParseQueryString(request.RequestUri!.Query);
            var page = int.Parse(query["page"] ?? "1");
            var perPage = int.Parse(query["per_page"]!);
            RequestedPages.Add(page);

            var from = (page - 1) * perPage;
            var items = Enumerable.Range(from, Math.Max(0, Math.Min(perPage, total - from)))
                .Select(i => $$"""{"id":{{1000 + i}},"name":"Běh {{i}}","sport_type":"Run","start_date":"2026-09-01T06:00:00Z","moving_time":1800,"distance":5000}""");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"[{string.Join(',', items)}]", Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    [Theory]
    [InlineData(450, 3)] // 200 + 200 + 50
    [InlineData(400, 3)] // two full pages, then an empty one confirms the end
    [InlineData(20, 1)]
    public async Task Fetches_every_page_until_a_short_one(int total, int expectedRequests)
    {
        var handler = new PagedActivitiesHandler(total);
        var provider = new StravaIntegrationProvider(new SingleClientFactory(handler), Options.Create(new StravaOptions()));

        var activities = await provider.FetchRecentActivitiesAsync("token", new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc));

        activities.Should().HaveCount(total);
        activities.Select(a => a.ExternalId).Should().OnlyHaveUniqueItems();
        handler.RequestedPages.Should().Equal(Enumerable.Range(1, expectedRequests));
    }
}
