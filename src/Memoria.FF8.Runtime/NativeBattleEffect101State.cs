using System.Buffers.Binary;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect101State([property: JsonRequired] NativeBattleEffectArena Globals)
{
    public static NativeBattleEffect101State FromColdProcess() => new(NativeBattleEffectArena.Clear());
    public static NativeBattleEffect101State FromUnknown() => new(NativeBattleEffectArena.Clear().MarkUnknown(0, NativeBattleEffect101Profile.GlobalsLength));
    public void Validate()
    {
        Globals.Validate();
        foreach (NativeBattleEffectMemoryRegion region in Globals.Regions)
        {
            Require(checked(NativeBattleEffect101Profile.BaseAddress + (uint)region.Offset), region.Data.Length);
        }
        if (Globals.UnknownRegions.Any(region => region.Offset < 0 || region.Length < 0 || region.Offset > NativeBattleEffect101Profile.GlobalsLength - region.Length))
        {
            throw new InvalidDataException("Effect101 retained globals leave their physical scalar owner.");
        }
    }
    public byte[] Read(uint address, int length) => Globals.Read(Require(address, length), length);
    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));
    public ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
    public NativeBattleEffect101State Write(uint address, ReadOnlySpan<byte> bytes) => this with { Globals = Globals.Write(Require(address, bytes.Length), bytes) };
    public NativeBattleEffect101State WriteDword(uint address, uint value) => Write(address, BitConverter.GetBytes(value));
    public NativeBattleEffect101State WriteWord(uint address, ushort value) => Write(address, BitConverter.GetBytes(value));
    public NativeBattleEffectArena ReadView(uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(Globals, Require(address, length), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    public NativeBattleEffect101State WriteView(uint address, NativeBattleEffectArena source, int length) => this with { Globals = NativeBattleEffectNodeContextMemory.Copy(source, 0, Globals, Require(address, length), length) };
    private static int Require(uint address, int length)
    {
        ulong end = (ulong)address + (uint)Math.Max(0, length);
        if (length < 0 || address < 0x2544E10 || end > 0x2544E74 || address < 0x2544E6C && end > 0x2544E38)
        {
            throw new InvalidDataException("Effect101 globals exclude its actual task bank and descriptors.");
        }
        return checked((int)(address - NativeBattleEffect101Profile.BaseAddress));
    }
}
