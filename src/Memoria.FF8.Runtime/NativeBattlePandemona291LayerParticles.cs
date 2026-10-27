using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattlePandemona291SpriteResource(uint Address, ImmutableArray<byte> Bytes);

public static class NativeBattlePandemona291LayerParticles
{
    public static NativeBattlePandemona291Frame Spawn6EDBCF(NativeBattlePandemona291Frame frame, int age)
    {
        int elapsed = unchecked(age - 24);
        int remaining = unchecked((int)((uint)elapsed >> 1));
        int radius = unchecked((int)(unchecked((uint)elapsed << 12) / 14)) >> 2;
        NativeBattlePandemona291State state = frame.Memory.Pandemona291 ?? throw new NotSupportedException("Native Pandemona particle births require their current module state.");
        int center = state.Center25562E8 ?? throw new NotSupportedException("Native Pandemona particle births require their current center.");
        return Spawn(frame, remaining, radius, unchecked(center + 2000), 4096, 2048, 128);
    }

    public static NativeBattlePandemona291Frame Spawn(NativeBattlePandemona291Frame frame, int remaining, int radius, int center, ushort scale, ushort displacement, ushort spin)
    {
        NativeBattleEffectTaskPool pool = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == 0x2556298);
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Pandemona particle births require their actual arena.");
        while (remaining-- > 0)
        {
            if (pool.Slots.All(task => (task.Flags & 1) != 0))
            {
                break;
            }
            (pool, int index) = pool.Append(0x6F0ED0);
            NativeCrtRandomState random = frame.Random.Draw(out ushort angle);
            frame = frame with { Random = random };
            NativeBattleModulePoolTask task = pool.Slots[index].WriteWord(0xC, 0).WriteWord(0xE, unchecked((ushort)radius)).WriteWord(0x10, angle).WriteWord(0x12, spin).WriteWord(0x14, unchecked((ushort)center)).WriteWord(0x16, scale).WriteWord(0x18, displacement);
            pool = pool.WriteSlot(index, task);
            int offset = checked((int)(pool.BankAddress - NativeBattleEffectNodeMemory.ArenaAddress) + index * pool.Stride + 0xC);
            arena = arena.Write(offset, task.ReadRawPayloadSpan().Slice(0, 14));
        }
        return frame with { Memory = frame.Memory with { Arena = arena }, Effect = frame.Effect with { Pools = frame.Effect.Pools.Select(current => current.DescriptorAddress == pool.DescriptorAddress ? pool : current).ToImmutableArray() } };
    }

    public static NativeBattleModuleDraw Draw6F0ED0(NativeBattleModulePoolTask task, NativeBattlePandemona291SpriteResource sprite, ImmutableArray<byte> trigonometry)
    {
        if (trigonometry.IsDefault || trigonometry.Length != 0x4000)
        {
            throw new NotSupportedException("Native Pandemona particles require their original trigonometry table.");
        }
        int angle = task.ReadWord(0x10) & 0xFFF;
        int radius = unchecked((short)task.ReadWord(0xE));
        int x = unchecked(BinaryPrimitives.ReadInt16LittleEndian(trigonometry.AsSpan().Slice(angle * 4, 2)) * radius) >> 12;
        int z = unchecked(BinaryPrimitives.ReadInt16LittleEndian(trigonometry.AsSpan().Slice(angle * 4 + 2, 2)) * radius) >> 12;
        NativeBattleModuleResource projected = new(0, sprite.Address, 0, 0, sprite.Bytes);
        ushort phase = task.ReadWord(0xC);
        return new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = projected, SpriteFrame = projected.ProjectedFrame571C80(phase), Phase = phase, Anchor = new(unchecked((short)x), 0, unchecked((short)(z + task.ReadWord(0x14))), 0), Scale = unchecked((short)task.ReadWord(0x16)), DepthOffset = -(unchecked((short)task.ReadWord(0x18)) >> 2), SpriteFlags = 0 };
    }

    public static NativeBattlePandemona291ChildStep Finish6F0ED0(NativeBattlePandemona291Frame frame, uint address, NativeBattleModulePoolTask task)
    {
        uint pause = frame.Memory.Pandemona291?.Pause2556258 ?? throw new NotSupportedException("Native Pandemona particles require their current pause DWORD.");
        if (pause != 0)
        {
            return new(frame, task, 0);
        }
        ushort next = unchecked((ushort)(task.ReadWord(0xC) + 1));
        task = task.WriteWord(0xC, next);
        int offset = checked((int)(address - NativeBattleEffectNodeMemory.ArenaAddress));
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Pandemona particles require their actual task bank.");
        arena = arena.Write(offset + 0xC, BitConverter.GetBytes(next));
        int result = unchecked((short)next) >= 12 ? 2 : 0;
        if (result == 0)
        {
            ushort angle = unchecked((ushort)(task.ReadWord(0x10) + task.ReadWord(0x12)));
            task = task.WriteWord(0x10, angle);
            arena = arena.Write(offset + 0x10, BitConverter.GetBytes(angle));
        }
        return new(frame with { Memory = frame.Memory with { Arena = arena } }, task, result);
    }
}
