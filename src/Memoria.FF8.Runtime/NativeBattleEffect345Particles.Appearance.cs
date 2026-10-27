using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect345Controller;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect345Particles
{
    internal static NativeBattleEffect345Frame PublishAppearance70A460(NativeBattleEffect345Frame frame, NativeBattleEffectNodeMemory memory, NativeBattleEffectNodeBank bank, int particle)
    {
        int count = unchecked((sbyte)memory.Byte(particle + 0x1D8));
        if (count <= 0)
        {
            return frame;
        }
        NativeBattleEffectNodeMemoryWrite written = memory.FlushStorage();
        bank.Publish();
        frame = frame with { Memory = written.Memory, Effect = written.Effect, State = frame.State.ReplaceNodeBank(bank.Address, bank.Source) };
        memory.PublishProcessMemory(frame.Memory);
        NativeBattlePackedMatrix32 copiedCamera = Packed(ReadBytes(frame, frame.State.ReadDword(0x2557208), 32));
        int selector = unchecked((sbyte)memory.Byte(particle + 0x1D6));
        uint descriptor = BinaryPrimitives.ReadUInt32LittleEndian(bank.Read(unchecked(memory.ContextDword(0x224) + (uint)(selector * 4)), 4));
        short bias = BinaryPrimitives.ReadInt16LittleEndian(bank.Read(descriptor + 0x38, 2));
        for (int instance = 0; instance < count; instance++)
        {
            NativeBattleEffectArena basis = ReadBytes(frame, memory.Pointer(particle + 0x8C), 32);
            (frame, NativeBattleEffectArena composed) = MultiplyMatrix(frame, copiedCamera, basis);
            for (int lane = 0; lane < 9; lane++)
            {
                memory.Word(particle + 0xAC + lane * 2, BinaryPrimitives.ReadUInt16LittleEndian(composed.Read(lane * 2, 2)));
            }
            int[] point = new int[3];
            for (int axis = 0; axis < 3; axis++)
            {
                point[axis] = unchecked((int)memory.Dword(particle + 0xDC + instance * 16 + axis * 4)) / 65536;
                memory.Dword(particle + 0xA0 + axis * 4, unchecked((uint)point[axis]));
            }
            frame = LoadMatrix(frame, copiedCamera, false, true);
            NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.LoadVector(Registers(frame), 0, new(unchecked((short)point[0]), unchecked((short)point[1]), unchecked((short)point[2])), point[2] < 0 ? (short)-1 : (short)0);
            registers = NativeBattleGeometry.Transform(registers);
            frame = Publish(frame, registers);
            for (int axis = 0; axis < 3; axis++)
            {
                int coordinate = registers.Colors.Mac[axis] ?? throw new NotSupportedException("Effect345 appearance requires its actual full MAC output.");
                memory.Dword(particle + 0xC0 + axis * 4, unchecked((uint)coordinate));
                if (count != 1)
                {
                    memory.Word(particle + 0x19C + instance * 8 + axis * 2, unchecked((ushort)(coordinate + (axis == 2 ? bias : 0))));
                }
            }
            if (count == 1)
            {
                memory.Dword(particle + 0xC8, unchecked(memory.Dword(particle + 0xC8) + (uint)(int)bias));
            }
        }
        memory.PublishProcessMemory(frame.Memory);

        return frame;
    }
}
