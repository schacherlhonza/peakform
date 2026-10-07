using FluentValidation;

namespace TrainCoach.Application.Planning;

public class HeartRateZoneInputValidator : AbstractValidator<HeartRateZoneInput>
{
    public HeartRateZoneInputValidator()
    {
        RuleFor(x => x.ZoneNumber).InclusiveBetween(1, 7);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        // Zone 1 commonly starts "from 0" — only negative values are invalid.
        RuleFor(x => x.MinBpm).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaxBpm).GreaterThan(x => x.MinBpm);
        RuleFor(x => x.MinPaceSecondsPerKm).GreaterThan(0).When(x => x.MinPaceSecondsPerKm.HasValue);
        RuleFor(x => x.MaxPaceSecondsPerKm).GreaterThan(0).When(x => x.MaxPaceSecondsPerKm.HasValue);
    }
}

public class AthleteThresholdsDtoValidator : AbstractValidator<AthleteThresholdsDto>
{
    public AthleteThresholdsDtoValidator()
    {
        // 2:00–15:00 /km — anything outside is a typo, not a threshold.
        RuleFor(x => x.ThresholdPaceSecondsPerKm).InclusiveBetween(120, 900).When(x => x.ThresholdPaceSecondsPerKm.HasValue);
    }
}

public class SetHeartRateZonesRequestValidator : AbstractValidator<SetHeartRateZonesRequest>
{
    public SetHeartRateZonesRequestValidator()
    {
        RuleFor(x => x.AthleteUserId).NotEmpty();
        RuleFor(x => x.Zones).NotEmpty();
        RuleForEach(x => x.Zones).SetValidator(new HeartRateZoneInputValidator());
    }
}
