using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>The returning mirror's physical sprite births, velocity updates and fourteen-frame lifetime.</summary>
public static class NativeBattleCarbuncle278ReturnParticles
{
    public static NativeBattleCarbuncle278Frame Spawn681C31(NativeBattleCarbuncle278Frame frame, ushort counter)
    {
        if (unchecked((uint)(counter - 231)) >= 4)
        {
            return frame;
        }
        NativeBattleEffectTaskPool pool = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == 0x2508168);
        if (pool.Stride != 0x1C || pool.Slots.Length != 8)
        {
            throw new InvalidDataException("Native Carbuncle return sprites require their actual physical task bank.");
        }
        if (pool.Slots.All(task => (task.Flags & 1) != 0))
        {
            return frame;
        }
        (pool, int index) = pool.Append(0x683DC0);
        frame = frame with { Memory = frame.Memory.DrawSharedEffectRandom7059E0(out ushort xRandom) };
        short x = unchecked((short)((xRandom & 127) - 64));
        frame = frame with { Memory = frame.Memory.DrawSharedEffectRandom7059E0(out ushort zRandom) };
        short z = unchecked((short)((zRandom & 127) - 64));
        frame = frame with { Memory = frame.Memory.DrawSharedEffectRandom7059E0(out ushort speedRandom) };
        short speed = unchecked((short)(-64 - (speedRandom & 31)));
        frame = frame with { Memory = frame.Memory.DrawSharedEffectRandom7059E0(out ushort kindRandom) };
        int floor = NativeBattleCarbuncle278Entry.RequireState(frame.Memory).Base81CC;
        NativeBattleModulePoolTask task = pool.Slots[index].WriteWord(0xC, unchecked((ushort)x)).WriteWord(0xE, 0).WriteWord(0x10, unchecked((ushort)(z + floor))).WriteWord(0x12, 0).WriteWord(0x14, unchecked((ushort)(x >> 2))).WriteWord(0x16, unchecked((ushort)speed)).WriteWord(0x18, unchecked((ushort)(z >> 2))).WriteWord(0x1A, (ushort)(kindRandom & 1));
        pool = pool with { Slots = pool.Slots.SetItem(index, task) };
        int offset = checked((int)(pool.BankAddress - NativeBattleEffectNodeMemory.ArenaAddress) + index * pool.Stride + 12);
        return frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(offset, task.Payload.AsSpan()) }, Effect = frame.Effect with { Pools = frame.Effect.Pools.Select(current => current.DescriptorAddress == pool.DescriptorAddress ? pool : current).ToImmutableArray() } };
    }

    public static NativeBattleModuleDraw Draw683DC0(NativeBattleModulePoolTask task, ImmutableArray<NativeBattleModuleResource> resources)
    {
        int index = task.ReadWord(0x1A) == 0 ? 0 : 1;
        if (resources.IsDefault || resources.Length != 2 || resources[index].Address != (index == 0 ? 0x10BD170u : 0x10BD2FCu))
        {
            throw new NotSupportedException("Native Carbuncle return sprites require both original phase resources.");
        }
        NativeBattleModuleResource resource = resources[index];
        return new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = resource, SpriteFrame = resource.ProjectedFrame571C80(task.ReadWord(0x12)), Phase = task.ReadWord(0x12), Anchor = new(unchecked((short)task.ReadWord(0xC)), unchecked((short)task.ReadWord(0xE)), unchecked((short)task.ReadWord(0x10)), 0), Scale = 2048, DepthOffset = -512, SpriteFlags = 0 };
    }

    public static NativeBattleCarbuncle278ChildStep Finish683DC0(NativeBattleModulePoolTask task, bool paused)
    {
        if (paused)
        {
            return new(task, 0);
        }
        task = task.WriteWord(0xC, unchecked((ushort)(task.ReadWord(0xC) + task.ReadWord(0x14)))).WriteWord(0xE, unchecked((ushort)(task.ReadWord(0xE) + task.ReadWord(0x16)))).WriteWord(0x10, unchecked((ushort)(task.ReadWord(0x10) + task.ReadWord(0x18)))).WriteWord(0x12, unchecked((ushort)(task.ReadWord(0x12) + 1)));
        return new(task, unchecked((short)task.ReadWord(0x12)) < 14 ? 0 : 2);
    }
}
