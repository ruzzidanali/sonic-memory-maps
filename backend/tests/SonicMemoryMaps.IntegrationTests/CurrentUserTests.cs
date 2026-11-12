using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SonicMemoryMaps.API.Services;

namespace SonicMemoryMaps.IntegrationTests;

public sealed class CurrentUserTests
{
    [Fact]
    public void UserId_WithValidSubClaim_ReturnsGuid()
    {
        // Arrange
        var expectedUserId = Guid.NewGuid();

        var context = CreateAuthenticatedContext(
            expectedUserId.ToString(),
            "user@example.com");

        var accessor = new HttpContextAccessor
        {
            HttpContext = context
        };

        var currentUser = new CurrentUser(accessor);

        // Act
        var result = currentUser.UserId;

        // Assert
        Assert.Equal(expectedUserId, result);
    }

    [Fact]
    public void Email_WithEmailClaim_ReturnsEmail()
    {
        // Arrange
        const string expectedEmail = "user@example.com";

        var context = CreateAuthenticatedContext(
            Guid.NewGuid().ToString(),
            expectedEmail);

        var accessor = new HttpContextAccessor
        {
            HttpContext = context
        };

        var currentUser = new CurrentUser(accessor);

        // Act
        var result = currentUser.Email;

        // Assert
        Assert.Equal(expectedEmail, result);
    }

    [Fact]
    public void IsAuthenticated_WithAuthenticatedIdentity_ReturnsTrue()
    {
        // Arrange
        var context = CreateAuthenticatedContext(
            Guid.NewGuid().ToString(),
            "user@example.com");

        var accessor = new HttpContextAccessor
        {
            HttpContext = context
        };

        var currentUser = new CurrentUser(accessor);

        // Act & Assert
        Assert.True(currentUser.IsAuthenticated);
    }

    private static DefaultHttpContext CreateAuthenticatedContext(
        string userId,
        string email)
    {
        var claims = new[]
        {
            new Claim("sub", userId),
            new Claim("email", email)
        };

        var identity = new ClaimsIdentity(
            claims,
            authenticationType: "Test");

        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };

        return context;
    }
}