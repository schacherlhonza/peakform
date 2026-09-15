using FluentValidation;

namespace TrainCoach.Application.Planning;

public class CreateTrainingPlanRequestValidator : AbstractValidator<CreateTrainingPlanRequest>
{
    public CreateTrainingPlanRequestValidator()
    {
        RuleFor(x => x.AthleteUserId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate).When(x => x.EndDate.HasValue);
    }
}

public class CreateTrainingWeekRequestValidator : AbstractValidator<CreateTrainingWeekRequest>
{
    public CreateTrainingWeekRequestValidator()
    {
        RuleFor(x => x.WeekIndex).GreaterThan(0);
    }
}

public class UpdateTrainingWeekRequestValidator : AbstractValidator<UpdateTrainingWeekRequest>
{
    public UpdateTrainingWeekRequestValidator()
    {
        RuleFor(x => x.AthleteWeeklyRating).InclusiveBetween(1, 5).When(x => x.AthleteWeeklyRating.HasValue);
        RuleFor(x => x.CoachWeekSummary).MaximumLength(2000);
        RuleFor(x => x.AthleteWeekReflection).MaximumLength(2000);
        RuleFor(x => x.WhatWasMissingNote).MaximumLength(1000);
    }
}

public class CreatePlannedWorkoutRequestValidator : AbstractValidator<CreatePlannedWorkoutRequest>
{
    public CreatePlannedWorkoutRequestValidator()
    {
        RuleFor(x => x.TrainingWeekId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CoachDescription).MaximumLength(4000);
        RuleForEach(x => x.Segments).SetValidator(new WorkoutSegmentDtoValidator()).When(x => x.Segments is not null);
    }
}

public class UpdatePlannedWorkoutRequestValidator : AbstractValidator<UpdatePlannedWorkoutRequest>
{
    public UpdatePlannedWorkoutRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CoachDescription).MaximumLength(4000);
        RuleForEach(x => x.Segments).SetValidator(new WorkoutSegmentDtoValidator()).When(x => x.Segments is not null);
    }
}

public class WorkoutSegmentDtoValidator : AbstractValidator<WorkoutSegmentDto>
{
    public WorkoutSegmentDtoValidator()
    {
        RuleFor(x => x.Order).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TargetRpe).InclusiveBetween(1, 10).When(x => x.TargetRpe.HasValue);
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}
