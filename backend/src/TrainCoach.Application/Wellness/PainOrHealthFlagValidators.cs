using FluentValidation;

namespace TrainCoach.Application.Wellness;

public class CreatePainOrHealthFlagRequestValidator : AbstractValidator<CreatePainOrHealthFlagRequest>
{
    public CreatePainOrHealthFlagRequestValidator()
    {
        RuleFor(x => x.AthleteUserId).NotEmpty();
        RuleFor(x => x.BodyPart).MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}

public class UpdateHealthFlagStatusRequestValidator : AbstractValidator<UpdateHealthFlagStatusRequest>
{
    public UpdateHealthFlagStatusRequestValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
    }
}
