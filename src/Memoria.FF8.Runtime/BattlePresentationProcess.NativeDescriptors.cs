using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    private static uint NativeParticipantAddress(int index)
    {
        return checked(0x1D27B10u + 208u * (uint)index);
    }

    private NativeBattleGraphicsState QueueNativeDescriptor(NativeBattleGraphicsState graphics, ushort command, uint argument, ushort? index = null, ushort? value = null, byte priority = 128)
    {
        (NativeBattleDescriptorState queued, _) = graphics.Descriptors.Enqueue(command, priority, argument, index, value);
        return graphics with { Descriptors = queued };
    }

    private NativeBattleGraphicsState QueueNativeModelDescriptor(NativeBattleGraphicsState graphics, ushort command, uint argument, BattleModelOwner owner, ushort? value = null)
    {
        NativeBattleModelAliases aliases = graphics.NativeBindings.CaptureAliases(graphics.ActorModels);
        (NativeBattleDescriptorState queued, _) = graphics.Descriptors.EnqueueModel(command, 128, argument, owner, aliases, value);
        return graphics with { Descriptors = queued };
    }

    private NativeBattleGraphicsState AdvanceNativeDescriptors(NativeBattleGraphicsState graphics)
    {
        if (HasBattleEntry)
        {
            graphics = FeedHostedEntryDescriptors(graphics);
        }
        Action<NativeBattleGraphicsState, NativeBattleDescriptorState, int, NativeBattleDescriptorPacket>? observeRemoval = services?.DescriptorRemoved;
        NativeBattleDescriptorState final = graphics.Descriptors.Advance(Dispatch, Poll, (state, slot) => ObserveNativeDescriptorStack(graphics, state, slot), HasBattleEntry ? ObserveHostedPacketRemoved : observeRemoval is null ? null : ObservePacketRemoved);
        return graphics with { Descriptors = final };

        void ObserveHostedPacketRemoved(NativeBattleDescriptorState state, int slot, NativeBattleDescriptorPacket packet)
        {
            ObservePacketRemoved(state, slot, packet);
            ObserveHostedActionPacketRemoved(slot, packet);
            ObserveHostedCompletionRemoved(graphics, state, slot, packet);
            ObserveHostedControlPacketRemoved(slot, packet);
        }

        void ObservePacketRemoved(NativeBattleDescriptorState state, int slot, NativeBattleDescriptorPacket packet)
        {
            observeRemoval?.Invoke(graphics, state, slot, packet);
        }

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
                    if (HasBattleEntry && IsHostedCompletionCallback(packet.Argument))
                    {
                        ReachHostedCompletion(graphics, slot, packet);
                        return new(graphics.Descriptors, 15);
                    }
                    if (packet.Argument is 0x4971F0 or 0x497270)
                    {
                        graphics = ReachPartyReplacement(graphics, slot, packet);
                        return new(graphics.Descriptors, 15);
                    }
                    graphics = InvokeNativeDescriptorCallback(graphics, packet.Argument, packet.Argument == 0x487670 ? packet.Data : 0);
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
                    graphics = BeginNativeActionGraphics(graphics, packet.Header ?? throw new InvalidDataException("Descriptor104 requires a published action header reference."));
                    kind = NativeBattleDescriptorTaskKind.Action104;
                    break;
                case 105:
                    NativeBattleGraphicsModel removed = packet.ModelSubject is NativeBattleDescriptorModelSubject.AuthoredOriginalSlot original
                        ? graphics.NativeBindings.ReadOriginalCell(original.Slot, graphics.ActorModels)
                        : graphics.ActorModels.Get(RequireDescriptorModelOwner(graphics, packet.ModelSubject));
                    if ((removed.Flags0 & 2) == 0)
                    {
                        return new(graphics.Descriptors, 15);
                    }
                    BattleModelOwner removedOwner = RequireDescriptorModelOwner(graphics, packet.ModelSubject);
                    graphics = graphics with { ActorModels = graphics.ActorModels.Set(removedOwner, removed with { Flags0 = (ushort)(removed.Flags0 | 0x10) }) };
                    kind = NativeBattleDescriptorTaskKind.Remove502ED0;
                    break;
                case 106:
                    BattleModelOwner dying = RequireDescriptorModelOwner(graphics, packet.ModelSubject);
                    graphics = ApplyNativeGraphicsStatus509CD0(graphics, dying, 0);
                    graphics = SelectNativeHeaderSequence(graphics, dying, 3);
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
                        graphics = graphics with { StageResourceIndex98990 = unchecked((byte)packet.ScalarWord8) };
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
                    var plan = task.ModelSubject as NativeBattleDescriptorModelSubject.PlannedLoad ?? throw new InvalidDataException("Native502670 requires its prepared model owner.");
                    BattleModelOwner loadingOwner = plan.Owner;
                    bool partyLoad = loadingOwner.Side == BattleSide.Allies;
                    if (phase == 0)
                    {
                        NativeBattleGraphicsModel loading;
                        if (plan.OriginalSlot is { } originalSlot)
                        {
                            loading = graphics.NativeBindings.ReadOriginalCell(originalSlot, graphics.ActorModels);
                        }
                        else if (graphics.ActorModels.TryGet(loadingOwner, out NativeBattleGraphicsModel? existing))
                        {
                            loading = existing;
                        }
                        else
                        {
                            // A newly allocated hosted row starts with cold visual timers. Existing rows keep their retained bytes.
                            ImmutableArray<byte?>? timers = HasBattleEntry ? Enumerable.Repeat<byte?>(0, 32).ToImmutableArray() : null;
                            loading = NativeBattleGraphicsModels.ClearUnloadedModel(null, timers, null, null, null);
                        }
                        if ((loading.Flags0 & 1) != 0)
                        {
                            return new(descriptors, false);
                        }
                        if (HasBattleEntry)
                        {
                            if (FindHostedAllocationBinding(hostedModelRegistry, loadingOwner) is null)
                            {
                                _ = RequireHostedEntryFacts(loadingOwner);
                            }
                        }
                        if (HasBattleEntry && packet.Value != plan.Binding.ModelId)
                        {
                            throw new InvalidDataException("Entry model load differs from its admitted resource binding.");
                        }
                        if (HasBattleEntry && hostedModelRegistry?.Loads.TryGetValue(loadingOwner, out HostedModelLoadAdmission? admission) == true && (admission.Load is HostedDispatchControl.LoadParty || plan.OriginalSlot is not null))
                        {
                            if (!TryRebindHostedAllocationModel(graphics, plan, slot, out graphics))
                            {
                                return new(descriptors, false);
                            }
                            loading = graphics.ActorModels.Get(loadingOwner);
                        }
                        else if (plan.OriginalSlot is { } slotToBind)
                        {
                            (NativeBattleModelBoundary boundary, BattleActorModels models) = graphics.NativeBindings.BindOriginalSlot(slotToBind, loadingOwner, graphics.ActorModels);
                            graphics = graphics with { NativeBindings = boundary, ActorModels = models };
                        }
                        else if (!graphics.ActorModels.Contains(loadingOwner))
                        {
                            graphics = graphics with { ActorModels = graphics.ActorModels.Add(loadingOwner, loading) };
                        }
                        loading = loading with { Flags0 = (ushort)(loading.Flags0 | 1), ModelId4 = checked((byte)packet.Value), FileSelector128 = plan.Binding.Resource.LinkedFileSelector128, FileSelector129 = plan.Binding.Resource.BodyFileSelector129, EffectCounter97 = 1 };
                        graphics = graphics with { ActorModels = graphics.ActorModels.Set(loadingOwner, loading), ModelLoadCount = graphics.ModelLoadCount + 1 };
                        if (HasBattleEntry)
                        {
                            if (FindHostedAllocationBinding(hostedModelRegistry, loadingOwner) is null)
                            {
                                graphics = graphics with { ActorModels = OrderHostedEntryModels(graphics.ActorModels) };
                            }
                            BattleModelRoster plannedRoster = nativeActionPackets.Roster;
                            nativeActionPackets = nativeActionPackets.WithRoster(plannedRoster, graphics.NativeBindings.CaptureAliases(graphics.ActorModels, plannedRoster));
                        }
                        else
                        {
                            nativeActionPackets = nativeActionPackets.WithRoster(graphics.ActorModels.Roster, graphics.NativeBindings.CaptureAliases(graphics.ActorModels));
                        }
                        QueueNativeModelResource507080(graphics, packet.Value, loadingOwner, packet.Argument);
                        phase = 1;
                    }
                    if (phase == 1 && PollNativeModelResource507070(ref graphics) == 0)
                    {
                        if (partyLoad && packet.Value != 7)
                        {
                            QueueNativeModelResource507080(graphics, checked((ushort)(packet.Value + 0x1000)), loadingOwner, packet.Argument);
                        }
                        phase = 2;
                    }
                    if (phase == 2 && (!partyLoad || PollNativeModelResource507070(ref graphics) == 0))
                    {
                        graphics = graphics with { ActorModels = graphics.ActorModels.Set(loadingOwner, graphics.ActorModels.Get(loadingOwner) with { EffectCounter97 = 2 }), ModelLoadCount = graphics.ModelLoadCount - 1 };
                        completed = true;
                    }
                    break;
                case NativeBattleDescriptorTaskKind.Remove502ED0:
                    BattleModelOwner removedIndex = RequireDescriptorModelOwner(graphics, task.ModelSubject);
                    NativeBattleGraphicsModel removed = graphics.ActorModels.Get(removedIndex);
                    if (phase == 0 && removed.Kind5 == 0)
                    {
                        graphics = graphics with { ActorModels = graphics.ActorModels.Set(removedIndex, NativeBattleGraphicsModels.StartService(removed, 5)) };
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
                    else if (!ModelsBusy(graphics.ActorModels))
                    {
                        graphics = graphics with { Flags = graphics.Flags & ~0x80u, EntryGatePhase = 2 };
                        completed = true;
                    }
                    break;
                case NativeBattleDescriptorTaskKind.Barrier5085F0:
                    byte completion = 0;
                    _ = NativeBattleGraphicsCompletion.PollTask112(graphics.Effects.PublishedCount, graphics.ActorModels, () => NativeCameraTaskMask, ref completion);
                    completed = completion == 255;
                    if (slot == graphics.EntryBarrierPacket)
                    {
                        graphics = graphics with { EntryBarrierCompletion = completion };
                    }
                    break;
                case NativeBattleDescriptorTaskKind.Callback502F30:
                    bool blocked = task.ModelSubject switch
                    {
                        NativeBattleDescriptorModelSubject.ExplicitModel model => NativeBattleDescriptorCallbacks.IsBlocked502F30(graphics.ActorModels.Get(model.Owner)),
                        NativeBattleDescriptorModelSubject.AuthoredOriginalSlot original => NativeBattleDescriptorCallbacks.IsBlocked502F30(graphics.NativeBindings.ReadOriginalCell(original.Slot, graphics.ActorModels)),
                        NativeBattleDescriptorModelSubject.PlannedLoad { OriginalSlot: { } originalProbeSlot } => NativeBattleDescriptorCallbacks.IsBlocked502F30(graphics.NativeBindings.ReadOriginalCell(originalProbeSlot, graphics.ActorModels)),
                        NativeBattleDescriptorModelSubject.PlannedLoad retainedPlan => NativeBattleDescriptorCallbacks.IsBlocked502F30(graphics.ActorModels.Get(retainedPlan.Owner)),
                        _ => NativeBattleDescriptorCallbacks.IsBlocked502F30(graphics.ActorModels, graphics.NativeBindings, packet.ScalarWord8, address => ReadNativeDescriptorByte(graphics, address))
                    };
                    if (blocked)
                    {
                        return new(descriptors, false);
                    }
                    if (packet.Argument == 0x487670)
                    {
                        BattleModelOwner owner = task.ModelSubject switch
                        {
                            NativeBattleDescriptorModelSubject.ExplicitModel model => model.Owner,
                            NativeBattleDescriptorModelSubject.AuthoredOriginalSlot original => graphics.NativeBindings.DecodeLiteral(original.Slot.Offset, graphics.ActorModels),
                            _ => throw new InvalidDataException("Native activation requires its published model subject.")
                        };
                        NativeBattleModelSlot? originalSlot = (task.ModelSubject as NativeBattleDescriptorModelSubject.AuthoredOriginalSlot)?.Slot;
                        graphics = CompleteNativeActivation(graphics, owner, originalSlot);
                    }
                    else
                    {
                        graphics = InvokeNativeDescriptorCallback(graphics, packet.Argument, 0);
                    }
                    completed = true;
                    break;
                case NativeBattleDescriptorTaskKind.Action104:
                    (graphics, phase, completed) = PollNativeRenzokukenAction104(graphics, slot, packet, phase);
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
            0x4852B0 => RunExpandedHostedDuelSelection(graphics),
            0x47DD90 => ResetNativeActionPacketCounts(graphics),
            0x48F350 or 0x48F3F0 => RunExpandedHostedRenzokukenFinisher(graphics, callback),
            0x48D1A0 or 0x4AD7C0 or 0x48E5A0 or 0x48E5E0 => RunNativeLimitGraphicsCallback(graphics, callback),
            0x47DD80 => graphics with { EntryLifecycle = 1 },
            0x4A94B0 => graphics with { UiServices = (graphics.UiServices ?? throw new NotSupportedException("Native4A94B0 requires its initialized942 menu owner.")).SetFlags(8) },
            0x47DD70 => graphics with { EntryLifecycle = 3, EntryReady = false },
            0x47DF60 => BeginNativeOutcome47DF60(graphics),
            0x47DFC0 => BeginNativeOutcomeWait47DFC0(graphics),
            0x48AD10 => ReachHostedNativeAllocationStatistics(graphics),
            0x487670 => CompleteNativeActivation(graphics, graphics.NativeBindings.DecodeLiteral(checked((byte)argument), graphics.ActorModels), NativeBattleModelSlot.Decode(checked((byte)argument))),
            0x4876B0 => CompleteNativeAllocationDescriptor(graphics),
            _ => throw new NotSupportedException($"Native graphics callback{callback:X} requires its original participant/global owner.")
        };
    }

    private NativeBattleGraphicsState CompleteNativeGraphicsModel(NativeBattleGraphicsState graphics, NativeBattleDescriptorPacket packet)
    {
        var plan = packet.ModelSubject as NativeBattleDescriptorModelSubject.PlannedLoad ?? throw new InvalidDataException("Native103 requires its prepared model owner.");
        BattleModelOwner owner = plan.Owner;
        bool isParty = owner.Side == BattleSide.Allies;
        if (HasBattleEntry)
        {
            if (FindHostedAllocationBinding(hostedModelRegistry, owner) is { } allocatedBinding)
            {
                NativeBattleModelLifecycleServices services = hostedFrameServices ?? throw new InvalidOperationException("Hosted native 103 requires its current actor facts.");
                if (!ReferenceEquals(plan.Binding, allocatedBinding) || !graphics.ActorModels.Contains(owner) ||
                    !services.Facts.TryGetValue(owner, out NativeBattleModelActorFacts? allocation) || allocation.PendingAllocationStatistics ||
                    plan.OriginalSlot is { } originalSlot && graphics.NativeBindings.FindOriginalOwner(originalSlot, graphics.ActorModels) != owner)
                {
                    throw new InvalidDataException("Hosted native 103 requires the loaded identity and committed allocation statistics.");
                }
                return CompleteNativeGraphicsModel(graphics, packet, owner, allocatedBinding, isParty, allocation.Information246, allocation.SavedStatus,
                    allocation.RawBattleStatuses ?? throw new InvalidDataException("Hosted native 103 requires the original battle status."), ReadModelCompletionRandom(), plan.OriginalSlot is null);
            }
            NativeBattleModelEntryFacts facts = RequireHostedEntryFacts(owner);
            ValidateHostedEntryModelCompletion(graphics, owner, plan.Binding, isParty);
            return CompleteNativeGraphicsModel(graphics, packet, owner, plan.Binding, isParty, facts.Information246, facts.SavedStatus, facts.BattleStatus, ReadModelCompletionRandom(), plan.OriginalSlot is null);
        }
        throw new NotSupportedException("Native model completion requires an entered battle model registry.");
    }

    private NativeGlobalRandomState ReadModelCompletionRandom()
    {
        return nativeGlobalRandom ?? throw new NotSupportedException("Native103 requires saved534AA0 random state.");
    }

    private NativeBattleGraphicsState CompleteNativeGraphicsModel(NativeBattleGraphicsState graphics, NativeBattleDescriptorPacket packet, BattleModelOwner owner, NativeBattleGraphicsBinding binding, bool isParty, byte? information246, ushort savedStatus, uint battleStatus, NativeGlobalRandomState randomState, bool initializeMissingWorldMatrix = false)
    {
        uint flags = this.resources.NativeGraphics!.Rules.Project(savedStatus, battleStatus);
        byte loadedModelId = graphics.ActorModels.Get(owner).ModelId4 ?? throw new NotSupportedException("Native103 requires its actually loaded model BYTE4.");
        NativeBattleGraphicsModel model = NativeBattleGraphicsModels.CompleteLoad(graphics.ActorModels.Get(owner), loadedModelId, 0, isParty, ref randomState, information246);
        if (!isParty)
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
            NativeBattlePoseSource pose = NativeGraphicsPoseResource(graphics with { ActorModels = graphics.ActorModels.Set(owner, model) }, owner).PoseSource ?? throw new NotSupportedException("Native103 transform requires its actual current skeleton scaleWORD2.");
            short scale = pose.ReadModelScale5027D0();
            model = model with { Transform = new(position, position, 0, 0, scale) };
        }
        nativeGlobalRandom = randomState;
        nativeGlobalRandomCalls = checked(nativeGlobalRandomCalls + 3);
        //5027D0 clears DWORD8 before50A070 invokes509D10, including its palette/texture side effects.
        model = SetNativeGraphicsFlags(owner, model, flags, graphics with { ActorModels = graphics.ActorModels.Set(owner, model) });
        byte standing = isParty ? NativeStandingSequence(model.Flags8!.Value) : (byte)1;
        model = model with { Flags0 = (packet.Value & 0x100) != 0 ? (ushort)(model.Flags0 | 4) : model.Flags0, SequenceState = (model.SequenceState ?? new()).InitializeControl5027D0(standing, isParty && standing != 8 ? standing : (byte)0) };
        if (loadedModelId == 143)
        {
            model = model with { Flags2 = (ushort)(model.Flags2!.Value & ~4), Transform = model.Transform is { } transform ? transform with { Scale38 = 4096 } : throw new NotSupportedException("Native model143 requires its actual initialized transform.") };
            foreach (BattleModelEntry<BattleEnemyModelOwner> entry in graphics.ActorModels.Enemies.Entries)
            {
                if ((entry.State.Flags0 & 2) != 0 && entry.State.ModelId4 == 142)
                {
                    model = model.WriteLinkedModel(entry.Owner);
                    graphics = graphics with { ActorModels = graphics.ActorModels.Set(entry.Owner, entry.State.WriteLinkedModel(owner)) };
                    break;
                }
            }
        }
        NativeBattleGraphicsState loaded = graphics with { ActorModels = graphics.ActorModels.Set(owner, model) };
        NativeBattlePoseSource loadedSkeleton = NativeGraphicsPoseResource(loaded, owner).PoseSource ?? throw new NotSupportedException("Native model completion requires its actual loaded body rig bytes.");
        model = NativeBattleBodyBoneMatrices.PublishLoadedSkeletons(model, loadedSkeleton, NativeGraphicsPoseResource(loaded, owner).LinkedPoseSource);
        if (initializeMissingWorldMatrix && model.GeometryWorldMatrix is null)
        {
            // Entry sequences may read bone anchors before the first model-frame matrix update.
            model = model with { GeometryWorldMatrix = NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(model, loadedSkeleton) };
        }
        return loaded with { ActorModels = loaded.ActorModels.Set(owner, model) };
    }

    private static BattleModelOwner RequireDescriptorModelOwner(NativeBattleGraphicsState graphics, NativeBattleDescriptorModelSubject? subject)
    {
        return subject switch
        {
            NativeBattleDescriptorModelSubject.ExplicitModel model when graphics.ActorModels.Roster.Contains(model.Owner) => model.Owner,
            NativeBattleDescriptorModelSubject.AuthoredOriginalSlot original => graphics.NativeBindings.DecodeLiteral(original.Slot.Offset, graphics.ActorModels),
            _ => throw new InvalidDataException("The native descriptor requires its current model subject.")
        };
    }
}
