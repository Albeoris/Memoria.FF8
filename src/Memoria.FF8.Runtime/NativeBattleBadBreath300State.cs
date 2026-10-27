using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBadBreath300Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleBadBreath300State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
    [JsonIgnore] public NativeBattleStackBytes? TargetScaleStack { get; init; }
}

public sealed record NativeBattleBadBreath300State([property: JsonRequired] NativeBattleEffectArena Globals, [property: JsonRequired] ImmutableArray<byte>? MeshA, [property: JsonRequired] ImmutableArray<byte>? MeshB)
{
    public const uint BaseAddress = 0x2507758;
    public const int Length = 0xB8;
    [JsonRequired] public bool PendingColdSources { get; init; }
    public static NativeBattleBadBreath300State FromColdProcess() => new(NativeBattleEffectArena.Clear(), null, null) { PendingColdSources = true };
    public static NativeBattleBadBreath300State FromUnknown() => new(NativeBattleEffectArena.Clear().MarkUnknown(0, Length), null, null);
    public static NativeBattleBadBreath300State FromColdProcess(NativeBattleBadBreath300Resources resources) => FromColdProcess().SeedColdSources(resources);
    public NativeBattleBadBreath300State SeedColdSources(NativeBattleBadBreath300Resources resources)
    {
        if (!PendingColdSources)
        {
            return this;
        }
        resources.Validate();
        if (MeshA is not null || MeshB is not null)
        {
            throw new InvalidDataException("Bad Breath cold sources already have a mutable owner.");
        }
        return this with { MeshA = resources.MeshAInitial, MeshB = resources.MeshBInitial, PendingColdSources = false };
    }
    public void Validate()
    {
        Globals.Validate();
        if (PendingColdSources && (MeshA is not null || MeshB is not null))
        {
            throw new InvalidDataException("Bad Breath pending cold sources cannot replace retained mutable banks.");
        }
        if (MeshA is { } meshA)
        {
            NativeBattleBadBreath300Resources.ValidateMesh(meshA);
        }
        if (MeshB is { } meshB)
        {
            NativeBattleBadBreath300Resources.ValidateMesh(meshB);
        }
        foreach (NativeBattleEffectStorageRange region in Globals.EnumerateDataRanges())
        {
            Require(checked(BaseAddress + (uint)region.Offset), region.Length);
        }
        Globals.ValidateExtent(Length);
    }
    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Globals.Read(Require(address, 4), 4));
    public NativeBattleBadBreath300State WriteDword(uint address, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        Globals.Write(Require(address, 4), bytes);
        return this;
    }
    public NativeBattleEffectArena ReadView(uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(Globals, Require(address, length), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    public NativeBattleBadBreath300State WriteView(uint address, NativeBattleEffectArena source, int length)
    {
        NativeBattleEffectNodeContextMemory.Copy(source, 0, Globals, Require(address, length), length);
        return this;
    }
    public ImmutableArray<byte> ReadMesh(uint address) => (address == NativeBattleBadBreath300Profile.MeshAAddress ? MeshA : address == NativeBattleBadBreath300Profile.MeshBAddress ? MeshB : throw new InvalidDataException("Bad Breath selected an unsupported mutable mesh owner.")) ?? throw new NotSupportedException("Bad Breath requires its actual retained executable mesh publication.");
    public NativeBattleBadBreath300State WriteMesh(uint address, ImmutableArray<byte> bytes)
    {
        NativeBattleBadBreath300Resources.ValidateMesh(bytes);
        return address == NativeBattleBadBreath300Profile.MeshAAddress ? this with { MeshA = bytes } : address == NativeBattleBadBreath300Profile.MeshBAddress ? this with { MeshB = bytes } : throw new InvalidDataException("Bad Breath selected an unsupported mutable mesh owner.");
    }
    private static int Require(uint address, int length)
    {
        if (address < BaseAddress || length < 0 || (ulong)address + (uint)length > BaseAddress + Length || address < 0x25077C4 && (ulong)address + (uint)length > 0x25077B0 || address < 0x2507810 && (ulong)address + (uint)length > 0x25077D0)
        {
            throw new InvalidDataException("Bad Breath globals exclude its physical root task and descriptors.");
        }
        return checked((int)(address - BaseAddress));
    }
}
