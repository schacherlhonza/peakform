using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Execution;
using TrainCoach.Domain.Identity;
using TrainCoach.Domain.Integrations;
using TrainCoach.Domain.Nutrition;
using TrainCoach.Domain.Planning;
using TrainCoach.Domain.Reporting;
using TrainCoach.Domain.Wellness;
using TrainCoach.Infrastructure.Identity;

namespace TrainCoach.Infrastructure.Persistence;

/// <summary>
/// Realistic Czech demo data for local development/evaluation: one coach, two athletes, an
/// active season, races, a four-week running block (easy runs, intervals, strength, OCR,
/// rest), completed activities, feedback, check-ins, sleep/HRV/resting-HR, nutrition, comments,
/// heart-rate zones, and a coach abbreviation dictionary. Idempotent — does nothing if the demo
/// coach already exists. Never runs against a real deployment: called only for
/// ASPNETCORE_ENVIRONMENT=Development, never Production, and never in the Testing test host.
/// No real personal or health data — everything here is fictional.
/// </summary>
public static class DemoDataSeeder
{
    private const string Password = "Demo1234";

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<TrainCoachDbContext>();

        if (await userManager.FindByEmailAsync("kouc@demo.traincoach.cz") is not null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var monday = today.AddDays(-(int)today.DayOfWeek + (today.DayOfWeek == DayOfWeek.Sunday ? -6 : 1));
        var blockStart = monday.AddDays(-21); // four-week block ending this week

        var coach = await CreateUserAsync(userManager, db, "kouc@demo.traincoach.cz", "Jana", "Procházková", AppRole.Coach);
        var jakub = await CreateUserAsync(userManager, db, "jakub@demo.traincoach.cz", "Jakub", "Dvořák", AppRole.Athlete);
        var tereza = await CreateUserAsync(userManager, db, "tereza@demo.traincoach.cz", "Tereza", "Svobodová", AppRole.Athlete);

        db.CustomAbbreviations.AddRange(
            Abbrev(coach, "WU", "warm-up / rozklus"),
            Abbrev(coach, "R", "rozklus"),
            Abbrev(coach, "CD", "cool-down / výklus"),
            Abbrev(coach, "V", "výklus"),
            Abbrev(coach, "MK", "meziklus"),
            Abbrev(coach, "MCH", "mezichůze"),
            Abbrev(coach, "ABC", "běžecká abeceda a drilly"),
            Abbrev(coach, "VS", "výběh svahu"),
            Abbrev(coach, "SS", "skoky svahu"),
            Abbrev(coach, "P", "pauza"));

        var relJakub = await CreateActiveRelationshipAsync(db, coach, jakub, now);
        var relTereza = await CreateActiveRelationshipAsync(db, coach, tereza, now);

        var seasonJakub = new Season { AthleteUserId = jakub.Id, Name = "Jarní sezóna 2026", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 6, 30), CreatedAtUtc = now };
        var seasonTereza = new Season { AthleteUserId = tereza.Id, Name = "Jarní sezóna 2026", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 6, 30), CreatedAtUtc = now };
        db.Seasons.AddRange(seasonJakub, seasonTereza);

        var goalJakub = new Goal { AthleteUserId = jakub.Id, SeasonId = seasonJakub.Id, Title = "Půlmaraton pod 1:35", Priority = GoalPriority.A, TargetDate = today.AddDays(70), CreatedAtUtc = now };
        db.Goals.Add(goalJakub);

        db.Races.AddRange(
            new Race { AthleteUserId = jakub.Id, SeasonId = seasonJakub.Id, GoalId = goalJakub.Id, Name = "Pražský půlmaraton", Sport = SportType.Running, StartsAtUtc = today.AddDays(70).ToDateTime(new TimeOnly(9, 0)), Location = "Praha", DistanceMeters = 21097, Priority = GoalPriority.A, TargetTimeSeconds = 5700, CreatedAtUtc = now },
            new Race { AthleteUserId = jakub.Id, SeasonId = seasonJakub.Id, Name = "Velikonoční desítka", Sport = SportType.Running, StartsAtUtc = today.AddDays(-10).ToDateTime(new TimeOnly(10, 0)), Location = "Brno", DistanceMeters = 10000, Priority = GoalPriority.B, TargetTimeSeconds = 2400, ActualTimeSeconds = 2465, ActualResultNote = "12. místo v kategorii", CreatedAtUtc = now },
            new Race { AthleteUserId = tereza.Id, SeasonId = seasonTereza.Id, Name = "Krajský přebor v běhu do vrchu", Sport = SportType.Running, StartsAtUtc = today.AddDays(35).ToDateTime(new TimeOnly(10, 30)), Location = "Jeseníky", DistanceMeters = 12000, ElevationGainMeters = 650, Priority = GoalPriority.A, CreatedAtUtc = now });

        db.HeartRateZones.AddRange(
            HrZone(jakub, 1, "Z1 Regenerace", 110, 130, today), HrZone(jakub, 2, "Z2 Vytrvalost", 131, 150, today), HrZone(jakub, 3, "Z3 Tempo", 151, 165, today), HrZone(jakub, 4, "Z4 Práh", 166, 178, today), HrZone(jakub, 5, "Z5 VO2max", 179, 195, today),
            HrZone(tereza, 1, "Z1 Regenerace", 115, 135, today), HrZone(tereza, 2, "Z2 Vytrvalost", 136, 155, today), HrZone(tereza, 3, "Z3 Tempo", 156, 170, today), HrZone(tereza, 4, "Z4 Práh", 171, 182, today), HrZone(tereza, 5, "Z5 VO2max", 183, 198, today));

        var plan = new TrainingPlan { AthleteUserId = jakub.Id, CoachUserId = coach.Id, SeasonId = seasonJakub.Id, Name = "Příprava na půlmaraton", StartDate = blockStart, IsActive = true, CreatedAtUtc = now };
        db.TrainingPlans.Add(plan);
        await db.SaveChangesAsync();

        var random = new Random(42);
        for (var weekIdx = 0; weekIdx < 4; weekIdx++)
        {
            var weekStart = blockStart.AddDays(weekIdx * 7);
            var week = new TrainingWeek
            {
                TrainingPlanId = plan.Id,
                WeekStartDate = weekStart,
                WeekIndex = weekIdx + 1,
                CoachWeekSummary = weekIdx == 3 ? "Poslední týden bloku, mírně snižujeme objem." : "Standardní vytrvalostní týden s jedním kvalitním tréninkem.",
                CreatedAtUtc = now,
            };
            db.TrainingWeeks.Add(week);
            await db.SaveChangesAsync();

            var monWorkout = NewWorkout(week, weekStart, SportType.Running, "Lehký běh", "45 min volným tempem v Z2, ABC 10 min před.", 8000, 2700, now);
            var tueWorkout = NewWorkout(week, weekStart.AddDays(1), SportType.Strength, "Posilovna", "Kruhový trénink na core a stabilitu, 40 min.", null, 2400, now);
            var wedWorkout = NewWorkout(week, weekStart.AddDays(2), SportType.Running, "Intervaly 6x1000m", "WU 15 min, 6x1000m v Z4 tempu s P 2 min klus, CD 10 min.", 12000, 3600, now);
            wedWorkout.Segments.Add(new WorkoutSegment { Order = 1, Type = WorkoutSegmentType.WarmUp, DurationSeconds = 900, IntensityTargetType = IntensityTargetType.Free, Notes = "WU + ABC" });
            wedWorkout.Segments.Add(new WorkoutSegment { Order = 2, Type = WorkoutSegmentType.Interval, RepeatCount = 6, DistanceMeters = 1000, IntensityTargetType = IntensityTargetType.HeartRateZone, Notes = "Z4 tempo" });
            wedWorkout.Segments.Add(new WorkoutSegment { Order = 3, Type = WorkoutSegmentType.Rest, RepeatCount = 6, DurationSeconds = 120, IntensityTargetType = IntensityTargetType.Free, Notes = "Klusem" });
            wedWorkout.Segments.Add(new WorkoutSegment { Order = 4, Type = WorkoutSegmentType.CoolDown, DurationSeconds = 600, IntensityTargetType = IntensityTargetType.Free });
            var thuWorkout = NewWorkout(week, weekStart.AddDays(3), SportType.Rest, "Odpočinek", string.Empty, null, null, now, isRest: true);
            var friWorkout = NewWorkout(week, weekStart.AddDays(4), SportType.CrossTraining, "OCR trénink", "Překážkový trénink: lezení, přenášení břemen, 50 min.", null, 3000, now);
            var satWorkout = NewWorkout(week, weekStart.AddDays(5), SportType.Running, "Dlouhý běh", "90 min v Z2, poslední 15 min v Z3.", 18000, 5400, now);
            var sunWorkout = NewWorkout(week, weekStart.AddDays(6), SportType.Rest, "Odpočinek / regenerace", "Volitelně 20 min plavání nebo jóga.", null, null, now, isRest: true);

            db.PlannedWorkouts.AddRange(monWorkout, tueWorkout, wedWorkout, thuWorkout, friWorkout, satWorkout, sunWorkout);
            await db.SaveChangesAsync();

            if (weekStart.ToDateTime(TimeOnly.MinValue) <= now)
            {
                AddCompletedActivity(db, jakub, monWorkout, weekStart, 8100, 2650, 148, random, now);
                AddCompletedActivity(db, jakub, wedWorkout, weekStart.AddDays(2), 12200, 3550, 168, random, now);
                AddCompletedActivity(db, jakub, satWorkout, weekStart.AddDays(5), 17800, 5300, 152, random, now);

                foreach (var (workout, date) in new[] { (monWorkout, weekStart), (wedWorkout, weekStart.AddDays(2)), (satWorkout, weekStart.AddDays(5)) })
                {
                    if (date.ToDateTime(TimeOnly.MinValue) > now) continue;
                    db.TrainingFeedbacks.Add(new TrainingFeedback
                    {
                        AthleteUserId = jakub.Id,
                        PlannedWorkoutId = workout.Id,
                        Date = date,
                        Rpe = 4 + random.Next(0, 5),
                        LegsFeeling = (WellnessScale)(3 + random.Next(0, 3)),
                        OverallRating = 3 + random.Next(0, 3),
                        FreeText = "Trénink proběhl podle plánu.",
                        CreatedAtUtc = now,
                    });
                }

                for (var d = 0; d < 7; d++)
                {
                    var date = weekStart.AddDays(d);
                    if (date.ToDateTime(TimeOnly.MinValue) > now) continue;

                    db.DailyCheckIns.Add(MorningCheckIn(jakub, date, random, now));
                    db.DailyCheckIns.Add(EveningCheckIn(jakub, date, random, now));
                    db.SleepRecords.Add(new SleepRecord { AthleteUserId = jakub.Id, Date = date, DurationMinutes = 400 + random.Next(-30, 40), Source = DataSource.Manual, CreatedAtUtc = now });
                    db.RecoveryMetrics.Add(new RecoveryMetric { AthleteUserId = jakub.Id, Date = date, RestingHeartRateBpm = 48 + random.Next(-3, 4), ReadinessScore = 65 + random.Next(0, 30), Source = DataSource.Manual, CreatedAtUtc = now });
                    db.HrvMeasurements.Add(new HrvMeasurement { AthleteUserId = jakub.Id, Date = date, RmssdMs = 58 + random.Next(-10, 10), Source = DataSource.Manual, CreatedAtUtc = now });

                    db.FoodEntries.Add(new FoodEntry { AthleteUserId = jakub.Id, ConsumedAtUtc = date.ToDateTime(new TimeOnly(7, 30)), MealType = MealType.Breakfast, Description = "Ovesná kaše s banánem a ořechy", EstimatedCarbsGrams = 65, EstimatedProteinGrams = 15, CreatedAtUtc = now });
                    db.HydrationEntries.Add(new HydrationEntry { AthleteUserId = jakub.Id, ConsumedAtUtc = date.ToDateTime(new TimeOnly(8, 0)), DrinkType = HydrationDrinkType.Water, VolumeMilliliters = 500, CreatedAtUtc = now });
                }
            }

            db.Comments.Add(new Comment { PlannedWorkoutId = wedWorkout.Id, AuthorUserId = coach.Id, AuthorRole = CommentAuthorRole.Coach, Text = "Nezapomeň na kvalitní rozklus, ať jsou intervaly technicky čisté.", CreatedAtUtc = now });
            if (weekStart.ToDateTime(TimeOnly.MinValue) <= now)
            {
                db.Comments.Add(new Comment { PlannedWorkoutId = wedWorkout.Id, AuthorUserId = jakub.Id, AuthorRole = CommentAuthorRole.Athlete, Text = "Dneska to šlo lehčeji než minule, tempo jsem udržel.", CreatedAtUtc = now });
            }
        }

        db.PainOrHealthFlags.Add(new PainOrHealthFlag
        {
            AthleteUserId = jakub.Id,
            Type = HealthFlagType.Pain,
            Severity = HealthFlagSeverity.Mild,
            Status = HealthFlagStatus.Improving,
            BodyPart = "Levé lýtko",
            Description = "Mírné napětí po posledním dlouhém běhu, ustupuje.",
            StartedOnDate = today.AddDays(-4),
            CreatedAtUtc = now,
        });

        db.IntegrationConnections.Add(new IntegrationConnection
        {
            AthleteUserId = tereza.Id,
            Provider = IntegrationProviderType.GarminDemoProvider,
            Status = IntegrationConnectionStatus.NotConnected,
            CreatedAtUtc = now,
        });

        await db.SaveChangesAsync();
    }

    private static async Task<UserProfile> CreateUserAsync(UserManager<ApplicationUser> userManager, TrainCoachDbContext db, string email, string firstName, string lastName, AppRole role)
    {
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = email, Email = email, EmailConfirmed = true };
        var result = await userManager.CreateAsync(user, Password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Nepodařilo se vytvořit demo účet {email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
        await userManager.AddToRoleAsync(user, role.ToString());

        var profile = new UserProfile
        {
            Id = user.Id,
            FirstName = firstName,
            LastName = lastName,
            PrimaryRole = role,
            IsOnboarded = true,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.UserProfiles.Add(profile);

        if (role == AppRole.Athlete)
        {
            db.AthleteProfiles.Add(new AthleteProfile { UserProfileId = profile.Id, RestingHeartRateBpm = 48, MaxHeartRateBpm = 195, PrimarySport = "Běh", CreatedAtUtc = DateTime.UtcNow });
        }
        else
        {
            db.CoachProfiles.Add(new CoachProfile { UserProfileId = profile.Id, Bio = "Trenérka vytrvalostních sportů se zaměřením na běh do 42 km.", YearsOfExperience = 8, CreatedAtUtc = DateTime.UtcNow });
        }

        await db.SaveChangesAsync();
        return profile;
    }

    private static async Task<CoachAthleteRelationship> CreateActiveRelationshipAsync(TrainCoachDbContext db, UserProfile coach, UserProfile athlete, DateTime now)
    {
        var relationship = new CoachAthleteRelationship
        {
            CoachUserId = coach.Id,
            AthleteUserId = athlete.Id,
            Status = RelationshipStatus.Active,
            InvitedByUserId = coach.Id,
            InvitedAtUtc = now.AddDays(-30),
            RespondedAtUtc = now.AddDays(-29),
            StartDateUtc = now.AddDays(-29),
            CreatedAtUtc = now.AddDays(-30),
        };
        db.CoachAthleteRelationships.Add(relationship);
        await db.SaveChangesAsync();

        foreach (var scope in new[] { PermissionScope.ViewTrainingPlan, PermissionScope.EditTrainingPlan, PermissionScope.ViewCompletedActivities, PermissionScope.ViewWellness, PermissionScope.CommentOnWorkouts, PermissionScope.ViewHealthFlags, PermissionScope.ViewPersonalRecords })
        {
            db.RelationshipPermissions.Add(new RelationshipPermission { RelationshipId = relationship.Id, Scope = scope, GrantedByUserId = athlete.Id, GrantedAtUtc = now.AddDays(-29) });
        }
        await db.SaveChangesAsync();
        return relationship;
    }

    private static CustomAbbreviation Abbrev(UserProfile coach, string abbr, string full) => new()
    {
        CoachUserId = coach.Id,
        Abbreviation = abbr,
        FullText = full,
        CreatedAtUtc = DateTime.UtcNow,
    };

    private static HeartRateZone HrZone(UserProfile athlete, int number, string name, int min, int max, DateOnly effectiveFrom) => new()
    {
        AthleteUserId = athlete.Id,
        ZoneNumber = number,
        Name = name,
        MinBpm = min,
        MaxBpm = max,
        EffectiveFromDate = effectiveFrom.AddDays(-60),
        CreatedAtUtc = DateTime.UtcNow,
    };

    private static PlannedWorkout NewWorkout(TrainingWeek week, DateOnly date, SportType sport, string title, string description, decimal? distanceMeters, int? durationSeconds, DateTime now, bool isRest = false) => new()
    {
        TrainingWeekId = week.Id,
        Date = date,
        Sport = sport,
        Title = title,
        CoachDescription = description,
        IsRestDay = isRest,
        PlannedDistanceMeters = distanceMeters,
        PlannedDurationSeconds = durationSeconds,
        CreatedAtUtc = now,
    };

    private static void AddCompletedActivity(TrainCoachDbContext db, UserProfile athlete, PlannedWorkout workout, DateOnly date, decimal distanceMeters, int durationSeconds, int avgHr, Random random, DateTime now)
    {
        db.CompletedActivities.Add(new CompletedActivity
        {
            AthleteUserId = athlete.Id,
            PlannedWorkoutId = workout.Id,
            Sport = workout.Sport,
            Title = workout.Title,
            StartedAtUtc = date.ToDateTime(new TimeOnly(7, 0)),
            DurationSeconds = durationSeconds + random.Next(-60, 90),
            DistanceMeters = distanceMeters + random.Next(-200, 300),
            ElevationGainMeters = 40 + random.Next(0, 60),
            AverageHeartRateBpm = avgHr + random.Next(-4, 5),
            MaxHeartRateBpm = avgHr + 25 + random.Next(0, 10),
            CreatedAtUtc = now,
            Provenance = new DataProvenance { Source = DataSource.Manual, FetchedAtUtc = now },
        });
    }

    private static DailyCheckIn MorningCheckIn(UserProfile athlete, DateOnly date, Random random, DateTime now) => new()
    {
        AthleteUserId = athlete.Id,
        Date = date,
        Type = CheckInType.Morning,
        Energy = (WellnessScale)(3 + random.Next(0, 3)),
        Fatigue = (WellnessScale)(2 + random.Next(0, 3)),
        LegsFeeling = (WellnessScale)(3 + random.Next(0, 3)),
        Stress = (WellnessScale)(2 + random.Next(0, 3)),
        SleepQuality = (WellnessScale)(3 + random.Next(0, 3)),
        MuscleSoreness = (WellnessScale)(2 + random.Next(0, 3)),
        Motivation = (WellnessScale)(3 + random.Next(0, 3)),
        HasPainOrIllness = false,
        CreatedAtUtc = now,
    };

    private static DailyCheckIn EveningCheckIn(UserProfile athlete, DateOnly date, Random random, DateTime now) => new()
    {
        AthleteUserId = athlete.Id,
        Date = date,
        Type = CheckInType.Evening,
        Energy = (WellnessScale)(3 + random.Next(0, 3)),
        Fatigue = (WellnessScale)(2 + random.Next(0, 3)),
        LegsFeeling = (WellnessScale)(3 + random.Next(0, 3)),
        Stress = (WellnessScale)(2 + random.Next(0, 3)),
        HydrationLiters = 2 + (decimal)random.NextDouble(),
        MealQuality = (WellnessScale)(3 + random.Next(0, 3)),
        CompletedPlannedWorkout = true,
        Rpe = 4 + random.Next(0, 5),
        HasPainOrIllness = false,
        CreatedAtUtc = now,
    };
}
