using Microsoft.EntityFrameworkCore;
using SonicMemoryMaps.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

builder.Services.AddHealthChecks();

builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapControllers();

app.MapHealthChecks("/health");

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
});

app.Run();

// Required later for integration testing with WebApplicationFactory.
public partial class Program;