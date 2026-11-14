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
        int index = graphics.ActorIds.IndexOf(actor.Id);
        if (index < 0)
        {
            throw new InvalidDataException("Native106 death descriptor has no bound graphical allocation.");
        }
        NativeGraphics = QueueNativeDescriptor(graphics, 106, 0, checked((ushort)index));
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
        int index = graphics.ActorIds.IndexOf(actor.Id);
        if (index < 0)
        {
            throw new InvalidDataException("Native105 removal descriptor has no bound graphical allocation.");
        }
        NativeGraphics = QueueNativeDescriptor(graphics, 105, 0, checked((ushort)index));
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
            NativeBattleGraphicsRoster roster = graphics.PresentationRoster ?? throw new InvalidDataException("Hosted graphics frames require their actual roster.");
            services.Validate(roster, graphics);
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
            if (services is null)
            {
                graphics = AdvanceGuardianFormPresentation(graphics);
            }
        }
        catch when (services is null && GuardianFormOwnsCurrentPresentation())
        {
            FailOwnedGuardianFormPresentation();
            throw;
        }
        flags = graphics.Flags;
        graphics = SubmitNativeModulePackets(graphics);
        graphics = ConsumeNativeGraphicsContacts(graphics);
        ImmutableArray<NativeBattleGraphicsModel> models = graphics.Models;
        NativeBattleEffectsStep effects = graphics.Effects.Advance(models, index => ProjectNativeDamageText(graphics, index), graphics.PresentationRoster);
        foreach (byte index in effects.ResetModelStatuses)
        {
            graphics = ApplyNativeGraphicsStatus509CD0(graphics with { Models = models }, index, 16);
            models = graphics.Models;
        }
        graphics = graphics with { Models = models, Effects = effects.State };
        if (services is null)
        {
            graphics = FrameGuardianFormIdleCamera(graphics);
        }
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

    private static bool ModelsBusy(IReadOnlyList<NativeBattleGraphicsModel> models)
    {
        return models.Any(model => (model.Flags0 & 2) != 0 && NativeBattleActionGraphics.IsModelBusy(model, 0x40));
    }

    private BattleCombatant GraphicsActor(NativeBattleGraphicsState graphics, int index) => GetActor(graphics.ActorIds[index] ?? throw new InvalidDataException("Native graphical row has no bound allocation identity."));

    private NativeBattleGraphicsState ConsumeNativeGraphicsContacts(NativeBattleGraphicsState graphics)
    {
        if (hostedBattlePresentation)
        {
            NativeBattleModelLifecycleServices services = hostedFrameServices ?? throw new InvalidOperationException("Hosted graphics contacts require the current frame's actor facts.");
            return ConsumeHostedNativeGraphicsContacts(graphics, services);
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
            if (packet.Secondary.ActorIndex != 255)
            {
                graphics = PublishNativeReadyContact(graphics, packet.Secondary);
            }
            NativeBattleGraphicsModel primary = graphics.Models[packet.Primary.ActorIndex];
            BattleCombatant actor = GraphicsActor(graphics, packet.Primary.ActorIndex);
            NativeBattleContactEffectPlan plan = NativeBattleContactEffects.Plan(packet.Primary, primary, definition.NativeGraphics!.Rules, NativeBattleStatusBits(actor));
            graphics = ApplyNativeGraphicsContact(graphics, packet.Primary, plan);
            if (plan.Loaded && packet.Secondary.ActorIndex != 255)
            {
                NativeBattleContactHalf half = packet.Secondary;
                BattleCombatant secondaryActor = GraphicsActor(graphics, half.ActorIndex);
                NativeBattleContactEffectPlan secondary = NativeBattleContactEffects.Plan(half, graphics.Models[half.ActorIndex], definition.NativeGraphics.Rules, NativeBattleStatusBits(secondaryActor), secondary: true);
                graphics = ApplyNativeGraphicsContact(graphics, half, secondary);
            }
        }
        return graphics with { PendingContacts = [] };
    }

    private NativeBattleGraphicsState ConsumeHostedNativeGraphicsContacts(NativeBattleGraphicsState graphics, NativeBattleModelLifecycleServices services)
    {
        NativeBattleGraphicsRoster roster = graphics.PresentationRoster ?? throw new InvalidDataException("Hosted graphics contacts require their presentation roster.");
        roster.Validate();
        services.Validate(roster, graphics);
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
            packet.Validate(roster.ActorCount);
            NativeBattleModelActorFacts primary = RequireHostedContactActorFacts(graphics, services, packet.Primary.ActorIndex);
            if (primary.RawBattleStatuses is null)
            {
                throw new InvalidDataException("Hosted primary contact requires the actor's current battle status.");
            }
            if (packet.Secondary.ActorIndex != 255)
            {
                _ = RequireHostedContactActorFacts(graphics, services, packet.Secondary.ActorIndex);
            }
        }
        NativeBattleGraphicsRules rules = definition.NativeGraphics!.Rules;
        foreach (NativeBattleContactPacket packet in graphics.PendingContacts)
        {
            NativeBattleContactHalf primary = packet.Primary;
            uint rawStatus = services.Facts[primary.ActorIndex]!.RawBattleStatuses!.Value;
            NativeBattleContactEffectPlan plan = NativeBattleContactEffects.Plan(primary, graphics.Models[primary.ActorIndex], rules, rawStatus);
            services.EmitContact(packet);
            graphics = ApplyNativeGraphicsContact(graphics, primary, plan, services);
            if (plan.Loaded && packet.Secondary.ActorIndex != 255)
            {
                NativeBattleContactHalf half = packet.Secondary;
                NativeBattleContactEffectPlan secondary = NativeBattleContactEffects.Plan(half, graphics.Models[half.ActorIndex], rules, half.BattleStatuses8, secondary: true);
                graphics = ApplyNativeGraphicsContact(graphics, half, secondary, services);
            }
            presentationContacts.Add(packet);
        }
        return graphics with { PendingContacts = [] };
    }

    private static NativeBattleModelActorFacts RequireHostedContactActorFacts(NativeBattleGraphicsState graphics, NativeBattleModelLifecycleServices services, int row)
    {
        if (graphics.PresentationRoster is not { } roster || !roster.ContainsRow(row) ||
            (uint)row >= (uint)graphics.Models.Length || graphics.Models[row]?.Index != row ||
            (uint)row >= (uint)graphics.ActorIds.Length || (uint)row >= (uint)services.Facts.Length ||
            services.Facts[row] is not { } facts || facts.ActorId != roster[row].ActorId || facts.ActorId != graphics.ActorIds[row] || facts.IsParty != roster[row].IsParty)
        {
            throw new InvalidDataException("Hosted graphics contact requires the current facts for its bound actor row.");
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
        else if (!plan.Secondary && (half.HitFlags3 & 2) != 0)
        {
            graphics = graphics with { ModuleEffects = graphics.ModuleEffects.AppendFlash(0, 1, 0, 255) };
        }
        int index = half.ActorIndex;
        graphics = ApplyNativeGraphicsStatus509CD0(graphics, index, plan.ImmediateStatusFlags!.Value);
        NativeBattleGraphicsModel model = graphics.Models[index];
        if (plan.EjectionService7)
        {
            NativeBattleModuleResource resource = RequireNativeModuleResource(21);
            model = NativeBattleGraphicsModels.StartService(model, 7);
            NativeBattleVector position = model.Transform?.Position28 ?? throw new NotSupportedException("Native ejection sound requires its current model positionWORD28.");
            NativeBattleModuleAnchor anchor = new(position.X, position.Y, position.Z, 0);
            graphics = graphics with { ModuleEffects = graphics.ModuleEffects.AppendModelResource(index, resource, 0x200015, graphics.PresentationRoster) };
            PublishNativeModuleSound(0x0D, 8, anchor, graphics.ActorIds[index]);
            PublishNativeModuleSound(0x0E, 16, anchor, graphics.ActorIds[index]);
        }
        if (plan.SelectSequence)
        {
            graphics = SelectNativeRingContactSequence(graphics with { Models = graphics.Models.SetItem(index, model) }, index, plan.Sequence);
            model = graphics.Models[index];
            if (model.ModelId4 < 16)
            {
                graphics = RefreshNativeGuardianStandingSequence(graphics with { Models = graphics.Models.SetItem(index, model) }, checked((byte)index));
                model = graphics.Models[index];
            }
        }
        if (!plan.Secondary && (half.Flags2 & 0x30) != 0)
        {
            byte resourceId = (half.Flags2 & 0x30) switch { 0x10 => 39, 0x20 => 40, _ => 38 };
            graphics = graphics with { ModuleEffects = graphics.ModuleEffects.AppendModelResource(index, RequireNativeModuleResource(resourceId), 0x100000u | resourceId, graphics.PresentationRoster) };
        }
        if (!plan.Secondary && (graphics.Flags & 0x10) == 0 && (half.HitFlags3 & 8) != 0)
        {
            graphics = ApplyNativeSequenceCover5065B0(graphics with { Models = graphics.Models.SetItem(index, model) }, half);
            model = graphics.Models[index];
        }
        NativeBattleEffectsState effects = NativeBattleContactEffects.ProduceTasks(graphics.Effects, half, plan, services is null ? null : graphics.PresentationRoster);
        return graphics with { Models = graphics.Models.SetItem(index, model), Effects = effects };
    }

    private NativeBattleModuleResource RequireNativeModuleResource(byte id)
    {
        return definition.NativeModuleResources.SingleOrDefault(resource => resource.Id == id) ?? throw new NotSupportedException($"Native contact effect requires its installed module resource{id}.");
    }

    private NativeBattleGraphicsModel SetNativeGraphicsFlags(NativeBattleGraphicsModel model, uint flags, NativeBattleGraphicsState graphics)
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
                UploadNativeStatusPalette509E00(checked((byte)model.Index), flags);
            }
            if ((removed & 2) != 0)
            {
                ApplyNativeTextureMotion(graphics with { Models = graphics.Models.SetItem(model.Index, model) }, model.Index, 0, 0);
                model = model with { Flags0 = (ushort)(model.Flags0 | 0x400) };
            }
            if ((added & 0x1000) != 0)
            {
                ApplyNativeTextureMotion(graphics with { Models = graphics.Models.SetItem(model.Index, model) }, model.Index, 0, 1);
            }
            else if ((removed & 0x1000) != 0)
            {
                ApplyNativeTextureMotion(graphics with { Models = graphics.Models.SetItem(model.Index, model) }, model.Index, 0, 0);
            }
        }
        return model with { Flags8 = flags, StatusTimers = timers };
    }
}
