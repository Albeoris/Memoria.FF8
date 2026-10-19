using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleDiablos325Timeline
{
    public static NativeBattleDiablos325Frame AdvanceOpening6545F0(NativeBattleDiablos325Frame frame, NativeBattleDiablos325Inputs inputs)
    {
        NativeBattleEffectTaskPool pool = frame.Effect.Pools.Single(item => item.DescriptorAddress == NativeBattleDiablos325Construction.TimelineDescriptor);
        NativeBattleEffectTaskStep step = pool.Advance((current, index) =>
        {
            NativeBattleModulePoolTask task = current.Slots[index];
            if (task.Callback != NativeBattleDiablos325Construction.TimelineCallback)
            {
                throw new NotSupportedException("Native Diablos timeline callback was replaced.");
            }
            ushort counter = task.ReadWord(0xC);
            if (counter >= 173)
            {
                throw new NotSupportedException("Native Diablos65551D stream playback requires its next original archive and phase services.");
            }
            if (counter >= 9 && inputs.PresentProgramme is null)
            {
                throw new NotSupportedException("Native Diablos656B70 opening programme requires its original current transform and primitive resource services.");
            }
            NativeBattleDiablos325State state = NativeBattleDiablos325Entry.RequireState(frame.Memory);
            NativeBattleDiablos325Resources resources = frame.Effect.Binding.Diablos325Resources ?? throw new NotSupportedException("Native Diablos requires its original files and sound table.");
            NativeBattleEffectMemory memory = frame.Memory;
            if (counter == 0)
            {
                if (resources.Sounds.IsDefaultOrEmpty || inputs.PlaySound is null)
                {
                    throw new NotSupportedException("Native Diablos requires its original first sound descriptor and service.");
                }
                memory = NativeBattleEffectIo.Queue5341D0(memory, 727, checked(state.ModelBuffer5208 + 0x1685C), 3);
                memory = NativeBattleEffectIo.Queue5341D0(memory, 728, checked(state.ModelBuffer5208 + 0x1685C), 2);
                memory = NativeBattleEffectIo.Queue5341D0(memory, 729, checked(state.ModelBuffer5208 + 0x2185C), 5);
                memory = NativeBattleEffectIo.Queue5341D0(memory, 730, checked(state.ModelBuffer5208 + 0x3505C), 5);
                memory = NativeBattleEffectIo.Queue5341D0(memory, 731, checked(state.ModelBuffer5208 + 0x1685C), 5);
            }
            else
            {
                state = state with { Flash51E8 = counter <= 8 ? checked(256u + (uint)(counter - 1) * 512) : 4096u };
                memory = memory with { Diablos325 = state };
            }
            if (counter >= 9)
            {
                NativeBattleDiablos325ProgrammeStep presented = inputs.PresentProgramme!(frame with { Memory = memory }, task);
                frame = presented.Frame;
                task = presented.Task;
                memory = frame.Memory;
            }
            if (counter is >= 31 and <= 90)
            {
                frame = (inputs.PresentWell ?? throw new NotSupportedException("Native Diablos well requires its original current geometry services."))(frame with { Memory = memory }, counter);
                memory = frame.Memory;
            }
            if (counter is >= 91 and <= 102)
            {
                NativeBattleDiablos325ProgrammeStep wings = (inputs.PresentWings ?? throw new NotSupportedException("Native Diablos paired wings require their original morph and geometry services."))(frame with { Memory = memory }, task);
                frame = wings.Frame;
                task = wings.Task;
                memory = frame.Memory;
            }
            if (NativeBattleDiablos325Entry.RequireState(memory).Mode51F0 == 3)
            {
                frame = (inputs.DrawPuffs ?? throw new NotSupportedException("Native Diablos requires its original projected puff drawing service."))(frame with { Memory = memory });
                memory = frame.Memory;
            }
            if (counter >= 103)
            {
                if (counter == 103 && NativeBattleDiablos325Entry.RequireState(memory).Pause5180 == 0)
                {
                    frame = NativeBattleDiablos325Particles.Initialize6548B9(frame with { Memory = memory });
                    memory = frame.Memory;
                }
                NativeBattleDiablos325ProgrammeStep shape = (inputs.PresentShapes ?? throw new NotSupportedException("Native Diablos requires its original current shape-cycle drawing service."))(frame with { Memory = memory }, task);
                frame = shape.Frame;
                task = shape.Task;
                memory = frame.Memory;
            }
            if (counter >= 92)
            {
                if (counter == 92)
                {
                    NativeBattleDiablos325BodyStep initialized = NativeBattleDiablos325Body.Initialize655190(memory, task, checked(current.BankAddress + (uint)(index * current.Stride)));
                    memory = initialized.Memory;
                    task = initialized.Task;
                }
                if (NativeBattleDiablos325Entry.RequireState(memory).Pause5180 == 0)
                {
                    NativeBattleDiablos325BodyStep posed = NativeBattleDiablos325Body.Advance6FBDB0(memory, task);
                    memory = posed.Memory;
                    task = posed.Task;
                }
                NativeBattleDiablos325ProgrammeStep body = (inputs.PresentBody ?? throw new NotSupportedException("Native Diablos requires its current height-faded body drawing service."))(frame with { Memory = memory }, task);
                frame = body.Frame;
                task = body.Task;
                memory = frame.Memory;
            }
            if (counter == 32)
            {
                NativeBattleEffectIoPoll selected = NativeBattleEffectIo.Select5342C0(memory, 0);
                frame = frame with { Memory = selected.Memory };
                if (selected.Request is { } selectedRequest)
                {
                    frame = (inputs.QueueFile ?? throw new NotSupportedException("Native Diablos requires its original file delivery service."))(frame, selectedRequest);
                }
                if (selected.Busy)
                {
                    frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(12, task.Payload.AsSpan()) } };
                    return new(current with { Slots = current.Slots.SetItem(index, task) }, 0);
                }
                (inputs.PlayStream ?? throw new NotSupportedException("Native Diablos requires its current summon stream playback service."))(frame, 128, 127);
                memory = frame.Memory;
            }
            if (counter is 87 or 169)
            {
                NativeBattleEffectIoPoll polled = NativeBattleEffectIo.Poll534270(memory);
                frame = frame with { Memory = polled.Memory };
                if (polled.Request is { } polledRequest)
                {
                    frame = (inputs.QueueFile ?? throw new NotSupportedException("Native Diablos requires its original file delivery service."))(frame, polledRequest);
                }
                if (polled.Busy)
                {
                    frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(12, task.Payload.AsSpan()) } };
                    return new(current with { Slots = current.Slots.SetItem(index, task) }, 0);
                }
                NativeBattleDiablos325StreamStep initialized = NativeBattleDiablos325Stream.Initialize655488(frame, task);
                frame = initialized.Frame;
                task = initialized.Task;
                memory = frame.Memory;
            }
            if (counter == 91)
            {
                if (task.ReadByte(0xF) == 0)
                {
                    frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(12, task.Payload.AsSpan()) } };
                    return new(current with { Slots = current.Slots.SetItem(index, task) }, 0);
                }
                (inputs.PlayStream ?? throw new NotSupportedException("Native Diablos requires its current summon stream playback service."))(frame, 128, 127);
                memory = NativeBattleEffectIo.Queue5341D0(memory, 732, checked(state.ModelBuffer5208 + 0x1685C), 5);
            }
            NativeBattleEffectIoAdvance advanced = NativeBattleEffectIo.Advance534210(memory);
            frame = frame with { Memory = advanced.Memory };
            if (advanced.Request is { } request)
            {
                frame = (inputs.QueueFile ?? throw new NotSupportedException("Native Diablos requires its original file delivery service."))(frame, request);
            }
            if (counter == 0)
            {
                inputs.PlaySound!(resources.Sounds[0], 1, 128);
            }
            if (counter == 91)
            {
                if (resources.Sounds.Length < 2 || inputs.PlaySound is null)
                {
                    throw new NotSupportedException("Native Diablos requires its second original sound descriptor and service.");
                }
                inputs.PlaySound(resources.Sounds[1], 1, 128);
            }
            if (counter is >= 92 and < 167 && NativeBattleDiablos325Entry.RequireState(frame.Memory).Pause5180 == 0)
            {
                if (((counter - 92) & 7) == 7)
                {
                    NativeBattleDiablos325PointChoice chosen = NativeBattleDiablos325Point.Choose6557E5(frame, task);
                    frame = frame with { Random = chosen.Random };
                    if (counter != 99)
                    {
                        frame = NativeBattleDiablos325Particles.Spawn659900(frame, chosen.Position, -128, chosen.Programme);
                    }
                }
                task = task.WriteDword(0x164, unchecked(task.ReadDword(0x164) + 32));
            }
            if (counter == 0 || counter >= 9)
            {
                frame = (inputs.PresentTail ?? throw new NotSupportedException("Native Diablos requires its executed camera and puff tail services."))(frame, counter);
            }
            task = task.WriteWord(0xC, checked((ushort)(counter + 1)));
            frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(12, task.Payload.AsSpan()) } };
            return new(current with { Slots = current.Slots.SetItem(index, task) }, 0);
        });
        return frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.Select(item => item.DescriptorAddress == step.Pool.DescriptorAddress ? step.Pool : item).ToImmutableArray() } };
    }
}
