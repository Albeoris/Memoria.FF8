using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private bool nativeGraphicsPresentationDriven;

    public void AdvanceNativeGraphicsPresentation(TimeSpan elapsed)
    {
        AdvanceNativeGraphicsPresentation(elapsed, null);
    }

    public void AdvanceNativeGraphicsPresentation(TimeSpan elapsed, TimeSpan? idleWorkBudget)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(elapsed.Ticks);
        if (idleWorkBudget is { } budget)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(budget.Ticks);
        }
        if (expandedPresentationProcess is { } process)
        {
            RequireExpandedPresentationDispatchIdle();
            long callbacks = process.AccumulateElapsed(elapsed);
            nativeGraphicsPresentationDriven = true;
            for (long count = callbacks; count > 0; count--)
            {
                DispatchTurnBasedFrame();
            }
            return;
        }
        if (NativeGraphics is not { } graphics)
        {
            return;
        }
        NativeBattleGraphicsPresentationStep step = NativeBattleGraphicsPresentationClock.Advance(graphics.PresentationSubticks, graphics.PresentationHalfSubtick, elapsed);
        nativeGraphicsPresentationDriven = true;
        NativeGraphics = graphics with { PresentationSubticks = step.Subticks, PresentationHalfSubtick = step.HalfSubtick };
        if (GuardianAnimationSkipPending)
        {
            AdvanceSkippedGuardianFormPresentation(idleWorkBudget ?? TimeSpan.FromMilliseconds(8));
            return;
        }
        for (long count = step.Callbacks; count > 0; count--)
        {
            DispatchTurnBasedFrame();
            if (GuardianAnimationSkipPending)
            {
                AdvanceSkippedGuardianFormPresentation(idleWorkBudget ?? TimeSpan.FromMilliseconds(8));
                break;
            }
        }
    }

    private void QueueNativeGraphicsDeath(BattleCombatant actor)
    {
        NativeBattleGraphicsState graphics = RequireNativeGraphics();
        BattleModelOwner owner = RequirePresentationModelOwner(graphics.ActorModels.Roster, actor);
        NativeGraphics = QueueNativeModelDescriptor(graphics, 106, 0, owner);
        actor.NativeStatus0 |= 1;
        actor.NativeCombatantFlags &= ~0xCu;
        actor.Atb = 0;
    }

    private void QueueNativeGraphicsBarrier()
    {
        NativeBattleGraphicsState graphics = RequireNativeGraphics();
        NativeGraphics = QueueNativeDescriptor(graphics, 112, 0);
    }

    private void QueueNativeGraphicsRemoval(BattleCombatant actor)
    {
        NativeBattleGraphicsState graphics = RequireNativeGraphics();
        BattleModelOwner owner = RequirePresentationModelOwner(graphics.ActorModels.Roster, actor);
        NativeGraphics = QueueNativeModelDescriptor(graphics, 105, 0, owner);
    }

    private NativeBattleGraphicsState RequireNativeGraphics() => NativeGraphics ?? throw new NotSupportedException("Native graphical descriptors require their explicitly initialized seven-row resource bank.");

    private void AdvanceNativeGraphicsFrame()
    {
        try
        {
            AdvanceNativeGraphicsFrameCore();
        }
        catch
        {
            if (encounterAllocation is { Phase: BattleEncounterAllocationPhase.Loading } && encounterAllocationRollback is not null)
            {
                AbortEncounterAllocation();
            }
            throw;
        }
    }

    private void AdvanceNativeGraphicsFrameCore(NativeBattleModelLifecycleServices? services = null)
    {
        if (NativeGraphics is not { } graphics)
        {
            return;
        }
        if (services is not null)
        {
            services.Validate(nativeActionPackets.Roster, graphics);
        }
        graphics.Validate();
        uint flags = graphics.Flags;
        if (graphics.EntryLifecycle == 1)
        {
            graphics = services is null ? QueueNativeEntryModels(graphics) : QueueHostedNativeEntryModels(graphics);
        }
        //500900: descriptor/callback listAA4, then published effect listAA8, then model listA94, then camera5033E0.
        try
        {
            graphics = AdvanceNativeGraphicsRequests(graphics);
            if (services is null)
            {
                graphics = AdvanceEncounterAllocationRetirements(graphics);
                graphics = AdvanceEncounterAllocationPreparation(graphics);
                graphics = AdvanceEncounterPolicySpawn(graphics);
            }
            flags = graphics.Flags;
            graphics = AdvanceNativeAuxiliary(graphics);
            graphics = AdvanceNativeModuleEffects(graphics);
            graphics = AdvanceNativeOriginalEffect(graphics);
            graphics = AdvanceGuardianFormPresentation(graphics, services);
        }
        catch when (services is null && GuardianFormOwnsCurrentPresentation())
        {
            FailOwnedGuardianFormPresentation();
            throw;
        }
        flags = graphics.Flags;
        graphics = SubmitNativeModulePackets(graphics);
        graphics = ConsumeNativeGraphicsContacts(graphics);
        NativeBattleEffectsStep effects = graphics.Effects.Advance(graphics.ActorModels, owner => ProjectNativeDamageText(graphics, owner));
        foreach (BattleModelOwner owner in effects.ResetModelStatuses)
        {
            graphics = ApplyNativeGraphicsStatus509CD0(graphics, owner, 16);
        }
        graphics = graphics with { Effects = effects.State };
        graphics = FrameGuardianFormIdleCamera(graphics, services?.GuardianForm);
        using NativeBattleLogicalOrderingTransaction? ordering = definition.NativeGraphics?.PrimitiveTables is not null && graphics.PrimitiveFrame is { } primitiveFrame ? new(primitiveFrame.Pending) : null;
        using NativeBattleCommonPacketTransaction? common = ordering is not null ? new(NativeEffectMemory?.PrimitiveProcess ?? throw new NotSupportedException("Native model producers require their retained common packet process.")) : null;
        if (ordering is not null)
        {
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame! with { Pending = ordering.View } };
            NativeEffectMemory = NativeEffectMemory! with { PrimitiveProcess = common!.View };
        }
        try
        {
            graphics = AdvanceNativeLoadedModels(graphics, flags, services);
            graphics = AdvanceNativeStage506C30(graphics);
            if (ordering is not null)
            {
                graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame! with { Pending = ordering.Freeze() } };
                NativeBattleEffectMemory produced = NativeEffectMemory ?? throw new NotSupportedException("Native model producers lost their retained common packet process.");
                NativeEffectMemory = produced with { PrimitiveProcess = common!.Freeze(produced.PrimitiveProcess ?? throw new NotSupportedException("Native model producers lost their retained common packet process.")) };
            }
        }
        catch
        {
            if (common is not null && NativeEffectMemory is { PrimitiveProcess: { } publishedProcess } publishedMemory && ReferenceEquals(publishedProcess.CommonTransaction, common))
            {
                NativeEffectMemory = publishedMemory with { PrimitiveProcess = common.Freeze(publishedProcess) };
            }
            if (ordering is not null && NativeGraphics is { PrimitiveFrame: { } published } && ReferenceEquals(published.Pending.Transaction, ordering))
            {
                NativeGraphics = NativeGraphics with { PrimitiveFrame = published with { Pending = ordering.Freeze() } };
            }
            throw;
        }
        // The committed owners are reused by the render tail; its resets are outside the producer journals.
        common?.Dispose();
        ordering?.Dispose();
        nativePilotSourceProjection = NativeCamera?.Projection;
        nativePilotSourceCamera = EncounterPresentationCamera;
        NativeGraphics = graphics with { Callback = checked(graphics.Callback + 1) };
        AdvanceNativeCamera(flags);
        if (NativeCamera is not null)
        {
            NativeCamera = NativeCamera.PublishProjection(flags, presentationPose: PrepareEncounterCamera(NativeCamera));
        }
        //The original loc_5005A0 tail calls501B60 at50065E regardless of flags4/8, before501230 and505D20.
        DrainNativeMusic501B60();
        if (NativeEffectMemory is { } effectMemory)
        {
            effectMemory = effectMemory.PublishSound();
            if ((graphics.Flags & 8) != 0 && effectMemory.Textures is { } textures)
            {
                effectMemory = DrainNativeTextureUploads(effectMemory, textures);
            }
            NativeEffectMemory = effectMemory;
        }
        NativeGraphics = FinishNativePrimitiveFrame500900(NativeGraphics!);
        NativeGraphics = PublishNativePcRenderTail47CF60(NativeGraphics);
        NativeGraphics = RetainNativeSceneReplaySource(NativeGraphics);
        ObserveDiagnosticInvocationGraphicsFrameEnd();
        ObserveDiagnosticInvocationAftercareFrameEnd();
    }

    private NativeBattleEffectMemory DrainNativeTextureUploads(NativeBattleEffectMemory memory, NativeBattleEffectTextureState textures)
    {
        return DrainNativeTextureUploads(memory, textures, NativeFileStaging);
    }

    internal static NativeBattleEffectMemory DrainNativeTextureUploads(NativeBattleEffectMemory memory, NativeBattleEffectTextureState textures, NativeBattleFileStaging? staging)
    {
        if (textures.PresentationTextures is { } presentationTextures)
        {
            textures.Validate();
            foreach (NativeBattlePresentationTextureBank bank in presentationTextures.Values)
            {
                if (bank.Textures.Count != 0 && (bank.Textures.Animation is not null || bank.Textures.Slots.Take(bank.Textures.Count).Any(upload => upload?.Kind == 2)))
                {
                    throw new NotSupportedException("Actor texture queues cannot publish shared arena readback or effect animation.");
                }
            }
        }
        NativeBattleEffectTextureDrain drain = Drain(textures, memory.Arena);
        NativeBattlePrimitiveProcess? process = memory.PrimitiveProcess;
        if (process?.TextureWindowCache is { } cache)
        {
            process = process with { TextureWindowCache = InvalidateTextureUploads(cache, drain.Uploads, textures) };
        }
        NativeBattleEffectArena? arena = drain.Arena ?? memory.Arena;
        NativeBattleEffectTextureState published = drain.State;
        if (published.PresentationTextures is { } banks)
        {
            foreach ((string actorId, NativeBattlePresentationTextureBank bank) in banks.OrderBy(entry => entry.Key, StringComparer.Ordinal))
            {
                if (bank.Textures.Count == 0)
                {
                    continue;
                }
                NativeBattleEffectTextureDrain actorDrain = Drain(bank.Textures, arena);
                NativeBattlePcTextureWindowCache actorCache = InvalidateTextureUploads(bank.Cache, actorDrain.Uploads, bank.Textures);
                published = published.WithPresentationTexture(actorId, bank with { Textures = actorDrain.State, Cache = actorCache });
            }
        }
        return memory with { Textures = published, PrimitiveProcess = process, Arena = arena };

        NativeBattleEffectTextureDrain Drain(NativeBattleEffectTextureState owner, NativeBattleEffectArena? currentArena) =>
            owner.Drain505D20(currentArena, staging, address => NativeBattleProcessTim.Resolve(memory, address), memory.ScriptedEffects);
    }

    private static NativeBattlePcTextureWindowCache InvalidateTextureUploads(NativeBattlePcTextureWindowCache cache, ImmutableArray<NativeBattleEffectVramUpload> uploads, NativeBattleEffectTextureState priorTextures)
    {
        foreach (NativeBattleEffectVramUpload upload in uploads)
        {
            cache = cache.Invalidate464850(upload.X, upload.Y, upload.X + upload.Width - 1, upload.Y + upload.Height - 1, priorTextures);
        }
        return cache;
    }

    private NativeBattleGraphicsState AdvanceNativeGraphicsRequests(NativeBattleGraphicsState graphics)
    {
        return AdvanceNativeDescriptors(graphics);
    }

    private static bool ModelsBusy(BattleActorModels models)
    {
        return models.Entries.Select(entry => entry.State).Any(model => (model.Flags0 & 2) != 0 && NativeBattleActionGraphics.IsModelBusy(model, 0x40));
    }

    private BattleCombatant GraphicsActor(NativeBattleGraphicsState graphics, BattleModelOwner owner)
    {
        if (!graphics.ActorModels.Contains(owner))
        {
            throw new InvalidDataException("The graphical actor is no longer bound.");
        }
        return GetActor(RequireCanonicalGraphicsActorId(owner));
    }

    private static string RequireCanonicalGraphicsActorId(BattleModelOwner owner)
    {
        return owner switch
        {
            BattlePartyActorModel party => party.Actor.ActorId,
            BattleEnemyActorModel enemy => enemy.Actor.ActorId,
            _ => throw new InvalidDataException("A foreign graphics owner has no logical combatant.")
        };
    }


    private static string ReadGraphicsPresentationId(BattleModelOwner owner)
    {
        return owner switch
        {
            BattleForeignPartyModel party => party.Presentation.Key,
            BattleForeignEnemyModel enemy => enemy.Presentation.Key,
            _ => RequireCanonicalGraphicsActorId(owner)
        };
    }

    private NativeBattleGraphicsState ConsumeNativeGraphicsContacts(NativeBattleGraphicsState graphics)
    {
        return ConsumeNativeGraphicsContactsFrom(graphics, NativeBattleContactOrigin.Ordinary);
    }

    private NativeBattleGraphicsState ConsumeNativeGraphicsContactsFrom(NativeBattleGraphicsState graphics, NativeBattleContactOrigin origin)
    {
        if (hostedBattlePresentation)
        {
            NativeBattleModelLifecycleServices services = hostedFrameServices ?? throw new InvalidOperationException("Hosted graphics contacts require the current frame's actor facts.");
            return ConsumeHostedNativeGraphicsContacts(graphics, services, origin);
        }
        if (presentationForeignActors is not null)
        {
            presentationContacts.AddRange(graphics.PendingContacts);
            return graphics with { PendingContacts = [] };
        }
        foreach (NativeBattleContactPacket packet in graphics.PendingContacts)
        {
            packet.Validate();
            //506690 invokes493D80 before checking whether the primary model is loaded.
            graphics = PublishNativeReadyContact(graphics, packet.Primary);
            if (packet.Secondary is { } readySecondary)
            {
                graphics = PublishNativeReadyContact(graphics, readySecondary);
            }
            BattleCombatant actor = GraphicsActor(graphics, packet.Primary.Recipient);
            NativeBattleContactEffectPlan plan = NativeBattleContactEffects.Plan(packet.Primary, graphics.ActorModels, definition.NativeGraphics!.Rules, NativeBattleStatusBits(actor));
            graphics = ApplyNativeGraphicsContact(graphics, packet.Primary, plan);
            if (plan.Loaded && packet.Secondary is { } half)
            {
                BattleCombatant secondaryActor = GraphicsActor(graphics, half.Recipient);
                NativeBattleContactEffectPlan secondary = NativeBattleContactEffects.Plan(half, graphics.ActorModels, definition.NativeGraphics.Rules, NativeBattleStatusBits(secondaryActor), secondary: true);
                graphics = ApplyNativeGraphicsContact(graphics, half, secondary);
            }
        }
        return graphics with { PendingContacts = [] };
    }

    private NativeBattleGraphicsState ConsumeHostedNativeGraphicsContacts(NativeBattleGraphicsState graphics, NativeBattleModelLifecycleServices services, NativeBattleContactOrigin origin)
    {
        BattleModelRoster roster = HostedPlannedModelRoster;
        services.ValidateCurrentModels(roster, graphics);
        if (graphics.PendingContacts.IsDefault)
        {
            throw new InvalidDataException("Hosted graphics contacts require their pending packet list.");
        }
        if (graphics.PendingContacts.IsEmpty)
        {
            return graphics;
        }
        foreach (NativeBattleContactPacket packet in graphics.PendingContacts)
        {
            if (packet is null)
            {
                throw new InvalidDataException("Hosted graphics contacts cannot contain an absent packet.");
            }
            packet.Validate();
            NativeBattleModelActorFacts primary = RequireHostedContactActorFacts(graphics, services, packet.Primary.Recipient);
            if (primary.RawBattleStatuses is null)
            {
                throw new InvalidDataException("Hosted primary contact requires the actor's current battle status.");
            }
            if (packet.Secondary is { } secondary)
            {
                _ = RequireHostedContactActorFacts(graphics, services, secondary.Recipient);
            }
            if (packet.Primary.CoverSource is { } primaryCover)
            {
                _ = RequireHostedContactActorFacts(graphics, services, primaryCover);
            }
            if (packet.Secondary?.CoverSource is { } secondaryCover)
            {
                _ = RequireHostedContactActorFacts(graphics, services, secondaryCover);
            }
        }
        NativeBattleGraphicsRules rules = definition.NativeGraphics!.Rules;
        foreach (NativeBattleContactPacket packet in graphics.PendingContacts)
        {
            NativeBattleContactHalf primary = packet.Primary;
            uint rawStatus = services.Facts[primary.Recipient]!.RawBattleStatuses!.Value;
            NativeBattleContactEffectPlan plan = NativeBattleContactEffects.Plan(primary, graphics.ActorModels, rules, rawStatus);
            services.EmitContact(packet, ReadHostedRenzokukenContact(), origin);
            graphics = ApplyNativeGraphicsContact(graphics, primary, plan, services);
            if (plan.Loaded && packet.Secondary is { } half)
            {
                NativeBattleContactEffectPlan secondary = NativeBattleContactEffects.Plan(half, graphics.ActorModels, rules, half.BattleStatuses8, secondary: true);
                graphics = ApplyNativeGraphicsContact(graphics, half, secondary, services);
            }
            presentationContacts.Add(packet);
        }
        return graphics with { PendingContacts = [] };
    }

    private static NativeBattleModelActorFacts RequireHostedContactActorFacts(NativeBattleGraphicsState graphics, NativeBattleModelLifecycleServices services, BattleModelOwner owner)
    {
        if (!graphics.ActorModels.Contains(owner) || !services.Facts.TryGetValue(owner, out NativeBattleModelActorFacts? facts))
        {
            throw new InvalidDataException("Hosted graphics contact requires the current facts for its bound actor.");
        }
        return facts;
    }

    private NativeBattleGraphicsState ApplyNativeGraphicsContact(NativeBattleGraphicsState graphics, NativeBattleContactHalf half, NativeBattleContactEffectPlan plan, NativeBattleModelLifecycleServices? services = null)
    {
        if (plan.StageTargetRefresh)
        {
            if (definition.NativeStageSource?.Callback?.Address == NativeBattleStage37.CallbackAddress)
            {
                UpdateNativeStage37Target3(graphics, services);
            }
            else if (definition.NativeStageSource?.Callback?.Address == NativeBattleFinalStage142.CallbackAddress)
            {
                UpdateNativeFinalStage142Target3(graphics, services);
            }
        }
        if (!plan.Loaded)
        {
            return graphics;
        }
        if (!plan.Secondary && (graphics.Flags & 0x10000000) != 0)
        {
            graphics = graphics with { Flags = graphics.Flags & ~0x10000000u };
        }
        else if (!plan.Secondary && (half.HitFlags & 2) != 0)
        {
            graphics = graphics with { ModuleEffects = graphics.ModuleEffects.AppendFlash(0, 1, 0, 255) };
        }
        BattleModelOwner index = half.Recipient;
        graphics = ApplyNativeGraphicsStatus509CD0(graphics, index, plan.ImmediateStatusFlags!.Value);
        NativeBattleGraphicsModel model = graphics.ActorModels.Get(index);
        if (plan.EjectionService7)
        {
            NativeBattleModuleResource resource = RequireNativeModuleResource(21);
            model = NativeBattleGraphicsModels.StartService(model, 7);
            NativeBattleVector position = model.Transform?.Position28 ?? throw new NotSupportedException("Native ejection sound requires its current model positionWORD28.");
            NativeBattleModuleAnchor anchor = new(position.X, position.Y, position.Z, 0);
            graphics = graphics with { ModuleEffects = graphics.ModuleEffects.AppendModelResource(index, resource, 0x200015, graphics.NativeBindings.CaptureAliases(graphics.ActorModels)) };
            PublishNativeModuleSound(0x0D, 8, anchor, RequireCanonicalGraphicsActorId(index));
            PublishNativeModuleSound(0x0E, 16, anchor, RequireCanonicalGraphicsActorId(index));
        }
        if (plan.SelectSequence)
        {
            graphics = SelectNativeRingContactSequence(graphics with { ActorModels = graphics.ActorModels.Set(index, model) }, index, plan.Sequence);
            model = graphics.ActorModels.Get(index);
            if (model.ModelId4 < 16)
            {
                graphics = RefreshNativeGuardianStandingSequence(graphics with { ActorModels = graphics.ActorModels.Set(index, model) }, index);
                model = graphics.ActorModels.Get(index);
            }
        }
        if (!plan.Secondary && (half.Flags2 & 0x30) != 0)
        {
            byte resourceId = (half.Flags2 & 0x30) switch { 0x10 => 39, 0x20 => 40, _ => 38 };
            graphics = graphics with { ModuleEffects = graphics.ModuleEffects.AppendModelResource(index, RequireNativeModuleResource(resourceId), 0x100000u | resourceId, graphics.NativeBindings.CaptureAliases(graphics.ActorModels)) };
        }
        if (!plan.Secondary && (graphics.Flags & 0x10) == 0 && (half.HitFlags & 8) != 0)
        {
            graphics = ApplyNativeSequenceCover5065B0(graphics with { ActorModels = graphics.ActorModels.Set(index, model) }, half);
            model = graphics.ActorModels.Get(index);
        }
        NativeBattleEffectsState effects = NativeBattleContactEffects.ProduceTasks(graphics.Effects, half, plan, graphics.ActorModels.Roster);
        return graphics with { ActorModels = graphics.ActorModels.Set(index, model), Effects = effects };
    }

    private NativeBattleModuleResource RequireNativeModuleResource(byte id)
    {
        return definition.NativeModuleResources.SingleOrDefault(resource => resource.Id == id) ?? throw new NotSupportedException($"Native contact effect requires its installed module resource{id}.");
    }

    private NativeBattleGraphicsModel SetNativeGraphicsFlags(BattleModelOwner owner, NativeBattleGraphicsModel model, uint flags, NativeBattleGraphicsState graphics)
    {
        uint previous = model.Flags8 ?? throw new InvalidDataException("Native509D10 requires its current graphics status DWORD.");
        uint added = (previous ^ flags) & flags;
        uint removed = (previous ^ flags) & ~flags;
        ImmutableArray<byte?> timers = model.StatusTimers;
        for (int bit = 0; bit < 32; bit++)
        {
            if ((added & (1u << bit)) != 0)
            {
                timers = timers.SetItem(bit, 0);
            }
        }
        if ((added & 0xA) != 0)
        {
            flags &= ~0x800000u;
        }
        if ((added & 0x400000) != 0)
        {
            if (model.ModelId4 >= 16)
            {
                model = NativeBattleGraphicsModels.StartService(model with { Flags0 = (ushort)(model.Flags0 | 0x10) }, 5);
                flags &= ~0x400000u;
            }
            else
            {
                model = model with { Flags0 = (ushort)(model.Flags0 & ~2) };
            }
        }
        if ((model.ModelId4 ?? throw new InvalidDataException("Native509D10 requires the model BYTE4.")) < 16)
        {
            if (((added | removed) & 0x410) != 0)
            {
                UploadNativeStatusPalette509E00(owner, flags);
            }
            if ((removed & 2) != 0)
            {
                ApplyNativeTextureMotion(graphics with { ActorModels = graphics.ActorModels.Set(owner, model) }, owner, 0, 0);
                model = model with { Flags0 = (ushort)(model.Flags0 | 0x400) };
            }
            if ((added & 0x1000) != 0)
            {
                ApplyNativeTextureMotion(graphics with { ActorModels = graphics.ActorModels.Set(owner, model) }, owner, 0, 1);
            }
            else if ((removed & 0x1000) != 0)
            {
                ApplyNativeTextureMotion(graphics with { ActorModels = graphics.ActorModels.Set(owner, model) }, owner, 0, 0);
            }
        }
        return model with { Flags8 = flags, StatusTimers = timers };
    }
}
