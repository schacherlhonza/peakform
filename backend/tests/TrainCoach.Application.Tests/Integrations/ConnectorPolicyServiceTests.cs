using FluentAssertions;
using TrainCoach.Application.Integrations;
using TrainCoach.Domain.Enums;
using Xunit;

namespace TrainCoach.Application.Tests.Integrations;

/// <summary>Pure tests of the default-policy rule table (internal, exposed via InternalsVisibleTo) —
/// no database. DB-backed seeding/reseed-preserves-overrides behavior is covered in Api.IntegrationTests.</summary>
public class ConnectorPolicyServiceTests
{
    [Fact]
    public void Strava_Activities_IsPrimary_WhenIntervalsIcuNotConnected()
    {
        var connected = new HashSet<IntegrationProviderType> { IntegrationProviderType.Strava };

        var mode = ConnectorPolicyService.DefaultMode(IntegrationProviderType.Strava, DataDomain.Activities, connected);

        mode.Should().Be(ConnectorMode.Primary);
    }

    [Fact]
    public void Strava_Activities_BecomesFallbackOnly_OnceIntervalsIcuIsConnected()
    {
        var connected = new HashSet<IntegrationProviderType> { IntegrationProviderType.Strava, IntegrationProviderType.IntervalsIcu };

        var mode = ConnectorPolicyService.DefaultMode(IntegrationProviderType.Strava, DataDomain.Activities, connected);

        mode.Should().Be(ConnectorMode.FallbackOnly);
    }

    [Theory]
    [InlineData(DataDomain.Sleep)]
    [InlineData(DataDomain.Hrv)]
    [InlineData(DataDomain.DailyWellness)]
    public void Strava_NeverSuppliesWellnessDomains_DefaultsToDisabled(DataDomain domain)
    {
        var connected = new HashSet<IntegrationProviderType> { IntegrationProviderType.Strava };

        var mode = ConnectorPolicyService.DefaultMode(IntegrationProviderType.Strava, domain, connected);

        mode.Should().Be(ConnectorMode.Disabled);
    }

    [Fact]
    public void IntervalsIcu_IsPrimary_ForActivitiesAndWellness()
    {
        var connected = new HashSet<IntegrationProviderType> { IntegrationProviderType.IntervalsIcu };

        ConnectorPolicyService.DefaultMode(IntegrationProviderType.IntervalsIcu, DataDomain.Activities, connected).Should().Be(ConnectorMode.Primary);
        ConnectorPolicyService.DefaultMode(IntegrationProviderType.IntervalsIcu, DataDomain.Sleep, connected).Should().Be(ConnectorMode.Primary);
        ConnectorPolicyService.DefaultMode(IntegrationProviderType.IntervalsIcu, DataDomain.BodyComposition, connected).Should().Be(ConnectorMode.Secondary);
    }
}
