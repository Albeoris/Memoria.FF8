using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRegen147Resources(NativeBattleEffectHeapAllocation Camera, NativeBattleStaticMeshResource Envelope, NativeBattleModuleResource Leaf, uint Sound);
public sealed record NativeBattleRegen147State([property: JsonRequired] NativeBattleEffectArena Storage)
{
    public const int PhysicalLength = 0x1B55C;
    public static NativeBattleRegen147State FromColdProcess() => new(NativeBattleEffectArena.Clear().MarkUnknown(PhysicalLength, NativeBattleEffectArena.Length - PhysicalLength));

    public void Validate()
    {
        if (Storage is null)
        {
            throw new InvalidDataException("Native Regen requires its physical process storage.");
        }
        Storage.Validate();
        if (!Storage.EnumerateUnknownRanges().Any(region => region.Offset == PhysicalLength && region.Length == NativeBattleEffectArena.Length - PhysicalLength))
        {
            throw new InvalidDataException("Native Regen cannot own bytes beyond its physical process bank.");
        }
    }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleRegen147State? Regen147 { get; init; }
}
