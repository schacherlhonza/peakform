using FluentAssertions;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Identity;
using Xunit;

namespace TrainCoach.Domain.Tests.Identity;

public class RelationshipPermissionTests
{
    [Fact]
    public void IsActive_WhenNeverRevoked_IsTrue()
    {
        var permission = new RelationshipPermission { Scope = PermissionScope.ViewWellness };

        permission.IsActive.Should().BeTrue();
    }

    [Fact]
    public void IsActive_AfterRevocation_IsFalse()
    {
        var permission = new RelationshipPermission
        {
            Scope = PermissionScope.ViewWellness,
            RevokedAtUtc = DateTime.UtcNow,
        };

        permission.IsActive.Should().BeFalse();
    }
}
