namespace Memoria.FF8.Runtime;

public abstract record NativeBattleDrain39Operation;
public sealed record NativeBattleDrain39StageFadeOperation(short Fade, bool ClearRgb) : NativeBattleDrain39Operation;
public sealed record NativeBattleDrain39PacketOperation(NativeBattleModulePacketBatch Batch) : NativeBattleDrain39Operation;

public static class NativeBattleDrain39Simple
{
    public static NativeBattleDrain39LeafStep Advance(NativeBattleDrain39Frame frame, uint address, NativeBattleDrain39Resources resources, Func<NativeBattleDrain39Frame, NativeBattleDrain39Operation, NativeBattleDrain39Frame> execute)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        if (task.Callback is 0x851710 or 0x8568A0)
        {
            NativeBattleStageFadeStep step = NativeBattleStageFadeTask.Advance(task, task.Callback == 0x851710);
            task = step.Task;
            if (step.Write is { } write)
            {
                Execute(new NativeBattleDrain39StageFadeOperation(write.Fade, write.ClearRgb));
            }
        }
        else if (task.Callback == 0x851900)
        {
            Flash();
        }
        else
        {
            throw new NotSupportedException("Native Drain selected an unrelated stage/flash callback.");
        }
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        hierarchy = hierarchy.Write(address, task);
        bool complete = (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
        if (complete)
        {
            hierarchy = hierarchy.Complete8DC530(address);
        }
        return new(frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } }, complete ? 2 : 0);

        void Execute(NativeBattleDrain39Operation operation)
        {
            hierarchy = hierarchy.Write(address, task);
            frame = execute(frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } }, operation);
            hierarchy = new(frame.Effect.Pools);
            task = hierarchy.Read(address);
        }
        void Flash()
        {
            var program = task.ReadDword(0x30) switch
            {
                0x15D190C => resources.FirstFlash,
                0x15D1918 => resources.SecondFlash,
                _ => throw new NotSupportedException("Native Drain requires its selected original flash script.")
            };
            int index = unchecked((short)task.ReadWord(0x38));
            uint entry = Read(index);
            byte command = (byte)(entry >> 24);
            if (command == 255)
            {
                task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1));
                return;
            }
            if (command == 254)
            {
                if (unchecked((short)task.ReadWord(0x24)) >= (byte)(entry >> 16))
                {
                    index++;
                    task = task.WriteWord(0x38, unchecked((ushort)index)).WriteDword(0x34, Read(index));
                }
            }
            else
            {
                task = task.WriteDword(0x34, entry).WriteWord(0x38, unchecked((ushort)(index + 1)));
            }
            uint color = (task.ReadDword(0x34) & 0xFFFFFF) | 0x2A000000;
            NativeBattleGeometryPrimitive quad = new(7, [0x5000000, color, 0, 320, 216u << 16, (216u << 16) | 320], [0, 0, 0, 0]);
            NativeBattleGeometryPrimitive mode = new(7, [0x2000000, 0xE100002A, 0], [0, 0, 0, 0]);
            Execute(new NativeBattleDrain39PacketOperation(new([new(quad, 0, 1), new(mode, 24, 0)], 36)));

            uint Read(int current)
            {
                if ((uint)current >= program.Length)
                {
                    throw new InvalidDataException("Native Drain flash index leaves its actual DWORD script.");
                }
                return program[current];
            }
        }
    }
}
