namespace Memoria.FF8.Runtime;

public sealed record NativeBattleShivaBurstDraw(NativeBattleStaticMeshResource Resource, NativeBattleActionMatrix Matrix, NativeBattleModuleMeshContext Context);

public static class NativeBattleShivaBurstTask
{
    public static NativeBattleShivaEarlyTaskStep Advance5C4900(NativeBattleShivaEarlyTaskFrame frame, int slot, uint graphicsFlags, NativeBattleActionMatrix worldView, NativeBattleStaticMeshResource mesh, Func<NativeBattleShivaEarlyTaskFrame, NativeBattleShivaBurstDraw, NativeBattleShivaEarlyTaskFrame> draw572200)
    {
        frame.Pool.Validate();
        if (frame.Pool.DescriptorAddress != 0x22BC198 || frame.Pool.BankAddress != 0x22BC1A8 || frame.Pool.Stride != 0x24 || frame.Pool.Slots.Length != 100 || (uint)slot >= frame.Pool.Slots.Length || frame.Pool.Slots[slot] is not { Callback: 0x5C4900 } task || (task.Flags & 1) == 0)
        {
            throw new InvalidDataException("Native Shiva burst requires its allocated5C4900 task in the original child bank.");
        }
        if (Word(task, 0x0E) > 0)
        {
            if ((graphicsFlags & 0x201) == 0)
            {
                task = task.WriteWord(0x0E, unchecked((ushort)(task.ReadWord(0x0E) - 1)));
                frame = frame with { Pool = frame.Pool.WriteSlot(slot, task) };
            }
            return new(frame, 0);
        }
        ArgumentNullException.ThrowIfNull(draw572200);
        if (mesh.Address != 0xD3138C || mesh.Bytes.IsDefaultOrEmpty)
        {
            throw new InvalidDataException("Native Shiva burst requires its originalD3138C mesh.");
        }
        int counter = Word(task, 0x0C);
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(new(0, Word(task, 0x18), 0));
        local = NativeBattlePoseMatrices.Scale56BEF0(local, Word(task, 0x1C), Word(task, 0x20), Word(task, 0x1C)) with { X = Word(task, 0x10), Y = Word(task, 0x12), Z = Word(task, 0x14) };
        NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.Compose56C2F0(worldView, local);
        NativeBattleModuleMeshContext context = new(counter < 8 ? 0x33u : 0xF3u, 0, counter < 8 ? null : (counter - 8) * 682, null, null, null, 17, 2);
        frame = draw572200(frame, new(mesh, matrix, context));
        if ((graphicsFlags & 0x201) != 0)
        {
            return new(frame, 0);
        }
        task = frame.Pool.Slots[slot];
        int speed = Word(task, 0x1A);
        int velocity = Word(task, 0x1E);
        int divisor = Word(task, 0x22);
        if (divisor == 0)
        {
            throw new NotSupportedException("Native Shiva burst scale decay requires its nonzero signed divisor.");
        }
        task = task.WriteWord(0x18, unchecked((ushort)(task.ReadWord(0x18) + task.ReadWord(0x1A)))).WriteWord(0x1A, unchecked((ushort)(speed - (speed >> 3)))).WriteWord(0x1C, unchecked((ushort)(task.ReadWord(0x1C) + task.ReadWord(0x1E)))).WriteWord(0x1E, unchecked((ushort)(velocity - velocity / divisor))).WriteWord(0x0C, unchecked((ushort)(task.ReadWord(0x0C) + 1)));
        frame = frame with { Pool = frame.Pool.WriteSlot(slot, task) };
        return new(frame, Word(task, 0x0C) >= 14 ? 2 : 0);
    }

    public static NativeBattleGfGeometryBatch Emit572200(NativeBattleShivaBurstDraw draw, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        return NativeBattleModulePackets.EmitBatch572200(draw.Resource.Bytes, 0, default, draw.Matrix, projection, draw.Context, colors);
    }

    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
}
