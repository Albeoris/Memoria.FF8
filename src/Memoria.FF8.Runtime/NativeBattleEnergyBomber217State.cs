using System.Buffers.Binary;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEnergyBomber217State([property: JsonRequired] NativeBattleEffectArena Globals)
{
    public static NativeBattleEnergyBomber217State FromColdProcess() => new(NativeBattleEffectArena.Clear());
    public static NativeBattleEnergyBomber217State FromUnknown() => new(NativeBattleEffectArena.Clear().MarkUnknown(0, NativeBattleEnergyBomber217Profile.GlobalsLength));
    public void Validate()
    {
        Globals.Validate();
        foreach (NativeBattleEffectStorageRange region in Globals.EnumerateDataRanges())
        {
            Require(checked(NativeBattleEnergyBomber217Profile.BaseAddress + (uint)region.Offset), region.Length);
        }
        Globals.ValidateExtent(NativeBattleEnergyBomber217Profile.GlobalsLength);
    }
    public byte[] Read(uint address, int length) => Globals.Read(Require(address, length), length);
    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));
    public ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
    public NativeBattleEnergyBomber217State Write(uint address, ReadOnlySpan<byte> bytes)
    {
        Globals.Write(Require(address, bytes.Length), bytes);
        return this;
    }
    public NativeBattleEnergyBomber217State WriteDword(uint address, uint value)
    {
        Span<byte> encoded = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(encoded, value);
        return Write(address, encoded);
    }
    public NativeBattleEnergyBomber217State WriteWord(uint address, ushort value)
    {
        Span<byte> encoded = stackalloc byte[2];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(encoded, value);
        return Write(address, encoded);
    }
    public NativeBattleEffectArena ReadView(uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(Globals, Require(address, length), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    public NativeBattleEnergyBomber217State WriteView(uint address, NativeBattleEffectArena source, int length)
    {
        NativeBattleEffectNodeContextMemory.Copy(source, 0, Globals, Require(address, length), length);
        return this;
    }
    private static int Require(uint address, int length)
    {
        ulong end = (ulong)address + (uint)Math.Max(0, length);
        if (length < 0 || address < 0x25087F0 || end > 0x2508870 || address < 0x2508844 && end > 0x2508800)
        {
            throw new InvalidDataException("EnergyBomber217 globals exclude its actual task bank and descriptors.");
        }
        return checked((int)(address - NativeBattleEnergyBomber217Profile.BaseAddress));
    }
}
