using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;
using static Memoria.FF8.Runtime.NativeBattleMiniPulseCannon286Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleMiniPulseCannon286State([property: JsonRequired] NativeBattleEffectArena Globals, [property: JsonRequired] NativeBattleEffectArena? RootBankStorage)
{
    private static readonly (uint Address, int Length)[] GlobalRanges = [(BaseAddress, 12), (CursorGlobal, 8)];

    public static NativeBattleMiniPulseCannon286State FromColdProcess() => new(NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear());

    public static NativeBattleMiniPulseCannon286State FromUnknown()
    {
        NativeBattleEffectArena globals = NativeBattleEffectArena.Clear();
        foreach ((uint address, int length) in GlobalRanges)
        {
            globals = globals.MarkUnknown(checked((int)(address - BaseAddress)), length);
        }
        return new(globals, NativeBattleEffectArena.Clear().MarkUnknown(0, RootBankLength));
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
        if (RootBankStorage is { } bank)
        {
            bank.Validate();
            bank.ValidateExtent(RootBankLength);
        }
    }

    public byte[] Read(uint address, int length) => Globals.Read(Require(address, length), length);
    public byte[] ReadBytes(uint address, int length) => Read(address, length);
    public ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));
    public NativeBattleMiniPulseCannon286State Write(uint address, ReadOnlySpan<byte> bytes)
    {
        Globals.Write(Require(address, bytes.Length), bytes);
        return this;
    }
    public NativeBattleMiniPulseCannon286State WriteBytes(uint address, ReadOnlySpan<byte> bytes) => Write(address, bytes);
    public NativeBattleMiniPulseCannon286State WriteWord(uint address, ushort value)
    {
        Span<byte> encoded = stackalloc byte[2];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(encoded, value);
        return Write(address, encoded);
    }
    public NativeBattleMiniPulseCannon286State WriteDword(uint address, uint value)
    {
        Span<byte> encoded = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(encoded, value);
        return Write(address, encoded);
    }
    public NativeBattleEffectArena ReadView(uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(Globals, Require(address, length), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    public NativeBattleMiniPulseCannon286State WriteView(uint address, NativeBattleEffectArena source, int length)
    {
        NativeBattleEffectNodeContextMemory.Copy(source, 0, Globals, Require(address, length), length);
        return this;
    }

    private static int Require(uint address, int length)
    {
        ulong end = (ulong)address + (uint)Math.Max(length, 0);
        if (length < 0 || !GlobalRanges.Any(range => address >= range.Address && end <= (ulong)range.Address + (uint)range.Length))
        {
            throw new InvalidDataException("Mini Pulse Cannon globals exclude module banks, descriptors, COMMON storage and unobserved gaps.");
        }
        return checked((int)(address - BaseAddress));
    }
}
