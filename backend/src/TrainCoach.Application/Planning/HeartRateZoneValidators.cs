using FluentValidation;

namespace TrainCoach.Application.Planning;

public class HeartRateZoneInputValidator : AbstractValidator<HeartRateZoneInput>
{
    public HeartRateZoneInputValidator()
    {
        RuleFor(x => x.ZoneNumber).InclusiveBetween(1, 7);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.MinBpm).GreaterThan(0);
        RuleFor(x => x.MaxBpm).GreaterThan(x => x.MinBpm);
        RuleFor(x => x.MinPaceSecondsPerKm).GreaterThan(0).When(x => x.MinPaceSecondsPerKm.HasValue);
        RuleFor(x => x.MaxPaceSecondsPerKm).GreaterThan(0).When(x => x.MaxPaceSecondsPerKm.HasValue);
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
