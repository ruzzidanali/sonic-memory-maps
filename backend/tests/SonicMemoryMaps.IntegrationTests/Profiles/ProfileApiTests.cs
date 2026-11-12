
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using SonicMemoryMaps.Domain.Entities;
using SonicMemoryMaps.IntegrationTests.Factory;
using Xunit;

namespace SonicMemoryMaps.IntegrationTests.Profiles;

public sealed class ProfileApiTests(
    SonicMemoryMapsApiFactory factory)
    : IClassFixture<SonicMemoryMapsApiFactory>
{
    [Fact]
    public async Task GetMyProfile_WithoutToken_ReturnsUnauthorized()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/profiles/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMyProfile_WhenProfileExists_ReturnsProfile()
    {
        var userId = Guid.NewGuid();

        factory.ProfileRepository.SeedProfile(CreateProfile(
            userId, "ruzzidan"));

        using var client = factory.CreateClient();
        using var request = CreateAuthenticatedRequest(
            HttpMethod.Get, "/api/profiles/me", userId);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var profile = await response.Content
            .ReadFromJsonAsync<ProfileResponse>();

        Assert.NotNull(profile);
        Assert.Equal(userId, profile.Id);
        Assert.Equal("ruzzidan", profile.Username);
        Assert.Equal("Ruzzidan", profile.DisplayName);
    }

    [Fact]
    public async Task GetMyProfile_WhenProfileDoesNotExist_ReturnsNotFound()
    {
        var userId = Guid.NewGuid();

        using var client = factory.CreateClient();
        using var request = CreateAuthenticatedRequest(
            HttpMethod.Get, "/api/profiles/me", userId);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateMyProfile_WithValidData_ReturnsUpdatedProfile()
    {
        var userId = Guid.NewGuid();

        factory.ProfileRepository.SeedProfile(
            CreateProfile(userId, "old_username"));

        using var client = factory.CreateClient();
        using var request = CreateAuthenticatedRequest(
            HttpMethod.Put, "/api/profiles/me", userId);

        request.Content = JsonContent.Create(new
        {
            username = "new_username",
            displayName = "New Display Name",
            bio = "Learning .NET and Flutter."
        });

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var profile = await response.Content
            .ReadFromJsonAsync<ProfileResponse>();

        Assert.NotNull(profile);
        Assert.Equal(userId, profile.Id);
        Assert.Equal("new_username", profile.Username);
        Assert.Equal("New Display Name", profile.DisplayName);
        Assert.Equal("Learning .NET and Flutter.", profile.Bio);
    }

    [Fact]
    public async Task UpdateMyProfile_WithExistingUsername_ReturnsConflict()
    {
        var userId = Guid.NewGuid();
        var anotherUserId = Guid.NewGuid();

        factory.ProfileRepository.SeedProfile(
            CreateProfile(userId, "original_user"));

        factory.ProfileRepository.SeedProfile(
            CreateProfile(anotherUserId, "taken_username"));

        using var client = factory.CreateClient();
        using var request = CreateAuthenticatedRequest(
            HttpMethod.Put, "/api/profiles/me", userId);

        request.Content = JsonContent.Create(new
        {
            username = "taken_username",
            displayName = "Ruzzidan",
            bio = "Test"
        });

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task UpdateMyProfile_WithShortUsername_ReturnsBadRequest()
    {
        var userId = Guid.NewGuid();

        factory.ProfileRepository.SeedProfile(
            CreateProfile(userId, "valid_username"));

        using var client = factory.CreateClient();
        using var request = CreateAuthenticatedRequest(
            HttpMethod.Put, "/api/profiles/me", userId);

        request.Content = JsonContent.Create(new
        {
            username = "ab",
            displayName = "Ruzzidan",
            bio = "Test"
        });

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateMyProfile_WithOverlongDisplayName_ReturnsBadRequest()
    {
        var userId = Guid.NewGuid();

        factory.ProfileRepository.SeedProfile(
            CreateProfile(userId, "valid_username"));

        using var client = factory.CreateClient();
        using var request = CreateAuthenticatedRequest(
            HttpMethod.Put, "/api/profiles/me", userId);

        request.Content = JsonContent.Create(new
        {
            username = "valid_username",
            displayName = new string('a', 101),
            bio = "Test"
        });

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static Profile CreateProfile(Guid userId, string username)
    {
        var now = DateTimeOffset.UtcNow;

        return new Profile
        {
            Id = userId,
            Username = username,
            DisplayName = "Ruzzidan",
            AvatarUrl = null,
            Bio = "Test profile",
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private HttpRequestMessage CreateAuthenticatedRequest(
        HttpMethod method,
        string url,
        Guid userId)
    {
        var request = new HttpRequestMessage(method, url);

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer", CreateToken(userId));

        return request;
    }

    private string CreateToken(Guid userId)
    {
        var now = DateTime.UtcNow;

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, "testing@example.com")
        };

        var credentials = new SigningCredentials(
            factory.SigningKey,
            SecurityAlgorithms.EcdsaSha256);

        var token = new JwtSecurityToken(
            issuer: factory.TestIssuer,
            audience: factory.TestAudience,
            claims: claims,
            notBefore: now.AddMinutes(-1),
            expires: now.AddMinutes(10),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed record ProfileResponse(
        Guid Id,
        string Username,
        string? DisplayName,
        string? AvatarUrl,
        string? Bio,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);
}
