using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsBinding? GuardianFormGraphicsBinding(string actorId, NativeBattleGraphicsBinding? normal)
    {
        return NativeGraphics is { GuardianFormPresentation: { } presentation } graphics && presentation.ActorId == actorId && graphics.Models[presentation.ActorIndex].ModelId4 == 77 ? new(actorId, 77, GuardianFormResources(2).VisualResource) { InitialPosition = normal?.InitialPosition } : null;
    }

    private ImmutableArray<NativeBattleModelDatSource> NativeModelResourceSources()
    {
        IEnumerable<NativeBattleModelDatSource> sources = definition.NativeGraphics!.Bindings.SelectMany(binding => binding.Resource.ModelLoadSources ?? []);
        if (!definition.GuardianFormsEnabled)
        {
            return sources.ToImmutableArray();
        }
        sources = sources.Concat(definition.GuardianFormResources.SelectMany(resource => resource.VisualResource.ModelLoadSources ?? []));
        return sources.DistinctBy(source => (source.ModelId, source.Linked, source.Selector)).ToImmutableArray();
    }

    private static NativeBattleEffectParameters GuardianFormPositioningParameters(NativeBattleGuardianFormPresentation presentation)
    {
        return new(presentation.ActorIndex, 0, checked((ushort)(1 << presentation.ActorIndex)), 0, 0, 0) { Parameter80 = 0, Parameter82 = 0 };
    }

    private static byte[] ReadGuardianFormPositioning(NativeBattleGuardianFormPresentation presentation, uint address, int length)
    {
        if (length < 0)
        {
            throw new InvalidDataException("Presentation positioning read has a negative extent.");
        }
        if (address >= WaterParametersAddress && (long)address - WaterParametersAddress <= 12L - length)
        {
            return NativeBattleCinematicParameterBytes.Read(GuardianFormPositioningParameters(presentation), checked((int)(address - WaterParametersAddress)), length);
        }
        byte[] header = new byte[20];
        header[0] = presentation.ActorIndex;
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(8), WaterContactsAddress);
        header[0x10] = 1;
        header[0x11] = 1;
        if (address >= WaterHeadersAddress && (long)address - WaterHeadersAddress <= header.Length - length)
        {
            return header.AsSpan(checked((int)(address - WaterHeadersAddress)), length).ToArray();
        }
        byte[] positioning = new byte[24];
        positioning[0] = presentation.ActorIndex;
        if (address >= WaterContactsAddress && (long)address - WaterContactsAddress <= positioning.Length - length)
        {
            return positioning.AsSpan(checked((int)(address - WaterContactsAddress)), length).ToArray();
        }
        throw new InvalidDataException("Presentation-only Ifrit read leaves its owned positioning inputs.");
    }

    private void PrepareGuardianFormPresentation(BattleCombatant actor, int guardianForceId)
    {
        if (guardianForceId != 2 || NativeGraphics is not { GuardianFormPresentation: null } graphics || graphics.ActorIds.IndexOf(actor.Id) is < 0 or >= 3 || NativeCamera is null || NativeStage is null)
        {
            throw new NotSupportedException("Ifrit transformation requires its native player presentation and camera/stage owners.");
        }
        BattleGuardianFormResources formResources = GuardianFormResources(guardianForceId);
        formResources.VisualResource.Validate();
        if (formResources.VisualResource.PoseSource is null || formResources.VisualResource.ModelLoadSources is not { IsDefaultOrEmpty: false })
        {
            throw new NotSupportedException("Ifrit transformation requires its original persistent model and pose resources.");
        }
        NativeBattleOriginalEffectState selected = NativeBattleEffectResourceResolver.MaterializeSelected(NativeBattleOriginalEffects.Select(201, definition.NativeEffectCatalog), nativeEffectResourceResolver);
        if (selected.Binding.CinematicResources is null || selected.Binding.ScriptedInitializationDirectories is null || NativeBattleEffectInitializerPolicy.Assets(selected.Binding).Length != 2)
        {
            throw new NotSupportedException("Ifrit transformation requires both original cinematic resources and their initialization directories.");
        }
    }

    private void BeginGuardianFormEntry(BattleCombatant actor, int guardianForceId, long invocationId)
    {
        NativeBattleGraphicsState graphics = RequireNativeGraphics();
        if (graphics.GuardianFormPresentation is { } existing)
        {
            if (existing.ActorId == actor.Id && existing.InvocationId == invocationId)
            {
                return;
            }
            throw new InvalidOperationException("Another GF presentation owns the battle scene.");
        }
        byte caster = checked((byte)graphics.ActorIds.IndexOf(actor.Id));
        NativeBattleGuardianFormPresentation presentation = new(actor.Id, invocationId, GuardianFormInvocationMode.EntryOnly, caster, 0, graphics.Models[caster], NativeCamera ?? throw new NotSupportedException("GF entry requires the native camera owner."), NativeStage ?? throw new NotSupportedException("GF entry requires the native stage owner."), graphics.Models.Select(model => model.Flags0).ToImmutableArray(), graphics.Flags) { ModelVisibilityFlags2 = graphics.Models.Select(model => model.Flags2).ToImmutableArray(), HumanFocalLength = NativeCamera.FocalLength ?? throw new NotSupportedException("GF entry requires the current human camera focal length.") };
        presentation.Validate();
        graphics = BeginNativeOriginalEffect(graphics, 201);
        for (int index = 0; index < 3; index++)
        {
            if ((graphics.Models[index].Flags0 & 2) == 0)
            {
                continue;
            }
            graphics = graphics with { Models = graphics.Models.SetItem(index, graphics.Models[index] with { Flags0 = (ushort)(graphics.Models[index].Flags0 | 8), Flags2 = (ushort)((graphics.Models[index].Flags2 ?? 0) | 4) }) };
        }
        NativeGraphics = graphics with { GuardianFormPresentation = presentation, Flags = graphics.Flags | 0x10 };
        QueueNativeModelResource507080(NativeGraphics, 77, caster);
        BeginNativeBattleActionPause();
    }

    private void PrepareGuardianFormFinisher(BattleCombatant actor, BattleGuardianFormState form)
    {
        NativeBattleGraphicsState graphics = RequireNativeGraphics();
        NativeBattleGuardianFormPresentation presentation = graphics.GuardianFormPresentation ?? throw new InvalidDataException("Ifrit finisher lost its persistent visual owner.");
        if (presentation.ActorId != actor.Id || presentation.InvocationId != form.InvocationId || presentation.Phase != 3 || presentation.Mode != GuardianFormInvocationMode.EntryOnly)
        {
            throw new InvalidOperationException("Ifrit finisher requires the active form's presentation owner.");
        }
    }

    private void ReturnGuardianFormPresentation(BattleCombatant actor, BattleGuardianFormState form)
    {
        if (NativeGraphics is not { GuardianFormPresentation: { } presentation } graphics || presentation.ActorId != actor.Id)
        {
            return;
        }
        bool ownsCinematic = presentation.Phase < 3 || presentation.Mode == GuardianFormInvocationMode.FinisherWithoutEntry;
        if (ownsCinematic)
        {
            NativeCamera = presentation.Camera.CurrentPose is { } pose ? presentation.Camera.WriteEffectGlobals(pose with { FocalLength = presentation.HumanFocalLength }).PublishProjection(presentation.GraphicsFlags) : (presentation.Camera with { FocalLength = presentation.HumanFocalLength }).PublishProjection(presentation.GraphicsFlags);
            NativeStage = presentation.Stage;
        }
        ImmutableArray<NativeBattleGraphicsModel> models = graphics.Models;
        if (ownsCinematic)
        {
            models = RestoreGuardianFormVisibility(models, presentation);
        }
        NativeBattleGraphicsModel body = models[presentation.ActorIndex];
        if (body.ModelId4 == 77 && body.TextureResourceIndex is int row && NativeModelTextureCache is { } cache && cache.Rows[row].Kind == 3)
        {
            (NativeModelCpuResources, NativeFileStaging) = NativeBattleModelEnemyHeap.Release(NativeModelCpuResources!, NativeFileStaging!, cache, row);
            NativeModelTextureCache = cache.Release5080D0(row);
        }
        models = models.SetItem(presentation.ActorIndex, presentation.HumanModel with { Flags8 = models[presentation.ActorIndex].Flags8, StatusTimers = models[presentation.ActorIndex].StatusTimers });
        graphics = graphics with { Models = models, Flags = ownsCinematic ? presentation.GraphicsFlags : graphics.Flags, GuardianFormPresentation = null, GuardianFormHumanFocalToRestore = ownsCinematic ? null : presentation.HumanFocalLength };
        graphics = FrameGuardianFormIdleCamera(graphics);
        NativeGraphics = ownsCinematic ? StopGuardianFormCinematic(graphics) : graphics;
        if (ownsCinematic)
        {
            SetNativeBattleActionPause(false);
        }
    }

    private static ImmutableArray<NativeBattleGraphicsModel> RestoreGuardianFormVisibility(ImmutableArray<NativeBattleGraphicsModel> models, NativeBattleGuardianFormPresentation presentation)
    {
        for (int index = 0; index < models.Length; index++)
        {
            NativeBattleGraphicsModel model = models[index];
            ushort flags = (ushort)((model.Flags0 & ~0xC) | (presentation.ModelVisibility[index] & 0xC));
            ushort? flags2 = model.Flags2 is ushort current ? (ushort)((current & ~4) | ((presentation.ModelVisibilityFlags2[index] ?? 0) & 4)) : null;
            models = models.SetItem(index, model with { Flags0 = flags, Flags2 = flags2 });
        }
        return models;
    }

    public bool CanSkipGuardianFormFinisher => NativeGraphics?.GuardianFormPresentation is { Mode: GuardianFormInvocationMode.FinisherWithoutEntry, Phase: 5, SkipRequested: false };

    private bool SkippingGuardianFormPresentation => NativeGraphics?.GuardianFormPresentation is { Mode: GuardianFormInvocationMode.FinisherWithoutEntry, Phase: 5, SkipRequested: true };

    private void AdvanceSkippedGuardianFormPresentation()
    {
        for (int callback = 0; callback < 32 && SkippingGuardianFormPresentation; callback++)
        {
            DispatchTurnBasedFrame();
        }
    }

    public bool SkipGuardianFormFinisher()
    {
        if (!CanSkipGuardianFormFinisher)
        {
            return false;
        }
        NativeBattleGraphicsState graphics = NativeGraphics!;
        NativeGraphics = graphics with { GuardianFormPresentation = graphics.GuardianFormPresentation! with { SkipRequested = true } };
        SettleGuardianFormSkipBoost();
        return true;
    }

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
            RecomputeNativeGuardianContacts(percent);
        }
    }

    private (NativeBattleGraphicsState Graphics, byte Phase, bool Complete) PollGuardianFormFinisherGraphics(NativeBattleGraphicsState graphics, NativeBattleActionGraphicsState context, byte phase)
    {
        NativeBattleGuardianFormPresentation presentation = graphics.GuardianFormPresentation ?? throw new InvalidDataException("Ifrit finisher has no current presentation invocation.");
        if (phase == 0)
        {
            presentation = presentation with { Mode = GuardianFormInvocationMode.FinisherWithoutEntry, Phase = 4, Camera = NativeCamera!, Stage = NativeStage!, ModelVisibility = graphics.Models.Select(model => model.Flags0).ToImmutableArray(), ModelVisibilityFlags2 = graphics.Models.Select(model => model.Flags2).ToImmutableArray(), GraphicsFlags = graphics.Flags };
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
            for (int index = 0; index < 3; index++)
            {
                if ((graphics.Models[index].Flags0 & 2) == 0)
                {
                    continue;
                }
                graphics = graphics with { Models = graphics.Models.SetItem(index, graphics.Models[index] with { Flags0 = (ushort)(graphics.Models[index].Flags0 | 8), Flags2 = (ushort)((graphics.Models[index].Flags2 ?? 0) | 4) }) };
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

    private NativeBattleGraphicsState StopGuardianFormCinematic(NativeBattleGraphicsState graphics)
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
            NativeBattleEffectInitializerAsset common = definition.NativeCommonEffectTexture ?? throw new NotSupportedException("GF presentation cleanup requires the common battle texture resource.");
            NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("GF presentation cleanup requires the retained texture upload owner.");
            (textures, _) = textures.EnqueueTim505E30(new(common.Name, common.Data));
            NativeEffectMemory = memory with { ReturnedTaskPointer1D96AAC = 0, DetachedReturnedTask = null, Active = 0, Cinematic = null, Textures = textures };
        }
        events.Add(new(Frame, "NativeSoundStopAll", null, null, 0, "GF presentation releases its sound voices.") { NativeStopAllSounds = true });
        return graphics with { OriginalEffect = graphics.OriginalEffect is { Binding.Effect: 201 } effect ? effect with { ReturnedDescriptor = 0, Pools = [] } : graphics.OriginalEffect, PrimitiveFrame = graphics.PrimitiveFrame is { } primitive ? primitive with { Pending = NativeBattleLogicalOrdering.Clear(), LastCommands = [] } : null };
    }

    private bool GuardianFormOwnsCurrentPresentation()
    {
        return NativeGraphics?.GuardianFormPresentation is { } presentation && (presentation.Phase < 3 || ActiveAction is { GuardianFormInvocation: { } invocation } action && action.ActorId == presentation.ActorId && invocation.Id == presentation.InvocationId);
    }

    private void FailOwnedGuardianFormPresentation()
    {
        if (NativeGraphics?.GuardianFormPresentation is not { } presentation || GetGuardianForm(presentation.ActorId) is not { } form)
        {
            return;
        }
        NativeGraphics = CancelGuardianFormPresentationDescriptor(NativeGraphics);
        if (NativeModelLoader?.Task is { Callback: 0x507120, ModelId: 77 } loader && loader.PhysicalIndex == presentation.ActorIndex)
        {
            if (loader.EnemyResourceIndex is int reserved && NativeModelTextureCache is { } cache && NativeGraphics.Models[presentation.ActorIndex].TextureResourceIndex != reserved)
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
        ReturnGuardianFormPresentation(GetActor(presentation.ActorId), form);
        FailGuardianFormPresentation(presentation.ActorId, presentation.InvocationId);
    }

    private NativeBattleGraphicsState AdvanceGuardianFormPresentation(NativeBattleGraphicsState graphics)
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
            NativeBattleGraphicsModel loaded = graphics.Models[presentation.ActorIndex] with { ModelId4 = 77, SequenceState = new() { Pose = 0, PoseFrame114 = 1, PoseLimit115 = resource.AnimationFrames[0], DecodedPose = source.Sample(0, 1, 0) }, CurrentBodyBoneMatrices = null };
            loaded = NativeBattleBodyBoneMatrices.Publish508C90(loaded, source);
            graphics = graphics with { Models = graphics.Models.SetItem(presentation.ActorIndex, loaded) };
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
            NativeBattleGraphicsModel model = graphics.Models[loading.ActorIndex];
            model = model with { ModelId4 = 77, Flags0 = (ushort)(model.Flags0 & ~0xC), Flags2 = (ushort)(model.Flags2!.Value & ~4), CurrentBodyBoneMatrices = null, LinkedModelIndex140 = null, LinkedTransform = null, HasLinkedModelRing140 = false, Rotation12 = 0, Rotation14 = 0, Rotation16 = 0, Transform = new(new(0, 0, 0), new(0, 0, 0), 0, 0, scale), SequenceState = new() { Default2 = NativeStandingSequence(model.Flags8!.Value) }, SequenceByte145 = 0, Sequence = 1, ResourceFlags44 = 1 };
            NativeBattleSequenceStep initializedPose = NativeBattleSequenceMachine.Advance(model, resource, new(NativeEntryAdvantage, 0) { OriginalResource = resource });
            if (initializedPose.Cues.Any(cue => cue.Kind != NativeBattleSequenceCueKind.RestoreStanding))
            {
                throw new NotSupportedException("GF body standing initialization reached a non-pose service.");
            }
            model = initializedPose.Model;
            model = model with { GeometryWorldMatrix = NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(model, pose, ReadNativeModelScaleVector502AB0(graphics, model)) };
            graphics = graphics with { Models = graphics.Models.SetItem(loading.ActorIndex, model), GuardianFormPresentation = loading with { Phase = 3 }, Flags = loading.GraphicsFlags };
            graphics = graphics with { Models = RestoreGuardianFormVisibility(graphics.Models, loading) };
            NativeGraphics = graphics;
            CompleteGuardianFormEntry(loading.ActorId, loading.InvocationId);
            graphics = NativeGraphics!;
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
