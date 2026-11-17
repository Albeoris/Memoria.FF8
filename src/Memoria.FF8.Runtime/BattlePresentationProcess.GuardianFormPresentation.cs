using System.Collections.Immutable;
using System.Diagnostics;

namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{

    private BattleGuardianFormResources GuardianFormResources(int guardianForceId)
    {
        return resources.GuardianFormResources.SingleOrDefault(form => form.GuardianForceId == guardianForceId)
            ?? throw new NotSupportedException($"Guardian Form {guardianForceId} requires its matching visual resources.");
    }

    private ImmutableArray<NativeBattleModelDatSource> NativeModelResourceSources()
    {
        IEnumerable<NativeBattleModelDatSource> sources = this.resources.NativeGraphics!.Bindings.SelectMany(binding => binding.Resource.ModelLoadSources ?? []);
        if (NativeModelTextureCache?.PresentationRoster is { } roster)
        {
            sources = sources.Concat(roster.Owners.SelectMany(owner => ReadOriginalPresentationModelResource(owner)?.ModelLoadSources ?? []));
            if (resources.GuardianFormsEnabled)
            {
                sources = sources.Concat(resources.GuardianFormResources.SelectMany(resource => resource.VisualResource.ModelLoadSources ?? []));
            }
            return sources.ToImmutableArray();
        }
        if (!resources.GuardianFormsEnabled)
        {
            return sources.ToImmutableArray();
        }
        sources = sources.Concat(resources.GuardianFormResources.SelectMany(resource => resource.VisualResource.ModelLoadSources ?? []));
        return sources.DistinctBy(source => (source.ModelId, source.Linked, source.Selector)).ToImmutableArray();
    }

    private void PrepareGuardianFormPresentation(BattlePartyActorModel caster, int guardianForceId)
    {
        if (guardianForceId != 2 || NativeGraphics is not { GuardianFormPresentation: null } graphics || !graphics.ActorModels.Contains(caster) || NativeCamera is null || NativeStage is null)
        {
            throw new NotSupportedException("Ifrit transformation requires its native player presentation and camera/stage owners.");
        }
        BattleGuardianFormResources formResources = GuardianFormResources(guardianForceId);
        formResources.VisualResource.Validate();
        if (formResources.VisualResource.PoseSource is null || formResources.VisualResource.ModelLoadSources is not { IsDefaultOrEmpty: false })
        {
            throw new NotSupportedException("Ifrit transformation requires its original persistent model and pose resources.");
        }
        NativeBattleOriginalEffectState selected = NativeBattleEffectResourceResolver.MaterializeSelected(NativeBattleOriginalEffects.Select(201, this.resources.NativeEffectCatalog), nativeEffectResourceResolver);
        if (selected.Binding.CinematicResources is null || selected.Binding.ScriptedInitializationDirectories is null || NativeBattleEffectInitializerPolicy.Assets(selected.Binding).Length != 2)
        {
            throw new NotSupportedException("Ifrit transformation requires both original cinematic resources and their initialization directories.");
        }
    }

    private void BeginGuardianFormEntry(HostedGuardianFormVisualOwner owner)
    {
        NativeBattleGraphicsState graphics = RequireNativeGraphics();
        if (graphics.GuardianFormPresentation is { } existing)
        {
            if (existing.Actor == owner.Actor && existing.ActorId == owner.ActorId && existing.InvocationId == owner.InvocationId)
            {
                return;
            }
            throw new InvalidOperationException("Another GF presentation owns the battle scene.");
        }
        BattlePartyActorModel caster = owner.Actor;
        NativeBattleGuardianFormPresentation presentation = new(owner.ActorId, owner.InvocationId, GuardianFormInvocationMode.EntryOnly, caster, 0, graphics.ActorModels.Get(caster), NativeCamera ?? throw new NotSupportedException("GF entry requires the native camera owner."), NativeStage ?? throw new NotSupportedException("GF entry requires the native stage owner."), CaptureGuardianFormVisibility(graphics.ActorModels), graphics.Flags) { UnboundVisibility = graphics.NativeBindings.CaptureUnboundVisibility(), HumanFocalLength = NativeCamera.FocalLength ?? throw new NotSupportedException("GF entry requires the current human camera focal length.") };
        presentation.Validate();
        graphics = BeginNativeOriginalEffect(graphics, 201);
        foreach (BattleModelEntry<BattlePartyModelOwner> entry in graphics.ActorModels.Party.Entries)
        {
            NativeBattleGraphicsModel model = entry.State;
            if ((model.Flags0 & 2) == 0)
            {
                continue;
            }
            graphics = graphics with { ActorModels = graphics.ActorModels.Set(entry.Owner, model with { Flags0 = (ushort)(model.Flags0 | 8), Flags2 = (ushort)((model.Flags2 ?? 0) | 4) }) };
        }
        NativeGraphics = graphics with { GuardianFormPresentation = presentation, Flags = graphics.Flags | 0x10 };
        QueueNativeModelResource507080(NativeGraphics, 77, caster);
    }

    private void ReturnGuardianFormPresentation(HostedGuardianFormVisualOwner owner, HostedGuardianFormFrameFacts? facts = null, Action<string>? stageStopSounds = null)
    {
        if (NativeGraphics is not { GuardianFormPresentation: { } presentation } graphics || presentation.Actor != owner.Actor || presentation.ActorId != owner.ActorId || presentation.InvocationId != owner.InvocationId)
        {
            return;
        }
        bool ownsCinematic = presentation.Phase < 3 || presentation.Mode == GuardianFormInvocationMode.FinisherWithoutEntry;
        if (ownsCinematic)
        {
            NativeCamera = presentation.Camera.CurrentPose is { } pose ? presentation.Camera.WriteEffectGlobals(pose with { FocalLength = presentation.HumanFocalLength }).PublishProjection(presentation.GraphicsFlags, EncounterCameraElevation) : (presentation.Camera with { FocalLength = presentation.HumanFocalLength }).PublishProjection(presentation.GraphicsFlags, EncounterCameraElevation);
            NativeStage = presentation.Stage;
        }
        if (ownsCinematic)
        {
            graphics = RestoreGuardianFormVisibility(graphics, presentation);
        }
        if (HasBattleEntry)
        {
            CancelGuardianFormModelLoad(graphics, presentation.Actor);
        }
        NativeBattleGraphicsModel body = graphics.ActorModels.Get(presentation.Actor);
        if (body.TextureResourceIndex is int row && NativeModelTextureCache is { } cache && cache.Rows[row] is { Kind: 3, ModelId: 77 } &&
            (body.ModelId4 == 77 || HasBattleEntry && cache.PresentationActors?.GetValueOrDefault(row) == presentation.Actor))
        {
            (NativeModelCpuResources, NativeFileStaging) = NativeBattleModelEnemyHeap.Release(NativeModelCpuResources!, NativeFileStaging!, cache, row);
            NativeModelTextureCache = cache.Release5080D0(row);
        }
        BattleActorModels models = graphics.ActorModels.Set(presentation.Actor, presentation.HumanModel with { Flags8 = body.Flags8, StatusTimers = body.StatusTimers });
        graphics = graphics with { ActorModels = models, Flags = ownsCinematic ? presentation.GraphicsFlags : graphics.Flags, GuardianFormPresentation = null, GuardianFormHumanFocalToRestore = ownsCinematic ? null : presentation.HumanFocalLength };
        graphics = FrameGuardianFormIdleCamera(graphics, facts);
        NativeGraphics = ownsCinematic ? StopGuardianFormCinematic(graphics, stageStopSounds) : graphics;
    }

    private static ImmutableDictionary<BattleModelOwner, NativeBattleGuardianModelVisibility> CaptureGuardianFormVisibility(BattleActorModels models)
    {
        return models.Entries.ToImmutableDictionary(entry => entry.Owner, entry => new NativeBattleGuardianModelVisibility(entry.State.Flags0, entry.State.Flags2));
    }

    private static NativeBattleGraphicsState RestoreGuardianFormVisibility(NativeBattleGraphicsState graphics, NativeBattleGuardianFormPresentation presentation)
    {
        BattleActorModels models = graphics.ActorModels;
        foreach (BattleModelEntry<BattleModelOwner> entry in graphics.ActorModels.Entries)
        {
            if (!presentation.ModelVisibility.TryGetValue(entry.Owner, out NativeBattleGuardianModelVisibility saved))
            {
                throw new InvalidDataException("GF visibility restoration reached a model outside its captured owners.");
            }
            models = models.Set(entry.Owner, NativeBattleModelBoundary.RestoreVisibility(entry.State, saved));
        }
        return graphics with { ActorModels = models, NativeBindings = graphics.NativeBindings.RestoreUnboundVisibility(presentation.UnboundVisibility) };
    }


    internal bool CanSkipAttachedGuardianFormFinisher => HasBattleEntry && HostedActiveActionToken is not null && NativeGraphics?.GuardianFormPresentation is { Mode: GuardianFormInvocationMode.FinisherWithoutEntry, Phase: 5, SkipRequested: false };

    internal bool SkippingAttachedGuardianFormFinisher => HasBattleEntry && NativeGraphics?.GuardianFormPresentation is { Mode: GuardianFormInvocationMode.FinisherWithoutEntry, Phase: 5, SkipRequested: true } && !NativeBattleIfritContinuation.HasReachedFinale(RequireWaterMemory().ScriptedEffects!);

    private void SettleGuardianFormSkipBoost()
    {
        if (nativeGuardianBoost is not { } widget)
        {
            return;
        }
        bool enabled = (NativeGraphics?.UiServices?.Flags942 & 1) != 0;
        NativeBattleGuardianBoostStep completed = NativeBattleGuardianBoostSkip.Complete(widget, enabled, nativeBattleUiRandom);
        nativeGuardianBoost = completed.Widget;
        nativeBattleUiRandom = completed.Random;
        if (completed.RecomputePercent is byte percent)
        {
            PublishHostedGuardianBoostRequest(percent);
        }
    }

    internal bool RequestAttachedGuardianFormSkip(HostedGuardianFormFrameFacts facts)
    {
        RequireIdle();
        if (!CanSkipAttachedGuardianFormFinisher || facts is not { Finishing: true, Form: { } owner })
        {
            return false;
        }
        NativeBattleGraphicsState graphics = RequireNativeGraphics();
        NativeBattleGuardianFormPresentation form = RequireHostedGuardianFormVisualOwner(graphics, owner);
        NativeGraphics = graphics with { GuardianFormPresentation = form with { SkipRequested = true } };
        return true;
    }

    private (NativeBattleGraphicsState Graphics, byte Phase, bool Complete) PollGuardianFormFinisherGraphics(NativeBattleGraphicsState graphics, NativeBattleActionGraphicsState context, byte phase)
    {
        NativeBattleGuardianFormPresentation presentation = graphics.GuardianFormPresentation ?? throw new InvalidDataException("Ifrit finisher has no current presentation invocation.");
        if (phase == 0)
        {
            presentation = presentation with { Mode = GuardianFormInvocationMode.FinisherWithoutEntry, Phase = 4, Camera = NativeCamera!, Stage = NativeStage!, ModelVisibility = CaptureGuardianFormVisibility(graphics.ActorModels), UnboundVisibility = graphics.NativeBindings.CaptureUnboundVisibility(), GraphicsFlags = graphics.Flags };
            graphics = graphics with { GuardianFormPresentation = presentation };
            NativeGraphics = graphics;
            graphics = BeginNativeOriginalEffect(graphics, 201);
            graphics = PrepareNativeOriginalEffectParameters(graphics, context);
            return (graphics, 1, false);
        }
        if (phase == 1)
        {
            if (NativeEffectIoResult < 0 || graphics.ModuleEffects.PublishedCount != 0)
            {
                return (graphics, phase, false);
            }
            foreach (BattleModelEntry<BattlePartyModelOwner> entry in graphics.ActorModels.Party.Entries)
            {
                NativeBattleGraphicsModel model = entry.State;
                if ((model.Flags0 & 2) == 0)
                {
                    continue;
                }
                graphics = graphics with { ActorModels = graphics.ActorModels.Set(entry.Owner, model with { Flags0 = (ushort)(model.Flags0 | 8), Flags2 = (ushort)((model.Flags2 ?? 0) | 4) }) };
            }
            NativeCamera = NativeCamera! with { ForceCamera = true };
            graphics = EnterNativeCinematic(graphics);
            NativeEffectMemory = RequireWaterMemory().AssignReturnedTask(graphics.OriginalEffect!);
            graphics = RegisterNativeGuardianBoost(graphics, checked((ushort)(64 + 2)));
            NativeGraphics = graphics;
            if (presentation.SkipRequested)
            {
                SettleGuardianFormSkipBoost();
            }
            return (graphics with { Flags = graphics.Flags | 0x10 }, 2, false);
        }
        if (NativeOriginalEffectBusy(graphics))
        {
            return (graphics, phase, false);
        }
        graphics = CompleteNativeHeaderGraphics(graphics);
        NativeEffectMemory = RequireWaterMemory() with { Active = 0 };
        return (graphics with { Flags = graphics.Flags & ~0x10u }, phase, true);
    }

    private NativeBattleGraphicsState StopGuardianFormCinematic(NativeBattleGraphicsState graphics, Action<string>? stageStopSounds = null)
    {
        if (NativeFileIo is { } io)
        {
            int cancelled = io.Rows.Count(row => row.SchedulerCallback != 0 && row.CompletionCallback == 0xB2BB40);
            if (cancelled > 0)
            {
                NativeFileIo = io with { Rows = io.Rows.Select(row => row.SchedulerCallback != 0 && row.CompletionCallback == 0xB2BB40 ? row with { SchedulerCallback = 0, Status = 0 } : row).ToImmutableArray(), ActiveCount = checked((short)(io.ActiveCount!.Value - cancelled)) };
            }
        }
        if (NativeEffectMemory is { } memory)
        {
            NativeBattleEffectInitializerAsset common = this.resources.NativeCommonEffectTexture ?? throw new NotSupportedException("GF presentation cleanup requires the common battle texture resource.");
            NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("GF presentation cleanup requires the retained texture upload owner.");
            textures = textures.OpenTextureFile(NativeBattleSharedEffectTexture.File);
            (textures, _) = textures.EnqueueTim505E30(new(common.Name, common.Data), identity: NativeBattleSharedEffectTexture.File);
            NativeEffectMemory = memory with { ReturnedTaskPointer1D96AAC = 0, DetachedReturnedTask = null, Active = 0, Cinematic = null, Textures = textures };
        }
        if (stageStopSounds is null)
        {
            PublishNativeStopAllSounds("GF presentation releases its sound voices.");
        }
        else
        {
            stageStopSounds("GF presentation releases its sound voices.");
        }
        return graphics with { OriginalEffect = graphics.OriginalEffect is { Binding.Effect: 201 } effect ? effect with { ReturnedDescriptor = 0, Pools = [] } : graphics.OriginalEffect, PrimitiveFrame = graphics.PrimitiveFrame is { } primitive ? primitive with { Pending = NativeBattleLogicalOrdering.Clear(), LastCommands = [] } : null };
    }





    private void CancelGuardianFormModelLoad(NativeBattleGraphicsState graphics, BattleModelOwner owner)
    {
        if (NativeModelLoader?.Task is { Callback: 0x507120, ModelId: 77 } loader && loader.Owner == owner)
        {
            if (loader.EnemyResourceIndex is int reserved && NativeModelTextureCache is { } cache && graphics.ActorModels.Get(owner).TextureResourceIndex != reserved)
            {
                // Reservation precedes heap construction and binding; only its texture row and linked slot exist here.
                NativeBattleModelCpuResources cpu = NativeModelCpuResources!;
                byte linked = cpu.Rows[reserved].LinkedPhysicalIndex36 ?? throw new InvalidDataException("Cancelled GF body reservation lost its linked slot owner.");
                if (linked != 255)
                {
                    NativeModelCpuResources = cpu with { EnemyLinkedSlots = checked((byte)(cpu.EnemyLinkedSlots!.Value & ~(1 << linked))) };
                }
                NativeModelTextureCache = cache.Release5080D0(reserved);
            }
            if (NativeFileIo is { } io)
            {
                int cancelled = io.Rows.Count(row => row.SchedulerCallback != 0 && row.CompletionCallback == 0x508470);
                NativeFileIo = io with { Rows = io.Rows.Select(row => row.SchedulerCallback != 0 && row.CompletionCallback == 0x508470 ? row with { SchedulerCallback = 0, Status = 0 } : row).ToImmutableArray(), ActiveCount = checked((short)(io.ActiveCount!.Value - cancelled)) };
            }
            NativeEffectIoResult = 0;
            NativeModelLoader = new(null);
        }
    }

    private NativeBattleGraphicsState AdvanceGuardianFormPresentation(NativeBattleGraphicsState graphics, NativeBattleModelLifecycleServices services)
    {
        if (graphics.GuardianFormPresentation is not { Mode: GuardianFormInvocationMode.EntryOnly } presentation)
        {
            return graphics;
        }
        if (presentation.Phase == 0)
        {
            if (PollNativeModelResource507070(ref graphics) != 0)
            {
                return graphics;
            }
            NativeBattleGraphicsResource resource = GuardianFormResources(2).VisualResource;
            NativeBattlePoseSource source = resource.PoseSource!;
            NativeBattleGraphicsModel loaded = graphics.ActorModels.Get(presentation.Actor) with { ModelId4 = 77, SequenceState = new() { Pose = 0, PoseFrame114 = 1, PoseLimit115 = resource.AnimationFrames[0], DecodedPose = source.Sample(0, 1, 0) }, CurrentBodyBoneMatrices = null };
            loaded = NativeBattleBodyBoneMatrices.Publish508C90(loaded, source);
            graphics = graphics with { ActorModels = graphics.ActorModels.Set(presentation.Actor, loaded) };
            NativeGraphics = graphics;
            if (NativeEffectIoResult < 0 || graphics.ModuleEffects.PublishedCount != 0)
            {
                return graphics;
            }
            graphics = EnterNativeCinematic(graphics);
            NativeEffectMemory = RequireWaterMemory().AssignReturnedTask(graphics.OriginalEffect!);
            return graphics with { GuardianFormPresentation = presentation with { Phase = 1 } };
        }
        if (presentation.Phase == 1 && ReachedIfritLandingBoundary())
        {
            graphics = StopGuardianFormCinematic(graphics);
            NativeCamera = presentation.Camera;
            NativeStage = presentation.Stage;
            graphics = graphics with { GuardianFormPresentation = presentation with { Phase = 2 } };
        }
        if (graphics.GuardianFormPresentation is { Phase: 2 } loading)
        {
            if (PollNativeModelResource507070(ref graphics) != 0)
            {
                return graphics;
            }
            NativeBattleGraphicsResource resource = GuardianFormResources(2).VisualResource;
            NativeBattlePoseSource pose = resource.PoseSource ?? throw new InvalidDataException("GF form visual requires its original pose source.");
            short scale = unchecked((short)(System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(pose.Skeleton.AsSpan().Slice(2)) * 4));
            NativeBattleGraphicsModel model = graphics.ActorModels.Get(loading.Actor);
            model = model with { ModelId4 = 77, Flags0 = (ushort)(model.Flags0 & ~0xC), Flags2 = (ushort)(model.Flags2!.Value & ~4), CurrentBodyBoneMatrices = null, LinkedModel = null, LinkedTransform = null, HasLinkedModelRing140 = false, Rotation12 = 0, Rotation14 = 0, Rotation16 = 0, Transform = new(new(0, 0, 0), new(0, 0, 0), 0, 0, scale), SequenceState = new(), SequenceByte145 = 0, Sequence = 1, ResourceFlags44 = 1 };
            NativeBattleSequenceStep initializedPose = NativeBattleSequenceMachine.Advance(loading.Actor, model, resource, new(NativeEntryAdvantage, 0)
            {
                OriginalResource = resource,
                ReadModelExternal = (current, variable) => ReadNativeGraphicsSequenceVariable(graphics with { ActorModels = graphics.ActorModels.Set(loading.Actor, current) }, loading.Actor, variable)
            });
            if (initializedPose.Cues.Any(cue => cue.Kind != NativeBattleSequenceCueKind.RestoreStanding))
            {
                throw new NotSupportedException("GF body standing initialization reached a non-pose service.");
            }
            model = initializedPose.Model;
            model = model with { GeometryWorldMatrix = NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(model, pose, ReadNativeModelScaleVector502AB0(graphics, model)) };
            graphics = graphics with { ActorModels = graphics.ActorModels.Set(loading.Actor, model), GuardianFormPresentation = loading with { Phase = 3 }, Flags = loading.GraphicsFlags };
            graphics = RestoreGuardianFormVisibility(graphics, loading);
            NativeGraphics = graphics;
            if (services.GuardianForm is not { Phase: BattleGuardianFormPhase.Entering, Form: { } owner })
            {
                throw new InvalidDataException("Hosted form landing requires its authenticated parent entry.");
            }
            _ = RequireHostedGuardianFormVisualOwner(graphics, owner);
            services.Emit(new NativeBattleGuardianFormLanded(owner));
        }
        return graphics;
    }

    private bool ReachedIfritLandingBoundary()
    {
        NativeBattleEffectMemory memory = RequireWaterMemory();
        NativeBattleScriptedEffectMemory core = memory.ScriptedEffects!;
        uint manager = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(core.Read(0x27973EC, 4));
        uint bank = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(core.Read(manager + 0x90, 4));
        uint resource = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(core.Read(0x2798A68, 4));
        uint pc = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(memory.Arena!.Read(checked((int)(bank - NativeBattleEffectNodeMemory.ArenaAddress)), 4));
        return pc == resource + NativeBattleIfritContinuation.LandingInstructionOffset;
    }
}
