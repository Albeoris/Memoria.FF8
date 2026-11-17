using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleStatusRules(byte BattleSpeed, ImmutableArray<byte> Durations);
public sealed record NativeBattleStatusSnapshot(ushort Saved, uint Battle, short[] Timers) : IJsonOnDeserialized
{
    [JsonRequired]
    public NativeBattleStatusMirror Presentation { get; init; } = new(null, null);
    public byte[]? Resistances { get; init; }

    [JsonInclude, JsonExtensionData]
    internal Dictionary<string, JsonElement>? UnrecognizedProperties { get; private set; }

    void IJsonOnDeserialized.OnDeserialized()
    {
        if (LegacyStatusStatSnapshot.HasSourceSpecificPercentages(UnrecognizedProperties))
        {
            throw new InvalidDataException("This battle snapshot uses source-specific stat rules. Migrate it explicitly to battle-owned status stat modifiers.");
        }
        UnrecognizedProperties = null;
    }
}

internal static class LegacyStatusStatSnapshot
{
    internal static bool HasSourceSpecificPercentages(Dictionary<string, JsonElement>? properties)
    {
        if (properties is null)
        {
            return false;
        }

        foreach ((string name, JsonElement value) in properties)
        {
            string normalized = name.Replace("_", "", StringComparison.Ordinal);
            if (normalized.Equals("MeltdownVitalityReductionPercent", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("MeltdownSpiritReductionPercent", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("MeltdownDefenseReductionPercent", StringComparison.OrdinalIgnoreCase))
            {
                if (value.ValueKind != JsonValueKind.Null && (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int percent) || percent != StatusStatRules.MaximumPercent))
                {
                    return true;
                }
            }
        }
        return false;
    }
}
public sealed record NativeBattleStatusAllocation(ushort Saved, uint Battle, ImmutableArray<byte> Resistances);
