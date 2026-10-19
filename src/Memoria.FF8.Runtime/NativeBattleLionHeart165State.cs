using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleLionHeart165ColdRegion(uint Address, ImmutableArray<byte> Data);
public sealed record NativeBattleLionHeart165Resources(NativeBattleEffectHeapAllocation StaticData, ImmutableArray<NativeBattleLionHeart165ColdRegion> ColdGlobals, ImmutableArray<NativeBattleEffectFileAsset> Files, ImmutableArray<NativeBattleSearchSpriteResource> Sprites, ImmutableArray<NativeBattleStaticMeshResource> Meshes)
{
    public const uint StaticAddress = 0xD780C8;
    public const int StaticLength = 0x109D8;

    public byte[] Read(uint address, int length)
    {
        long offset = address - (long)StaticAddress;
        if (length < 0 || offset < 0 || offset + length > StaticData.Data.Length)
        {
            throw new NotSupportedException("Native Lion Heart static read leaves its authenticated resource extent.");
        }
        return StaticData.Data.AsSpan().Slice(checked((int)offset), length).ToArray();
    }

    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));

    public void Validate()
    {
        if (StaticData is null || StaticData.Data.IsDefault || StaticData.Data.Length != StaticLength || ColdGlobals.IsDefault || ColdGlobals.Length != 4 || Files.IsDefault || Files.Length != 3 || Files[0].FileId != 540 || Files[1].FileId != 541 || Files[2].FileId != 542 || Files.Any(file => file.Data.IsDefaultOrEmpty) || Sprites.IsDefault || Sprites.Length != 4 || Meshes.IsDefault || Meshes.Length != 16)
        {
            throw new InvalidDataException("Native Lion Heart requires its original static image, texture files and physical resource directories.");
        }
        foreach (NativeBattleLionHeart165ColdRegion region in ColdGlobals)
        {
            if (region is null || region.Data.IsDefaultOrEmpty || region.Data.Any(value => value != 0))
            {
                throw new InvalidDataException("Native Lion Heart cold globals require their authenticated loader bytes.");
            }
        }
    }
}

public sealed record NativeBattleLionHeart165State([property: JsonRequired] bool GlobalsInitialized, [property: JsonRequired] NativeBattleEffectParameters? Parameters, [property: JsonRequired] byte? FirstRecipient, [property: JsonRequired] NativeBattleLionHeart165Resources? RetainedResources, [property: JsonRequired] NativeBattleGuardianCameraTask? CameraTask)
{
    public static NativeBattleLionHeart165State FromColdProcess() => new(false, null, null, null, null);

    public void Validate()
    {
        if (GlobalsInitialized)
        {
            if (Parameters is null || Parameters.Actor78 >= 7 || Parameters.HeaderIndex7C is < 0 or >= NativeBattleActionPacketsState.HeaderCapacity || FirstRecipient is null or >= 7 || RetainedResources is null)
            {
                throw new InvalidDataException("Native Lion Heart requires its actual entered action and immutable resources.");
            }
            RetainedResources.Validate();
        }
        else if (Parameters is not null || FirstRecipient is not null || RetainedResources is not null || CameraTask is not null)
        {
            throw new InvalidDataException("Native Lion Heart cannot retain entered owners before entry.");
        }
    }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleLionHeart165State? LionHeart165 { get; init; }
}

public sealed record NativeBattleLionHeart165Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleFileStaging Staging, NativeCrtRandomState Random)
{
    public NativeBattleEffectParameters Parameters => Memory.LionHeart165?.Parameters ?? throw new InvalidDataException("Native Lion Heart requires its actual entered parameter owner.");
    public byte FirstRecipient => Memory.LionHeart165?.FirstRecipient ?? throw new InvalidDataException("Native Lion Heart requires its actual first recipient.");
}

public sealed record NativeBattleLionHeart165ChildStep(NativeBattleLionHeart165Frame Frame, NativeBattleEffectTaskPool Pool, int Result);
