using FluentValidation;

namespace TrainCoach.Application.Wellness;

public class UpsertRecoveryMetricRequestValidator : AbstractValidator<UpsertRecoveryMetricRequest>
{
    public UpsertRecoveryMetricRequestValidator()
    {
        RuleFor(x => x.AthleteUserId).NotEmpty();
        RuleFor(x => x.RestingHeartRateBpm).InclusiveBetween(20, 220).When(x => x.RestingHeartRateBpm.HasValue);
        RuleFor(x => x.ReadinessScore).InclusiveBetween(0, 100).When(x => x.ReadinessScore.HasValue);
        RuleFor(x => x.StressScore).InclusiveBetween(0, 100).When(x => x.StressScore.HasValue);
        RuleFor(x => x.Source).IsInEnum();
    }
}
