using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using TrainCoach.Api.IntegrationTests.Infrastructure;

namespace TrainCoach.Api.IntegrationTests.Scenarios;

public class AuthTests : IntegrationTestBase
{
    [Fact]
    public async Task Register_ThenLogin_Succeeds()
    {
        var email = $"athlete-{Guid.NewGuid():N}@example.com";
        var registerResult = await RegisterAsync(email, "Athlete");
        registerResult.AccessToken.Should().NotBeNullOrEmpty();
        registerResult.Role.Should().Be("Athlete");

        var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", new { email, password = "Heslo123" });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsConflict()
    {
        var email = $"dup-{Guid.NewGuid():N}@example.com";
        await RegisterAsync(email, "Athlete");

        var response = await Client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Heslo123",
            firstName = "Another",
            lastName = "Person",
            role = "Athlete",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        var email = $"wrongpw-{Guid.NewGuid():N}@example.com";
        await RegisterAsync(email, "Athlete");

        var response = await Client.PostAsJsonAsync("/api/auth/login", new { email, password = "NotTheRightOne1" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Register_WithWeakPassword_ReturnsBadRequest()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/register", new
        {
            email = $"weak-{Guid.NewGuid():N}@example.com",
            password = "weak",
            firstName = "Weak",
            lastName = "Password",
            role = "Athlete",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_ReturnsUnauthorized()
    {
        var response = await Client.GetAsync("/api/relationships");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_RotatesToken_AndOldTokenNoLongerWorks()
    {
        var email = $"refresh-{Guid.NewGuid():N}@example.com";
        var auth = await RegisterAsync(email, "Athlete");

        var refreshResponse = await Client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = auth.RefreshToken });
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var newAuth = await refreshResponse.Content.ReadFromJsonAsync<AuthResultDto>(JsonOptions);
        newAuth!.RefreshToken.Should().NotBe(auth.RefreshToken);

        var reuseOldToken = await Client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = auth.RefreshToken });
        reuseOldToken.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
