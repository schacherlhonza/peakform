namespace TrainCoach.Application.Planning;

public record CustomAbbreviationDto(
    Guid Id,
    Guid CoachUserId,
    string Abbreviation,
    string FullText,
    string? Description);

public record CreateCustomAbbreviationRequest(
    string Abbreviation,
    string FullText,
    string? Description);

public record UpdateCustomAbbreviationRequest(
    string Abbreviation,
    string FullText,
    string? Description);
