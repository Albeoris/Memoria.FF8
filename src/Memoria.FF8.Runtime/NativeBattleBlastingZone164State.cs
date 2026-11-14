using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBlastingZone164ColdRegion(uint Address, ImmutableArray<byte> Data);
public sealed record NativeBattleBlastingZone164Resources(NativeBattleEffectHeapAllocation StaticData, ImmutableArray<NativeBattleBlastingZone164ColdRegion> ColdGlobals, ImmutableArray<NativeBattleEffectFileAsset> Files, ImmutableArray<NativeBattleSearchSpriteResource> Sprites, ImmutableArray<NativeBattleStaticMeshResource> Meshes)
{
    public const uint StaticAddress = 0xD88AA0;
    public const int StaticLength = 0x11328;

    public byte[] Read(uint address, int length)
    {
        long offset = address - (long)StaticAddress;
        if (length < 0 || offset < 0 || offset + length > StaticData.Data.Length)
        {
            throw new NotSupportedException("Native Blasting Zone static read leaves its authenticated resource extent.");
        }
        return StaticData.Data.AsSpan().Slice(checked((int)offset), length).ToArray();
    }

    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));

    public void Validate()
    {
        if (StaticData is null || StaticData.Data.IsDefault || StaticData.Data.Length != StaticLength || ColdGlobals.IsDefault || ColdGlobals.Length != 4 || Files.IsDefault || Files.Length != 2 || Files[0].FileId != 538 || Files[1].FileId != 539 || Files.Any(file => file.Data.IsDefaultOrEmpty) || Sprites.IsDefault || Sprites.Length != 3 || Meshes.IsDefault || Meshes.Length != 19)
        {
            throw new InvalidDataException("Native Blasting Zone requires its original static image, texture files and physical resource directories.");
        }
        foreach (NativeBattleBlastingZone164ColdRegion region in ColdGlobals)
        {
            if (region is null || region.Data.IsDefaultOrEmpty || region.Data.Any(value => value != 0))
            {
                throw new InvalidDataException("Native Blasting Zone cold globals require their authenticated loader bytes.");
            }
        }
    }
}

public sealed record NativeBattleBlastingZone164State([property: JsonRequired] bool GlobalsInitialized, [property: JsonRequired] NativeBattleBlastingZone164Resources? RetainedResources, [property: JsonRequired] NativeBattleGuardianCameraTask? CameraTask)
{
    public static NativeBattleBlastingZone164State FromColdProcess() => new(false, null, null);

    public void Validate()
    {
        if (GlobalsInitialized)
        {
            if (RetainedResources is null)
            {
                throw new InvalidDataException("Native Blasting Zone requires its immutable resources retained by actual entry.");
            }
            RetainedResources.Validate();
        }
        else if (RetainedResources is not null || CameraTask is not null)
        {
            throw new InvalidDataException("Native Blasting Zone cannot retain entered owners before entry.");
        }
    }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleBlastingZone164State? BlastingZone164
    {
        get => effectState.BlastingZone164;
        init => effectState = effectState with { BlastingZone164 = value };
    }
}

public sealed record NativeBattleBlastingZone164Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleFileStaging Staging, NativeCrtRandomState Random)
{
    public NativeBattleEffectParameters Parameters => Effect.Parameters ?? throw new InvalidDataException("Native Blasting Zone requires its actual entered parameter owner.");
    public BattleModelOwner FirstRecipient => Effect.EntryFirstRecipient ?? throw new InvalidDataException("Native Blasting Zone requires its actual first recipient.");
}

public sealed record NativeBattleBlastingZone164ChildStep(NativeBattleBlastingZone164Frame Frame, NativeBattleEffectTaskPool Pool, int Result);
