
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SonicMemoryMaps.Infrastructure.Persistence;
using SonicMemoryMaps.API.Services;
using SonicMemoryMaps.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using SonicMemoryMaps.Application.Profiles;
using SonicMemoryMaps.Infrastructure.Persistence.Repositories;

var builder = WebApplication.CreateBuilder(args);

// --------------------------------------------------
// Configuration
// --------------------------------------------------

var supabaseUrl = builder.Configuration["Supabase:Url"];
var supabaseAudience = builder.Configuration["Supabase:Jwt:Audience"];

if (!Uri.TryCreate(supabaseUrl, UriKind.Absolute, out var supabaseUri) ||
    supabaseUri.Scheme != Uri.UriSchemeHttps)
{
    throw new InvalidOperationException(
        "A valid HTTPS Supabase URL must be configured.");
}

if (string.IsNullOrWhiteSpace(supabaseAudience))
{
    throw new InvalidOperationException(
        "Supabase JWT audience is not configured.");
}

var supabaseAuthority =
    $"{supabaseUri.GetLeftPart(UriPartial.Authority)}/auth/v1";

// --------------------------------------------------
// Database
// --------------------------------------------------

var connectionString =
    builder.Configuration.GetConnectionString("SonicMemoryMaps");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Database connection string is not configured.");
}

builder.Services.AddDbContext<SonicMemoryMapsDbContext>(options =>
{
    options.UseNpgsql(
        connectionString,
        npgsql => npgsql.UseNetTopologySuite());
});

// --------------------------------------------------
// Authentication - Supabase JWT
// --------------------------------------------------

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Supabase OpenID Connect discovery and JWKS.
        options.Authority = supabaseAuthority;
        options.Audience = supabaseAudience;
        options.RequireHttpsMetadata = true;

        // Preserve original JWT claim names, including "sub".
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = supabaseAuthority,

            ValidateAudience = true,
            ValidAudience = supabaseAudience,

            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidAlgorithms =
            [
                SecurityAlgorithms.EcdsaSha256
            ],

            NameClaimType = "sub",
            RoleClaimType = "role",

            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy =
        new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();
});

// --------------------------------------------------
// API services
// --------------------------------------------------

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your Supabase access token."
    });

    options.AddSecurityRequirement(document =>
        new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = []
        });
});

builder.Services.AddHealthChecks();
builder.Services.AddProblemDetails();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<IProfileRepository, ProfileRepository>();

// --------------------------------------------------
// Middleware pipeline
// --------------------------------------------------

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Authentication must run before authorization.
app.UseAuthentication();
app.UseAuthorization();

// --------------------------------------------------
// Public health endpoints
// --------------------------------------------------

app.MapControllers();

app.MapHealthChecks("/health")
    .AllowAnonymous();

app.MapGet("/health/database", async (
    SonicMemoryMapsDbContext db) =>
{
    var canConnect = await db.Database.CanConnectAsync();

    if (!canConnect)
    {
        return Results.Problem(
            title: "Database unavailable",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    return Results.Ok(new
    {
        status = "Healthy",
        database = "Connected"
    });
})
.RequireAuthorization();

// --------------------------------------------------
// Protected authentication diagnostic endpoint
// --------------------------------------------------

app.MapGet("/auth/me", (ICurrentUser currentUser) =>
{
    return Results.Ok(new
    {
        userId = currentUser.UserId,
        email = currentUser.Email,
        isAuthenticated = currentUser.IsAuthenticated
    });
})
.RequireAuthorization();

app.Run();

// Required for integration testing with WebApplicationFactory.
public partial class Program;
