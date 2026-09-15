using FluentValidation;

namespace TrainCoach.Application.Planning;

public class CreateWorkoutTemplateRequestValidator : AbstractValidator<CreateWorkoutTemplateRequest>
{
    public CreateWorkoutTemplateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleForEach(x => x.Segments).SetValidator(new WorkoutSegmentDtoValidator()).When(x => x.Segments is not null);
    }
}

public class UpdateWorkoutTemplateRequestValidator : AbstractValidator<UpdateWorkoutTemplateRequest>
{
    public UpdateWorkoutTemplateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleForEach(x => x.Segments).SetValidator(new WorkoutSegmentDtoValidator()).When(x => x.Segments is not null);
    }
}
