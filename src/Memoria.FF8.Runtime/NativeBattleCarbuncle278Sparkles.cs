using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCarbuncle278SparkleStep(NativeBattleModulePoolTask Task, int Result);

/// <summary>The physical sparkle bank, shared effect random draws and eleven-frame billboard callback.</summary>
public static class NativeBattleCarbuncle278Sparkles
{
    public static NativeBattleCarbuncle278Frame Spawn681B3F(NativeBattleCarbuncle278Frame frame, NativeBattleModulePoolTask creature)
    {
        NativeBattleEffectTaskPool pool = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == 0x2508178);
        if (pool.Stride != 0x14 || pool.Slots.Length != 64)
        {
            throw new InvalidDataException("Native Carbuncle sparkles require their actual physical task bank.");
        }
        for (int spawn = 0; spawn < 4; spawn++)
        {
            if (pool.Slots.All(task => (task.Flags & 1) != 0))
            {
                continue;
            }
            (pool, int index) = pool.Append(0x683E80);
            frame = frame with { Memory = frame.Memory.DrawSharedEffectRandom7059E0(out ushort random) };
            NativeBattleCarbuncleExecutableMemory executable = frame.Memory.CarbuncleExecutableMemory ?? throw new NotSupportedException("Native Carbuncle sparkles require their current creature skeleton.");
            int count = executable.Read(NativeBattleCarbuncleExecutableMemory.Address, 1)[0];
            NativeBattleVector point = NativeBattleCarbuncle278Anchors.ReadBone502170(creature, executable, random * count >> 15, 0);
            NativeBattleModulePoolTask task = pool.Slots[index].WriteWord(0xC, unchecked((ushort)point.X)).WriteWord(0xE, unchecked((ushort)point.Y)).WriteWord(0x10, unchecked((ushort)point.Z)).WriteWord(0x12, 0);
            pool = pool.WriteSlot(index, task);
            int offset = checked((int)(pool.BankAddress - NativeBattleEffectNodeMemory.ArenaAddress) + index * pool.Stride + 12);
            frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(offset, task.ReadRawPayloadSpan()) } };
        }
        return frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.Select(current => current.DescriptorAddress == pool.DescriptorAddress ? pool : current).ToImmutableArray() } };
    }

    public static NativeBattleModuleDraw Draw683E80(NativeBattleModulePoolTask task, NativeBattleModuleResource resource)
    {
        ushort age = task.ReadWord(0x12);
        if (resource.Address != 0x10BD044)
        {
            throw new InvalidDataException("Native Carbuncle sparkles require their original frame resource.");
        }
        return new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = resource, SpriteFrame = resource.ProjectedFrame571C80(age), Phase = age, Anchor = new(unchecked((short)task.ReadWord(0xC)), unchecked((short)task.ReadWord(0xE)), unchecked((short)task.ReadWord(0x10)), 0), Scale = 1024, DepthOffset = -256, SpriteFlags = 0 };
    }

    public static NativeBattleCarbuncle278SparkleStep Finish683E80(NativeBattleModulePoolTask task, bool paused)
    {
        if (paused)
        {
            return new(task, 0);
        }
        task = task.WriteWord(0x12, unchecked((ushort)(task.ReadWord(0x12) + 1)));
        return new(task, unchecked((short)task.ReadWord(0x12)) < 11 ? 0 : 2);
    }
}
