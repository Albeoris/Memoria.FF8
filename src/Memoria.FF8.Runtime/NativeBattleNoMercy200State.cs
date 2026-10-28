using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleNoMercy200ColdRegion(uint Address, ImmutableArray<byte> Data);
public sealed record NativeBattleNoMercy200Resources(NativeBattleEffectHeapAllocation StaticData, ImmutableArray<NativeBattleNoMercy200ColdRegion> ColdRegions, NativeBattleEffectHeapAllocation InitialTim, ImmutableArray<NativeBattleEffectFileAsset> Files, ImmutableArray<NativeBattleStaticMeshResource> Meshes, ImmutableArray<NativeBattleSearchSpriteResource> Sprites, NativeBattleGfAnimationSource Camera)
{
    public const uint StaticAddress = 0xE2B518;
    public const int StaticLength = 0x8858;

    public ReadOnlySpan<byte> Read(uint address, int count)
    {
        uint offset = unchecked(address - StaticAddress);
        if (StaticData.Data.IsDefault || offset > StaticData.Data.Length || count < 0 || offset > StaticData.Data.Length - count)
        {
            throw new InvalidDataException("Native No Mercy source access leaves its original static bank.");
        }
        return StaticData.Data.AsSpan().Slice((int)offset, count);
    }

    public void Validate()
    {
        if (StaticData is null || StaticData.Data.IsDefault || StaticData.Data.Length != StaticLength || ColdRegions.IsDefault || ColdRegions.Length != 4 || InitialTim is null || InitialTim.Data.IsDefaultOrEmpty || Files.IsDefault || Files.Length != 3 || Files.Where((file, index) => file.FileId != 562 + index || file.Data.IsDefaultOrEmpty).Any() || Meshes.IsDefault || Meshes.Length != 9 || Sprites.IsDefault || Sprites.Length != 6 || Camera is null || Camera.Address != 0xE33D8C || Camera.Data.IsDefaultOrEmpty)
        {
            throw new InvalidDataException("Native No Mercy requires its original static image, entry texture, texture files and resource directories.");
        }
        foreach (NativeBattleNoMercy200ColdRegion region in ColdRegions)
        {
            if (region is null || region.Data.IsDefaultOrEmpty)
            {
                throw new InvalidDataException("Native No Mercy cold storage requires its authenticated loader bytes.");
            }
        }
        _ = NativeBattleDatDirectory.Section(ReadActorDat().AsSpan(), 0);
    }

    public ImmutableArray<byte> ReadActorDat()
    {
        if (Dword(StaticAddress) != 3)
        {
            throw new InvalidDataException("Native No Mercy actor override requires its original three-section directory.");
        }
        int length = checked((int)Dword(StaticAddress + 16));
        ImmutableArray<byte> actor = Read(StaticAddress, length).ToArray().ToImmutableArray();
        _ = NativeBattleDatDirectory.Section(actor.AsSpan(), 0);
        return actor;
    }

    public uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));
}

public sealed record NativeBattleNoMercy200State([property: JsonRequired] bool GlobalsInitialized)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleNoMercy200Resources? Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleGuardianCameraTask? CameraTask { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleEffectParameters? Parameters { get; init; }
    public static NativeBattleNoMercy200State FromColdProcess() => new(false);

    public void Validate()
    {
        if (!GlobalsInitialized)
        {
            if (Resources is not null || Parameters is not null || CameraTask is not null)
            {
                throw new InvalidDataException("Native No Mercy cannot retain entered owners before actual entry.");
            }
            return;
        }
        if (Resources is null || Parameters is null || Parameters.Actor78 >= 7 || Parameters.HeaderIndex7C is < 0 or >= NativeBattleActionPacketsState.HeaderCapacity)
        {
            throw new InvalidDataException("Native No Mercy requires resources and parameters retained by its actual entry.");
        }
        Resources.Validate();
    }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleNoMercy200State? NoMercy200
    {
        get => effectState.NoMercy200;
        init => effectState = effectState with { NoMercy200 = value };
    }
}

public sealed record NativeBattleNoMercy200Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleFileStaging Staging, NativeCrtRandomState Random)
{
    [JsonIgnore]
    public NativeBattleEffectParameters Parameters => Memory.NoMercy200?.Parameters ?? throw new NotSupportedException("Native No Mercy requires its entered parameter owner.");
}
public sealed record NativeBattleNoMercy200ChildStep(NativeBattleNoMercy200Frame Frame, NativeBattleEffectTaskPool Pool, int Result);
