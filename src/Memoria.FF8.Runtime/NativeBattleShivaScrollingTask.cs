namespace Memoria.FF8.Runtime;

public sealed record NativeBattleShivaScrollingDraw(NativeBattleStaticMeshResource Resource, NativeBattleActionMatrix Matrix, NativeBattleShivaScrollingContext Context);

public static class NativeBattleShivaScrollingTask
{
    public static NativeBattleShivaEarlyTaskFrame Initialize5C6FA0(NativeBattleShivaEarlyTaskFrame frame)
    {
        NativeBattleEffectTaskPool pool = frame.Pool;
        (pool, int slot) = pool.Append(0x5C6FF0);
        NativeBattleModulePoolTask task = pool.Slots[slot].WriteWord(0x0C, 0).WriteWord(0x10, 0).WriteWord(0x12, 0).WriteWord(0x14, unchecked((ushort)-2500)).WriteWord(0x1C, 2730).WriteWord(0x1E, 2730).WriteWord(0x20, 1877).WriteWord(0x22, 1877);
        return frame with { Pool = pool.WriteSlot(slot, task) };
    }

    public static NativeBattleShivaEarlyTaskStep Advance5C6FF0(NativeBattleShivaEarlyTaskFrame frame, int slot, uint graphicsFlags, NativeBattleActionMatrix viewWorld, NativeBattleStaticMeshResource mesh, Func<NativeBattleShivaEarlyTaskFrame, NativeBattleShivaScrollingDraw, NativeBattleShivaEarlyTaskFrame> draw)
    {
        frame.Pool.Validate();
        if ((uint)slot >= frame.Pool.Slots.Length || frame.Pool.Slots[slot] is not { Callback: 0x5C6FF0 } task || (task.Flags & 1) == 0 || mesh.Address != 0xD2A50C)
        {
            throw new InvalidDataException("Native Shiva scrolling requires its actual allocated backdrop task and source.");
        }
        int counter = Word(0x0C);
        NativeBattleActionMatrix local = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(0, 2048, 0)), Word(0x1C), Word(0x20), Word(0x1C)) with { X = Word(0x10), Y = Word(0x12), Z = Word(0x14) };
        NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.Compose56C2F0(viewWorld, local);
        frame = draw(frame, new(mesh, matrix, new(counter < 22 ? 0x33u : 0xF3u, counter < 22 ? null : (counter - 22) << 9, 0, counter << 4, 17, 2, new(64, 0, 64, 128))));
        if ((graphicsFlags & 0x201) != 0)
        {
            return new(frame, 0);
        }
        task = frame.Pool.Slots[slot];
        if (counter < 6)
        {
            task = task.WriteWord(0x1C, unchecked((ushort)(task.ReadWord(0x1C) + task.ReadWord(0x1E))));
            task = task.WriteWord(0x1E, unchecked((ushort)(Word(0x1E) - Word(0x1E) / 3)));
            task = task.WriteWord(0x20, unchecked((ushort)(task.ReadWord(0x20) + task.ReadWord(0x22))));
            task = task.WriteWord(0x22, unchecked((ushort)(Word(0x22) - Word(0x22) / 3)));
        }
        task = task.WriteWord(0x0C, unchecked((ushort)(task.ReadWord(0x0C) + 1)));
        frame = frame with { Pool = frame.Pool.WriteSlot(slot, task) };
        return new(frame, unchecked((short)task.ReadWord(0x0C)) >= 30 ? 2 : 0);

        short Word(int offset) => unchecked((short)task.ReadWord(offset));
    }
}
