
namespace SonicMemoryMaps.Infrastructure.Storage;

public sealed class SupabaseOptions
{
    public const string SectionName = "Supabase";

    public string Url { get; set; } = string.Empty;

    public string SecretKey { get; set; } = string.Empty;
}
