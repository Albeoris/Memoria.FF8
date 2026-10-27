using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBadBreath131State([property: JsonRequired] NativeBattleEffectArena Globals, [property: JsonRequired] ImmutableArray<byte>? MeshA, [property: JsonRequired] ImmutableArray<byte>? MeshB)
{
    public const uint BaseAddress = 0x2520FF0;
    public const int Length = 0xB8;
    [JsonRequired] public bool? PendingColdSources { get; init; }
    public static NativeBattleBadBreath131State FromColdProcess() => new(NativeBattleEffectArena.Clear(), null, null) { PendingColdSources = true };
    public static NativeBattleBadBreath131State FromUnknown() => new(NativeBattleEffectArena.Clear().MarkUnknown(0, Length), null, null) { PendingColdSources = null };
    public static NativeBattleBadBreath131State FromColdProcess(NativeBattleBadBreath131Resources resources) => FromColdProcess().SeedColdSources(resources);
    public NativeBattleBadBreath131State SeedColdSources(NativeBattleBadBreath131Resources resources)
    {
        if (PendingColdSources is null)
        {
            throw new NotSupportedException("Bad Breath requires its actual mutable mesh source provenance.");
        }
        if (PendingColdSources == false)
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
        if (PendingColdSources == true && (MeshA is not null || MeshB is not null))
        {
            throw new InvalidDataException("Bad Breath pending cold sources cannot replace retained mutable banks.");
        }
        if (MeshA is { } meshA)
        {
            NativeBattleBadBreath131Resources.ValidateMesh(meshA);
        }
        if (MeshB is { } meshB)
        {
            NativeBattleBadBreath131Resources.ValidateMesh(meshB);
        }
        foreach (NativeBattleEffectStorageRange region in Globals.EnumerateDataRanges())
        {
            Require(checked(BaseAddress + (uint)region.Offset), region.Length);
        }
        Globals.ValidateExtent(Length);
    }
    public ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Globals.Read(Require(address, 2), 2));
    public NativeBattleBadBreath131State WriteWord(uint address, ushort value)
    {
        Span<byte> encoded = stackalloc byte[2];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(encoded, value);
        Globals.Write(Require(address, 2), encoded);
        return this;
    }
    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Globals.Read(Require(address, 4), 4));
    public NativeBattleBadBreath131State WriteDword(uint address, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        Globals.Write(Require(address, 4), bytes);
        return this;
    }
    public NativeBattleEffectArena ReadView(uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(Globals, Require(address, length), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    public NativeBattleBadBreath131State WriteView(uint address, NativeBattleEffectArena source, int length)
    {
        NativeBattleEffectNodeContextMemory.Copy(source, 0, Globals, Require(address, length), length);
        return this;
    }
    public ImmutableArray<byte> ReadMesh(uint address) => (address == NativeBattleBadBreath131Profile.MeshAAddress ? MeshA : address == NativeBattleBadBreath131Profile.MeshBAddress ? MeshB : throw new InvalidDataException("Bad Breath selected an unsupported mutable mesh owner.")) ?? throw new NotSupportedException("Bad Breath requires its actual retained executable mesh publication.");
    public NativeBattleBadBreath131State WriteMesh(uint address, ImmutableArray<byte> bytes)
    {
        NativeBattleBadBreath131Resources.ValidateMesh(bytes);
        return address == NativeBattleBadBreath131Profile.MeshAAddress ? this with { MeshA = bytes } : address == NativeBattleBadBreath131Profile.MeshBAddress ? this with { MeshB = bytes } : throw new InvalidDataException("Bad Breath selected an unsupported mutable mesh owner.");
    }
    private static int Require(uint address, int length)
    {
        bool allowed = length >= 0 && address >= BaseAddress && ((ulong)address + (uint)length <= 0x2521014 || address >= 0x2521038 && (ulong)address + (uint)length <= 0x2521044 || address >= 0x252105C && (ulong)address + (uint)length <= 0x2521064);
        if (!allowed)
        {
            throw new InvalidDataException("Bad Breath globals exclude its original task banks, descriptors and unused intervals.");
        }
        return checked((int)(address - BaseAddress));
    }
}
