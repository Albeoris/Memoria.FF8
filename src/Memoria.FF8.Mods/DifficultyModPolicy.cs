using System.Collections.Frozen;
using System.Text.Json;

namespace Memoria.FF8.Mods;

internal static class DifficultyModPolicy
{
    private static readonly FrozenDictionary<string, Requirement> HardcoreRequirements = ReadHardcoreRequirements();

    public static string? LockReason(bool hardcore, string packageId)
    {
        return hardcore ? HardcoreRequirements.GetValueOrDefault(packageId)?.Reason : null;
    }

    public static bool LocksDefaults(bool hardcore, string packageId)
    {
        return hardcore && HardcoreRequirements.GetValueOrDefault(packageId)?.LockDefaults == true;
    }

    private static FrozenDictionary<string, Requirement> ReadHardcoreRequirements()
    {
        using Stream source = typeof(DifficultyModPolicy).Assembly.GetManifestResourceStream("Memoria.DifficultyMods") ?? throw new InvalidOperationException("The bundled difficulty policy is missing.");
        var policy = JsonSerializer.Deserialize<Dictionary<string, Requirement[]>>(source, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? throw new InvalidDataException("The bundled difficulty policy is invalid.");
        return policy["Hardcore"].ToFrozenDictionary(requirement => requirement.PackageId, StringComparer.OrdinalIgnoreCase);
    }

    private sealed record Requirement(string PackageId, string Reason, bool LockDefaults);
}
