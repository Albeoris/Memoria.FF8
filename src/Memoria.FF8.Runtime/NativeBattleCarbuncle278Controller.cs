using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Carbuncle's physical root callback and initial creature mirror window.</summary>
public static class NativeBattleCarbuncle278Controller
{
    public static NativeBattleCarbuncle278Frame Advance680DF0(NativeBattleCarbuncle278Frame frame, Action<uint, int> stage5713E0, NativeBattleCarbuncle278Inputs? inputs = null)
    {
        NativeBattleCarbuncle278Entry.RequireBinding(frame.Effect);
        ArgumentNullException.ThrowIfNull(stage5713E0);
        NativeBattleEffectTaskPool? pool = frame.Effect.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleCarbuncle278Entry.MainDescriptor);
        if (pool is not { BankAddress: NativeBattleCarbuncle278Entry.MainBank, Stride: 0x14 } || pool.Slots.Length != 1)
        {
            throw new InvalidDataException("Native Carbuncle requires its actual single root task bank.");
        }
        NativeBattleEffectTaskStep step = pool.Advance((current, index) =>
        {
            NativeBattleModulePoolTask task = current.Slots[index];
            if (task.Callback != NativeBattleCarbuncle278Entry.MainCallback)
            {
                throw new NotSupportedException("Native Carbuncle root callback was replaced.");
            }
            NativeBattleCarbuncle278State state = NativeBattleCarbuncle278Entry.RequireState(frame.Memory);
            uint primary = task.ReadDword(0x10) != 0 ? 0x3ECCu : 0x13ECCu;
            state = state with { PacketCursor81BC = unchecked(state.ModelBuffer81F8 + primary), AlternateCursor81C0 = unchecked(state.ModelBuffer81F8 + primary + 0x10000), Flash81D4 = 0 };
            task = task.WriteDword(0x10, task.ReadDword(0x10) != 0 ? 0u : 1u);
            frame = frame with { Memory = frame.Memory with { Carbuncle278 = state } };
            if (task.ReadWord(0xC) == 2 && state.Pause8114 == 0 && task.ReadByte(0xF) == 0)
            {
                if (inputs is null)
                {
                    throw new NotSupportedException("Native Carbuncle6D6070 and6812E0 construction requires its actual model and renderer inputs.");
                }
                task = task.WriteByte(0xF, 1);
                frame = NativeBattleCarbuncle278Construction.Initialize680E36(frame, inputs);
            }
            if (task.ReadByte(0xF) != 0)
            {
                frame = AdvanceSpawned(frame, inputs);
            }
            state = NativeBattleCarbuncle278Entry.RequireState(frame.Memory);
            stage5713E0(state.Flash81D4, 0);
            if (state.Pause8114 == 0)
            {
                task = task.WriteWord(0xC, unchecked((ushort)(task.ReadWord(0xC) + 1)));
            }
            frame = frame with { Memory = frame.Memory with { Carbuncle278 = state } };
            return new(current with { Slots = current.Slots.SetItem(index, task) }, 0);
        });
        return frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.Select(pool => pool.DescriptorAddress == step.Pool.DescriptorAddress ? step.Pool : pool).ToImmutableArray(), ReturnedDescriptor = step.Survivors == 0 ? 0u : NativeBattleCarbuncle278Entry.MainDescriptor } };
    }

    private static NativeBattleCarbuncle278Frame AdvanceSpawned(NativeBattleCarbuncle278Frame frame, NativeBattleCarbuncle278Inputs? inputs)
    {
        foreach (uint descriptor in new uint[] { 0x2508188, 0x2508178, 0x2508168, 0x2508158, 0x2508138, 0x2508148, 0x2508128 })
        {
            NativeBattleEffectTaskPool pool = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == descriptor);
            NativeBattleEffectTaskStep step = pool.Advance((current, index) =>
            {
                NativeBattleModulePoolTask task = current.Slots[index];
                if (task.Callback == 0x6822F0)
                {
                    int age = unchecked((short)task.ReadWord(0xC));
                    if (age is < 0 or >= 15)
                    {
                        throw new NotSupportedException("Native Carbuncle6822F0 reaches its683760/683330 recipient child families at age15.");
                    }
                    NativeBattleCarbuncle278ModelStep recipient = (inputs?.AdvanceRecipient ?? throw new NotSupportedException("Native Carbuncle requires its original recipient programme and actor services."))(frame, task);
                    frame = recipient.Frame;
                    task = recipient.Task;
                    if (NativeBattleCarbuncle278Entry.RequireState(frame.Memory).Pause8114 == 0)
                    {
                        task = task.WriteWord(0xC, unchecked((ushort)(task.ReadWord(0xC) + 1)));
                    }
                    int recipientOffset = checked((int)(current.BankAddress - NativeBattleEffectNodeMemory.ArenaAddress) + index * current.Stride + 12);
                    frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(recipientOffset, task.Payload.AsSpan()).MarkUnknown(recipientOffset + 0x46 - 12, 2) } };
                    return new(current with { Slots = current.Slots.SetItem(index, task) }, 0);
                }
                if (task.Callback == 0x683E80)
                {
                    frame = (inputs?.DrawSparkle ?? throw new NotSupportedException("Native Carbuncle683E80 requires its original sprite services."))(frame, task);
                    NativeBattleCarbuncle278SparkleStep sparkle = NativeBattleCarbuncle278Sparkles.Finish683E80(task, NativeBattleCarbuncle278Entry.RequireState(frame.Memory).Pause8114 != 0);
                    int sparkleOffset = checked((int)(current.BankAddress - NativeBattleEffectNodeMemory.ArenaAddress) + index * current.Stride + 12);
                    frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(sparkleOffset, sparkle.Task.Payload.AsSpan()) } };
                    return new(current with { Slots = current.Slots.SetItem(index, sparkle.Task) }, sparkle.Result);
                }
                if (task.Callback != 0x681630)
                {
                    throw new NotSupportedException($"Native Carbuncle task callback0x{task.Callback:X8} requires its actual current leaf contract.");
                }
                ushort counter = task.ReadWord(0xC);
                if (counter >= 215)
                {
                    throw new NotSupportedException("Native Carbuncle681630 reaches its returning creature model, third sound and bone-follow camera at creature215.");
                }
                NativeBattleCarbuncle278State state = NativeBattleCarbuncle278Entry.RequireState(frame.Memory);
                frame = frame with { Memory = frame.Memory with { Carbuncle278 = state with { Flash81D4 = counter < 8 ? unchecked((uint)(counter << 8)) : 0x800 } } };
                if (counter is >= 36 and <= 145)
                {
                    NativeBattleCarbuncle278ModelStep model = (inputs?.ModelCreature ?? throw new NotSupportedException("Native Carbuncle6FBDB0 and6A79B0 require its current creature model services."))(frame, task);
                    frame = model.Frame;
                    task = model.Task;
                }
                if (counter is > 0 and < 145)
                {
                    frame = (inputs?.DrawCreature ?? throw new NotSupportedException("Native Carbuncle6839B0 requires its original mirror mesh services."))(frame, counter);
                }
                if (counter is >= 110 and < 177)
                {
                    NativeBattleCarbuncle278ModelStep programme = (inputs?.AdvanceProgramme ?? throw new NotSupportedException("Native Carbuncle requires its current original creature programme services."))(frame, task);
                    frame = programme.Frame;
                    task = programme.Task;
                }
                if (counter == 150)
                {
                    NativeBattleEffectIoPoll poll = NativeBattleEffectIo.Poll534270(frame.Memory);
                    frame = frame with { Memory = poll.Memory };
                    if (poll.Request is not null)
                    {
                        frame = (inputs?.QueueFile ?? throw new NotSupportedException("Native Carbuncle requires its current archive IO delivery services."))(frame, poll.Request);
                    }
                    if (poll.Busy)
                    {
                        int heldOffset = checked((int)(current.BankAddress - NativeBattleEffectNodeMemory.ArenaAddress) + index * current.Stride + 12);
                        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(heldOffset, task.Payload.AsSpan()) } };
                        return new(current with { Slots = current.Slots.SetItem(index, task) }, 0);
                    }
                    uint destination = checked(NativeBattleCarbuncle278Entry.RequireState(frame.Memory).ModelBuffer81F8 + 0x23ECC);
                    frame = frame with { Memory = NativeBattleEffectIo.Queue5341D0(NativeBattleEffectIo.Queue5341D0(frame.Memory, 717, destination, 1), 718, destination, 0) };
                }
                NativeBattleEffectIoAdvance io = NativeBattleEffectIo.Advance534210(frame.Memory);
                if (io.Request is not null)
                {
                    frame = (inputs?.QueueFile ?? throw new NotSupportedException("Native Carbuncle534210 requires its original pending descriptor completion binding."))(frame with { Memory = io.Memory }, io.Request);
                }
                else
                {
                    frame = frame with { Memory = io.Memory };
                }
                if (counter >= 110)
                {
                    NativeBattleCarbuncle278ModelStep presentation = (inputs?.PresentProgramme ?? throw new NotSupportedException("Native Carbuncle requires its programme pose and camera services."))(frame, task);
                    frame = presentation.Frame;
                    task = presentation.Task;
                }
                else if (counter >= 86)
                {
                    frame = (inputs?.PresentSparkleWindow ?? throw new NotSupportedException("Native Carbuncle requires its sparkle spawning and bone-follow camera services."))(frame, task);
                }
                else if (counter != 0)
                {
                    frame = (inputs?.PresentCreature ?? throw new NotSupportedException("Native Carbuncle requires its current camera and sound services."))(frame, counter);
                }
                task = task.WriteWord(0xC, unchecked((ushort)(counter + 1)));
                int offset = checked((int)(current.BankAddress - NativeBattleEffectNodeMemory.ArenaAddress) + index * current.Stride + 12);
                frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(offset, task.Payload.AsSpan()) } };
                return new(current with { Slots = current.Slots.SetItem(index, task) }, 0);
            });
            //6811E0's balanced borrowed context is consumed only by active beam callbacks in681270.
            frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.Select(pool => pool.DescriptorAddress == step.Pool.DescriptorAddress ? step.Pool : pool).ToImmutableArray() } };
        }
        return frame;
    }
}
