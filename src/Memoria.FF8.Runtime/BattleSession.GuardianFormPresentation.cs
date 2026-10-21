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
        if (definition.GuardianFormsEnabled)
        {
            sources = sources.Concat(definition.GuardianFormResources.SelectMany(resource => resource.VisualResource.ModelLoadSources ?? []));
        }
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
        NativeBattleGuardianFormPresentation presentation = new(actor.Id, invocationId, GuardianFormInvocationMode.EntryOnly, caster, 0, graphics.Models[caster], NativeCamera ?? throw new NotSupportedException("GF entry requires the native camera owner."), NativeStage ?? throw new NotSupportedException("GF entry requires the native stage owner."), graphics.Models.Select(model => model.Flags0).ToImmutableArray(), graphics.Flags);
        presentation.Validate();
        graphics = BeginNativeOriginalEffect(graphics, 201);
        for (int index = 0; index < 3; index++)
        {
            graphics = graphics with { Models = graphics.Models.SetItem(index, graphics.Models[index] with { Flags0 = (ushort)(graphics.Models[index].Flags0 | 8) }) };
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
        NativeGraphics = graphics with { GuardianFormPresentation = presentation with { Mode = GuardianFormInvocationMode.FinisherWithoutEntry, Phase = 4, Camera = NativeCamera!, Stage = NativeStage!, ModelVisibility = graphics.Models.Select(model => model.Flags0).ToImmutableArray(), GraphicsFlags = graphics.Flags } };
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
            NativeCamera = presentation.Camera;
            NativeStage = presentation.Stage;
        }
        ImmutableArray<NativeBattleGraphicsModel> models = graphics.Models;
        if (ownsCinematic)
        {
            for (int index = 0; index < models.Length; index++)
            {
                ushort flags = (ushort)((models[index].Flags0 & ~8) | (presentation.ModelVisibility[index] & 8));
                models = models.SetItem(index, models[index] with { Flags0 = flags });
            }
        }
        NativeBattleGraphicsModel body = models[presentation.ActorIndex];
        if (body.ModelId4 == 77 && body.TextureResourceIndex is int row && NativeModelTextureCache is { } cache && cache.Rows[row].Kind == 3)
        {
            (NativeModelCpuResources, NativeFileStaging) = NativeBattleModelEnemyHeap.Release(NativeModelCpuResources!, NativeFileStaging!, cache, row);
            NativeModelTextureCache = cache.Release5080D0(row);
        }
        models = models.SetItem(presentation.ActorIndex, presentation.HumanModel with { Flags8 = models[presentation.ActorIndex].Flags8, StatusTimers = models[presentation.ActorIndex].StatusTimers });
        graphics = graphics with { Models = models, Flags = ownsCinematic ? presentation.GraphicsFlags : graphics.Flags, GuardianFormPresentation = null };
        NativeGraphics = ownsCinematic ? StopGuardianFormCinematic(graphics) : graphics;
        if (ownsCinematic)
        {
            SetNativeBattleActionPause(false);
        }
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
                graphics = graphics with { Models = graphics.Models.SetItem(index, graphics.Models[index] with { Flags0 = (ushort)(graphics.Models[index].Flags0 | 8) }) };
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
        if (NativeEffectMemory is { } memory)
        {
            NativeEffectMemory = memory with { ReturnedTaskPointer1D96AAC = 0, DetachedReturnedTask = null, Active = 0, Cinematic = null };
        }
        events.Add(new(Frame, "NativeSoundStopAll", null, null, 0, "GF presentation releases its sound voices.") { NativeStopAllSounds = true });
        return graphics with { OriginalEffect = graphics.OriginalEffect is { Binding.Effect: 201 } effect ? effect with { ReturnedDescriptor = 0, Pools = [] } : graphics.OriginalEffect, PrimitiveFrame = graphics.PrimitiveFrame is { } primitive ? primitive with { Pending = NativeBattleLogicalOrdering.Clear(), LastCommands = [] } : null };
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
            graphics = graphics with { Models = graphics.Models.SetItem(presentation.ActorIndex, graphics.Models[presentation.ActorIndex] with { ModelId4 = 77 }) };
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
            model = model with { ModelId4 = 77, Flags0 = (ushort)(model.Flags0 & ~8), Flags2 = (ushort)(model.Flags2!.Value & ~4), CurrentBodyBoneMatrices = null, LinkedModelIndex140 = null, LinkedTransform = null, HasLinkedModelRing140 = false, Rotation12 = 0, Rotation14 = 0, Rotation16 = 0, Transform = new(new(0, 0, 0), new(0, 0, 0), 0, 0, scale), SequenceState = new() { Default2 = NativeStandingSequence(model.Flags8!.Value) }, SequenceByte145 = 0, Sequence = 1, ResourceFlags44 = 1 };
            graphics = graphics with { Models = graphics.Models.SetItem(loading.ActorIndex, model), GuardianFormPresentation = loading with { Phase = 3 }, Flags = loading.GraphicsFlags };
            for (int index = 0; index < 3; index++)
            {
                if (index != loading.ActorIndex)
                {
                    graphics = graphics with { Models = graphics.Models.SetItem(index, graphics.Models[index] with { Flags0 = (ushort)((graphics.Models[index].Flags0 & ~8) | (loading.ModelVisibility[index] & 8)) }) };
                }
            }
            NativeGraphics = graphics;
            CompleteGuardianFormEntry(loading.ActorId, loading.InvocationId);
            SetNativeBattleActionPause(false);
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
        return pc == resource + 0x154;
    }
}
