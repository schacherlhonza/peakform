using FluentAssertions;
using TrainCoach.Domain.Identity;
using Xunit;

namespace TrainCoach.Domain.Tests.Identity;

public class RefreshTokenTests
{
    [Fact]
    public void IsActive_WhenNotRevokedAndNotExpired_IsTrue()
    {
        var token = new RefreshToken { ExpiresAtUtc = DateTime.UtcNow.AddDays(1) };

        token.IsActive.Should().BeTrue();
    }

    [Fact]
    public void IsActive_WhenExpired_IsFalse()
    {
        var token = new RefreshToken { ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1) };

        token.IsActive.Should().BeFalse();
    }

    [Fact]
    public void IsActive_WhenRevoked_IsFalse()
    {
        var token = new RefreshToken { ExpiresAtUtc = DateTime.UtcNow.AddDays(1), RevokedAtUtc = DateTime.UtcNow };

        token.IsActive.Should().BeFalse();
    }
}
