using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleSonic139Controller;

namespace Memoria.FF8.Runtime;

public static class NativeBattleSonic139Morph
{
    public static NativeBattleSonic139Frame Morph6A9BB0(NativeBattleSonic139Frame frame, uint first, uint second, uint destination, int ratio, NativeBattleSonic139Inputs inputs, NativeBattleSonic139Resources resources)
    {
        int count = BinaryPrimitives.ReadInt32LittleEndian(Source(frame, first + 4, 4, resources).Read(0, 4));
        if (count <= 0 || count > NativeBattleEffectArena.Length / 8)
        {
            throw new InvalidDataException("Native Sonic XY morph requires its bounded positive vertex count.");
        }
        NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Native Sonic morph requires its retained geometry registers."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native Sonic morph requires its retained data registers."));
        registers = registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)ratio) } };
        for (int index = 0; index < count; index++)
        {
            uint offset = checked(8u + (uint)index * 8);
            NativeBattleEffectArena a = Source(frame, checked(first + offset), 8, resources);
            NativeBattleEffectArena b = Source(frame, checked(second + offset), 8, resources);
            int x = BinaryPrimitives.ReadUInt16LittleEndian(a.Read(0, 2));
            int y = BinaryPrimitives.ReadUInt16LittleEndian(a.Read(2, 2));
            int dx = BinaryPrimitives.ReadUInt16LittleEndian(b.Read(0, 2)) - x;
            int dy = BinaryPrimitives.ReadUInt16LittleEndian(b.Read(2, 2)) - y;
            registers = registers with { Colors = registers.Colors with { Ir = registers.Colors.Ir.SetItem(0, dx).SetItem(1, dy) } };
            registers = NativeBattleRaldo19BallRendering.Weighted(registers, false, inputs.Colors);
            NativeBattleEffectArena output = NativeBattleEffectNodeContextMemory.Copy(a, 4, ReadBytes(frame, checked(destination + (uint)index * 8), 8), 4, 4);
            output = output.Write(0, BitConverter.GetBytes(unchecked((ushort)(x + registers.Colors.Ir[0]!.Value)))).Write(2, BitConverter.GetBytes(unchecked((ushort)(y + registers.Colors.Ir[1]!.Value))));
            frame = WriteBytes(frame, checked(destination + (uint)index * 8), output, 8);
        }
        return frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };
    }

    internal static NativeBattleEffectArena Source(NativeBattleSonic139Frame frame, uint address, int length, NativeBattleSonic139Resources resources)
    {
        return address >= 0x11D72B0 && (ulong)address + (uint)length <= 0x11D7900 ? resources.ReadView(address, length) : ReadBytes(frame, address, length);
    }
}
