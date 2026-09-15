using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace TrainCoach.Api.IntegrationTests.Infrastructure;

public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected TrainCoachApiFactory Factory { get; private set; } = null!;
    protected HttpClient Client { get; private set; } = null!;

    protected static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public virtual async Task InitializeAsync()
    {
        Factory = new TrainCoachApiFactory();
        await Factory.InitializeDatabaseAsync();
        Client = Factory.CreateClient();
    }

    public virtual Task DisposeAsync()
    {
        Client.Dispose();
        Factory.Dispose();
        return Task.CompletedTask;
    }

    protected record AuthResultDto(
        string UserId, string Email, string DisplayName, string Role, bool IsOnboarded,
        string AccessToken, DateTime AccessTokenExpiresAtUtc, string RefreshToken, DateTime RefreshTokenExpiresAtUtc);

    protected async Task<AuthResultDto> RegisterAsync(string email, string role, string password = "Heslo123", string firstName = "Test", string lastName = "User")
    {
        var response = await Client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password,
            firstName,
            lastName,
            role,
            timeZoneId = "Europe/Prague",
            locale = "cs-CZ",
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResultDto>(JsonOptions))!;
    }

    protected HttpClient AuthenticatedClient(AuthResultDto auth)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }
}
