using Microsoft.Extensions.Options;
using TrainCoach.Application.Common;
using TrainCoach.Application.Integrations;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Integrations.Oura;

/// <summary>
/// Architecture-ready only. Oura's role in this app is direct wellness/sleep/HRV — never a
/// primary activity source (see ConnectorMode defaults in ConnectorPolicyService) — but no real
/// developer credentials exist yet, so every method throws a clear, actionable error rather than
/// pretending to work. Do not invent endpoints here: implement the real HTTP calls only once real
/// credentials exist, against Oura's current official API docs at that time — see
/// docs/integrations/oura-whoop-activation.md.
/// </summary>
public class OuraIntegrationProvider(IOptions<OuraOptions> options) : IIntegrationProvider, IWellnessDataProvider
{
    private readonly OuraOptions _options = options.Value;

    public IntegrationProviderType ProviderType => IntegrationProviderType.Oura;
    public bool RequiresOAuthRedirect => true;

    public string BuildAuthorizationUrl(string state)
    {
        RequireConfigured();
        throw new BusinessRuleException("Oura OAuth authorize URL není implementována — viz docs/integrations/oura-whoop-activation.md.");
    }

    public Task<ExternalTokenResult> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        RequireConfigured();
        throw new BusinessRuleException("Oura OAuth token exchange není implementována — viz docs/integrations/oura-whoop-activation.md.");
    }

    public Task<ExternalTokenResult> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        RequireConfigured();
        throw new BusinessRuleException("Oura token refresh není implementován — viz docs/integrations/oura-whoop-activation.md.");
    }

    public Task<IReadOnlyList<ExternalActivity>> FetchRecentActivitiesAsync(string accessToken, DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        RequireConfigured();
        throw new BusinessRuleException("Oura activities fetch není implementován — viz docs/integrations/oura-whoop-activation.md.");
    }

    public Task<IReadOnlyList<ExternalWellnessSample>> FetchWellnessAsync(string accessToken, DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        RequireConfigured();
        throw new BusinessRuleException("Oura wellness fetch není implementován — viz docs/integrations/oura-whoop-activation.md.");
    }

    private void RequireConfigured()
    {
        if (!_options.IsConfigured)
        {
            throw new BusinessRuleException(
                "Oura integrace není nakonfigurována — chybí Client Id/Secret. Viz docs/integrations/oura-whoop-activation.md pro návod na aktivaci.");
        }
    }
}
