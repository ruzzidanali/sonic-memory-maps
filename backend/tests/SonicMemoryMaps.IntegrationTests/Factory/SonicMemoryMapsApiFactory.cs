
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SonicMemoryMaps.Application.Abstractions;
using SonicMemoryMaps.IntegrationTests.Repositories;
using SonicMemoryMaps.IntegrationTests.Storage;

namespace SonicMemoryMaps.IntegrationTests.Factory;

public sealed class SonicMemoryMapsApiFactory
    : WebApplicationFactory<Program>
{
    private readonly ECDsa _testKey =
        ECDsa.Create(ECCurve.NamedCurves.nistP256);

    private readonly InMemoryProfileRepository _profileRepository = new();
    private readonly InMemoryMemoryRepository _memoryRepository = new();
    private readonly FakeAudioStorageService _audioStorageService = new();
    public InMemoryProfileRepository ProfileRepository =>
        _profileRepository;

    public InMemoryMemoryRepository MemoryRepository =>
    _memoryRepository;

    public FakeAudioStorageService AudioStorageService =>
    _audioStorageService;

    public string TestIssuer =>
        "https://test.sonic-memory-maps.local/auth/v1";

    public string TestAudience => "authenticated";

    public ECDsaSecurityKey SigningKey =>
        new(_testKey);

    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        var testSettings = new Dictionary<string, string?>
        {
            ["Supabase:Url"] =
            "https://test-project.supabase.co",

            ["Supabase:SecretKey"] =
            "test-secret-key",

            ["Supabase:Storage:Bucket"] =
            "memory-audio-test",

            ["Supabase:Jwt:Audience"] = TestAudience,

            ["ConnectionStrings:SonicMemoryMaps"] =
                "Host=localhost;Port=5432;" +
                "Database=sonic_memory_maps_test;" +
                "Username=test;Password=test;Timeout=1;"
        };

        var earlyConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(testSettings)
            .Build();

        builder.UseConfiguration(earlyConfiguration);

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(testSettings);
        });

        builder.ConfigureTestServices(services =>
        {
            services.PostConfigure<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme,
                options =>
                {
                    options.Authority = null;

                    options.TokenValidationParameters =
                    new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = TestIssuer,

                        ValidateAudience = true,
                        ValidAudience = TestAudience,

                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,

                        IssuerSigningKey = SigningKey,

                        ValidAlgorithms =
                        [
                            SecurityAlgorithms.EcdsaSha256
                        ],

                        NameClaimType = "sub",
                        RoleClaimType = "role",

                        ClockSkew = TimeSpan.FromSeconds(30)
                    };
                });

            services.RemoveAll<IProfileRepository>();
            services.AddSingleton<IProfileRepository>(_profileRepository);

            services.RemoveAll<IMemoryRepository>();
            services.AddSingleton<IMemoryRepository>(_memoryRepository);

            services.RemoveAll<IAudioStorageService>();

            services.AddSingleton<IAudioStorageService>(
                _audioStorageService);
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _testKey.Dispose();
        }

        base.Dispose(disposing);
    }
}
