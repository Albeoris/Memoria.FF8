using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRoughDivide163ColdRegion(uint Address, ImmutableArray<byte> Data);
public sealed record NativeBattleRoughDivide163Resources(NativeBattleEffectHeapAllocation StaticData, ImmutableArray<NativeBattleRoughDivide163ColdRegion> ColdGlobals, ImmutableArray<NativeBattleSearchSpriteResource> Sprites, ImmutableArray<NativeBattleStaticMeshResource> Meshes)
{
    public const uint StaticAddress = 0xD99DC8;
    public const int StaticLength = 0x6338;

    public void Validate()
    {
        if (StaticData is null || StaticData.Data.IsDefault || StaticData.Data.Length != StaticLength || ColdGlobals.IsDefault || ColdGlobals.Length != 4 || Sprites.IsDefault || Sprites.Length != 2 || Meshes.IsDefault || Meshes.Length != 10)
        {
            throw new InvalidDataException("Native Rough Divide requires its original static image and physical resource directories.");
        }
        foreach (NativeBattleRoughDivide163ColdRegion region in ColdGlobals)
        {
            if (region is null || region.Data.IsDefaultOrEmpty || region.Data.Any(value => value != 0))
            {
                throw new InvalidDataException("Native Rough Divide cold globals require their authenticated loader bytes.");
            }
        }
    }
}

public sealed record NativeBattleRoughDivide163State([property: JsonRequired] bool GlobalsInitialized, [property: JsonRequired] NativeBattleEffectParameters? Parameters, [property: JsonRequired] byte? FirstRecipient, [property: JsonRequired] NativeBattleRoughDivide163Resources? RetainedResources)
{
    public static NativeBattleRoughDivide163State FromColdProcess() => new(false, null, null, null);

    public void Validate()
    {
        if (GlobalsInitialized)
        {
            if (Parameters is null || Parameters.Actor78 >= 7 || Parameters.HeaderIndex7C is < 0 or >= NativeBattleActionPacketsState.HeaderCapacity || FirstRecipient is null or >= 7 || RetainedResources is null)
            {
                throw new InvalidDataException("Native Rough Divide requires its actual entered action and immutable resources.");
            }
            RetainedResources.Validate();
        }
        else if (Parameters is not null || FirstRecipient is not null || RetainedResources is not null)
        {
            throw new InvalidDataException("Native Rough Divide cannot retain entered owners before entry.");
        }
    }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleRoughDivide163State? RoughDivide163 { get; init; }
}
