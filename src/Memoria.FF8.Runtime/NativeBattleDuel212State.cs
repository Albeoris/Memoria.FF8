using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDuel212Resources(ImmutableArray<byte> StaticData, ImmutableArray<byte> ColdGlobals, ImmutableArray<byte> SelectorTable, ImmutableArray<NativeBattleSearchSpriteResource> Sprites, ImmutableArray<NativeBattleStaticMeshResource> Meshes, NativeBattleEffectHeapAllocation InitialTim)
{
    public ImmutableArray<NativeBattleEffectFileAsset> Files { get; init; }

    public const uint Address = 0x1181070;
    public const uint EndAddress = 0x11AC200;

    public ImmutableArray<byte> Read(uint address, int length)
    {
        long offset = (long)address - Address;
        if (StaticData.IsDefault || StaticData.Length != EndAddress - Address || offset < 0 || length < 0 || offset > StaticData.Length - length)
        {
            throw new InvalidDataException("Native Burning Rave read leaves its original static owner.");
        }
        return StaticData.AsSpan().Slice((int)offset, length).ToArray().ToImmutableArray();
    }

    public uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4).AsSpan());

    public ImmutableArray<byte> ReadActorDat()
    {
        ImmutableArray<byte> actor = Read(0x1181078, 0x1350);
        _ = NativeBattleDatDirectory.Section(actor.AsSpan(), 1);
        return actor;
    }

    public void Validate()
    {
        _ = Read(Address, checked((int)(EndAddress - Address)));
        if (ColdGlobals.IsDefault || ColdGlobals.Length != 0x68 || SelectorTable.IsDefault || SelectorTable.Length != 80 || Sprites.IsDefault || Sprites.Length != 2 || Meshes.IsDefault || Meshes.Length != 5 || InitialTim is null || InitialTim.Data.IsDefaultOrEmpty)
        {
            throw new InvalidDataException("Native Burning Rave requires original texture, geometry, selectors and loader globals.");
        }
        if (Files.IsDefault || Files.Length != 2 || !Files.Select(file => file.FileId).SequenceEqual(new short[] { 360, 361 }) || Files.Any(file => file.Data.IsDefaultOrEmpty))
        {
            throw new InvalidDataException("Native Burning Rave requires both original reload files.");
        }
        _ = ReadActorDat();
    }
}

public sealed record NativeBattleDuel212State([property: JsonRequired] bool GlobalsInitialized, [property: JsonRequired] NativeBattleEffectParameters? Parameters, [property: JsonRequired] NativeBattleDuel212Resources? Resources)
{
    public static NativeBattleDuel212State FromColdProcess() => new(false, null, null);

    public void Validate()
    {
        if (!GlobalsInitialized)
        {
            if (Parameters is not null || Resources is not null)
            {
                throw new InvalidDataException("Native Burning Rave cannot retain entered owners before entry.");
            }
            return;
        }
        if (Parameters is null || Parameters.Actor78 >= 7 || Resources is null)
        {
            throw new InvalidDataException("Native Burning Rave requires its actual entered owners.");
        }
        Resources.Validate();
    }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleDuel212State? Duel212 { get; init; }
}

public sealed record NativeBattleDuel212Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleFileStaging Staging, NativeCrtRandomState Random);
public sealed record NativeBattleDuel212Step(NativeBattleDuel212Frame Frame, NativeBattleEffectTaskPool Pool, int Result);
