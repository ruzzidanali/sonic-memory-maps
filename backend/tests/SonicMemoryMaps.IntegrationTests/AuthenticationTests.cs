
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using SonicMemoryMaps.IntegrationTests.Factory;

namespace SonicMemoryMaps.IntegrationTests;

public sealed class AuthenticationTests
    : IClassFixture<SonicMemoryMapsApiFactory>
{
    private readonly HttpClient _client;
    private readonly SonicMemoryMapsApiFactory _factory;

    public AuthenticationTests(SonicMemoryMapsApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_WithoutAuthentication_ReturnsOk()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AuthMe_WithMalformedToken_ReturnsUnauthorized()
    {
        var response = await SendAuthenticatedRequestAsync(
            "this-is-not-a-valid-jwt");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task AuthMe_WithValidEs256Token_ReturnsUserIdentity()
    {
        // Arrange
        var expectedUserId = Guid.NewGuid();
        const string expectedEmail = "test@example.com";

        var token = CreateToken(
            _factory.TestIssuer,
            _factory.TestAudience,
            DateTime.UtcNow.AddMinutes(5),
            expectedUserId,
            expectedEmail);

        // Act
        var response = await SendAuthenticatedRequestAsync(token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content
            .ReadFromJsonAsync<AuthMeResponse>();

        Assert.NotNull(result);
        Assert.Equal(expectedUserId, result.UserId);
        Assert.Equal(expectedEmail, result.Email);
        Assert.True(result.IsAuthenticated);
    }

    [Fact]
    public async Task AuthMe_WithInvalidIssuer_ReturnsUnauthorized()
    {
        // Arrange
        var token = CreateToken(
            "https://attacker.example.com/auth/v1",
            _factory.TestAudience,
            DateTime.UtcNow.AddMinutes(5));

        // Act
        var response = await SendAuthenticatedRequestAsync(token);

        // Assert
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task AuthMe_WithWrongAudience_ReturnsUnauthorized()
    {
        // Arrange
        var token = CreateToken(
            _factory.TestIssuer,
            "wrong-audience",
            DateTime.UtcNow.AddMinutes(5));

        // Act
        var response = await SendAuthenticatedRequestAsync(token);

        // Assert
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task AuthMe_WithExpiredToken_ReturnsUnauthorized()
    {
        // Arrange
        var token = CreateToken(
            _factory.TestIssuer,
            _factory.TestAudience,
            DateTime.UtcNow.AddMinutes(-5));

        // Act
        var response = await SendAuthenticatedRequestAsync(token);

        // Assert
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task DatabaseHealth_WithoutToken_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/database");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task AuthMe_WithoutToken_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/auth/me");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    private string CreateToken(
        string issuer,
        string audience,
        DateTime expires,
        Guid? userId = null,
        string email = "test@example.com")
    {
        var claims = new[]
        {
            new Claim(
                "sub",
                (userId ?? Guid.NewGuid()).ToString()),

            new Claim("email", email),

            new Claim("role", "authenticated")
        };

        var credentials = new SigningCredentials(
            _factory.SigningKey,
            SecurityAlgorithms.EcdsaSha256);

        var jwt = new JwtSecurityToken(
    issuer: issuer,
    audience: audience,
    claims: claims,
    notBefore: expires.AddMinutes(-10),
    expires: expires,
    signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    private async Task<HttpResponseMessage> SendAuthenticatedRequestAsync(
        string token)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/auth/me");

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        return await _client.SendAsync(request);
    }

    private sealed record AuthMeResponse(
        Guid? UserId,
        string? Email,
        bool IsAuthenticated);
}
