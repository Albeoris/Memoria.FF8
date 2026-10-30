using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private static uint NativeParticipantAddress(int index)
    {
        return checked(0x1D27B10u + 208u * (uint)index);
    }

    private NativeBattleGraphicsState QueueNativeDescriptor(NativeBattleGraphicsState graphics, ushort command, uint argument, ushort? index = null, ushort? value = null, byte priority = 128)
    {
        if (command != 10 || argument is not (0x47DDA0 or 0x47DE70 or 0x47E120))
        {
            StopTurnAdvance();
        }
        (NativeBattleDescriptorState queued, int slot) = graphics.Descriptors.Enqueue(command, priority, argument, index, value);
        if (diagnosticAftercare is { HandlerActive: true })
        {
            ObserveDiagnosticInvocationAftercareDescriptorQueued(slot, command, argument, queued.Packets[slot]);
        }
        return graphics with { Descriptors = queued };
    }

    private NativeBattleGraphicsState QueueNativeEntryLoads(NativeBattleGraphicsState graphics)
    {
        ushort stage = checked((ushort)(definition.StageId ?? throw new NotSupportedException("Native1002 entry requires its actual formation stage BYTE.")));
        graphics = QueueNativeDescriptor(graphics, 1002, 0, stage);
        graphics = QueueNativeDescriptor(graphics, 1, 0);
        graphics = QueueNativeDescriptor(graphics, 1003, 0);
        graphics = QueueNativeDescriptor(graphics, 9, 0);
        foreach (BattleCombatant actor in combatants.Where(actor => !actor.IsPlayer && actor.IsActive).OrderBy(actor => actor.SceneSlot))
        {
            graphics = QueueNativeDescriptor(graphics, 102, NativeParticipantAddress(actor.NativeIndex), checked((ushort)actor.NativeIndex), checked((ushort)NativeModelId(actor)));
        }
        return QueueNativeDescriptor(graphics, 10, 0x47DD80);
    }

    private NativeBattleGraphicsState QueueNativeEntryModels(NativeBattleGraphicsState graphics)
    {
        foreach (BattleCombatant actor in combatants.Where(actor => !actor.IsPlayer && actor.IsActive).OrderBy(actor => actor.SceneSlot))
        {
            graphics = QueueNativeDescriptor(graphics, 103, NativeParticipantAddress(actor.NativeIndex), checked((ushort)actor.NativeIndex), (ushort)(1 | (actor.Hidden ? 0x100 : 0)));
        }
        foreach (BattleCombatant actor in combatants.Where(actor => actor.IsPlayer && actor.IsActive).OrderBy(actor => actor.NativeIndex))
        {
            graphics = QueueNativeDescriptor(graphics, 102, NativeParticipantAddress(actor.NativeIndex), checked((ushort)actor.NativeIndex), checked((ushort)NativeModelId(actor)));
        }
        graphics = QueueNativeDescriptor(graphics, 13, 0);
        foreach (BattleCombatant actor in combatants.Where(actor => actor.IsPlayer && actor.IsActive).OrderBy(actor => actor.NativeIndex))
        {
            graphics = QueueNativeDescriptor(graphics, 103, NativeParticipantAddress(actor.NativeIndex), checked((ushort)actor.NativeIndex), 0);
        }
        graphics = QueueNativeDescriptor(graphics, 10, 0x4A94B0);
        graphics = QueueNativeEntryAdvantageMessage(graphics);
        (NativeBattleDescriptorState descriptors, int barrier) = graphics.Descriptors.Enqueue(112, 128, 0);
        graphics = graphics with { Descriptors = descriptors, EntryBarrierPacket = barrier, EntryLifecycle = 2 };
        return QueueNativeDescriptor(graphics, 10, 0x47DD70);
    }

    private NativeBattleGraphicsState AdvanceNativeDescriptors(NativeBattleGraphicsState graphics)
    {
        NativeBattleDescriptorState final = graphics.Descriptors.Advance(Dispatch, Poll, (state, slot) => ObserveNativeDescriptorStack(graphics, state, slot), diagnosticInvocationPolicy is null ? null : ObserveDiagnosticInvocationPacketRemoved);
        return graphics with { Descriptors = final };

        NativeBattleDescriptorDispatch Dispatch(NativeBattleDescriptorState descriptors, int slot)
        {
            graphics = graphics with { Descriptors = descriptors };
            NativeBattleDescriptorPacket packet = descriptors.Packets[slot];
            NativeBattleDescriptorTaskKind kind;
            switch (packet.Command)
            {
                case 2:
                    PublishNativeGraphics500590();
                    return new(graphics.Descriptors, 15);
                case 3:
                    graphics = graphics with { Auxiliary = graphics.Auxiliary.AppendOpeningFade501CE0(15) };
                    return new(graphics.Descriptors, 15);
                case 14:
                    kind = NativeBattleDescriptorTaskKind.DeferredCompletion500BC0;
                    break;
                case 4:
                    graphics = BeginNativeOutcomeFade501E10(graphics);
                    return new(graphics.Descriptors, 15);
                case 1:
                    NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native500520 requires its camera owner.")).Initialize500520();
                    NativeEffectMemory = NativeBattleGraphicsInitialization.Initialize500520(NativeEffectMemory ?? throw new NotSupportedException("Native500520 requires its retained geometry owners."));
                    graphics = graphics with { Flags = graphics.Flags | 4 };
                    return new(graphics.Descriptors, 15);
                case 9:
                    kind = NativeBattleDescriptorTaskKind.Map500AC0;
                    break;
                case 8:
                    graphics = PublishNativeDescriptorMessage(graphics, packet);
                    return new(graphics.Descriptors, 15);
                case 10:
                    graphics = InvokeNativeDescriptorCallback(graphics, packet.Argument, packet.Data);
                    return new(graphics.Descriptors, 15);
                case 13:
                    kind = NativeBattleDescriptorTaskKind.Appearance500B60;
                    break;
                case 102:
                    kind = NativeBattleDescriptorTaskKind.Load502670;
                    break;
                case 103:
                    graphics = CompleteNativeGraphicsModel(graphics, packet);
                    return new(graphics.Descriptors, 15);
                case 104:
                    graphics = BeginNativeActionGraphics(graphics, packet.Argument);
                    kind = NativeBattleDescriptorTaskKind.Action104;
                    break;
                case 105:
                    NativeBattleGraphicsModel removed = graphics.Models[RequireDescriptorModel(packet.Index)];
                    if ((removed.Flags0 & 2) == 0)
                    {
                        return new(graphics.Descriptors, 15);
                    }
                    graphics = graphics with { Models = graphics.Models.SetItem(removed.Index, removed with { Flags0 = (ushort)(removed.Flags0 | 0x10) }) };
                    kind = NativeBattleDescriptorTaskKind.Remove502ED0;
                    break;
                case 106:
                    int index = RequireDescriptorModel(packet.Index);
                    graphics = ApplyNativeGraphicsStatus509CD0(graphics, index, 0);
                    graphics = SelectNativeHeaderSequence(graphics, checked((byte)index), 3);
                    return new(graphics.Descriptors, 15);
                case 112:
                    kind = NativeBattleDescriptorTaskKind.Barrier5085F0;
                    break;
                case 113:
                    kind = NativeBattleDescriptorTaskKind.Callback502F30;
                    break;
                case 108:
                case 109:
                case 110:
                case 111:
                case 114:
                case 117:
                case 118:
                case 119:
                    graphics = ApplyNativeDescriptorStatus502380(graphics, packet);
                    return new(graphics.Descriptors, 15);
                case 116:
                    kind = NativeBattleDescriptorTaskKind.AfterBattle502F90;
                    break;
                case 115:
                    kind = NativeBattleDescriptorTaskKind.Victory503040;
                    break;
                case 1002:
                    kind = NativeBattleDescriptorTaskKind.Stage506CF0;
                    break;
                case 1003:
                    kind = NativeBattleDescriptorTaskKind.Texture506DE0;
                    break;
                default:
                    throw new NotSupportedException($"Native graphics descriptor{packet.Command} requires its original dispatcher.");
            }
            return new(graphics.Descriptors.AppendTask(kind, slot).State, 8);
        }

        NativeBattleDescriptorCompletion Poll(NativeBattleDescriptorState descriptors, int taskSlot)
        {
            graphics = graphics with { Descriptors = descriptors };
            NativeBattleDescriptorTask task = descriptors.Tasks[taskSlot];
            int slot = task.PacketSlot;
            NativeBattleDescriptorPacket packet = descriptors.Packets[slot];
            bool completed = false;
            bool completePacket = true;
            byte phase = task.Phase;
            switch (task.Kind)
            {
                case NativeBattleDescriptorTaskKind.Stage506CF0:
                    if (phase == 0)
                    {
                        if ((packet.UnknownByteMask & 0x300) != 0)
                        {
                            throw new NotSupportedException("Native506CF0 requires its physical stage file index WORD8.");
                        }
                        graphics = graphics with { StageResourceIndex98990 = unchecked((byte)packet.Index) };
                    }
                    // TODO(TD-009): The authenticated stage is already loaded; retain native506CF0 phases while materializing its IO-ready input.
                    if (phase == 1)
                    {
                        LoadNativeStage500EA0();
                        graphics = graphics with { Flags = graphics.Flags | 8, UiServices = NativeBattleUiServices.Initialize() };
                        LoadNativeStagePalette506CF0();
                    }
                    completed = phase == 3;
                    phase++;
                    break;
                case NativeBattleDescriptorTaskKind.Texture506DE0:
                    graphics = RegisterNativeStage506DE0(graphics);
                    completed = true;
                    phase = 1;
                    break;
                case NativeBattleDescriptorTaskKind.Map500AC0:
                    NativeBattleEntryWaveStep wave = AdvanceNativeEntryWave500AC0(phase, task.Payload[2]);
                    phase = wave.Phase;
                    completed = wave.Completed;
                    byte[] wavePayload = task.Payload.ToArray();
                    wavePayload[2] = wave.Completion;
                    graphics = graphics with { Descriptors = graphics.Descriptors with { Tasks = graphics.Descriptors.Tasks.SetItem(taskSlot, task with { Payload = wavePayload.ToImmutableArray() }) } };
                    break;
                case NativeBattleDescriptorTaskKind.Load502670:
                    int modelIndex = RequireDescriptorModel(packet.Index);
                    NativeBattleGraphicsModel loading = graphics.Models[modelIndex];
                    BattleCombatant loadingActor = combatants.Single(candidate => candidate.NativeIndex == modelIndex);
                    if (phase == 0)
                    {
                        if ((loading.Flags0 & 1) != 0)
                        {
                            return new(descriptors, false);
                        }
                        NativeBattleGraphicsBinding loadingBinding = FindNativeGraphicsBinding(loadingActor.Id) ?? throw new InvalidDataException("Native502670 has no original resource binding.");
                        loading = loading with { Flags0 = (ushort)(loading.Flags0 | 1), ModelId4 = checked((byte)packet.Value), FileSelector128 = loadingBinding.Resource.LinkedFileSelector128, FileSelector129 = loadingBinding.Resource.BodyFileSelector129, EffectCounter97 = 1 };
                        graphics = graphics with { Models = graphics.Models.SetItem(modelIndex, loading), ActorIds = graphics.ActorIds.SetItem(modelIndex, loadingActor.Id), ModelLoadCount = graphics.ModelLoadCount + 1 };
                        QueueNativeModelResource507080(graphics, checked((ushort)packet.Value), checked((byte)modelIndex), packet.Argument);
                        phase = 1;
                    }
                    if (phase == 1 && PollNativeModelResource507070(ref graphics) == 0)
                    {
                        if (loadingActor.IsPlayer && packet.Value != 7)
                        {
                            QueueNativeModelResource507080(graphics, checked((ushort)(packet.Value + 0x1000)), checked((byte)modelIndex), packet.Argument);
                        }
                        phase = 2;
                    }
                    if (phase == 2 && (!loadingActor.IsPlayer || PollNativeModelResource507070(ref graphics) == 0))
                    {
                        graphics = graphics with { Models = graphics.Models.SetItem(modelIndex, graphics.Models[modelIndex] with { EffectCounter97 = 2 }), ModelLoadCount = graphics.ModelLoadCount - 1 };
                        completed = true;
                    }
                    break;
                case NativeBattleDescriptorTaskKind.Remove502ED0:
                    int removedIndex = RequireDescriptorModel(packet.Index);
                    NativeBattleGraphicsModel removed = graphics.Models[removedIndex];
                    if (phase == 0 && removed.Kind5 == 0)
                    {
                        graphics = graphics with { Models = graphics.Models.SetItem(removedIndex, NativeBattleGraphicsModels.StartService(removed, 5)) };
                        phase = 1;
                    }
                    else if (phase == 1 && removed.Kind5 == 0)
                    {
                        completed = true;
                    }
                    break;
                case NativeBattleDescriptorTaskKind.Appearance500B60:
                    completePacket = false;
                    if (phase == 0)
                    {
                        graphics = graphics with { Flags = graphics.Flags | 0x80, EntryGatePhase = 1, Descriptors = descriptors.SetPacketState(slot, 255) };
                        phase = 1;
                    }
                    else if (!ModelsBusy(graphics.Models))
                    {
                        graphics = graphics with { Flags = graphics.Flags & ~0x80u, EntryGatePhase = 2 };
                        completed = true;
                    }
                    break;
                case NativeBattleDescriptorTaskKind.Barrier5085F0:
                    byte completion = 0;
                    _ = NativeBattleGraphicsCompletion.PollTask112(graphics.Effects.PublishedCount, graphics.Models.Select(model => model.CompletionInputs()).ToArray(), NativeCameraTaskMask, ref completion);
                    completed = completion == 255;
                    if (slot == graphics.EntryBarrierPacket)
                    {
                        graphics = graphics with { EntryBarrierCompletion = completion };
                    }
                    break;
                case NativeBattleDescriptorTaskKind.Callback502F30:
                    int index = RequireDescriptorModel(packet.Index);
                    NativeBattleGraphicsModel model = graphics.Models[index];
                    if ((model.Flags0 & 2) != 0 && (model.Flags0 & 8) == 0 && (model.Flags8!.Value & 26) == 0 && (model.Kind5 != 0 || (model.ResourceFlags44!.Value & 64) == 0))
                    {
                        return new(descriptors, false);
                    }
                    graphics = InvokeNativeDescriptorCallback(graphics, packet.Argument, unchecked((ushort)packet.Index));
                    completed = true;
                    break;
                case NativeBattleDescriptorTaskKind.Action104:
                    (graphics, phase, completed) = PollNativeActionGraphics(graphics, phase);
                    break;
                case NativeBattleDescriptorTaskKind.DeferredCompletion500BC0:
                    if (phase == 0)
                    {
                        uint completionAddress = NativeBattleDescriptorState.CompletionAddress503040(taskSlot);
                        NativeBattleAuxiliaryState auxiliary = graphics.Auxiliary.AppendDeferredCompletion5086C0(completionAddress, (address, value) => graphics = graphics with { Descriptors = graphics.Descriptors.WriteCompletion501A70(address, value) });
                        graphics = graphics with { Auxiliary = auxiliary };
                        phase = 1;
                    }
                    else if (phase == 1 && task.Payload[2] != 0)
                    {
                        completed = true;
                    }
                    break;
                case NativeBattleDescriptorTaskKind.AfterBattle502F90:
                    (graphics, phase, completed) = PollNativeAfterBattle502F90(graphics, phase);
                    break;
                case NativeBattleDescriptorTaskKind.Victory503040:
                    (graphics, phase, completed) = PollNativeVictory503040(graphics, taskSlot, phase);
                    break;
                default:
                    throw new NotSupportedException("Native descriptor callback requires its original task owner.");
            }
            descriptors = graphics.Descriptors;
            byte[] payload = descriptors.Tasks[taskSlot].Payload.ToArray();
            payload[1] = phase;
            descriptors = descriptors with { Tasks = descriptors.Tasks.SetItem(taskSlot, descriptors.Tasks[taskSlot] with { Payload = payload.ToImmutableArray() }) };
            return new(completed && completePacket ? descriptors.SetPacketState(slot, 255) : descriptors, completed);
        }
    }

    private NativeBattleGraphicsState InvokeNativeDescriptorCallback(NativeBattleGraphicsState graphics, uint callback, uint argument)
    {
        return callback switch
        {
            0 => graphics,
            0x4852B0 => RunNativeDuelSelection4852B0(graphics),
            0x47DD90 => ResetNativeActionPacketCounts(graphics),
            0x47DDA0 or 0x47DE70 or 0x47E120 => RunNativeDescriptorActionCallback(graphics, callback),
            0x48F350 or 0x48F3F0 => RunNativeRenzokukenFinisherCallback(graphics, callback),
            0x48D1A0 or 0x4AD7C0 or 0x48E5A0 or 0x48E5E0 => RunNativeLimitGraphicsCallback(graphics, callback),
            0x47DD80 => graphics with { EntryLifecycle = 1 },
            0x4A94B0 => graphics with { UiServices = (graphics.UiServices ?? throw new NotSupportedException("Native4A94B0 requires its initialized942 menu owner.")).SetFlags(8) },
            0x47DD70 => graphics with { EntryLifecycle = 3, EntryReady = false },
            0x47DF60 => BeginNativeOutcome47DF60(graphics),
            0x47DFC0 => BeginNativeOutcomeWait47DFC0(graphics),
            0x48AD10 => CompleteNativeAllocationStatistics(graphics),
            0x487670 => CompleteNativeActivation(graphics, checked((int)argument)),
            0x4876B0 => CompleteNativeAllocationDescriptor(graphics),
            _ => throw new NotSupportedException($"Native graphics callback{callback:X} requires its original participant/global owner.")
        };
    }

    private NativeBattleGraphicsState CompleteNativeGraphicsModel(NativeBattleGraphicsState graphics, NativeBattleDescriptorPacket packet)
    {
        int index = RequireDescriptorModel(packet.Index);
        BattleCombatant actor = FindNativeActor(index);
        NativeBattleGraphicsBinding binding = FindNativeGraphicsBinding(actor.Id) ?? throw new InvalidDataException("Native103 has no original allocation/resource binding.");
        NativeGlobalRandomState randomState = nativeGlobalRandom ?? throw new NotSupportedException("Native103 requires saved534AA0 random state.");
        uint flags = definition.NativeGraphics!.Rules.Project(NativeSavedStatusBits(actor), NativeBattleStatusBits(actor));
        byte loadedModelId = graphics.Models[index].ModelId4 ?? throw new NotSupportedException("Native103 requires its actually loaded model BYTE4.");
        NativeBattleGraphicsModel model = NativeBattleGraphicsModels.CompleteLoad(graphics.Models[index], loadedModelId, 0, ref randomState, actor.NativeInformation246);
        if (!actor.IsPlayer)
        {
            if (model.EffectCounter97 != 2 || model.ActiveResourceRow100 is null || model.BodyTextureSlots is null)
            {
                throw new NotSupportedException("Native enemy103 requires its completed507120/507F80 physical resource load.");
            }
            model = model with { OriginalResourceRow104 = model.ActiveResourceRow100 };
        }
        else
        {
            if (model.EffectCounter97 != 2 || model.ActiveResourceRow100 is null || model.BodyTextureSlots is null)
            {
                throw new NotSupportedException("Native5027D0 requires its completed507080 resource load; no materialized substitute is available.");
            }
            model = model with { OriginalResourceRow104 = model.ActiveResourceRow100, LinkedTransform = binding.ModelId is 1 or 7 or 9 ? null : new(new(0, 0, 0), new(0, 0, 0)) };
        }
        NativeBattleStageColors colors = NativeStage?.ModelColors ?? throw new NotSupportedException("Native5027D0 requires the actual stage500EA0 color DWORD producers.");
        model = model with { Color40 = colors.Color40, Color44 = colors.Color44 };
        if (binding.InitialPosition is NativeBattleVector position)
        {
            NativeBattlePoseSource pose = NativeGraphicsPoseResource(graphics with { Models = graphics.Models.SetItem(index, model) }, index).PoseSource ?? throw new NotSupportedException("Native103 transform requires its actual current skeleton scaleWORD2.");
            short scale = unchecked((short)(System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(pose.Skeleton.AsSpan().Slice(2)) * 4));
            model = model with { Transform = new(position, position, 0, 0, scale) };
        }
        nativeGlobalRandom = randomState;
        nativeGlobalRandomCalls = checked(nativeGlobalRandomCalls + 3);
        //5027D0 clears DWORD8 before50A070 invokes509D10, including its palette/texture side effects.
        model = SetNativeGraphicsFlags(model, flags, graphics with { Models = graphics.Models.SetItem(index, model), ActorIds = graphics.ActorIds.SetItem(index, actor.Id) });
        byte standing = actor.IsPlayer ? NativeStandingSequence(model.Flags8!.Value) : (byte)1;
        model = model with { Flags0 = (packet.Value & 0x100) != 0 ? (ushort)(model.Flags0 | 4) : model.Flags0, SequenceState = (model.SequenceState ?? new()).InitializeControl5027D0(standing, actor.IsPlayer && standing != 8 ? standing : (byte)0) };
        if (loadedModelId == 143)
        {
            model = model with { Flags2 = (ushort)(model.Flags2!.Value & ~4), Transform = model.Transform is { } transform ? transform with { Scale38 = 4096 } : throw new NotSupportedException("Native model143 requires its actual initialized transform.") };
            for (int other = 3; other < 7; other++)
            {
                NativeBattleGraphicsModel owner = graphics.Models[other];
                if ((owner.Flags0 & 2) != 0 && owner.ModelId4 == 142)
                {
                    model = model.WriteLinkedModelIndex140(other);
                    graphics = graphics with { Models = graphics.Models.SetItem(other, owner.WriteLinkedModelIndex140(index)) };
                    break;
                }
            }
        }
        NativeBattleGraphicsState loaded = graphics with { Models = graphics.Models.SetItem(index, model), ActorIds = graphics.ActorIds.SetItem(index, actor.Id) };
        NativeBattlePoseSource loadedSkeleton = NativeGraphicsPoseResource(loaded, index).PoseSource ?? throw new NotSupportedException("Native model completion requires its actual loaded body rig bytes.");
        model = NativeBattleBodyBoneMatrices.PublishLoadedSkeletons(model, loadedSkeleton, NativeGraphicsPoseResource(loaded, index).LinkedPoseSource);
        return loaded with { Models = loaded.Models.SetItem(index, model) };
    }

    private static int RequireDescriptorModel(int index)
    {
        if ((uint)index >= 7)
        {
            throw new InvalidDataException("Native descriptor retained payload does not identify a physical graphics model.");
        }
        return index;
    }
}
