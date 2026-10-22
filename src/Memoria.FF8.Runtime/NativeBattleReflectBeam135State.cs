using System.Buffers.Binary;
using System.Text.Json.Serialization;
using static Memoria.FF8.Runtime.NativeBattleReflectBeam135Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleReflectBeam135State([property: JsonRequired] NativeBattleEffectArena Globals)
{
    public static NativeBattleReflectBeam135State FromColdProcess() => new(NativeBattleEffectArena.Clear());
    public static NativeBattleReflectBeam135State FromUnknown() => new(NativeBattleEffectArena.Clear().MarkUnknown(0, GlobalsLength));
    public void Validate()
    {
        Globals.Validate();
        foreach (NativeBattleEffectMemoryRegion region in Globals.Regions)
        {
            Require(checked(BaseAddress + (uint)region.Offset), region.Data.Length);
        }
        if (Globals.UnknownRegions.Any(region => region.Offset < 0 || region.Length < 0 || region.Offset > GlobalsLength - region.Length))
        {
            throw new InvalidDataException("Reflect Beam unknown globals leave their original scalar and matrix owner.");
        }
    }
    public byte[] Read(uint address, int length) => Globals.Read(Require(address, length), length);
    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));
    public ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
    public NativeBattleReflectBeam135State Write(uint address, ReadOnlySpan<byte> bytes) => this with { Globals = Globals.Write(Require(address, bytes.Length), bytes) };
    public NativeBattleReflectBeam135State WriteDword(uint address, uint value) => Write(address, BitConverter.GetBytes(value));
    public NativeBattleReflectBeam135State WriteWord(uint address, ushort value) => Write(address, BitConverter.GetBytes(value));
    public NativeBattleEffectArena ReadView(uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(Globals, Require(address, length), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    public NativeBattleReflectBeam135State WriteView(uint address, NativeBattleEffectArena source, int length) => this with { Globals = NativeBattleEffectNodeContextMemory.Copy(source, 0, Globals, Require(address, length), length) };
    private static int Require(uint address, int length)
    {
        ulong end = (ulong)address + (uint)Math.Max(length, 0);
        if (length < 0 || !(address >= 0x2520DD0 && end <= 0x2520DE0 || address >= 0x2520DF4 && end <= 0x2520DF8 || address >= 0x2520E48 && end <= 0x2520E68))
        {
            throw new InvalidDataException("Reflect Beam scalar and matrix bytes exclude its physical root task and all five descriptors.");
        }
        return checked((int)(address - BaseAddress));
    }
}
