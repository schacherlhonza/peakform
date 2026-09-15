namespace TrainCoach.Domain.Enums;

public enum AppRole
{
    Athlete = 1,
    Coach = 2,
}

public enum RelationshipStatus
{
    PendingInvite = 1,
    Active = 2,
    Revoked = 3,
    Ended = 4,
    Declined = 5,
}

/// <summary>
/// One row per granted scope on a <see cref="TrainCoach.Domain.Identity.CoachAthleteRelationship"/>.
/// Modeled as a claims-style table so new scopes can be added without a schema change.
/// </summary>
public enum PermissionScope
{
    ViewTrainingPlan = 1,
    EditTrainingPlan = 2,
    ViewCompletedActivities = 3,
    ViewWellness = 4,
    ViewNutrition = 5,
    ViewHealthFlags = 6,
    CommentOnWorkouts = 7,
    ViewPersonalRecords = 8,
}

public enum AuditAction
{
    Created = 1,
    Updated = 2,
    Deleted = 3,
    AccessGranted = 4,
    AccessRevoked = 5,
    LoggedIn = 6,
    PasswordChanged = 7,
    DataExported = 8,
    AccountDeleted = 9,
}
