using FluentValidation;

namespace TrainCoach.Application.Wellness;

public class CreatePersonalRecordRequestValidator : AbstractValidator<CreatePersonalRecordRequest>
{
    public CreatePersonalRecordRequestValidator()
    {
        RuleFor(x => x.AthleteUserId).NotEmpty();
        RuleFor(x => x.DistanceLabel).NotEmpty().MaximumLength(50);
        RuleFor(x => x.TimeSeconds).GreaterThan(0).When(x => x.TimeSeconds.HasValue);
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}

public class UpdatePersonalRecordRequestValidator : AbstractValidator<UpdatePersonalRecordRequest>
{
    public UpdatePersonalRecordRequestValidator()
    {
        RuleFor(x => x.DistanceLabel).NotEmpty().MaximumLength(50);
        RuleFor(x => x.TimeSeconds).GreaterThan(0).When(x => x.TimeSeconds.HasValue);
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}
