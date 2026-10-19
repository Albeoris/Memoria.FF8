using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Physical recipient tasks and their current actor-space matrix cache.</summary>
public static class NativeBattleCarbuncle278Recipients
{
    public static NativeBattleCarbuncle278Frame Spawn681B8C(NativeBattleCarbuncle278Frame frame, NativeBattleModulePoolTask creature, Func<uint, NativeBattleModuleAnchor> readAnchor)
    {
        int phase = unchecked((short)creature.ReadWord(0xC)) - 145;
        if ((uint)phase >= 12 || (phase & 3) != 0)
        {
            return frame;
        }
        int target = unchecked((short)creature.ReadWord(0xE)) - (phase >> 2) - 1;
        if (target < 0)
        {
            return frame;
        }
        NativeBattleEffectTaskPool pool = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == 0x2508158);
        if (pool.Stride != 0x10C || pool.Slots.Length != 3)
        {
            throw new InvalidDataException("Native Carbuncle recipients require their actual physical task bank.");
        }
        if (pool.Slots.All(task => (task.Flags & 1) != 0))
        {
            return frame;
        }
        (pool, int index) = pool.Append(0x6822F0);
        uint actor = creature.ReadDword(checked(0x14 + 4 * target));
        NativeBattleModuleAnchor anchor = readAnchor(actor);
        NativeBattleModulePoolTask task = pool.Slots[index] with { Payload = new byte[0x10C - 12].ToImmutableArray() };
        task = task.WriteDword(0x10, actor).WriteWord(0x5C, unchecked((ushort)anchor.X)).WriteWord(0x5E, 0).WriteWord(0x60, unchecked((ushort)anchor.Z)).WriteWord(0x62, unchecked((ushort)anchor.W)).WriteDword(0x28, unchecked((uint)anchor.X)).WriteDword(0x2C, unchecked((uint)anchor.Y)).WriteDword(0x30, unchecked((uint)anchor.Z));
        NativeBattleGfAnimationSource source = frame.Effect.Binding.Carbuncle278Resources?.RecipientProgramme ?? throw new NotSupportedException("Native Carbuncle recipients require their original programme.");
        if (source.Address != 0x10C1CD8)
        {
            throw new InvalidDataException("Native Carbuncle recipient source changed its original address.");
        }
        NativeBattleGfAnimationWorkspace workspace = new(task.Payload.AsSpan().Slice(0x64 - 12, 0xA8).ToArray().ToImmutableArray());
        workspace = workspace.Initialize7016B0(source, 0xA8);
        byte[] bytes = task.Payload.ToArray();
        workspace.Data.AsSpan().CopyTo(bytes.AsSpan(0x64 - 12, 0xA8));
        task = task with { Payload = bytes.ToImmutableArray() };
        pool = pool with { Slots = pool.Slots.SetItem(index, task) };
        int offset = checked((int)(pool.BankAddress - NativeBattleEffectNodeMemory.ArenaAddress) + index * pool.Stride + 12);
        return frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(offset, task.Payload.AsSpan()) }, Effect = frame.Effect with { Pools = frame.Effect.Pools.Select(current => current.DescriptorAddress == pool.DescriptorAddress ? pool : current).ToImmutableArray() } };
    }

    public static NativeBattleCarbuncle278Frame SpawnSparkle6822F0(NativeBattleCarbuncle278Frame frame, uint actor, Func<uint, int> readBoneCount, Func<uint, int, NativeBattleVector> readBone)
    {
        NativeBattleEffectTaskPool pool = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == 0x2508178);
        if (pool.Stride != 0x14 || pool.Slots.Length != 64)
        {
            throw new InvalidDataException("Native Carbuncle recipient sparkles require their actual physical task bank.");
        }
        if (pool.Slots.All(task => (task.Flags & 1) != 0))
        {
            return frame;
        }
        (pool, int index) = pool.Append(0x683E80);
        frame = frame with { Memory = frame.Memory.DrawSharedEffectRandom7059E0(out ushort random) };
        int count = readBoneCount(actor);
        NativeBattleVector point = readBone(actor, random * count >> 15);
        NativeBattleModulePoolTask task = pool.Slots[index].WriteWord(0xC, unchecked((ushort)point.X)).WriteWord(0xE, unchecked((ushort)point.Y)).WriteWord(0x10, unchecked((ushort)point.Z)).WriteWord(0x12, 0);
        pool = pool with { Slots = pool.Slots.SetItem(index, task) };
        int offset = checked((int)(pool.BankAddress - NativeBattleEffectNodeMemory.ArenaAddress) + index * pool.Stride + 12);
        return frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(offset, task.Payload.AsSpan()) }, Effect = frame.Effect with { Pools = frame.Effect.Pools.Select(current => current.DescriptorAddress == pool.DescriptorAddress ? pool : current).ToImmutableArray() } };
    }

    public static NativeBattleModulePoolTask Cache6DBCC0(NativeBattleModulePoolTask task, short yaw, short scale38, NativeBattleActionMatrix view)
    {
        short scale = unchecked((short)Math.Min(scale38 * 4, 4096));
        short sine = unchecked((short)(NativeBattleCameraMath.Sine(yaw) * scale >> 12));
        short cosine = unchecked((short)(NativeBattleCameraMath.Cosine(yaw) * scale >> 12));
        NativeBattleActionMatrix world = new([cosine, 0, sine, 0, scale, 0, unchecked((short)-sine), 0, cosine], unchecked((int)task.ReadDword(0x28)), unchecked((int)task.ReadDword(0x2C)), unchecked((int)task.ReadDword(0x30)), 4096);
        for (int index = 0; index < 9; index++)
        {
            task = task.WriteWord(0x14 + 2 * index, unchecked((ushort)world.Rotation[index]));
        }
        NativeBattleActionMatrix current = NativeBattleGfMatrices.Compose56C2F0(view, world);
        for (int index = 0; index < 9; index++)
        {
            task = task.WriteWord(0x34 + 2 * index, unchecked((ushort)current.Rotation[index]));
        }
        return task.WriteDword(0x48, unchecked((uint)current.X)).WriteDword(0x4C, unchecked((uint)current.Y)).WriteDword(0x50, unchecked((uint)current.Z));
    }
}
