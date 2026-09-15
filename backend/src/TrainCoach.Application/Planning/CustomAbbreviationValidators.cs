using FluentValidation;

namespace TrainCoach.Application.Planning;

public class CreateCustomAbbreviationRequestValidator : AbstractValidator<CreateCustomAbbreviationRequest>
{
    public CreateCustomAbbreviationRequestValidator()
    {
        RuleFor(x => x.Abbreviation).NotEmpty().MaximumLength(20);
        RuleFor(x => x.FullText).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}

public class UpdateCustomAbbreviationRequestValidator : AbstractValidator<UpdateCustomAbbreviationRequest>
{
    public UpdateCustomAbbreviationRequestValidator()
    {
        RuleFor(x => x.Abbreviation).NotEmpty().MaximumLength(20);
        RuleFor(x => x.FullText).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}
