using FluentValidation;

namespace TrainCoach.Application.Nutrition;

public class CreateHydrationEntryRequestValidator : AbstractValidator<CreateHydrationEntryRequest>
{
    public CreateHydrationEntryRequestValidator()
    {
        RuleFor(x => x.AthleteUserId).NotEmpty();
        RuleFor(x => x.VolumeMilliliters).GreaterThan(0);
        RuleFor(x => x.CaffeineMilligrams).GreaterThanOrEqualTo(0).When(x => x.CaffeineMilligrams.HasValue);
        RuleFor(x => x.Note).MaximumLength(500);
    }
}
