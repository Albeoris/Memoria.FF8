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
        foreach (NativeBattleEffectMemoryRegion region in Globals.Regions)
        {
            Require(checked(NativeBattleEnergyBomber217Profile.BaseAddress + (uint)region.Offset), region.Data.Length);
        }
        if (Globals.UnknownRegions.Any(region => region.Offset < 0 || region.Length < 0 || region.Offset > NativeBattleEnergyBomber217Profile.GlobalsLength - region.Length))
        {
            throw new InvalidDataException("EnergyBomber217 retained globals leave their physical scalar owner.");
        }
    }
    public byte[] Read(uint address, int length) => Globals.Read(Require(address, length), length);
    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));
    public ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
    public NativeBattleEnergyBomber217State Write(uint address, ReadOnlySpan<byte> bytes) => this with { Globals = Globals.Write(Require(address, bytes.Length), bytes) };
    public NativeBattleEnergyBomber217State WriteDword(uint address, uint value) => Write(address, BitConverter.GetBytes(value));
    public NativeBattleEnergyBomber217State WriteWord(uint address, ushort value) => Write(address, BitConverter.GetBytes(value));
    public NativeBattleEffectArena ReadView(uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(Globals, Require(address, length), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    public NativeBattleEnergyBomber217State WriteView(uint address, NativeBattleEffectArena source, int length) => this with { Globals = NativeBattleEffectNodeContextMemory.Copy(source, 0, Globals, Require(address, length), length) };
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
