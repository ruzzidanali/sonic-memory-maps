
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using SonicMemoryMaps.IntegrationTests.Factory;

namespace SonicMemoryMaps.IntegrationTests.AudioUploads;

public sealed class AudioUploadsApiTests(
    SonicMemoryMapsApiFactory factory)
    : IClassFixture<SonicMemoryMapsApiFactory>
{
    [Fact]
    public async Task CreateUploadAuthorization_WithoutToken_ReturnsUnauthorized()
    {
        var initialCallCount =
            factory.AudioStorageService.CreateAuthorizationCallCount;

        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/audio-uploads/authorization",
            new { requestedAudioContentType = "audio/mp4" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        Assert.Equal(
            initialCallCount,
            factory.AudioStorageService.CreateAuthorizationCallCount);
    }

    [Fact]
    public async Task CreateUploadAuthorization_WithValidRequest_ReturnsAuthorization()
    {
        var userId = Guid.NewGuid();

        var initialCallCount =
            factory.AudioStorageService.CreateAuthorizationCallCount;

        using var client = factory.CreateClient();

        using var request = CreateAuthenticatedRequest(userId);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content
            .ReadFromJsonAsync<AudioUploadResponse>();

        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.MemoryId);

        Assert.Equal(
            $"{userId:D}/{result.MemoryId:D}.m4a",
            result.ObjectPath);

        Assert.Contains("signed", result.UploadUrl);

        Assert.True(result.ExpiresAt > DateTimeOffset.UtcNow);

        Assert.Equal(
            initialCallCount + 1,
            factory.AudioStorageService.CreateAuthorizationCallCount);

        Assert.Equal(
            userId,
            factory.AudioStorageService.LastUserId);

        Assert.Equal(
            result.MemoryId,
            factory.AudioStorageService.LastMemoryId);

        Assert.Equal(
            "audio/mp4",
            factory.AudioStorageService.LastContentType);
    }

    [Theory]
    [InlineData("audio/flac")]
    [InlineData("application/json")]
    [InlineData("video/mp4")]
    public async Task CreateUploadAuthorization_WithUnsupportedContentType_ReturnsBadRequest(
        string contentType)
    {
        var userId = Guid.NewGuid();

        var initialCallCount =
            factory.AudioStorageService.CreateAuthorizationCallCount;

        using var client = factory.CreateClient();

        using var request = CreateAuthenticatedRequest(
            userId,
            contentType);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        Assert.Equal(
            initialCallCount,
            factory.AudioStorageService.CreateAuthorizationCallCount);
    }

    private HttpRequestMessage CreateAuthenticatedRequest(
        Guid userId,
        string contentType = "audio/mp4")
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/audio-uploads/authorization");

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                CreateToken(userId));

        request.Content = JsonContent.Create(new
        {
            requestedAudioContentType = contentType
        });

        return request;
    }

    private string CreateToken(Guid userId)
    {
        var now = DateTime.UtcNow;

        var claims = new[]
        {
            new Claim(
                JwtRegisteredClaimNames.Sub,
                userId.ToString()),

            new Claim(
                JwtRegisteredClaimNames.Email,
                "testing@example.com")
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

    private sealed record AudioUploadResponse(
        Guid MemoryId,
        string ObjectPath,
        string UploadUrl,
        DateTimeOffset ExpiresAt);
}
