using Microsoft.Extensions.Options;
using TrainCoach.Application.Common;
using TrainCoach.Application.Integrations;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Integrations.Whoop;

/// <summary>
/// Architecture-ready only — same role/rationale as OuraIntegrationProvider: WHOOP's real
/// authoritative role here (recovery, strain, cycles) is direct-source only, never activities
/// primary. No real developer credentials exist yet; every method throws until activated. See
/// docs/integrations/oura-whoop-activation.md — do not invent endpoints.
/// </summary>
public class WhoopIntegrationProvider(IOptions<WhoopOptions> options) : IIntegrationProvider, IWellnessDataProvider
{
    private readonly WhoopOptions _options = options.Value;

    public IntegrationProviderType ProviderType => IntegrationProviderType.Whoop;
    public bool RequiresOAuthRedirect => true;

    public string BuildAuthorizationUrl(string state)
    {
        RequireConfigured();
        throw new BusinessRuleException("WHOOP OAuth authorize URL není implementována — viz docs/integrations/oura-whoop-activation.md.");
    }

    public Task<ExternalTokenResult> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        RequireConfigured();
        throw new BusinessRuleException("WHOOP OAuth token exchange není implementována — viz docs/integrations/oura-whoop-activation.md.");
    }

    public Task<ExternalTokenResult> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        RequireConfigured();
        throw new BusinessRuleException("WHOOP token refresh není implementován — viz docs/integrations/oura-whoop-activation.md.");
    }

    public Task<IReadOnlyList<ExternalActivity>> FetchRecentActivitiesAsync(string accessToken, DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        RequireConfigured();
        throw new BusinessRuleException("WHOOP activities fetch není implementován — viz docs/integrations/oura-whoop-activation.md.");
    }

    public Task<IReadOnlyList<ExternalWellnessSample>> FetchWellnessAsync(string accessToken, DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        RequireConfigured();
        throw new BusinessRuleException("WHOOP wellness fetch není implementován — viz docs/integrations/oura-whoop-activation.md.");
    }

    private void RequireConfigured()
    {
        if (!_options.IsConfigured)
        {
            throw new BusinessRuleException(
                "WHOOP integrace není nakonfigurována — chybí Client Id/Secret. Viz docs/integrations/oura-whoop-activation.md pro návod na aktivaci.");
        }
    }
}
