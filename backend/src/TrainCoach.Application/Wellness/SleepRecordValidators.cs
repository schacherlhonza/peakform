using FluentValidation;

namespace TrainCoach.Application.Wellness;

public class UpsertSleepRecordRequestValidator : AbstractValidator<UpsertSleepRecordRequest>
{
    public UpsertSleepRecordRequestValidator()
    {
        RuleFor(x => x.AthleteUserId).NotEmpty();
        RuleFor(x => x.DurationMinutes).InclusiveBetween(0, 1440).When(x => x.DurationMinutes.HasValue);
        RuleFor(x => x.DeepSleepMinutes).InclusiveBetween(0, 1440).When(x => x.DeepSleepMinutes.HasValue);
        RuleFor(x => x.RemSleepMinutes).InclusiveBetween(0, 1440).When(x => x.RemSleepMinutes.HasValue);
        RuleFor(x => x.Notes).MaximumLength(1000);
        RuleFor(x => x.Source).IsInEnum();
    }
}
