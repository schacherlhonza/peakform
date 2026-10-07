using FluentValidation;
using TrainCoach.Domain.Enums;

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
    public WorkoutSegmentDtoValidator() : this(isBlockStep: false)
    {
    }

    private WorkoutSegmentDtoValidator(bool isBlockStep)
    {
        RuleFor(x => x.Order).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TargetRpe).InclusiveBetween(1, 10).When(x => x.TargetRpe.HasValue);
        RuleFor(x => x.TargetHeartRateZoneNumber).InclusiveBetween(1, 7).When(x => x.TargetHeartRateZoneNumber.HasValue);
        RuleFor(x => x.RepeatCount).InclusiveBetween(1, 100).When(x => x.RepeatCount.HasValue);
        RuleFor(x => x.DistanceMeters).GreaterThan(0).When(x => x.DistanceMeters.HasValue);
        RuleFor(x => x.DurationSeconds).GreaterThan(0).When(x => x.DurationSeconds.HasValue);
        // A Garmin step has exactly one end condition; neither means "until lap press".
        RuleFor(x => x.DistanceMeters).Null().When(x => x.DurationSeconds.HasValue)
            .WithMessage("Úsek končí buď časem, nebo vzdáleností, ne obojím.");
        RuleFor(x => x.TargetPaceSecondsPerKmMax).GreaterThanOrEqualTo(x => x.TargetPaceSecondsPerKmMin!.Value)
            .When(x => x.TargetPaceSecondsPerKmMin.HasValue && x.TargetPaceSecondsPerKmMax.HasValue);
        RuleFor(x => x.Notes).MaximumLength(500);

        if (isBlockStep)
        {
            // intervals.icu, our path to Garmin, has no nested repeats.
            RuleFor(x => x.Type).NotEqual(WorkoutSegmentType.Repeat).WithMessage("Blok opakování nelze vnořit do jiného bloku.");
            RuleFor(x => x.RepeatCount).Null().WithMessage("Krok v bloku se neopakuje samostatně — opakuje se celý blok.");
            RuleFor(x => x.Steps).Empty();
            return;
        }

        When(x => x.Type == WorkoutSegmentType.Repeat, () =>
        {
            RuleFor(x => x.Steps).NotEmpty().WithMessage("Blok opakování musí obsahovat aspoň jeden krok.");
            RuleFor(x => x.RepeatCount).NotNull().GreaterThanOrEqualTo(2).WithMessage("Blok se musí opakovat aspoň dvakrát.");
            RuleFor(x => x.DurationSeconds).Null().WithMessage("Délku mají kroky bloku, ne blok.");
            RuleFor(x => x.DistanceMeters).Null().WithMessage("Délku mají kroky bloku, ne blok.");
            RuleFor(x => x.IntensityTargetType).Equal(IntensityTargetType.Free).WithMessage("Cíl mají kroky bloku, ne blok.");
            RuleForEach(x => x.Steps).SetValidator(new WorkoutSegmentDtoValidator(isBlockStep: true));
        }).Otherwise(() =>
        {
            RuleFor(x => x.Steps).Empty().WithMessage("Kroky může mít jen blok opakování.");
        });
    }
}
