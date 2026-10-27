using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;
using static Memoria.FF8.Runtime.NativeBattleResonance49Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleResonance49State([property: JsonRequired] NativeBattleEffectArena Globals, [property: JsonRequired] ImmutableArray<NativeBattleEffectArena?> InactiveBanks)
{
    private static readonly (uint Address, int Length)[] GlobalRanges = [(0x26464D8, 8), (0x26465DC, 8), (0x26466B0, 16), (0x26466E0, 0x104), (0x2646BE8, 4), (0x26472D0, 0x88), (0x2647760, 0x194), (0x2647900, 0x7E0), (0x26480E4, 12)];

    public static NativeBattleResonance49State FromColdProcess() => new(NativeBattleEffectArena.Clear(), Pools.Select(_ => (NativeBattleEffectArena?)NativeBattleEffectArena.Clear()).ToImmutableArray());

    public static NativeBattleResonance49State FromUnknown()
    {
        NativeBattleEffectArena globals = NativeBattleEffectArena.Clear();
        foreach ((uint address, int length) in GlobalRanges)
        {
            globals = globals.MarkUnknown(checked((int)(address - BaseAddress)), length);
        }
        return new(globals, Pools.Select(pool => (NativeBattleEffectArena?)NativeBattleEffectArena.Clear().MarkUnknown(0, checked(pool.Stride * pool.Capacity))).ToImmutableArray());
    }

    public void Validate()
    {
        Globals.Validate();
        foreach (NativeBattleEffectStorageRange region in Globals.EnumerateDataRanges())
        {
            Require(checked(BaseAddress + (uint)region.Offset), region.Length);
        }
        foreach (NativeBattleEffectStorageRange region in Globals.EnumerateUnknownRanges())
        {
            Require(checked(BaseAddress + (uint)region.Offset), region.Length);
        }
        if (InactiveBanks.IsDefault || InactiveBanks.Length != Pools.Length)
        {
            throw new InvalidDataException("Resonance requires its six exclusive retained hierarchy-bank owners.");
        }
        for (int index = 0; index < Pools.Length; index++)
        {
            if (InactiveBanks[index] is not { } bank)
            {
                continue;
            }
            bank.Validate();
            int length = checked(Pools[index].Stride * Pools[index].Capacity);
            bank.ValidateExtent(length);
        }
    }

    public byte[] Read(uint address, int length) => Globals.Read(Require(address, length), length);
    public byte[] ReadBytes(uint address, int length) => Read(address, length);
    public ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));
    public NativeBattleResonance49State Write(uint address, ReadOnlySpan<byte> bytes)
    {
        Globals.Write(Require(address, bytes.Length), bytes);
        return this;
    }
    public NativeBattleResonance49State WriteBytes(uint address, ReadOnlySpan<byte> bytes) => Write(address, bytes);
    public NativeBattleResonance49State WriteWord(uint address, ushort value)
    {
        Span<byte> encoded = stackalloc byte[2];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(encoded, value);
        return Write(address, encoded);
    }
    public NativeBattleResonance49State WriteDword(uint address, uint value)
    {
        Span<byte> encoded = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(encoded, value);
        return Write(address, encoded);
    }
    public NativeBattleEffectArena ReadView(uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(Globals, Require(address, length), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    public NativeBattleResonance49State WriteView(uint address, NativeBattleEffectArena source, int length)
    {
        NativeBattleEffectNodeContextMemory.Copy(source, 0, Globals, Require(address, length), length);
        return this;
    }

    private static int Require(uint address, int length)
    {
        ulong end = (ulong)address + (uint)Math.Max(length, 0);
        if (length < 0 || !GlobalRanges.Any(range => address >= range.Address && end <= (ulong)range.Address + (uint)range.Length))
        {
            throw new InvalidDataException("Resonance globals exclude hierarchy banks, descriptors, shared camera, COMMON storage and unobserved gaps.");
        }
        return checked((int)(address - BaseAddress));
    }
}
