using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Common;

/// <summary>Implemented in the Api layer from the current HTTP request's JWT claims.</summary>
public interface ICurrentUserService
{
    bool IsAuthenticated { get; }
    Guid UserId { get; }
    AppRole Role { get; }
}
