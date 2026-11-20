
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using SonicMemoryMaps.Application.Abstractions;
using SonicMemoryMaps.Domain.Enums;
using SonicMemoryMaps.IntegrationTests.Factory;
using NetTopologySuite.Geometries;
using SonicMemoryMaps.Domain.Entities;
using Xunit;

namespace SonicMemoryMaps.IntegrationTests.Memories;

public sealed class MemoriesApiTests(
    SonicMemoryMapsApiFactory factory)
    : IClassFixture<SonicMemoryMapsApiFactory>
{
    [Fact]
    public async Task CreateMemory_WithoutToken_ReturnsUnauthorized()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/memories",
            CreateRequest(Guid.NewGuid()));

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task CreateMemory_WithValidData_ReturnsCreatedMemory()
    {
        var userId = Guid.NewGuid();
        var memoryId = Guid.NewGuid();

        factory.AudioStorageService.Metadata = CreateMetadata(
            userId,
            memoryId);

        var initialAddCount = factory.MemoryRepository.AddCallCount;

        using var client = factory.CreateClient();
        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post,
            "/api/memories",
            userId);

        request.Content = JsonContent.Create(CreateRequest(memoryId));

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var memory = await response.Content
            .ReadFromJsonAsync<MemoryResponse>();

        Assert.NotNull(memory);
        Assert.Equal(memoryId, memory.Id);
        Assert.Equal(userId, memory.UserId);
        Assert.Equal("A memory near the lake", memory.Title);
        Assert.Equal("A test audio memory.", memory.Description);
        Assert.Equal(120, memory.AudioDurationSeconds);
        Assert.Equal("audio/mp4", memory.AudioContentType);
        Assert.Equal(1024, memory.AudioSizeBytes);
        Assert.Equal(MemoryVisibility.Public, memory.Visibility);
        Assert.Equal(50, memory.NotificationRadiusMeters);
        Assert.Equal(ModerationStatus.Pending, memory.ModerationStatus);

        Assert.Equal(initialAddCount + 1, factory.MemoryRepository.AddCallCount);

        var savedMemory = await factory.MemoryRepository
            .GetByIdForOwnerAsync(memoryId, userId);

        Assert.NotNull(savedMemory);
        Assert.Equal(userId, savedMemory.UserId);
        Assert.Equal(memoryId, savedMemory.Id);
        Assert.Equal(ModerationStatus.Pending, savedMemory.ModerationStatus);

        Assert.Equal(3.1390, savedMemory.Location.Y, 4);
        Assert.Equal(101.6869, savedMemory.Location.X, 4);
        Assert.Equal(4326, savedMemory.Location.SRID);
    }

    [Fact]
    public async Task CreateMemory_WhenAudioDoesNotExist_ReturnsNotFound()
    {
        var userId = Guid.NewGuid();
        var memoryId = Guid.NewGuid();

        factory.AudioStorageService.Metadata = null;

        var initialAddCount = factory.MemoryRepository.AddCallCount;

        using var client = factory.CreateClient();
        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post,
            "/api/memories",
            userId);

        request.Content = JsonContent.Create(CreateRequest(memoryId));

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(initialAddCount, factory.MemoryRepository.AddCallCount);
    }

    [Fact]
    public async Task CreateMemory_WhenAudioExceedsSizeLimit_ReturnsBadRequest()
    {
        var userId = Guid.NewGuid();
        var memoryId = Guid.NewGuid();

        factory.AudioStorageService.Metadata = CreateMetadata(
            userId,
            memoryId,
            sizeBytes: 52_428_801);

        var initialAddCount = factory.MemoryRepository.AddCallCount;

        using var client = factory.CreateClient();
        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post,
            "/api/memories",
            userId);

        request.Content = JsonContent.Create(CreateRequest(memoryId));

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(initialAddCount, factory.MemoryRepository.AddCallCount);
    }

    [Fact]
    public async Task CreateMemory_WhenAudioContentTypeDoesNotMatch_ReturnsBadRequest()
    {
        var userId = Guid.NewGuid();
        var memoryId = Guid.NewGuid();

        factory.AudioStorageService.Metadata = CreateMetadata(
            userId,
            memoryId,
            contentType: "audio/mpeg");

        var initialAddCount = factory.MemoryRepository.AddCallCount;

        using var client = factory.CreateClient();
        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post,
            "/api/memories",
            userId);

        request.Content = JsonContent.Create(CreateRequest(memoryId));

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(initialAddCount, factory.MemoryRepository.AddCallCount);
    }

    [Fact]
    public async Task CreateMemory_WhenAudioObjectPathIsInvalid_ReturnsBadRequest()
    {
        var userId = Guid.NewGuid();
        var memoryId = Guid.NewGuid();

        factory.AudioStorageService.Metadata = new AudioObjectMetadata(
            $"another-user/{memoryId:D}.m4a",
            "audio/mp4",
            1024);

        var initialAddCount = factory.MemoryRepository.AddCallCount;

        using var client = factory.CreateClient();
        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post,
            "/api/memories",
            userId);

        request.Content = JsonContent.Create(CreateRequest(memoryId));

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(initialAddCount, factory.MemoryRepository.AddCallCount);
    }


    [Fact]
    public async Task GetMyMemories_WithoutToken_ReturnsUnauthorized()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/memories/me");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task GetMyMemories_WhenMemoriesExist_ReturnsOnlyOwnMemories()
    {
        var userId = Guid.NewGuid();
        var anotherUserId = Guid.NewGuid();

        var ownMemory = CreateMemory(
            userId,
            "My memory",
            DateTimeOffset.UtcNow.AddMinutes(-5));

        var anotherMemory = CreateMemory(
            anotherUserId,
            "Another user's memory",
            DateTimeOffset.UtcNow);

        factory.MemoryRepository.SeedMemory(ownMemory);
        factory.MemoryRepository.SeedMemory(anotherMemory);

        using var client = factory.CreateClient();
        using var request = CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/memories/me",
            userId);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var memories = await response.Content
            .ReadFromJsonAsync<List<MemoryResponse>>();

        Assert.NotNull(memories);
        var memory = Assert.Single(memories);

        Assert.Equal(ownMemory.Id, memory.Id);
        Assert.Equal(userId, memory.UserId);
        Assert.Equal("My memory", memory.Title);
    }

    [Fact]
    public async Task GetMyMemories_WhenDeletedMemoryExists_ExcludesDeletedMemory()
    {
        var userId = Guid.NewGuid();

        var activeMemory = CreateMemory(
            userId,
            "Active memory",
            DateTimeOffset.UtcNow);

        var deletedMemory = CreateMemory(
            userId,
            "Deleted memory",
            DateTimeOffset.UtcNow.AddMinutes(-5));

        deletedMemory.DeletedAt = DateTimeOffset.UtcNow;

        factory.MemoryRepository.SeedMemory(activeMemory);
        factory.MemoryRepository.SeedMemory(deletedMemory);

        using var client = factory.CreateClient();
        using var request = CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/memories/me",
            userId);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var memories = await response.Content
            .ReadFromJsonAsync<List<MemoryResponse>>();

        Assert.NotNull(memories);

        var memory = Assert.Single(memories);

        Assert.Equal(activeMemory.Id, memory.Id);
        Assert.Equal("Active memory", memory.Title);
    }

    [Fact]
    public async Task GetMyMemories_WhenUserHasNoMemories_ReturnsEmptyList()
    {
        var userId = Guid.NewGuid();

        using var client = factory.CreateClient();
        using var request = CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/memories/me",
            userId);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var memories = await response.Content
            .ReadFromJsonAsync<List<MemoryResponse>>();

        Assert.NotNull(memories);
        Assert.Empty(memories);
    }

    [Fact]
    public async Task GetMyMemories_ReturnsNewestMemoriesFirst()
    {
        var userId = Guid.NewGuid();

        var olderMemory = CreateMemory(
            userId,
            "Older memory",
            DateTimeOffset.UtcNow.AddDays(-2));

        var newerMemory = CreateMemory(
            userId,
            "Newer memory",
            DateTimeOffset.UtcNow);

        factory.MemoryRepository.SeedMemory(olderMemory);
        factory.MemoryRepository.SeedMemory(newerMemory);

        using var client = factory.CreateClient();
        using var request = CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/memories/me",
            userId);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var memories = await response.Content
            .ReadFromJsonAsync<List<MemoryResponse>>();

        Assert.NotNull(memories);
        Assert.Equal(2, memories.Count);

        Assert.Equal(newerMemory.Id, memories[0].Id);
        Assert.Equal(olderMemory.Id, memories[1].Id);
    }


    [Fact]
    public async Task GetMemoryById_WithoutToken_ReturnsUnauthorized()
    {
        var memoryId = Guid.NewGuid();

        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/api/memories/{memoryId}");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task GetMemoryById_WhenMemoryBelongsToUser_ReturnsMemory()
    {
        var userId = Guid.NewGuid();

        var memory = CreateMemory(
            userId,
            "My memory",
            DateTimeOffset.UtcNow);

        factory.MemoryRepository.SeedMemory(memory);

        using var client = factory.CreateClient();
        using var request = CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/memories/{memory.Id}",
            userId);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content
            .ReadFromJsonAsync<MemoryResponse>();

        Assert.NotNull(result);
        Assert.Equal(memory.Id, result.Id);
        Assert.Equal(userId, result.UserId);
        Assert.Equal("My memory", result.Title);
    }

    [Fact]
    public async Task GetMemoryById_WhenMemoryBelongsToAnotherUser_ReturnsNotFound()
    {
        var ownerId = Guid.NewGuid();
        var anotherUserId = Guid.NewGuid();

        var memory = CreateMemory(
            ownerId,
            "Private memory",
            DateTimeOffset.UtcNow);

        memory.Visibility = MemoryVisibility.Private;

        factory.MemoryRepository.SeedMemory(memory);

        using var client = factory.CreateClient();
        using var request = CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/memories/{memory.Id}",
            anotherUserId);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetMemoryById_WhenMemoryDoesNotExist_ReturnsNotFound()
    {
        var userId = Guid.NewGuid();
        var memoryId = Guid.NewGuid();

        using var client = factory.CreateClient();
        using var request = CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/memories/{memoryId}",
            userId);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetMemoryById_WhenMemoryIsDeleted_ReturnsNotFound()
    {
        var userId = Guid.NewGuid();

        var memory = CreateMemory(
            userId,
            "Deleted memory",
            DateTimeOffset.UtcNow);

        memory.DeletedAt = DateTimeOffset.UtcNow;

        factory.MemoryRepository.SeedMemory(memory);

        using var client = factory.CreateClient();
        using var request = CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/memories/{memory.Id}",
            userId);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }


    [Fact]
    public async Task GetMemoryPlayback_WithoutToken_ReturnsUnauthorized()
    {
        var memoryId = Guid.NewGuid();

        var initialRepositoryCallCount =
            factory.MemoryAccessRepository.GetCallCount;

        var initialDownloadCallCount =
            factory.AudioStorageService.CreateDownloadAuthorizationCallCount;

        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/api/memories/{memoryId}/audio");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        Assert.Equal(
            initialRepositoryCallCount,
            factory.MemoryAccessRepository.GetCallCount);

        Assert.Equal(
            initialDownloadCallCount,
            factory.AudioStorageService.CreateDownloadAuthorizationCallCount);
    }

    [Fact]
    public async Task GetMemoryPlayback_WhenAccessIsDenied_ReturnsNotFound()
    {
        var userId = Guid.NewGuid();
        var memoryId = Guid.NewGuid();

        factory.MemoryAccessRepository.MemoryToReturn = null;

        var initialDownloadCallCount =
            factory.AudioStorageService.CreateDownloadAuthorizationCallCount;

        using var client = factory.CreateClient();

        using var request = CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/memories/{memoryId}/audio",
            userId);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        Assert.Equal(
            initialDownloadCallCount,
            factory.AudioStorageService.CreateDownloadAuthorizationCallCount);

        Assert.Equal(memoryId, factory.MemoryAccessRepository.LastMemoryId);
        Assert.Equal(userId, factory.MemoryAccessRepository.LastRequestingUserId);
    }

    [Fact]
    public async Task GetMemoryPlayback_WithAuthorizedMemory_ReturnsPlaybackUrl()
    {
        var userId = Guid.NewGuid();

        var memory = CreateMemory(
            userId,
            "Memory for playback",
            DateTimeOffset.UtcNow);

        factory.MemoryAccessRepository.MemoryToReturn = memory;

        var initialDownloadCallCount =
            factory.AudioStorageService.CreateDownloadAuthorizationCallCount;

        using var client = factory.CreateClient();

        using var request = CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/memories/{memory.Id}/audio",
            userId);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content
            .ReadFromJsonAsync<MemoryPlaybackResponse>();

        Assert.NotNull(result);

        Assert.Equal(memory.Id, result.MemoryId);

        Assert.Equal(
            "https://test-project.supabase.co/storage/v1/object/sign/memory-audio/test-token",
            result.PlaybackUrl);

        Assert.True(result.ExpiresAt > DateTimeOffset.UtcNow);

        Assert.Equal(
            initialDownloadCallCount + 1,
            factory.AudioStorageService.CreateDownloadAuthorizationCallCount);

        Assert.Equal(userId, factory.AudioStorageService.LastDownloadUserId);
        Assert.Equal(memory.Id, factory.AudioStorageService.LastDownloadMemoryId);
    }




    [Fact]
    public async Task UpdateMemory_WithValidData_ReturnsUpdatedMemory()
    {
        var userId = Guid.NewGuid();

        var memory = CreateMemory(
            userId,
            "Original title",
            DateTimeOffset.UtcNow);

        factory.MemoryRepository.SeedMemory(memory);

        using var client = factory.CreateClient();
        using var request = CreateAuthenticatedRequest(
            HttpMethod.Put,
            $"/api/memories/{memory.Id}",
            userId);

        request.Content = JsonContent.Create(new
        {
            title = "Updated title",
            description = "Updated description",
            visibility = MemoryVisibility.FriendsOnly,
            notificationRadiusMeters = 100
        });

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content
            .ReadFromJsonAsync<MemoryResponse>();

        Assert.NotNull(result);
        Assert.Equal(memory.Id, result.Id);
        Assert.Equal(userId, result.UserId);
        Assert.Equal("Updated title", result.Title);
        Assert.Equal("Updated description", result.Description);
        Assert.Equal(MemoryVisibility.FriendsOnly, result.Visibility);
        Assert.Equal(100, result.NotificationRadiusMeters);
    }

    [Fact]
    public async Task UpdateMemory_WithoutToken_ReturnsUnauthorized()
    {
        var memoryId = Guid.NewGuid();

        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            $"/api/memories/{memoryId}",
            new
            {
                title = "Updated title",
                description = "Updated description",
                visibility = MemoryVisibility.Public,
                notificationRadiusMeters = 50
            });

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task UpdateMemory_WhenMemoryBelongsToAnotherUser_ReturnsNotFound()
    {
        var ownerId = Guid.NewGuid();
        var anotherUserId = Guid.NewGuid();

        var memory = CreateMemory(
            ownerId,
            "Original title",
            DateTimeOffset.UtcNow);

        factory.MemoryRepository.SeedMemory(memory);

        using var client = factory.CreateClient();
        using var request = CreateAuthenticatedRequest(
            HttpMethod.Put,
            $"/api/memories/{memory.Id}",
            anotherUserId);

        request.Content = JsonContent.Create(new
        {
            title = "Attempted update",
            description = "Unauthorized change",
            visibility = MemoryVisibility.Public,
            notificationRadiusMeters = 50
        });

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateMemory_WhenMemoryDoesNotExist_ReturnsNotFound()
    {
        var userId = Guid.NewGuid();
        var memoryId = Guid.NewGuid();

        using var client = factory.CreateClient();
        using var request = CreateAuthenticatedRequest(
            HttpMethod.Put,
            $"/api/memories/{memoryId}",
            userId);

        request.Content = JsonContent.Create(new
        {
            title = "Updated title",
            description = "Updated description",
            visibility = MemoryVisibility.Public,
            notificationRadiusMeters = 50
        });

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateMemory_WithInvalidTitle_ReturnsBadRequest()
    {
        var userId = Guid.NewGuid();

        var memory = CreateMemory(
            userId,
            "Original title",
            DateTimeOffset.UtcNow);

        factory.MemoryRepository.SeedMemory(memory);

        using var client = factory.CreateClient();
        using var request = CreateAuthenticatedRequest(
            HttpMethod.Put,
            $"/api/memories/{memory.Id}",
            userId);

        request.Content = JsonContent.Create(new
        {
            title = "",
            description = "Updated description",
            visibility = MemoryVisibility.Public,
            notificationRadiusMeters = 50
        });

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }


    [Fact]
    public async Task DeleteMemory_WithoutToken_ReturnsUnauthorized()
    {
        var memoryId = Guid.NewGuid();

        using var client = factory.CreateClient();

        var response = await client.DeleteAsync(
            $"/api/memories/{memoryId}");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task DeleteMemory_WhenMemoryBelongsToUser_ReturnsNoContent()
    {
        var userId = Guid.NewGuid();

        var memory = CreateMemory(
            userId,
            "Memory to delete",
            DateTimeOffset.UtcNow);

        factory.MemoryRepository.SeedMemory(memory);

        using var client = factory.CreateClient();
        using var request = CreateAuthenticatedRequest(
            HttpMethod.Delete,
            $"/api/memories/{memory.Id}",
            userId);

        using var response = await client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.NoContent,
            response.StatusCode);

        Assert.NotNull(memory.DeletedAt);

        var deletedMemory = await factory.MemoryRepository
            .GetByIdForOwnerAsync(memory.Id, userId);

        Assert.Null(deletedMemory);
    }

    [Fact]
    public async Task DeleteMemory_WhenMemoryBelongsToAnotherUser_ReturnsNotFound()
    {
        var ownerId = Guid.NewGuid();
        var anotherUserId = Guid.NewGuid();

        var memory = CreateMemory(
            ownerId,
            "Another user's memory",
            DateTimeOffset.UtcNow);

        factory.MemoryRepository.SeedMemory(memory);

        using var client = factory.CreateClient();
        using var request = CreateAuthenticatedRequest(
            HttpMethod.Delete,
            $"/api/memories/{memory.Id}",
            anotherUserId);

        using var response = await client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);

        Assert.Null(memory.DeletedAt);

        var existingMemory = await factory.MemoryRepository
            .GetByIdForOwnerAsync(memory.Id, ownerId);

        Assert.NotNull(existingMemory);
    }

    [Fact]
    public async Task DeleteMemory_WhenMemoryDoesNotExist_ReturnsNotFound()
    {
        var userId = Guid.NewGuid();
        var memoryId = Guid.NewGuid();

        using var client = factory.CreateClient();
        using var request = CreateAuthenticatedRequest(
            HttpMethod.Delete,
            $"/api/memories/{memoryId}",
            userId);

        using var response = await client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task DeleteMemory_WhenMemoryIsAlreadyDeleted_ReturnsNotFound()
    {
        var userId = Guid.NewGuid();

        var memory = CreateMemory(
            userId,
            "Already deleted memory",
            DateTimeOffset.UtcNow);

        memory.DeletedAt = DateTimeOffset.UtcNow;

        factory.MemoryRepository.SeedMemory(memory);

        using var client = factory.CreateClient();
        using var request = CreateAuthenticatedRequest(
            HttpMethod.Delete,
            $"/api/memories/{memory.Id}",
            userId);

        using var response = await client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    private static Memory CreateMemory(
        Guid userId,
        string title,
        DateTimeOffset createdAt)
    {
        return new Memory
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = title,
            Description = "Integration test memory",
            AudioPath = $"{userId:D}/test-memory.m4a",
            AudioDurationSeconds = 60,
            AudioContentType = "audio/mp4",
            AudioSizeBytes = 1024,
            Location = new Point(101.6869, 3.1390)
            {
                SRID = 4326
            },
            Visibility = MemoryVisibility.Public,
            NotificationRadiusMeters = 50,
            ModerationStatus = ModerationStatus.Approved,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
    }


    private static object CreateRequest(Guid memoryId)
    {
        return new
        {
            memoryId,
            title = "A memory near the lake",
            description = "A test audio memory.",
            requestedAudioContentType = "audio/mp4",
            audioDurationSeconds = 120,
            latitude = 3.1390,
            longitude = 101.6869,
            visibility = MemoryVisibility.Public,
            notificationRadiusMeters = 50
        };
    }

    private static AudioObjectMetadata CreateMetadata(
        Guid userId,
        Guid memoryId,
        string contentType = "audio/mp4",
        long sizeBytes = 1024)
    {
        var extension = contentType switch
        {
            "audio/mp4" => ".m4a",
            "audio/mpeg" => ".mp3",
            _ => ".m4a"
        };

        return new AudioObjectMetadata(
            $"{userId:D}/{memoryId:D}{extension}",
            contentType,
            sizeBytes);
    }

    private HttpRequestMessage CreateAuthenticatedRequest(
        HttpMethod method,
        string url,
        Guid userId)
    {
        var request = new HttpRequestMessage(method, url);

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                CreateToken(userId));

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

    private sealed record MemoryResponse(
        Guid Id,
        Guid UserId,
        string Title,
        string? Description,
        int AudioDurationSeconds,
        string AudioContentType,
        long AudioSizeBytes,
        double Latitude,
        double Longitude,
        MemoryVisibility Visibility,
        int NotificationRadiusMeters,
        ModerationStatus ModerationStatus,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);

    private sealed record MemoryPlaybackResponse(
    Guid MemoryId,
    string PlaybackUrl,
    DateTimeOffset ExpiresAt);
}
