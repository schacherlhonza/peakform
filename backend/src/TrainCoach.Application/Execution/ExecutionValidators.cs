using FluentValidation;

namespace TrainCoach.Application.Execution;

public class CreateManualActivityRequestValidator : AbstractValidator<CreateManualActivityRequest>
{
    public CreateManualActivityRequestValidator()
    {
        RuleFor(x => x.AthleteUserId).NotEmpty();
        RuleFor(x => x.DurationSeconds).GreaterThan(0);
        RuleFor(x => x.Title).MaximumLength(200);
    }
}

public class UpsertTrainingFeedbackRequestValidator : AbstractValidator<UpsertTrainingFeedbackRequest>
{
    public UpsertTrainingFeedbackRequestValidator()
    {
        RuleFor(x => x.AthleteUserId).NotEmpty();
        RuleFor(x => x.Rpe).InclusiveBetween(1, 10).When(x => x.Rpe.HasValue);
        RuleFor(x => x.OverallRating).InclusiveBetween(1, 5).When(x => x.OverallRating.HasValue);
        RuleFor(x => x.PainNote).MaximumLength(1000);
        RuleFor(x => x.FreeText).MaximumLength(2000);
    }
}

public class CreateCommentRequestValidator : AbstractValidator<CreateCommentRequest>
{
    public CreateCommentRequestValidator()
    {
        RuleFor(x => x.PlannedWorkoutId).NotEmpty();
        RuleFor(x => x.Text).NotEmpty().MaximumLength(2000);
    }
}
