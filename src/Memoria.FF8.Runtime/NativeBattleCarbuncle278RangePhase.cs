using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCarbuncle278RangePhase(short Minimum, short Maximum, int Fade, ushort VOffset, int Phase);

/// <summary>The recipient range window and its physical ring births.</summary>
public static class NativeBattleCarbuncle278RangeSetup
{
    public static NativeBattleCarbuncle278RangePhase Read6825A5(NativeBattleEffectMemory memory, short age)
    {
        int phase = age - 20;
        if ((uint)phase >= 35)
        {
            throw new ArgumentOutOfRangeException(nameof(age));
        }
        byte[] bounds = (memory.Arena ?? throw new NotSupportedException("Native Carbuncle range requires its current signed bound WORDs.")).Read(0, 4);
        short minimum = BinaryPrimitives.ReadInt16LittleEndian(bounds);
        short span = BinaryPrimitives.ReadInt16LittleEndian(bounds.AsSpan(2));
        short maximum = phase <= 20 ? unchecked((short)(minimum + unchecked((uint)(span * (phase + 1))) / 20u)) : unchecked((short)(minimum + span));
        int fade = phase <= 20 ? 0 : (phase - 20) * 4096 / 15;
        ushort offset = unchecked((ushort)(-(age << 3) & 127));
        return new(minimum, maximum, fade, offset, phase);
    }

    public static NativeBattleCarbuncle278Frame Spawn6826B7(NativeBattleCarbuncle278Frame frame, uint parentAddress, NativeBattleCarbuncle278RangePhase phase, short actorScale38)
    {
        if (NativeBattleCarbuncle278Entry.RequireState(frame.Memory).Pause8114 != 0 || phase.Phase >= 18 || phase.Phase % 6 != 0)
        {
            return frame;
        }
        NativeBattleEffectTaskPool pool = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == 0x2508148);
        if (pool.Stride != 0x1C || pool.Slots.Length != 9)
        {
            throw new InvalidDataException("Native Carbuncle rings require their actual physical task bank.");
        }
        if (pool.Slots.All(task => (task.Flags & 1) != 0))
        {
            return frame;
        }
        (pool, int index) = pool.Append(0x6831D0);
        short scale = unchecked((short)Math.Min(actorScale38 * 4, 4096));
        NativeBattleModulePoolTask task = pool.Slots[index] with { Payload = new byte[16].ToImmutableArray() };
        task = task.WriteDword(0x18, parentAddress).WriteWord(0xE, unchecked((ushort)(phase.Maximum * scale >> 12))).WriteWord(0x10, 2048).WriteWord(0x12, 256).WriteWord(0x14, unchecked((ushort)-8));
        pool = pool with { Slots = pool.Slots.SetItem(index, task) };
        int offset = checked((int)(pool.BankAddress - NativeBattleEffectNodeMemory.ArenaAddress) + index * pool.Stride + 12);
        return frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(offset, task.Payload.AsSpan()) }, Effect = frame.Effect with { Pools = frame.Effect.Pools.Select(current => current.DescriptorAddress == pool.DescriptorAddress ? pool : current).ToImmutableArray() } };
    }

    public static NativeBattleCarbuncle278ChildStep Finish6831D0(NativeBattleModulePoolTask task, bool paused)
    {
        if (paused)
        {
            return new(task, 0);
        }
        short age = unchecked((short)task.ReadWord(0xC));
        if (age >= 20)
        {
            return new(task, 2);
        }
        short velocity = unchecked((short)(task.ReadWord(0x12) + task.ReadWord(0x14)));
        velocity = (short)Math.Max(16, (int)velocity);
        task = task.WriteWord(0x12, unchecked((ushort)velocity)).WriteWord(0x10, unchecked((ushort)(task.ReadWord(0x10) + velocity))).WriteWord(0xC, unchecked((ushort)(age + 1)));
        return new(task, 0);
    }
}
