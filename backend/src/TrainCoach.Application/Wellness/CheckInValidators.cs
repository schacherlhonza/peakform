using FluentValidation;

namespace TrainCoach.Application.Wellness;

public class SubmitCheckInRequestValidator : AbstractValidator<SubmitCheckInRequest>
{
    public SubmitCheckInRequestValidator()
    {
        RuleFor(x => x.AthleteUserId).NotEmpty();
        RuleFor(x => x.Rpe).InclusiveBetween(1, 10).When(x => x.Rpe.HasValue);
        RuleFor(x => x.HydrationLiters).InclusiveBetween(0, 20).When(x => x.HydrationLiters.HasValue);
        RuleFor(x => x.Note).MaximumLength(1000);
    }
}
