using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;
using static Memoria.FF8.Runtime.NativeBattleGastricJuice128Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGastricJuice128State([property: JsonRequired] NativeBattleEffectArena Globals)
{
    public static NativeBattleGastricJuice128State FromColdProcess() => new(NativeBattleEffectArena.Clear());
    public static NativeBattleGastricJuice128State FromUnknown() => new(NativeBattleEffectArena.Clear().MarkUnknown(0, GlobalsLength));
    public void Validate()
    {
        Globals.Validate();
        foreach (NativeBattleEffectMemoryRegion region in Globals.Regions)
        {
            Require(checked(BaseAddress + (uint)region.Offset), region.Data.Length);
        }
        if (Globals.UnknownRegions.Any(region => region.Offset < 0 || region.Length < 0 || region.Offset > GlobalsLength - region.Length))
        {
            throw new InvalidDataException("Gastric Juice unknown globals leave their original scalar owner.");
        }
    }
    public NativeBattlePackedMatrix32 ReadCurrentMatrix()
    {
        (byte[] bytes, bool[] known) = Globals.ReadSnapshot(checked((int)(CurrentMatrixAddress - BaseAddress)), 32);
        return new(bytes.ToImmutableArray(), known.ToImmutableArray());
    }
    public byte[] Read(uint address, int length) => Globals.Read(Require(address, length), length);
    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));
    public ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
    public NativeBattleGastricJuice128State Write(uint address, ReadOnlySpan<byte> bytes) => this with { Globals = Globals.Write(Require(address, bytes.Length), bytes) };
    public NativeBattleGastricJuice128State WriteDword(uint address, uint value) => Write(address, BitConverter.GetBytes(value));
    public NativeBattleGastricJuice128State WriteWord(uint address, ushort value) => Write(address, BitConverter.GetBytes(value));
    public NativeBattleEffectArena ReadView(uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(Globals, Require(address, length), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    public NativeBattleGastricJuice128State WriteView(uint address, NativeBattleEffectArena source, int length) => this with { Globals = NativeBattleEffectNodeContextMemory.Copy(source, 0, Globals, Require(address, length), length) };
    private static int Require(uint address, int length)
    {
        ulong end = (ulong)address + (uint)Math.Max(length, 0);
        if (length < 0 || !(address >= CasterGlobal && end <= RootBank || address >= CursorGlobal && end <= SpriteDescriptor))
        {
            throw new InvalidDataException("Gastric Juice scalar bytes exclude its physical root task and all four descriptors.");
        }
        return checked((int)(address - BaseAddress));
    }
}
