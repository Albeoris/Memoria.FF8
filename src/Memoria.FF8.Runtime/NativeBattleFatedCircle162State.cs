using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleFatedCircle162ColdRegion(uint Address, ImmutableArray<byte> Data);
public sealed record NativeBattleFatedCircle162Resources(NativeBattleEffectHeapAllocation StaticData, NativeBattleEffectFileAsset TextureFile, ImmutableArray<NativeBattleFatedCircle162ColdRegion> ColdGlobals, ImmutableArray<NativeBattleSearchSpriteResource> Sprites, ImmutableArray<NativeBattleStaticMeshResource> Meshes)
{
    public const uint StaticAddress = 0xDA0100;
    public const int StaticLength = 0x8C18;

    public byte[] Read(uint address, int length)
    {
        long offset = address - (long)StaticAddress;
        if (length < 0 || offset < 0 || offset + length > StaticData.Data.Length)
        {
            throw new NotSupportedException("Native Fated Circle static read leaves its authenticated resource extent.");
        }
        return StaticData.Data.AsSpan().Slice(checked((int)offset), length).ToArray();
    }

    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));
    public short ReadSignedWord(uint address) => BinaryPrimitives.ReadInt16LittleEndian(Read(address, 2));

    public void Validate()
    {
        if (StaticData is null || StaticData.Data.IsDefault || StaticData.Data.Length != StaticLength || TextureFile is null || TextureFile.FileId != 537 || TextureFile.Data.IsDefaultOrEmpty || ColdGlobals.IsDefault || ColdGlobals.Length != 5 || Sprites.IsDefault || Sprites.Length != 5 || Meshes.IsDefault || Meshes.Length != 16)
        {
            throw new InvalidDataException("Native Fated Circle requires its original static image, texture file and physical resource directories.");
        }
        foreach (NativeBattleFatedCircle162ColdRegion region in ColdGlobals)
        {
            if (region is null || region.Data.IsDefaultOrEmpty || region.Data.Any(value => value != 0))
            {
                throw new InvalidDataException("Native Fated Circle cold globals must preserve their proved loader-zero bytes.");
            }
        }
    }
}

public sealed record NativeBattleFatedCircle162State([property: JsonRequired] bool GlobalsInitialized, [property: JsonRequired] NativeBattleEffectParameters? Parameters, [property: JsonRequired] NativeBattleFatedCircle162Resources? RetainedResources)
{
    public static NativeBattleFatedCircle162State FromColdProcess() => new(false, null, null);

    public void Validate()
    {
        if (GlobalsInitialized)
        {
            if (Parameters is null || Parameters.Actor78 >= 7 || Parameters.HeaderIndex7C is < 0 or >= NativeBattleActionPacketsState.HeaderCapacity || RetainedResources is null)
            {
                throw new InvalidDataException("Native Fated Circle requires its actual entered action parameters and resource bundle.");
            }
            RetainedResources.Validate();
        }
        else if (Parameters is not null || RetainedResources is not null)
        {
            throw new InvalidDataException("Native Fated Circle cannot retain entered data before actual native entry.");
        }
    }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleFatedCircle162State? FatedCircle162 { get; init; }
}

public sealed record NativeBattleFatedCircle162Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleFileStaging Staging, NativeCrtRandomState Random);
public sealed record NativeBattleFatedCircle162ChildStep(NativeBattleFatedCircle162Frame Frame, NativeBattleEffectTaskPool Pool, int Result);
