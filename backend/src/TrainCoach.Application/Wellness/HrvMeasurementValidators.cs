using FluentValidation;

namespace TrainCoach.Application.Wellness;

public class UpsertHrvMeasurementRequestValidator : AbstractValidator<UpsertHrvMeasurementRequest>
{
    public UpsertHrvMeasurementRequestValidator()
    {
        RuleFor(x => x.AthleteUserId).NotEmpty();
        RuleFor(x => x.RmssdMs).GreaterThan(0);
        RuleFor(x => x.Notes).MaximumLength(1000);
        RuleFor(x => x.Source).IsInEnum();
    }
}
