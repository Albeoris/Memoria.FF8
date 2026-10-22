using System.Buffers.Binary;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBeamLaser134State([property: JsonRequired] NativeBattleEffectArena Globals)
{
    public static NativeBattleBeamLaser134State FromColdProcess() => new(NativeBattleEffectArena.Clear());
    public static NativeBattleBeamLaser134State FromUnknown() => new(NativeBattleEffectArena.Clear().MarkUnknown(0, NativeBattleBeamLaser134Profile.GlobalsLength));
    public void Validate()
    {
        Globals.Validate();
        foreach (NativeBattleEffectMemoryRegion region in Globals.Regions)
        {
            Require(checked(NativeBattleBeamLaser134Profile.BaseAddress + (uint)region.Offset), region.Data.Length);
        }
        if (Globals.UnknownRegions.Any(region => region.Offset < 0 || region.Length < 0 || region.Offset > NativeBattleBeamLaser134Profile.GlobalsLength - region.Length))
        {
            throw new InvalidDataException("BeamLaser134 retained globals leave their physical scalar owner.");
        }
    }
    public byte[] Read(uint address, int length) => Globals.Read(Require(address, length), length);
    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));
    public ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
    public NativeBattleBeamLaser134State Write(uint address, ReadOnlySpan<byte> bytes) => this with { Globals = Globals.Write(Require(address, bytes.Length), bytes) };
    public NativeBattleBeamLaser134State WriteDword(uint address, uint value) => Write(address, BitConverter.GetBytes(value));
    public NativeBattleBeamLaser134State WriteWord(uint address, ushort value) => Write(address, BitConverter.GetBytes(value));
    public NativeBattleEffectArena ReadView(uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(Globals, Require(address, length), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    public NativeBattleBeamLaser134State WriteView(uint address, NativeBattleEffectArena source, int length) => this with { Globals = NativeBattleEffectNodeContextMemory.Copy(source, 0, Globals, Require(address, length), length) };
    private static int Require(uint address, int length)
    {
        ulong end = (ulong)address + (uint)Math.Max(0, length);
        if (length < 0 || address < 0x2520E68 || end > 0x2520EE8 || address < 0x2520E94 && end > 0x2520E80 || address < 0x2520EE8 && end > 0x2520E98)
        {
            throw new InvalidDataException("BeamLaser134 globals exclude its actual task bank and descriptors.");
        }
        return checked((int)(address - NativeBattleBeamLaser134Profile.BaseAddress));
    }
}
