using FluentValidation;

namespace TrainCoach.Application.Planning;

public class CreateRaceRequestValidator : AbstractValidator<CreateRaceRequest>
{
    public CreateRaceRequestValidator()
    {
        RuleFor(x => x.AthleteUserId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Location).MaximumLength(200);
        RuleFor(x => x.DistanceMeters).GreaterThan(0).When(x => x.DistanceMeters.HasValue);
        RuleFor(x => x.ElevationGainMeters).GreaterThanOrEqualTo(0).When(x => x.ElevationGainMeters.HasValue);
        RuleFor(x => x.TargetTimeSeconds).GreaterThan(0).When(x => x.TargetTimeSeconds.HasValue);
        RuleFor(x => x.TargetResultNote).MaximumLength(1000);
    }
}

public class UpdateRaceRequestValidator : AbstractValidator<UpdateRaceRequest>
{
    public UpdateRaceRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Location).MaximumLength(200);
        RuleFor(x => x.DistanceMeters).GreaterThan(0).When(x => x.DistanceMeters.HasValue);
        RuleFor(x => x.ElevationGainMeters).GreaterThanOrEqualTo(0).When(x => x.ElevationGainMeters.HasValue);
        RuleFor(x => x.TargetTimeSeconds).GreaterThan(0).When(x => x.TargetTimeSeconds.HasValue);
        RuleFor(x => x.TargetResultNote).MaximumLength(1000);
        RuleFor(x => x.ActualTimeSeconds).GreaterThan(0).When(x => x.ActualTimeSeconds.HasValue);
        RuleFor(x => x.ActualResultNote).MaximumLength(1000);
        RuleFor(x => x.ResultNotes).MaximumLength(2000);
    }
}
