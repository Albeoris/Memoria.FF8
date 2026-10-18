using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGilgameshSwordTrailInitialization(NativeBattleEffectTaskPool Pool, NativeBattleGilgameshSwordTrailState State);
public sealed record NativeBattleGilgameshSwordTrailStep(NativeBattleGilgameshSwordTrailState State, NativeBattleModulePoolTask UpdatedTask, NativeBattleGfGeometryBatch Packets, int? PoolCursorAdvance, int GatheredCount, int Result);

/// <summary>5943A0/5943D0's retained reverse history and low32 fixed-point sword trail.</summary>
public static class NativeBattleGilgameshSwordTrail
{
    public static NativeBattleGilgameshSwordTrailInitialization Initialize5943A0(NativeBattleEffectTaskPool pool, NativeBattleGilgameshSwordTrailState state)
    {
        state.Validate();
        if (pool.DescriptorAddress != 0x21FFD58 || pool.BankAddress != 0x21FFD68 || pool.Stride != 36 || pool.Slots.Length != 100)
        {
            throw new InvalidDataException("Native5943A0 requires its actual raw child task bank.");
        }
        int slot;
        (pool, slot) = pool.Append(0x5943D0);
        pool = pool with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteWord(12, 0)) };
        NativeBattleEffectArena memory = state.Memory;
        for (int index = 0; index < 10; index++)
        {
            memory = memory.Write(NativeBattleGilgameshSwordTrailState.History + index * 20, new byte[4]);
        }
        return new(pool, state with { Memory = memory });
    }

    public static NativeBattleGilgameshSwordTrailStep Step5943D0(NativeBattleGilgameshSwordTrailState state, NativeBattleModulePoolTask task, int selector, Func<int, byte, short, NativeBattleVector> readBone502170, NativeBattleActionMatrix worldView, NativeBattleCameraProjection projection, NativeBattleScriptedGraphicsMemory? registers, NativeBattleSummonColors colors, NativeBattleGilgameshSwordTrailResources resources, uint graphicsFlags)
    {
        state.Validate();
        if (task.Payload.IsDefault || task.Payload.Length != 24 || (uint)selector >= 4)
        {
            throw new InvalidDataException("Native5943D0 requires its raw0x24-byte task and actual sword selector.");
        }
        int counter = unchecked((short)task.ReadWord(12));
        if (counter < 0)
        {
            throw new NotSupportedException("Native sword trail negative signed ring remainder leaves its admitted history owner.");
        }
        NativeBattleVector a = readBone502170(selector, 1, 0);
        NativeBattleVector originalB = readBone502170(selector, 1, 4096);
        var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(originalB.X - a.X, originalB.Y - a.Y, originalB.Z - a.Z);
        NativeBattleVector b = new(unchecked((short)(a.X + (unchecked(direction.X * 1400) >> 12))), unchecked((short)(a.Y + (unchecked(direction.Y * 1400) >> 12))), unchecked((short)(a.Z + (unchecked(direction.Z * 1400) >> 12))));
        int slot = counter % 10;
        int record = NativeBattleGilgameshSwordTrailState.History + slot * 20;
        NativeBattleEffectArena memory = state.Memory.Write(record, new byte[] { 1, 0, 0, 0 });
        memory = WritePoint(memory, record + 4, a).MarkUnknown(record + 10, 2);
        memory = WritePoint(memory, record + 12, b).MarkUnknown(record + 18, 2);
        int count = 0;
        for (int index = slot; count < 10; index = index == 0 ? 9 : index - 1)
        {
            int history = NativeBattleGilgameshSwordTrailState.History + index * 20;
            if (BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(history, 4)) == 0)
            {
                break;
            }
            memory = NativeBattleGilgameshSwordTrailState.CopyPoint(memory, history + 4, NativeBattleGilgameshSwordTrailState.GatherA + count * 8);
            memory = NativeBattleGilgameshSwordTrailState.CopyPoint(memory, history + 12, NativeBattleGilgameshSwordTrailState.GatherB + count * 8);
            count++;
        }
        NativeBattleGfGeometryBatch packets = new([], 0);
        int? poolCursor = null;
        if (count > 1)
        {
            resources.Validate();
            NativeBattleEffectArena work = NativeBattleEffectArena.Clear().MarkUnknown(0, 0x190);
            work = NativeBattleGilgameshSwordTrailCurve.Prepare571620(count, memory, NativeBattleGilgameshSwordTrailState.GatherA, work);
            for (int index = 0; index < 32; index++)
            {
                memory = NativeBattleGilgameshSwordTrailCurve.Evaluate571690(count, work, index * 4096 / 31, memory, NativeBattleGilgameshSwordTrailState.CurveA + index * 8);
            }
            work = NativeBattleGilgameshSwordTrailCurve.Prepare571620(count, memory, NativeBattleGilgameshSwordTrailState.GatherB, work);
            for (int index = 0; index < 32; index++)
            {
                memory = NativeBattleGilgameshSwordTrailCurve.Evaluate571690(count, work, index * 4096 / 31, memory, NativeBattleGilgameshSwordTrailState.CurveB + index * 8);
            }
            NativeBattleGilgameshSwordTrailPacketResult drawn = NativeBattleGilgameshSwordTrailPackets.Emit(state with { Memory = memory }, worldView, projection, registers, colors, resources.Colors[selector]);
            packets = drawn.Batch;
            poolCursor = drawn.PoolCursorAdvance;
        }
        state = state with { Memory = memory };
        if ((graphicsFlags & 0x201) != 0)
        {
            return new(state, task, packets, poolCursor, count, 0);
        }
        task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        return new(state, task, packets, poolCursor, count, unchecked((short)task.ReadWord(12)) >= 10 ? 2 : 0);
    }

    private static NativeBattleEffectArena WritePoint(NativeBattleEffectArena memory, int offset, NativeBattleVector point)
    {
        byte[] bytes = new byte[6];
        BinaryPrimitives.WriteInt16LittleEndian(bytes, point.X);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2), point.Y);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), point.Z);
        return memory.Write(offset, bytes);
    }
}
