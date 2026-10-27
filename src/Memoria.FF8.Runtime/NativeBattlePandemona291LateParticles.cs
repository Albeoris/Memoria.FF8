using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattlePandemona291LateParticles
{
    public static NativeBattlePandemona291Frame Spawn254(NativeBattlePandemona291Frame frame, int ground)
    {
        NativeBattleEffectTaskPool pool = frame.Effect.Pools.Single(current => current.DescriptorAddress == 0x2556298);
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Pandemona late particles require their current arena.");
        for (int remaining = 3; remaining >= 0; remaining--)
        {
            if (pool.Slots.All(task => (task.Flags & 1) != 0))
            {
                break;
            }
            (pool, int index) = pool.Append(0x6F0FB0);
            NativeBattleModulePoolTask task = pool.Slots[index].WriteWord(0x12, 0).WriteWord(0x18, 8192).WriteWord(0x1A, 4096);
            NativeCrtRandomState random = frame.Random.Draw(out ushort selected);
            frame = frame with { Random = random };
            int x = (selected & 0x1FFF) - 4096;
            sbyte low = unchecked((sbyte)x);
            int quotient = unchecked((int)(unchecked((long)low * unchecked((int)0xD5555555u)) >> 32)) >> 5;
            quotient = unchecked(quotient + (int)((uint)quotient >> 31));
            task = task.WriteWord(0xC, unchecked((ushort)x)).WriteByte(0x14, unchecked((byte)quotient)).WriteWord(0x10, unchecked((ushort)ground)).WriteByte(0x16, 20).WriteWord(0xE, 0).WriteByte(0x15, 0);
            int start = NativeBattleWaterExecutionFields.ArenaOffset(unchecked(pool.BankAddress + (uint)(index * pool.Stride)), pool.Stride);
            foreach (int field in new[] { 0xC, 0xE, 0x10, 0x12, 0x18, 0x1A })
            {
                arena = arena.Write(start + field, BitConverter.GetBytes(task.ReadWord(field)));
            }
            arena = arena.Write(start + 0x14, [task.ReadByte(0x14), task.ReadByte(0x15), task.ReadByte(0x16)]);
            pool = pool.WriteSlot(index, task);
        }
        return frame with { Memory = frame.Memory with { Arena = arena }, Effect = frame.Effect with { Pools = frame.Effect.Pools.Select(current => current.DescriptorAddress == pool.DescriptorAddress ? pool : current).ToImmutableArray() } };
    }

    public static NativeBattleModuleDraw Draw6F0FB0(NativeBattleModulePoolTask task, NativeBattlePandemona291SpriteResource sprite)
    {
        NativeBattleModuleResource resource = new(0, sprite.Address, 0, 0, sprite.Bytes);
        ushort phase = task.ReadWord(0x12);
        return new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = resource, SpriteFrame = resource.ProjectedFrame571C80(phase), Phase = phase, Anchor = new(unchecked((short)task.ReadWord(0xC)), unchecked((short)task.ReadWord(0xE)), unchecked((short)task.ReadWord(0x10)), unchecked((short)phase)), Scale = unchecked((short)task.ReadWord(0x18)), DepthOffset = -(unchecked((short)task.ReadWord(0x1A)) >> 2), SpriteFlags = 0 };
    }

    public static NativeBattlePandemona291ChildStep Finish6F0FB0(NativeBattlePandemona291Frame frame, uint address, NativeBattleModulePoolTask task)
    {
        if ((frame.Memory.Pandemona291?.Pause2556258 ?? throw new NotSupportedException("Native Pandemona late particles require their current pause DWORD.")) != 0)
        {
            return new(frame, task, 0);
        }
        NativeBattleEffectArena arena = frame.Memory.Arena!;
        int start = NativeBattleWaterExecutionFields.ArenaOffset(address, 0x1C);
        ushort phase = unchecked((ushort)(task.ReadWord(0x12) + 1));
        Word(0x12, phase);
        if (unchecked((short)phase) >= 12)
        {
            return new(frame with { Memory = frame.Memory with { Arena = arena } }, task, 2);
        }
        Word(0xC, task.ReadWord(0xC) + (unchecked((sbyte)task.ReadByte(0x14)) << 4));
        Word(0xE, task.ReadWord(0xE) + (unchecked((sbyte)task.ReadByte(0x15)) << 4));
        Word(0x10, task.ReadWord(0x10) + (unchecked((sbyte)task.ReadByte(0x16)) << 4));
        return new(frame with { Memory = frame.Memory with { Arena = arena } }, task, 0);

        void Word(int offset, int value)
        {
            ushort word = unchecked((ushort)value);
            task = task.WriteWord(offset, word);
            arena = arena.Write(start + offset, BitConverter.GetBytes(word));
        }
    }
}
