using FluentValidation;

namespace TrainCoach.Application.Relationships;

public class InviteAthleteRequestValidator : AbstractValidator<InviteAthleteRequest>
{
    public InviteAthleteRequestValidator()
    {
        RuleFor(x => x.AthleteEmail).NotEmpty().EmailAddress();
        RuleFor(x => x.Note).MaximumLength(500);
    }
}
