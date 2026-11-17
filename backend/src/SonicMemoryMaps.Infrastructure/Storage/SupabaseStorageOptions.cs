
namespace SonicMemoryMaps.Infrastructure.Storage;

public sealed class SupabaseStorageOptions
{
    public const string SectionName = "Supabase:Storage";

    public string Bucket { get; set; } = string.Empty;
}
