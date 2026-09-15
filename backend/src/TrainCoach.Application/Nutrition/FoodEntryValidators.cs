using FluentValidation;

namespace TrainCoach.Application.Nutrition;

public class CreateFoodEntryRequestValidator : AbstractValidator<CreateFoodEntryRequest>
{
    public CreateFoodEntryRequestValidator()
    {
        RuleFor(x => x.AthleteUserId).NotEmpty();
        RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
        RuleFor(x => x.EstimatedCarbsGrams).GreaterThanOrEqualTo(0).When(x => x.EstimatedCarbsGrams.HasValue);
        RuleFor(x => x.EstimatedProteinGrams).GreaterThanOrEqualTo(0).When(x => x.EstimatedProteinGrams.HasValue);
        RuleFor(x => x.PhotoUrl).MaximumLength(2000);
        RuleFor(x => x.RelativeToWorkoutNote).MaximumLength(200);
    }
}
