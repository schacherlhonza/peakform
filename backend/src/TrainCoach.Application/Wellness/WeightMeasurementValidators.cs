using FluentValidation;

namespace TrainCoach.Application.Wellness;

public class UpsertWeightMeasurementRequestValidator : AbstractValidator<UpsertWeightMeasurementRequest>
{
    public UpsertWeightMeasurementRequestValidator()
    {
        RuleFor(x => x.AthleteUserId).NotEmpty();
        RuleFor(x => x.WeightKg).InclusiveBetween(20, 300);
        RuleFor(x => x.Source).IsInEnum();
    }
}
