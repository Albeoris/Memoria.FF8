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
            if (counter is >= 9 and <= 521 && inputs.PresentProgramme is null)
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
                state = state with { Flash51E8 = counter <= 8 ? checked(256u + (uint)(counter - 1) * 512) : counter < 522 ? 4096u : counter < 530 ? checked(256u + (uint)(529 - counter) * 512) : 0u };
                memory = memory with { Diablos325 = state };
            }
            if (counter is >= 9 and <= 521)
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
            if (counter is >= 103 and <= 202)
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
            if (counter is >= 238 and <= 297)
            {
                if ((counter is 238 or 246) && NativeBattleDiablos325Entry.RequireState(memory).Pause5180 == 0)
                {
                    NativeBattleDiablos325BodyStep selected = NativeBattleDiablos325Body.Select6574D0(memory, task, counter == 238 ? (byte)3 : (byte)4);
                    memory = selected.Memory;
                    task = selected.Task;
                }
                NativeBattleDiablos325ProgrammeStep shape = (inputs.PresentShapes ?? throw new NotSupportedException("Native Diablos requires its original thirteen-shape drawing service."))(frame with { Memory = memory }, task);
                frame = shape.Frame;
                task = shape.Task;
                memory = frame.Memory;
            }
            if (counter is >= 268 and <= 297 && NativeBattleDiablos325Entry.RequireState(memory).Pause5180 == 0)
            {
                task = task.WriteDword(0x164, unchecked(task.ReadDword(0x164) - 10));
            }
            if (counter is >= 268 and <= 327)
            {
                NativeBattleDiablos325ProgrammeStep programme = (inputs.PresentLateProgramme ?? throw new NotSupportedException("Native Diablos requires its current late programme service."))(frame with { Memory = memory }, task, false);
                frame = programme.Frame;
                task = programme.Task;
                memory = frame.Memory;
            }
            if (counter is >= 298 and <= 327)
            {
                NativeBattleDiablos325ProgrammeStep programme = (inputs.PresentLateProgramme ?? throw new NotSupportedException("Native Diablos requires its current simultaneous programme service."))(frame with { Memory = memory }, task, true);
                frame = programme.Frame;
                task = programme.Task;
                memory = frame.Memory;
            }
            if (counter == 328)
            {
                NativeBattleDiablos325BodyStep first = NativeBattleDiablos325Body.Select6574D0(memory, task, 5);
                NativeBattleDiablos325BodyStep second = NativeBattleDiablos325Body.Select6574D0(first.Memory, first.Task, 1, 0x1A8, 0x254);
                memory = second.Memory;
                task = second.Task;
            }
            if (counter is >= 360 and <= 367)
            {
                frame = (inputs.PresentLateStatic ?? throw new NotSupportedException("Native Diablos requires its current late static model service."))(frame with { Memory = memory }, counter);
                memory = frame.Memory;
            }
            if (counter is >= 368 and <= 405)
            {
                NativeBattleDiablos325ProgrammeStep sink = (inputs.PresentRecipients ?? throw new NotSupportedException("Native Diablos requires its current recipient sink service."))(frame with { Memory = memory }, task);
                frame = sink.Frame;
                task = sink.Task;
                memory = frame.Memory;
            }
            if (counter is >= 402 and <= 413)
            {
                frame = (inputs.PresentFlash ?? throw new NotSupportedException("Native Diablos requires its current fullscreen packet service."))(frame with { Memory = memory }, counter);
                memory = frame.Memory;
            }
            if (counter is >= 406 and <= 455)
            {
                NativeBattleDiablos325ProgrammeStep programme = (inputs.PresentLateProgramme ?? throw new NotSupportedException("Native Diablos requires its current final programme service."))(frame with { Memory = memory }, task, false);
                frame = programme.Frame;
                task = programme.Task;
                memory = frame.Memory;
            }
            if (counter == 456)
            {
                NativeBattleDiablos325BodyStep selected = NativeBattleDiablos325Body.Select6574D0(memory, task, 6);
                memory = selected.Memory;
                task = selected.Task;
            }
            if (counter is >= 92 and <= 621)
            {
                if (counter == 92)
                {
                    NativeBattleDiablos325BodyStep initialized = NativeBattleDiablos325Body.Initialize655190(memory, task, checked(current.BankAddress + (uint)(index * current.Stride)));
                    memory = initialized.Memory;
                    task = initialized.Task;
                }
                if (counter == 203 && NativeBattleDiablos325Entry.RequireState(memory).Pause5180 == 0)
                {
                    task = task.WriteDword(0x164, unchecked((uint)-6272));
                }
                if (counter is 173 or 201)
                {
                    NativeBattleDiablos325BodyStep selected = NativeBattleDiablos325Body.Select6574D0(memory, task, counter == 173 ? (byte)1 : (byte)2);
                    memory = selected.Memory;
                    task = selected.Task;
                }
                if (NativeBattleDiablos325Entry.RequireState(memory).Pause5180 == 0)
                {
                    NativeBattleDiablos325BodyStep posed = NativeBattleDiablos325Body.Advance6FBDB0(memory, task);
                    memory = posed.Memory;
                    task = posed.Task;
                }
                if (counter <= 368 || counter is >= 456 and <= 469)
                {
                    NativeBattleDiablos325ProgrammeStep body = (inputs.PresentBody ?? throw new NotSupportedException("Native Diablos requires its current height-faded body drawing service."))(frame with { Memory = memory }, task);
                    frame = body.Frame;
                    task = body.Task;
                    memory = frame.Memory;
                }
            }
            if (counter is >= 298 and <= 367)
            {
                if (counter == 298)
                {
                    NativeBattleDiablos325BodyStep initialized = NativeBattleDiablos325Body.InitializeSecondary6552FC(memory, task, checked(current.BankAddress + (uint)(index * current.Stride)));
                    memory = initialized.Memory;
                    task = initialized.Task;
                }
                if (NativeBattleDiablos325Entry.RequireState(memory).Pause5180 == 0)
                {
                    NativeBattleDiablos325BodyStep posed = NativeBattleDiablos325Body.Advance6FBDB0(memory, task, 0x1A8, 0x254);
                    memory = posed.Memory;
                    task = posed.Task;
                }
                NativeBattleDiablos325ProgrammeStep body = (inputs.PresentSecondaryBody ?? throw new NotSupportedException("Native Diablos requires its current secondary body service."))(frame with { Memory = memory }, task);
                frame = body.Frame;
                task = body.Task;
                memory = frame.Memory;
            }
            if (NativeBattleDiablos325Entry.RequireState(memory).Mode51F0 == 2)
            {
                frame = frame with { Memory = memory };
                if (NativeBattleDiablos325Entry.RequireState(memory).Pause5180 == 0)
                {
                    NativeBattleDiablos325LateResources tables = resources.LateResources ?? throw new NotSupportedException("Native Diablos trails require their original width table.");
                    frame = NativeBattleDiablos325Trail.Advance6583E0(frame, tables.Read(0xFDFF74, 4098));
                }
                frame = (inputs.DrawTrails ?? throw new NotSupportedException("Native Diablos requires its current physical trail drawing service."))(frame);
                memory = frame.Memory;
            }
            if (NativeBattleDiablos325Entry.RequireState(memory).Mode51F0 == 3)
            {
                frame = (inputs.DrawPuffs ?? throw new NotSupportedException("Native Diablos requires its original projected puff drawing service."))(frame with { Memory = memory });
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
                    frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(12, task.ReadRawPayloadSpan()) } };
                    return new(current.WriteSlot(index, task), 0);
                }
                (inputs.PlayStream ?? throw new NotSupportedException("Native Diablos requires its current summon stream playback service."))(frame, 128, 127);
                memory = frame.Memory;
            }
            if (counter is 87 or 169 or 465)
            {
                NativeBattleEffectIoPoll polled = NativeBattleEffectIo.Poll534270(memory);
                frame = frame with { Memory = polled.Memory };
                if (polled.Request is { } polledRequest)
                {
                    frame = (inputs.QueueFile ?? throw new NotSupportedException("Native Diablos requires its original file delivery service."))(frame, polledRequest);
                }
                if (polled.Busy)
                {
                    frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(12, task.ReadRawPayloadSpan()) } };
                    return new(current.WriteSlot(index, task), 0);
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
                    frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(12, task.ReadRawPayloadSpan()) } };
                    return new(current.WriteSlot(index, task), 0);
                }
                (inputs.PlayStream ?? throw new NotSupportedException("Native Diablos requires its current summon stream playback service."))(frame, 128, 127);
                memory = NativeBattleEffectIo.Queue5341D0(memory, 732, checked(state.ModelBuffer5208 + 0x1685C), 5);
            }
            if (counter == 173)
            {
                if (task.ReadByte(0xF) == 0)
                {
                    frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(12, task.ReadRawPayloadSpan()) } };
                    return new(current.WriteSlot(index, task), 0);
                }
                (inputs.PlayStream ?? throw new NotSupportedException("Native Diablos requires its current summon stream playback service."))(frame, 128, 127);
                memory = NativeBattleEffectIo.Queue5341D0(memory, 733, checked(state.ModelBuffer5208 + 0x1685C), 5);
            }
            if (counter == 203)
            {
                frame = NativeBattleDiablos325LateReset.ClearParticleLinks655567(frame with { Memory = memory });
                memory = NativeBattleEffectIo.Queue5341D0(frame.Memory, 734, checked(state.ModelBuffer5208 + 0x3505C), 5);
            }
            if (counter == 214)
            {
                NativeBattleEffectIoPoll selected = NativeBattleEffectIo.Select5342C0(memory, 0);
                frame = frame with { Memory = selected.Memory };
                if (selected.Request is { } selectedRequest)
                {
                    frame = (inputs.QueueFile ?? throw new NotSupportedException("Native Diablos requires its original file delivery service."))(frame, selectedRequest);
                }
                if (selected.Busy)
                {
                    frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(12, task.ReadRawPayloadSpan()) } };
                    return new(current.WriteSlot(index, task), 0);
                }
                NativeBattleDiablos325StreamStep initialized = NativeBattleDiablos325Stream.Initialize655488(frame, task);
                frame = initialized.Frame;
                task = initialized.Task;
                memory = frame.Memory;
            }
            if (counter is 218 or 469)
            {
                if (task.ReadByte(0xF) == 0)
                {
                    frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(12, task.ReadRawPayloadSpan()) } };
                    return new(current.WriteSlot(index, task), 0);
                }
                (inputs.PlayStream ?? throw new NotSupportedException("Native Diablos requires its current summon stream playback service."))(frame, 128, 127);
            }
            if (counter == 237)
            {
                NativeBattleEffectIoPoll polled = NativeBattleEffectIo.Poll534270(memory);
                frame = frame with { Memory = polled.Memory };
                if (polled.Request is { } polledRequest)
                {
                    frame = (inputs.QueueFile ?? throw new NotSupportedException("Native Diablos requires its original file delivery service."))(frame, polledRequest);
                }
                if (polled.Busy)
                {
                    frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(12, task.ReadRawPayloadSpan()) } };
                    return new(current.WriteSlot(index, task), 0);
                }
                memory = frame.Memory;
            }
            if (counter == 327)
            {
                memory = NativeBattleEffectIo.Queue5341D0(memory, 735, checked(state.ModelBuffer5208 + 0x3505C), 5);
            }
            if (counter == 359)
            {
                NativeBattleEffectIoPoll polled = NativeBattleEffectIo.Poll534270(memory);
                frame = frame with { Memory = polled.Memory };
                if (polled.Request is { } polledRequest)
                {
                    frame = (inputs.QueueFile ?? throw new NotSupportedException("Native Diablos requires its original file delivery service."))(frame, polledRequest);
                }
                if (polled.Busy)
                {
                    frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(12, task.ReadRawPayloadSpan()) } };
                    return new(current.WriteSlot(index, task), 0);
                }
                frame = (inputs.UploadLateTexture ?? throw new NotSupportedException("Native Diablos requires its current original texture upload service."))(frame, checked(state.ModelBuffer5208 + 0x42E54));
                memory = NativeBattleEffectIo.Queue5341D0(frame.Memory, 736, checked(state.ModelBuffer5208 + 0x1685C), 3);
            }
            if (counter == 436)
            {
                NativeBattleEffectIoPoll polled = NativeBattleEffectIo.Poll534270(memory);
                frame = frame with { Memory = polled.Memory };
                if (polled.Request is { } polledRequest)
                {
                    frame = (inputs.QueueFile ?? throw new NotSupportedException("Native Diablos requires its original file delivery service."))(frame, polledRequest);
                }
                if (polled.Busy)
                {
                    frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(12, task.ReadRawPayloadSpan()) } };
                    return new(current.WriteSlot(index, task), 0);
                }
                (inputs.PlayStream ?? throw new NotSupportedException("Native Diablos requires its current final stream playback service."))(frame, 128, 127);
                memory = NativeBattleEffectIo.Queue5341D0(frame.Memory, 727, checked(state.ModelBuffer5208 + 0x1685C), 5);
            }
            if (counter == 500)
            {
                memory = NativeBattleEffectIo.Queue5341D0(memory, 360, checked(state.ModelBuffer5208 + 0x1685C), 1);
                memory = NativeBattleEffectIo.Queue5341D0(memory, 361, checked(state.ModelBuffer5208 + 0x1685C), 1);
                memory = NativeBattleEffectIo.Queue5341D0(memory, 362, checked(state.ModelBuffer5208 + 0x1685C), 0);
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
            if (counter == 238)
            {
                if (resources.Sounds.Length < 3 || inputs.PlaySound is null)
                {
                    throw new NotSupportedException("Native Diablos requires its third original sound descriptor and service.");
                }
                inputs.PlaySound(resources.Sounds[2], 1, 128);
                frame = NativeBattleDiablos325Trail.Initialize657E70(frame, 5, 0, unchecked((ushort)-32));
            }
            if (counter == 333)
            {
                (inputs.PlaySound ?? throw new NotSupportedException("Native Diablos requires its fourth original sound service."))(resources.Sounds[3], 1, 128);
            }
            if (counter is >= 246 and <= 297)
            {
                frame = NativeBattleDiablos325Trail.SpawnAttached6559D3(frame, task);
            }
            if (counter is >= 298 and <= 317)
            {
                frame = NativeBattleDiablos325Trail.SpawnPaired655B7F(frame, task);
            }
            if (counter is 402 or 456)
            {
                int sound = counter == 402 ? 4 : 5;
                (inputs.PlaySound ?? throw new NotSupportedException("Native Diablos requires its final original sound services."))(resources.Sounds[sound], 1, 128);
            }
            if (counter is >= 376 and <= 405)
            {
                frame = NativeBattleDiablos325Trail.SpawnGround655D04(frame, counter);
            }
            if (counter is >= 406 and <= 421)
            {
                frame = NativeBattleDiablos325Trail.SpawnWide655F2B(frame, counter);
            }
            if (counter is >= 470 and <= 477)
            {
                NativeBattleDiablos325ProgrammeStep born = NativeBattleDiablos325Particles.Spawn6560B1(frame, task);
                frame = born.Frame;
                task = born.Task;
            }
            if (counter is >= 469 and <= 498)
            {
                if (counter == 469)
                {
                    frame = NativeBattleDiablos325Puffs.SpawnModel658D90(NativeBattleDiablos325Puffs.Reset6545B0(frame), task);
                }
                frame = NativeBattleDiablos325Puffs.Drift658CE0(frame);
            }
            if (counter == 499)
            {
                frame = (inputs.RestoreRecipients ?? throw new NotSupportedException("Native Diablos requires its current actor flag restoration service."))(frame, task);
                frame = frame with { Memory = frame.Memory with { Diablos325 = NativeBattleDiablos325Entry.RequireState(frame.Memory) with { Mode51F0 = 0 } } };
                frame = (inputs.ApplyContact ?? throw new NotSupportedException("Native Diablos requires its current contact application service."))(frame);
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
            if (counter is 177 or 178)
            {
                frame = NativeBattleDiablos325Puffs.SpawnRing6558A4(frame);
            }
            if (counter is >= 173 and <= 202)
            {
                frame = NativeBattleDiablos325Puffs.Drift658CE0(frame);
            }
            if (counter == 0 || counter >= 9)
            {
                frame = (inputs.PresentTail ?? throw new NotSupportedException("Native Diablos requires its executed camera and puff tail services."))(frame, counter);
            }
            if (counter == 526)
            {
                frame = (inputs.StopVibration ?? throw new NotSupportedException("Native Diablos requires its retained vibration stop service."))(frame, unchecked((int)task.ReadDword(0x10)));
            }
            int nextCounter = unchecked((short)(counter + 1));
            int result = 0;
            if (nextCounter >= 530)
            {
                if (nextCounter == 530)
                {
                    frame = (inputs.RestoreRecipients ?? throw new NotSupportedException("Native Diablos requires its final actor flag restoration service."))(frame, task);
                }
                NativeBattleEffectIoPoll ready = NativeBattleEffectIo.Select5342C0(frame.Memory, 2);
                frame = frame with { Memory = ready.Memory };
                if (ready.Request is { } finalRequest)
                {
                    frame = (inputs.QueueFile ?? throw new NotSupportedException("Native Diablos requires its original final file delivery service."))(frame, finalRequest);
                }
                result = ready.Busy ? 0 : 2;
                if (ready.Busy)
                {
                    nextCounter = 546;
                }
            }
            task = task.WriteWord(0xC, unchecked((ushort)nextCounter));
            frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(12, task.ReadRawPayloadSpan()) } };
            return new(current.WriteSlot(index, task), result);
        });
        return frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.Select(item => item.DescriptorAddress == step.Pool.DescriptorAddress ? step.Pool : item).ToImmutableArray() } };
    }
}
