using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

internal sealed class NativeBattleApocalypse221BreakupMemory(NativeBattleWaterExecutionFrame frame)
{
    public NativeBattleWaterExecutionFrame Frame { get; set; } = frame;
    public uint Slot => CoreDword(Frame, 0x27973E8);
    public uint Manager => CoreDword(Frame, 0x27973EC);
    public uint Temporary => Frame.TemporaryFrame.Address;
    public byte[] Read(uint address, int length)
    {
        if (address >= Temporary && (ulong)address + (uint)length <= (ulong)Temporary + 0x180)
        {
            return Frame.ReadTemporary(checked((int)(address - Temporary)), length);
        }
        if (address >= NativeBattleScriptedEffectMemory.Address && (ulong)address + (uint)length <= (ulong)NativeBattleScriptedEffectMemory.Address + NativeBattleScriptedEffectMemory.Length)
        {
            return Frame.Core.Read(address, length);
        }
        return Frame.Arena.Read(ArenaOffset(address, length), length);
    }
    public byte Byte(uint address) => Read(address, 1)[0];
    public ushort Word(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
    public short Signed(uint address) => unchecked((short)Word(address));
    public uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));
    public void Put(uint address, ReadOnlySpan<byte> data)
    {
        if (address >= Temporary && (ulong)address + (uint)data.Length <= (ulong)Temporary + 0x180)
        {
            Frame = Frame.WriteTemporary(checked((int)(address - Temporary)), data.ToArray());
        }
        else if (address >= NativeBattleScriptedEffectMemory.Address && (ulong)address + (uint)data.Length <= (ulong)NativeBattleScriptedEffectMemory.Address + NativeBattleScriptedEffectMemory.Length)
        {
            Frame = Frame with { Core = Frame.Core.Write(address, data) };
        }
        else
        {
            Frame = Frame with { Arena = Frame.Arena.Write(ArenaOffset(address, data.Length), data) };
        }
    }
    public void Byte(uint address, byte value) => Put(address, new[] { value });
    public void Word(uint address, int value) => Put(address, BitConverter.GetBytes(unchecked((ushort)value)));
    public void Dword(uint address, uint value) => Put(address, BitConverter.GetBytes(value));
    public uint Allocate(uint count)
    {
        (NativeBattleWaterExecutionFrame result, uint address) = NativeBattleWaterParticleCommands.AllocateB656E0(Frame, count);
        Frame = result;
        return address;
    }
    public int Random(short magnitude)
    {
        NativeBattleWaterVectorRandom sample = NativeBattleWaterVectorCommands.RandomB65740(magnitude, Frame.Random);
        Frame = Frame with { Random = sample.Random };
        return sample.Value;
    }
    public static int Divide(int numerator, int denominator)
    {
        if (denominator == 0 || numerator == int.MinValue && denominator == -1)
        {
            throw new NotSupportedException("Native Apocalypse breakup division faults on its actual signed operands.");
        }
        return numerator / denominator;
    }
}
