using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleShivaLateChildren
{
    public static NativeBattleShivaEarlyTaskStep Advance5C6690(NativeBattleShivaEarlyTaskFrame frame, int slot, uint graphicsFlags, ImmutableArray<byte> spawnRows)
    {
        ValidateTask(frame, slot, 0x5C6690);
        if ((graphicsFlags & 0x201) != 0)
        {
            return new(frame, 0);
        }
        NativeBattleModulePoolTask task = frame.Pool.Slots[slot];
        int counter = Word(task, 0x0C);
        if (counter == 0)
        {
            NativeBattleShivaProcess process = frame.Memory.Shiva ?? throw new NotSupportedException("Native Shiva late spawning requires its actual local anchor and retained child pointer.");
            NativeBattleShivaRetainedVector anchor = process.LocalAnchor22BCFC8;
            task = task.WriteWord(0x10, unchecked((ushort)anchor.X)).WriteWord(0x12, unchecked((ushort)(anchor.Y - 150))).WriteWord(0x14, unchecked((ushort)anchor.Z)).WriteWord(0x1C, 4096).WriteWord(0x1E, 341).WriteWord(0x20, 0).WriteWord(0x22, 2048);
            frame = Write(frame, slot, task);
            int childSlot;
            (frame, childSlot) = Append(frame, 0x5C6870);
            NativeBattleModulePoolTask child = frame.Pool.Slots[childSlot].WriteWord(0x0C, 0).WriteDword(0x10, task.ReadDword(0x10)).WriteDword(0x14, task.ReadDword(0x14)).WriteWord(0x18, (ushort)(Draw(ref frame) % 4096)).WriteWord(0x1A, unchecked((ushort)(-80 - Draw(ref frame) % 30))).WriteWord(0x1C, task.ReadWord(0x1C)).WriteWord(0x1E, task.ReadWord(0x1E)).WriteWord(0x20, task.ReadWord(0x20)).WriteWord(0x22, task.ReadWord(0x22));
            frame = Write(frame, childSlot, child);
            uint pointer = checked(frame.Pool.BankAddress + (uint)(childSlot * frame.Pool.Stride));
            frame = frame with { Memory = frame.Memory with { Shiva = process with { SpawnedChildD32508 = pointer } } };
            (frame, childSlot) = Append(frame, 0x5C6BB0);
            child = frame.Pool.Slots[childSlot].WriteWord(0x0C, 0).WriteDword(0x10, task.ReadDword(0x10)).WriteDword(0x14, task.ReadDword(0x14)).WriteWord(0x18, (ushort)(Draw(ref frame) % 4096)).WriteWord(0x1A, (ushort)(Draw(ref frame) % 50 + 60));
            frame = Write(frame, childSlot, child);
        }
        else if (counter == 1)
        {
            if (spawnRows.IsDefault || spawnRows.Length != 16)
            {
                throw new InvalidDataException("Native Shiva late spawning requires its four original signed delay/Z rows.");
            }
            for (int index = 0; index < 4; index++)
            {
                int childSlot;
                (frame, childSlot) = Append(frame, 0x5C69E0);
                short delay = BinaryPrimitives.ReadInt16LittleEndian(spawnRows.AsSpan().Slice(index * 4));
                short z = BinaryPrimitives.ReadInt16LittleEndian(spawnRows.AsSpan().Slice(index * 4 + 2));
                NativeBattleModulePoolTask child = frame.Pool.Slots[childSlot].WriteWord(0x0C, 0).WriteWord(0x0E, unchecked((ushort)delay)).WriteWord(0x10, task.ReadWord(0x10)).WriteWord(0x12, task.ReadWord(0x12)).WriteWord(0x14, unchecked((ushort)(task.ReadWord(0x14) + z))).WriteWord(0x18, (ushort)(Draw(ref frame) % 4096)).WriteWord(0x1A, (ushort)(Draw(ref frame) % 190 + 120)).WriteWord(0x20, (ushort)(Draw(ref frame) % 4608 + 7680));
                frame = Write(frame, childSlot, child);
            }
        }
        return Increment(frame, slot, 32);
    }

    public static NativeBattleShivaEarlyTaskStep Advance5C6870(NativeBattleShivaEarlyTaskFrame frame, int slot, uint graphicsFlags, NativeBattleActionMatrix viewWorld, NativeBattleStaticMeshResource mesh, Func<NativeBattleShivaEarlyTaskFrame, NativeBattleShivaModelMeshDraw, NativeBattleShivaEarlyTaskFrame> drawMesh)
    {
        ValidateTask(frame, slot, 0x5C6870);
        frame = Draw(frame, slot, viewWorld, mesh, 0xD21B9C, 22, 409, false, drawMesh);
        if ((graphicsFlags & 0x201) != 0)
        {
            return new(frame, 0);
        }
        NativeBattleModulePoolTask task = frame.Pool.Slots[slot];
        short scale = unchecked((short)(task.ReadWord(0x1C) + task.ReadWord(0x1E)));
        short height = unchecked((short)(task.ReadWord(0x20) + task.ReadWord(0x22)));
        task = task.WriteWord(0x1C, unchecked((ushort)Math.Min((int)scale, 28672))).WriteWord(0x20, unchecked((ushort)Math.Min((int)height, 28672))).WriteWord(0x18, unchecked((ushort)(task.ReadWord(0x18) + task.ReadWord(0x1A))));
        return Increment(Write(frame, slot, task), slot, 32);
    }

    public static NativeBattleShivaEarlyTaskStep Advance5C69E0(NativeBattleShivaEarlyTaskFrame frame, int slot, uint graphicsFlags, NativeBattleActionMatrix viewWorld, NativeBattleStaticMeshResource mesh, Func<NativeBattleShivaEarlyTaskFrame, NativeBattleShivaModelMeshDraw, NativeBattleShivaEarlyTaskFrame> drawMesh)
    {
        ValidateTask(frame, slot, 0x5C69E0);
        NativeBattleModulePoolTask task = frame.Pool.Slots[slot];
        bool paused = (graphicsFlags & 0x201) != 0;
        if (Word(task, 0x0E) > 0)
        {
            if (!paused)
            {
                frame = Write(frame, slot, task.WriteWord(0x0E, unchecked((ushort)(task.ReadWord(0x0E) - 1))));
            }
            return new(frame, 0);
        }
        if (Word(task, 0x0C) == 0)
        {
            uint pointer = frame.Memory.Shiva?.SpawnedChildD32508 ?? throw new NotSupportedException("Native Shiva delayed mesh requires its actual retained child task pointer.");
            long offset = (long)pointer - frame.Pool.BankAddress;
            if (offset < 0 || offset >= (long)frame.Pool.Stride * frame.Pool.Slots.Length || offset % frame.Pool.Stride != 0)
            {
                throw new InvalidDataException("Native Shiva retained child pointer is not a physical slot in its current task bank.");
            }
            int scale = (Draw(ref frame) % 2048 + Word(frame.Pool.Slots[(int)(offset / frame.Pool.Stride)], 0x1C)) / 5;
            task = task.WriteWord(0x1C, unchecked((ushort)scale)).WriteWord(0x1E, unchecked((ushort)scale));
            frame = Write(frame, slot, task);
        }
        frame = Draw(frame, slot, viewWorld, mesh, 0xD3138C, 12, 682, true, drawMesh);
        if (paused)
        {
            return new(frame, 0);
        }
        task = frame.Pool.Slots[slot];
        int speed = Word(task, 0x1A);
        int velocity = Word(task, 0x1E);
        task = task.WriteWord(0x18, unchecked((ushort)(task.ReadWord(0x18) + task.ReadWord(0x1A)))).WriteWord(0x1A, unchecked((ushort)(speed - (speed >> 3)))).WriteWord(0x1C, unchecked((ushort)(task.ReadWord(0x1C) + task.ReadWord(0x1E)))).WriteWord(0x1E, unchecked((ushort)(velocity - velocity / 3)));
        return Increment(Write(frame, slot, task), slot, 18);
    }

    private static NativeBattleShivaEarlyTaskFrame Draw(NativeBattleShivaEarlyTaskFrame frame, int slot, NativeBattleActionMatrix view, NativeBattleStaticMeshResource mesh, uint address, int fadeStart, int fadeStep, bool delayed, Func<NativeBattleShivaEarlyTaskFrame, NativeBattleShivaModelMeshDraw, NativeBattleShivaEarlyTaskFrame> drawMesh)
    {
        ArgumentNullException.ThrowIfNull(drawMesh);
        if (mesh.Address != address)
        {
            throw new InvalidDataException("Native Shiva late child mesh differs from its original resource.");
        }
        NativeBattleModulePoolTask task = frame.Pool.Slots[slot];
        NativeBattleActionMatrix local = delayed ? NativeBattleGfMatrices.Euler56CD50(new(1024, Word(task, 0x18), 0)) : NativeBattleGfMatrices.Euler56CE30(new(0, 0, Word(task, 0x18)));
        local = NativeBattlePoseMatrices.Scale56BEF0(local, Word(task, 0x1C), Word(task, 0x20), Word(task, 0x1C)) with { X = Word(task, 0x10), Y = Word(task, 0x12), Z = Word(task, 0x14) };
        NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.Compose56C2F0(view, local);
        int counter = Word(task, 0x0C);
        NativeBattleModuleMeshContext context = new(counter < fadeStart ? 0x33u : 0xF3u, 0, counter < fadeStart ? null : (counter - fadeStart) * fadeStep, null, null, null, 17, 2);
        return drawMesh(frame, new(mesh, matrix, context));
    }

    private static NativeBattleShivaEarlyTaskFrame Write(NativeBattleShivaEarlyTaskFrame frame, int slot, NativeBattleModulePoolTask task) => frame with { Pool = frame.Pool.WriteSlot(slot, task) };
    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));

    private static NativeBattleShivaEarlyTaskStep Increment(NativeBattleShivaEarlyTaskFrame frame, int slot, int limit)
    {
        NativeBattleModulePoolTask task = frame.Pool.Slots[slot].WriteWord(0x0C, unchecked((ushort)(frame.Pool.Slots[slot].ReadWord(0x0C) + 1)));
        frame = Write(frame, slot, task);
        return new(frame, Word(task, 0x0C) >= limit ? 2 : 0);
    }

    private static ushort Draw(ref NativeBattleShivaEarlyTaskFrame frame)
    {
        NativeCrtRandomState random = frame.Random.Draw(out ushort value);
        frame = frame with { Random = random };
        return value;
    }

    private static (NativeBattleShivaEarlyTaskFrame Frame, int Slot) Append(NativeBattleShivaEarlyTaskFrame frame, uint callback)
    {
        (NativeBattleEffectTaskPool pool, int slot) = frame.Pool.Append(callback);
        return (frame with { Pool = pool }, slot);
    }

    private static void ValidateTask(NativeBattleShivaEarlyTaskFrame frame, int slot, uint callback)
    {
        frame.Pool.Validate();
        if (frame.Pool.DescriptorAddress != 0x22BC198 || frame.Pool.BankAddress != 0x22BC1A8 || frame.Pool.Stride != 0x24 || frame.Pool.Slots.Length != 100 || (uint)slot >= frame.Pool.Slots.Length || frame.Pool.Slots[slot].Callback != callback || (frame.Pool.Slots[slot].Flags & 1) == 0)
        {
            throw new InvalidDataException("Native Shiva late callback requires its allocated original physical task.");
        }
    }
}
