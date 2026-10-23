using System.Buffers.Binary;
using System.Text.Json.Serialization;
using static Memoria.FF8.Runtime.NativeBattleDisolvingAcid132Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDisolvingAcid132State([property: JsonRequired] NativeBattleEffectArena Globals)
{
    public static NativeBattleDisolvingAcid132State FromColdProcess() => new(NativeBattleEffectArena.Clear());
    public static NativeBattleDisolvingAcid132State FromUnknown() => new(NativeBattleEffectArena.Clear().MarkUnknown(0, GlobalsLength));
    public void Validate()
    {
        Globals.Validate();
        foreach (NativeBattleEffectMemoryRegion region in Globals.Regions)
        {
            Require(checked(BaseAddress + (uint)region.Offset), region.Data.Length);
        }
        if (Globals.UnknownRegions.Any(region => region.Offset < 0 || region.Length < 0 || region.Offset > GlobalsLength - region.Length))
        {
            throw new InvalidDataException("Disolving Acid unknown globals leave their original scalar owner.");
        }
    }
    public byte[] Read(uint address, int length) => Globals.Read(Require(address, length), length);
    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));
    public ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
    public NativeBattleDisolvingAcid132State Write(uint address, ReadOnlySpan<byte> bytes) => this with { Globals = Globals.Write(Require(address, bytes.Length), bytes) };
    public NativeBattleDisolvingAcid132State WriteDword(uint address, uint value) => Write(address, BitConverter.GetBytes(value));
    public NativeBattleDisolvingAcid132State WriteWord(uint address, ushort value) => Write(address, BitConverter.GetBytes(value));
    public NativeBattleEffectArena ReadView(uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(Globals, Require(address, length), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    public NativeBattleDisolvingAcid132State WriteView(uint address, NativeBattleEffectArena source, int length) => this with { Globals = NativeBattleEffectNodeContextMemory.Copy(source, 0, Globals, Require(address, length), length) };
    private static int Require(uint address, int length)
    {
        ulong end = (ulong)address + (uint)Math.Max(length, 0);
        if (length < 0 || !(address >= CasterGlobal && end <= RootBank || address >= TemporaryGlobal && end <= CursorGlobal + 4))
        {
            throw new InvalidDataException("Disolving Acid scalar bytes exclude its physical root task and all six descriptors.");
        }
        return checked((int)(address - BaseAddress));
    }
}
