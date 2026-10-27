using System.Buffers.Binary;
using System.Text.Json.Serialization;
using static Memoria.FF8.Runtime.NativeBattleShockwavePulsar260Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleShockwavePulsar260Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleShockwavePulsar260State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleShockwavePulsar260State([property: JsonRequired] NativeBattleEffectArena Globals)
{
    public const uint GlobalsAddress = 0x2508610;
    public const int GlobalsLength = 0xEC;
    public static NativeBattleShockwavePulsar260State FromColdProcess() => new(NativeBattleEffectArena.Clear());
    public static NativeBattleShockwavePulsar260State FromUnknown() => new(NativeBattleEffectArena.Clear().MarkUnknown(0, GlobalsLength));
    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Globals.Read(Require(address, 4), 4));
    public ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Globals.Read(Require(address, 2), 2));
    public NativeBattleShockwavePulsar260State WriteDword(uint address, uint value)
    {
        Span<byte> encoded = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(encoded, value);
        Globals.Write(Require(address, 4), encoded);
        return this;
    }
    public NativeBattleShockwavePulsar260State WriteWord(uint address, ushort value)
    {
        Span<byte> encoded = stackalloc byte[2];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(encoded, value);
        Globals.Write(Require(address, 2), encoded);
        return this;
    }
    public NativeBattleEffectArena ReadView(uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(Globals, Require(address, length), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    public NativeBattleShockwavePulsar260State WriteView(uint address, NativeBattleEffectArena view, int length)
    {
        NativeBattleEffectNodeContextMemory.Copy(view, 0, Globals, Require(address, length), length);
        return this;
    }
    public void Validate()
    {
        Globals.Validate();
        foreach (NativeBattleEffectStorageRange region in Globals.EnumerateDataRanges())
        {
            _ = Require(checked(GlobalsAddress + (uint)region.Offset), region.Length);
        }
        Globals.ValidateExtent(GlobalsLength);
    }
    private static int Require(uint address, int length)
    {
        bool first = address >= GlobalsAddress && (ulong)address + (uint)Math.Max(length, 0) <= ChildDescriptor;
        bool last = address >= RootBank + 16 && (ulong)address + (uint)Math.Max(length, 0) <= GlobalsAddress + GlobalsLength;
        if (length < 0 || !first && !last)
        {
            throw new InvalidDataException("Shockwave Pulsar globals exclude its original descriptors and task pools.");
        }
        return checked((int)(address - GlobalsAddress));
    }
}
