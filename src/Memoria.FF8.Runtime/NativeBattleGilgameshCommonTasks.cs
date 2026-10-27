using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGilgameshCommonStep(NativeBattleModulePoolTask UpdatedTask, int Result);

/// <summary>Finite raw-pool fade, stage and texture-completion tasks.</summary>
public static class NativeBattleGilgameshCommonTasks
{
    public static NativeBattleEffectTaskPool Append5929B0(NativeBattleEffectTaskPool pool, int mode, ushort duration) => Append(pool, mode != 0 ? 0x592A50u : 0x592A00u, duration, false);

    public static NativeBattleEffectTaskPool Append592AB0(NativeBattleEffectTaskPool pool) => Append(pool, 0x592AE0, 16, true);

    public static NativeBattleEffectTaskPool Append592B30(NativeBattleEffectTaskPool pool) => Append(pool, 0x592B60, 16, true);

    public static NativeBattleGilgameshCommonStep StepFade(NativeBattleModulePoolTask task, uint graphicsFlags, Action<int, int, int, int, int> fade47CF50)
    {
        ValidateTask(task);
        if (task.Callback is not (0x592A00 or 0x592A50))
        {
            throw new NotSupportedException("Native Gilgamesh fade requires its finite selected callback.");
        }
        ArgumentNullException.ThrowIfNull(fade47CF50);
        if (task.Callback == 0x592A00 || Signed(task, 0x0C) > 0)
        {
            fade47CF50(0, 255, 255, 255, 63);
        }
        return Advance(task, graphicsFlags);
    }

    public static NativeBattleGilgameshCommonStep StepStage(NativeBattleModulePoolTask task, uint graphicsFlags, Action<int, int> stage5713E0)
    {
        ValidateTask(task);
        if (task.Callback is not (0x592AE0 or 0x592B60))
        {
            throw new NotSupportedException("Native Gilgamesh stage requires its finite selected callback.");
        }
        ArgumentNullException.ThrowIfNull(stage5713E0);
        int extent = Signed(task, 0x18);
        int value = extent / Signed(task, 0x0E) * Signed(task, 0x0C);
        stage5713E0(task.Callback == 0x592B60 ? extent - value : value, 0);
        NativeBattleGilgameshCommonStep result = Advance(task, graphicsFlags);
        if (result.Result == 2 && task.Callback == 0x592B60)
        {
            stage5713E0(0, 0);
        }
        return result;
    }

    public static NativeBattleGilgameshCommonStep Step596B30(NativeBattleModulePoolTask task, uint arenaBase, uint taskAddress, Func<uint, uint, NativeBattleModulePoolTask, NativeBattleModulePoolTask> texture508630)
    {
        ValidateTask(task);
        if (task.Callback != 0x596B30)
        {
            throw new NotSupportedException("Native596B30 requires its actual texture-completion callback.");
        }
        ArgumentNullException.ThrowIfNull(texture508630);
        if (Signed(task, 0x0C) == 1)
        {
            NativeBattleModulePoolTask updated = texture508630(checked(arenaBase + 0xE000), checked(taskAddress + 0x0E), task);
            ValidateTask(updated);
            if (updated.Flags != task.Flags || updated.State != task.State || updated.Next != task.Next || updated.Callback != task.Callback || !updated.ReadRawPayloadSpan().Slice(0, 2).SequenceEqual(task.ReadRawPayloadSpan().Slice(0, 2)) || !updated.ReadRawPayloadSpan().Slice(3).SequenceEqual(task.ReadRawPayloadSpan().Slice(3)))
            {
                throw new InvalidDataException("Native508630 may update only its actual completion BYTE in the supplied task.");
            }
            task = updated;
        }
        int result = task.ReadWord(0x0E) != 0 ? 2 : 0;
        return new(task.WriteWord(0x0C, unchecked((ushort)(task.ReadWord(0x0C) + 1))), result);
    }

    public static ImmutableArray<byte> SetActorFlags5904D0(ImmutableArray<byte> actorFlags) => ActorFlags(actorFlags, true);

    public static ImmutableArray<byte> ClearActorFlags5904F0(ImmutableArray<byte> actorFlags) => ActorFlags(actorFlags, false);

    private static ImmutableArray<byte> ActorFlags(ImmutableArray<byte> flags, bool set)
    {
        if (flags.IsDefault || flags.Length != 4)
        {
            throw new InvalidDataException("Native Gilgamesh actor flags require the four current graphics-row BYTE owners.");
        }
        return flags.Select(value => set ? (byte)(value | 2) : (byte)(value & ~2)).ToImmutableArray();
    }

    private static NativeBattleEffectTaskPool Append(NativeBattleEffectTaskPool pool, uint callback, ushort duration, bool stage)
    {
        if (pool.DescriptorAddress != 0x21FFD58 || pool.BankAddress != 0x21FFD68 || pool.Stride != 0x24 || pool.Slots.Length != 100)
        {
            throw new InvalidDataException("Native Gilgamesh common initializer requires its actual raw task bank.");
        }
        pool.Validate();
        int slot;
        (pool, slot) = pool.Append(callback);
        NativeBattleModulePoolTask task = pool.Slots[slot].WriteWord(0x0C, 0).WriteWord(0x0E, duration);
        if (stage)
        {
            task = task.WriteWord(0x18, 2048);
        }
        return pool.WriteSlot(slot, task);
    }

    private static NativeBattleGilgameshCommonStep Advance(NativeBattleModulePoolTask task, uint graphicsFlags)
    {
        if ((graphicsFlags & 0x201) != 0)
        {
            return new(task, 0);
        }
        task = task.WriteWord(0x0C, unchecked((ushort)(task.ReadWord(0x0C) + 1)));
        return new(task, Signed(task, 0x0C) >= Signed(task, 0x0E) ? 2 : 0);
    }

    private static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));

    private static void ValidateTask(NativeBattleModulePoolTask task)
    {
        if (task is null || task.PayloadLength != 24)
        {
            throw new InvalidDataException("Native Gilgamesh common tasks require their original raw0x24-byte task.");
        }
    }
}
