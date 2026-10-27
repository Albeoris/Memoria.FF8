using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCarbuncle278ChildStep(NativeBattleModulePoolTask Task, int Result);
public sealed record NativeBattleCarbuncle278ChildBirth(NativeBattleCarbuncle278Frame Frame, NativeBattleModulePoolTask Parent);

/// <summary>Ordered recipient child births and their fixed-point physical task updates.</summary>
public static class NativeBattleCarbuncle278RecipientChildren
{
    public static NativeBattleCarbuncle278ChildBirth Spawn6823F0(NativeBattleCarbuncle278Frame frame, NativeBattleModulePoolTask parent, uint parentAddress, NativeBattleStaticMeshResource bounds)
    {
        int phase = unchecked((short)parent.ReadWord(0xC)) - 15;
        if ((uint)phase >= 20)
        {
            return new(frame, parent);
        }
        if (phase == 0)
        {
            frame = frame with { Memory = frame.Memory.DrawSharedEffectRandom7059E0(out ushort angle) };
            for (int child = 0; child < 8; child++)
            {
                NativeBattleEffectTaskPool pool = Pool(frame, 0x2508138, 0x18);
                if (pool.Slots.All(task => (task.Flags & 1) != 0))
                {
                    continue;
                }
                (pool, int index) = pool.Append(0x683760);
                NativeBattleModulePoolTask task = pool.Slots[index].ClearPayload(16);
                task = task.WriteDword(0x18, parentAddress).WriteWord(0xE, 1024).WriteWord(0x10, angle).WriteWord(0x12, 16);
                frame = frame with { Memory = frame.Memory.DrawSharedEffectRandom7059E0(out ushort direction) };
                if ((direction & 1) != 0)
                {
                    task = task.WriteWord(0x12, unchecked((ushort)-16));
                }
                frame = frame with { Memory = frame.Memory.DrawSharedEffectRandom7059E0(out ushort drift) };
                task = task.WriteWord(0x14, unchecked((ushort)(-37 - (drift & 31))));
                frame = Store(frame, pool, index, task);
                angle = unchecked((ushort)(angle + 512));
            }
        }
        if (bounds.Address != 0x10BD488)
        {
            throw new InvalidDataException("Native Carbuncle recipient interpolation requires its original bound vertices.");
        }
        int progress = phase * 15 * 4096 / 20;
        int vertex = progress >> 12;
        int fraction = progress & 4095;
        short Interpolate(int lane)
        {
            int offset = checked(8 + vertex * 8 + lane);
            short first = BinaryPrimitives.ReadInt16LittleEndian(bounds.Bytes.AsSpan().Slice(offset, 2));
            short next = BinaryPrimitives.ReadInt16LittleEndian(bounds.Bytes.AsSpan().Slice(offset + 8, 2));
            return unchecked((short)(first + ((next - first) * fraction >> 12)));
        }
        parent = parent.WriteWord(0x54, 0).WriteWord(0x56, unchecked((ushort)Interpolate(2))).WriteWord(0x58, unchecked((ushort)Interpolate(4)));
        frame = frame with { Memory = frame.Memory.DrawSharedEffectRandom7059E0(out ushort seed) };
        if (NativeBattleCarbuncle278Entry.RequireState(frame.Memory).Pause8114 == 0)
        {
            for (int child = 0; child < 8; child++)
            {
                NativeBattleEffectTaskPool pool = Pool(frame, 0x2508128, 0x100);
                if (pool.Slots.All(task => (task.Flags & 1) != 0))
                {
                    continue;
                }
                (pool, int index) = pool.Append(0x683330);
                NativeBattleModulePoolTask task = pool.Slots[index].WriteWord(0xC, 0).WriteWord(0x12, 0).WriteWord(0x14, 0).WriteDword(0x18, parentAddress).WriteWord(0xE, seed);
                seed = unchecked((ushort)(seed + 0x155));
                frame = frame with { Memory = frame.Memory.DrawSharedEffectRandom7059E0(out ushort scale) };
                task = task.WriteWord(0x10, unchecked((ushort)((scale & 1023) + 512)));
                frame = Store(frame, pool, index, task);
            }
        }
        return new(frame, parent);
    }

    public static NativeBattleVector Polar6836F0(NativeBattleVector source, int angle, ImmutableArray<byte> table)
    {
        int offset = (angle & 4095) * 4;
        if (table.IsDefault || table.Length != 4096 * 4)
        {
            throw new InvalidDataException("Native Carbuncle polar transform requires its complete original fixed-point table.");
        }
        short first = BinaryPrimitives.ReadInt16LittleEndian(table.AsSpan().Slice(offset, 2));
        short second = BinaryPrimitives.ReadInt16LittleEndian(table.AsSpan().Slice(offset + 2, 2));
        int x = unchecked(source.X * second + source.Y * first) >> 12;
        int y = unchecked(source.Y * second - source.X * first) >> 12;
        return new(unchecked((short)x), unchecked((short)y), source.Z);
    }

    public static NativeBattleCarbuncle278ChildStep Finish683760(NativeBattleModulePoolTask task, bool paused)
    {
        if (paused)
        {
            return new(task, 0);
        }
        short age = unchecked((short)task.ReadWord(0xC));
        if (age >= 16)
        {
            return new(task, 2);
        }
        task = task.WriteWord(0x10, unchecked((ushort)(task.ReadWord(0x10) + task.ReadWord(0x12)))).WriteWord(0xE, unchecked((ushort)(task.ReadWord(0xE) + task.ReadWord(0x14)))).WriteWord(0xC, unchecked((ushort)(age + 1)));
        return new(task, 0);
    }

    public static NativeBattleCarbuncle278ChildStep Finish683330(NativeBattleModulePoolTask task, bool paused)
    {
        if (paused)
        {
            return new(task, 0);
        }
        short age = unchecked((short)task.ReadWord(0xC));
        return age >= 15 ? new(task, 2) : new(task.WriteWord(0xC, unchecked((ushort)(age + 1))), 0);
    }

    private static NativeBattleEffectTaskPool Pool(NativeBattleCarbuncle278Frame frame, uint descriptor, int count)
    {
        NativeBattleEffectTaskPool pool = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == descriptor);
        if (pool.Stride != 0x1C || pool.Slots.Length != count)
        {
            throw new InvalidDataException("Native Carbuncle recipient children require their actual physical task banks.");
        }
        return pool;
    }

    private static NativeBattleCarbuncle278Frame Store(NativeBattleCarbuncle278Frame frame, NativeBattleEffectTaskPool pool, int index, NativeBattleModulePoolTask task)
    {
        pool = pool.WriteSlot(index, task);
        int offset = checked((int)(pool.BankAddress - NativeBattleEffectNodeMemory.ArenaAddress) + index * pool.Stride + 12);
        return frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(offset, task.ReadRawPayloadSpan()) }, Effect = frame.Effect with { Pools = frame.Effect.Pools.Select(current => current.DescriptorAddress == pool.DescriptorAddress ? pool : current).ToImmutableArray() } };
    }
}
