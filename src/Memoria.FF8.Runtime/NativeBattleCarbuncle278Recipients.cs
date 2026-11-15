using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Physical recipient tasks and their current actor-space matrix cache.</summary>
public static class NativeBattleCarbuncle278Recipients
{
    public static NativeBattleCarbuncle278Frame Spawn681B8C(NativeBattleCarbuncle278Frame frame, NativeBattleModulePoolTask creature, NativeBattleModelAliases aliases, Func<BattleModelOwner, NativeBattleModuleAnchor> readAnchor)
    {
        int phase = unchecked((short)creature.ReadWord(0xC)) - 145;
        if ((uint)phase >= 12 || (phase & 3) != 0)
        {
            return frame;
        }
        int target = creature.ReadContactCount(0xE) - (phase >> 2) - 1;
        if (target < 0)
        {
            return frame;
        }
        NativeBattleEffectTaskPool pool = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == 0x2508158);
        if (pool.Stride != 0x10C || !frame.ExpandedStorage && pool.Slots.Length != 3)
        {
            throw new InvalidDataException("Native Carbuncle recipients require their actual physical task bank.");
        }
        int last = frame.ExpandedStorage && phase == 8 ? 0 : target;
        for (; target >= last; target--)
        {
            if (!NativeBattleCarbuncle278Banks.CanAppend(frame, pool, required: true))
            {
                return frame;
            }
            (pool, int index) = pool.Append(0x6822F0);
            var recipients = creature.ReadExecutionReference<BattleExecutionReference.OrderedContactRecipients>(0x14, 4);
            (BattleModelOwner actor, BattleContactOccurrence occurrence) = recipients.Recipients[target];
            NativeBattleModuleAnchor anchor = readAnchor(actor);
            NativeBattleModulePoolTask task = pool.Slots[index].ClearPayload(0x10C - 12);
            BattleExecutionReference owner = frame.ExpandedStorage
                ? new BattleExecutionReference.OrderedContactRecipients([(actor, occurrence)])
                : new BattleExecutionReference.Model(actor);
            task.WriteExecutionReference(0x10, 4, owner, aliases.EncodeAddress(actor));
            task = task.WriteWord(0x5C, unchecked((ushort)anchor.X)).WriteWord(0x5E, 0).WriteWord(0x60, unchecked((ushort)anchor.Z)).WriteWord(0x62, unchecked((ushort)anchor.W)).WriteDword(0x28, unchecked((uint)anchor.X)).WriteDword(0x2C, unchecked((uint)anchor.Y)).WriteDword(0x30, unchecked((uint)anchor.Z));
            NativeBattleGfAnimationSource source = frame.Effect.Binding.Carbuncle278Resources?.RecipientProgramme ?? throw new NotSupportedException("Native Carbuncle recipients require their original programme.");
            if (source.Address != 0x10C1CD8)
            {
                throw new InvalidDataException("Native Carbuncle recipient source changed its original address.");
            }
            NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(task, 0x64, 0xA8);
            workspace = workspace.Initialize7016B0(source, 0xA8);
            task = workspace.CopyTo(task, 0x64, retainUnknown: true);
            pool = pool.WriteSlot(index, task);
            if (!NativeBattleCarbuncle278Banks.IsHosted(pool))
            {
                int offset = checked((int)(pool.BankAddress - NativeBattleEffectNodeMemory.ArenaAddress) + index * pool.Stride + 12);
                (byte[] bytes, bool[] known) = task.BorrowPayloadArena(12, task.PayloadLength).ReadSnapshot(0, task.PayloadLength);
                frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.WriteWithKnownness(offset, bytes, known) } };
            }
            frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.Select(current => current.DescriptorAddress == pool.DescriptorAddress ? pool : current).ToImmutableArray() } };
        }
        return frame;
    }

    internal static (BattleModelOwner Owner, BattleContactOccurrence Occurrence) ReadOccurrence(NativeBattleModulePoolTask task) =>
        task.ReadExecutionReference<BattleExecutionReference.OrderedContactRecipients>(0x10, 4).Recipients.Single();

    internal static BattleModelOwner ReadOwner(NativeBattleCarbuncle278Frame frame, NativeBattleModulePoolTask task) =>
        frame.ExpandedStorage ? ReadOccurrence(task).Owner : task.ReadExecutionReference<BattleExecutionReference.Model>(0x10, 4).Owner;

    public static NativeBattleCarbuncle278Frame SpawnSparkle6822F0(NativeBattleCarbuncle278Frame frame, BattleModelOwner actor, Func<BattleModelOwner, int> readBoneCount, Func<BattleModelOwner, int, NativeBattleVector> readBone)
    {
        NativeBattleEffectTaskPool pool = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == 0x2508178);
        if (pool.Stride != 0x14 || !frame.ExpandedStorage && pool.Slots.Length != 64)
        {
            throw new InvalidDataException("Native Carbuncle recipient sparkles require their actual physical task bank.");
        }
        if (!NativeBattleCarbuncle278Banks.CanAppend(frame, pool))
        {
            return frame;
        }
        (pool, int index) = pool.Append(0x683E80);
        frame = frame with { Memory = frame.Memory.DrawSharedEffectRandom7059E0(out ushort random) };
        int count = readBoneCount(actor);
        NativeBattleVector point = readBone(actor, random * count >> 15);
        NativeBattleModulePoolTask task = pool.Slots[index].WriteWord(0xC, unchecked((ushort)point.X)).WriteWord(0xE, unchecked((ushort)point.Y)).WriteWord(0x10, unchecked((ushort)point.Z)).WriteWord(0x12, 0);
        pool = pool.WriteSlot(index, task);
        frame = NativeBattleCarbuncle278Banks.MirrorPayload(frame, pool, index, task);
        return frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.Select(current => current.DescriptorAddress == pool.DescriptorAddress ? pool : current).ToImmutableArray() } };
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
