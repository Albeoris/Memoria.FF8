namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    /// <summary>50B2A0: the descriptor owns the phase; this context retains only the original action globals.</summary>
    private (NativeBattleGraphicsState Graphics, byte Phase, bool Complete) PollNativeGuardianGraphics(NativeBattleGraphicsState graphics, NativeBattleActionGraphicsState context, byte phase)
    {
        if (ActiveAction?.GuardianFormInvocation is { Mode: GuardianFormInvocationMode.FinisherWithoutEntry })
        {
            return PollGuardianFormFinisherGraphics(graphics, context, phase);
        }
        NativeBattleActionHeaderSlot header = nativeActionPackets.ReadHeader(context.Header);
        if (context.Actor is not BattlePartyModelOwner caster)
        {
            throw new InvalidDataException("Native50B2A0 requires its party caster and CPU model bank.");
        }
        byte? casterSlot = graphics.NativeBindings.TryEncodeExactActor(caster, graphics.ActorModels, out byte originalCasterSlot)
            ? originalCasterSlot
            : null;
        if (phase == 0)
        {
            if (graphics.ActorModels.Entries.Select(entry => entry.State).Any(model => (model.Flags0 & 2) != 0 && NativeBattleActionGraphics.IsModelBusy(model, 0x40)) || NativeOriginalSoundBusy())
            {
                return (graphics, phase, false);
            }
            phase = 1;
        }
        if (phase == 1)
        {
            NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native50B2A0 requires its actual camera owner.")) with { ForceCamera = true };
            graphics = PrepareNativeHeaderGeometry(graphics with { Flags = graphics.Flags | 0x10 }, context);
            NativeBattleGraphicsModel actor = graphics.ActorModels.Get(caster);
            actor = NativeBattleGuardianInvocation.EnterSource(actor);
            graphics = SelectNativeHeaderSequence(graphics with { ActorModels = graphics.ActorModels.Set(caster, actor) }, caster, header.Sequence);
            byte selector = (byte)(NextNativeGlobalRandom() % 3 | 0x10);
            StartNativeGuardianCamera(graphics, caster, selector);
            BattleModelTargets hidden = BattleModelTargets.Empty;
            foreach (BattleModelEntry<BattlePartyModelOwner> entry in graphics.ActorModels.Party.Entries)
            {
                if (entry.Owner != caster && (entry.State.Flags0 & 2) != 0)
                {
                    graphics = graphics with { ActorModels = graphics.ActorModels.Set(entry.Owner, NativeBattleGraphicsModels.StartService(entry.State, 11)) };
                    hidden = hidden.Add(entry.Owner);
                }
            }
            graphics = BeginNativeOriginalEffect(graphics, header.Effect);
            context = context with { Guardian = new(hidden, selector, 0) };
            return (graphics with { ActionGraphics = context, OriginalEffect = graphics.OriginalEffect! with { Parameters = CreateNativeOriginalEffectParameters(graphics, context) } }, 2, false);
        }
        NativeBattleGuardianServiceState guardian = context.Guardian ?? throw new InvalidDataException("Native50B2A0 lost its initialized action globals.");
        if (phase == 2)
        {
            if ((NativeEffectIoResult ?? throw new NotSupportedException("Native50B2A0 requires the actual signed effect IO result.")) < 0 || NativeCameraTaskMask != 0)
            {
                return (graphics, phase, false);
            }
            graphics = SetNativeCastingResourceFlags(graphics, caster, 0x10);
            NativeEffectMemory = (NativeEffectMemory ?? throw new InvalidDataException("Native50B2A0 lost its effect memory.")) with { GuardianCasterBank = casterSlot is byte slot ? Math.Min(slot, (byte)1) : null };
            return (graphics, 3, false);
        }
        if (phase == 3)
        {
            NativeBattleGraphicsModel actor = graphics.ActorModels.Get(caster);
            if (graphics.ModuleEffects.PublishedCount != 0 || (actor.Flags0 & 4) == 0)
            {
                return (graphics, phase, false);
            }
            PublishNativeStopAllSounds("Native46B440 stops all32 sound-effect voices.");
            actor = NativeBattleGuardianInvocation.HideSource(actor);
            graphics = graphics with { ActorModels = graphics.ActorModels.Set(caster, actor) };
            if (casterSlot is byte copyCaster && copyCaster != 2 &&
                graphics.NativeBindings.FindOriginalOwner(NativeBattleModelSlot.Decode(2), graphics.ActorModels) is { } originalBankOwner &&
                guardian.HiddenPartyActors.Contains(originalBankOwner))
            {
                CopyNativeGuardianCpuBank(2, copyCaster);
                NativeBattleGraphicsModel bankModel = graphics.ActorModels.Get(originalBankOwner);
                graphics = graphics with { ActorModels = graphics.ActorModels.Set(originalBankOwner, bankModel with { Flags0 = (ushort)(bankModel.Flags0 & ~2) }) };
            }
            AdmitDiagnosticVisualEntry(header);
            graphics = InvokeNativeOriginalEffect(graphics);
            if (header.Command == 0xFE)
            {
                graphics = RegisterNativeGuardianBoost(graphics, header.Action);
            }
            phase = 4;
        }
        if (phase == 4)
        {
            if (NativeOriginalEffectBusy(graphics))
            {
                return (graphics, phase, false);
            }
            if (casterSlot is byte copyCaster && copyCaster != 2 &&
                graphics.NativeBindings.FindOriginalOwner(NativeBattleModelSlot.Decode(2), graphics.ActorModels) is { } originalBankOwner &&
                guardian.HiddenPartyActors.Contains(originalBankOwner))
            {
                CopyNativeGuardianCpuBank(copyCaster, 2);
                NativeBattleGraphicsModel bankModel = graphics.ActorModels.Get(originalBankOwner);
                graphics = graphics with { ActorModels = graphics.ActorModels.Set(originalBankOwner, bankModel with { Flags0 = (ushort)(bankModel.Flags0 | 2) }) };
            }
            byte model = graphics.ActorModels.Get(caster).ModelId4 ?? throw new InvalidDataException("Native50B2A0 lost the caster model BYTE4.");
            QueueNativeModelResource507080(graphics, model, caster);
            phase = 5;
        }
        if (phase == 5)
        {
            if (PollNativeModelResource507070(ref graphics) != 0)
            {
                return (graphics, phase, false);
            }
            byte model = graphics.ActorModels.Get(caster).ModelId4 ?? throw new InvalidDataException("Native50B2A0 lost the caster model BYTE4.");
            QueueNativeModelResource507080(graphics, (ushort)(model | 0x1000), caster);
            phase = 6;
        }
        if (phase == 6)
        {
            if (PollNativeModelResource507070(ref graphics) != 0)
            {
                return (graphics, phase, false);
            }
            graphics = ReapplyNativeGuardianStatus(graphics, caster);
            BattlePartyModelOwner? neighbor = null;
            if (casterSlot is byte original)
            {
                byte neighborSlot = (byte)(original >= 2 ? original - 1 : original + 1);
                neighbor = graphics.NativeBindings.FindOriginalOwner(NativeBattleModelSlot.Decode(neighborSlot), graphics.ActorModels) as BattlePartyModelOwner;
            }
            context = context with { Guardian = guardian with { Neighbor = new(neighbor) } };
            guardian = context.Guardian!;
            graphics = graphics with { ActionGraphics = context };
            if (neighbor is not null && (graphics.ActorModels.Get(neighbor).Flags0 & 2) != 0)
            {
                NativeBattleGraphicsModel model = graphics.ActorModels.Get(neighbor);
                QueueNativeModelResource507080(graphics, (ushort)((model.ModelId4 ?? throw new InvalidDataException("Native50B2A0 lost its neighbor model BYTE4.")) | 0x1000), neighbor);
            }
            phase = 7;
        }
        if (phase == 7)
        {
            if (PollNativeModelResource507070(ref graphics) != 0)
            {
                return (graphics, phase, false);
            }
            NativeBattleGuardianNeighbor selected = guardian.Neighbor ?? throw new InvalidDataException("Native50B2A0 lost its initialized neighbor selection.");
            if (selected.Owner is { } neighbor)
            {
                NativeBattleGraphicsModel adjacent = graphics.ActorModels.Get(neighbor);
                if ((adjacent.Flags0 & 2) != 0)
                {
                    adjacent = adjacent with { Flags2 = (ushort)((adjacent.Flags2 ?? throw new InvalidDataException("Native50B2A0 requires the neighbor WORD2.")) | 4) };
                    graphics = graphics with { ActorModels = graphics.ActorModels.Set(neighbor, adjacent) };
                    graphics = SelectNativeHeaderSequence(graphics, neighbor, (ushort)(NativeStandingSequence(adjacent.Flags8 ?? throw new InvalidDataException("Native509C10 requires the neighbor DWORD8.")) | 0x1000));
                }
            }
            byte parameter81 = graphics.OriginalEffect?.Parameters?.Parameter81 ?? throw new InvalidDataException("Native50B2A0 lost its module parameter BYTE81.");
            foreach (BattleModelEntry<BattlePartyModelOwner> entry in graphics.ActorModels.Party.Entries)
            {
                NativeBattleGraphicsModel model = entry.State;
                if (entry.Owner != caster && (model.Flags0 & 2) != 0)
                {
                    model = model with { Flags0 = (ushort)(model.Flags0 & ~4) };
                    model = parameter81 == 2 ? model with { Color44 = NativeStage?.ModelColors?.Color44 ?? throw new NotSupportedException("Native50B2A0 requires the actual stage normal color DWORDB8B9A8.") } : NativeBattleGraphicsModels.StartService(model, 12);
                    graphics = graphics with { ActorModels = graphics.ActorModels.Set(entry.Owner, model) };
                }
            }
            NativeBattleGraphicsModel actor = graphics.ActorModels.Get(caster);
            actor = NativeBattleGuardianInvocation.RestoreSource(actor);
            if (parameter81 == 2)
            {
                actor = actor with { SequenceState = (actor.SequenceState ?? throw new InvalidDataException("Native50B2A0 requires the caster resource WORD40.")) with { Header40 = 0 } };
                graphics = SelectNativeHeaderSequence(graphics with { ActorModels = graphics.ActorModels.Set(caster, actor) }, caster, 1);
                graphics = RefreshNativeGuardianStandingSequence(graphics, caster);
            }
            else
            {
                graphics = SelectNativeHeaderSequence(graphics with { ActorModels = graphics.ActorModels.Set(caster, actor) }, caster, 20);
                StartNativeGuardianCamera(graphics, caster, unchecked((byte)(guardian.CameraSelector4E + 3)));
            }
            return (graphics, 8, false);
        }
        if (phase == 8)
        {
            if (NativeCameraTaskMask != 0)
            {
                return (graphics, phase, false);
            }
            return (CompleteNativeHeaderGraphics(graphics), 9, false);
        }
        if (phase == 9)
        {
            graphics = FinishNativeGuardianInvocationContacts(graphics);
            NativeEffectMemory = (NativeEffectMemory ?? throw new InvalidDataException("Native50B2A0 lost its effect memory owner.")) with { Active = 0 };
            return (graphics with { Flags = graphics.Flags & ~0x10u }, phase, true);
        }
        return (graphics, phase, false);
    }

    private NativeBattleGraphicsState FinishNativeGuardianInvocationContacts(NativeBattleGraphicsState graphics)
    {
        if (graphics.OriginalEffect?.Parameters?.Parameter81 == 3)
        {
            PublishNativeModuleSound(new(0x87, 0, null) { Pan = 0x80 });
            PublishNativeModuleSound(new(0x88, 0, null) { Pan = 0x80 });
            graphics = ApplyNativeActiveHeaderContacts(graphics);
        }
        return graphics;
    }

    private void StartNativeGuardianCamera(NativeBattleGraphicsState graphics, BattleModelOwner caster, byte selector)
    {
        NativeBattleGraphicsResource resource = NativeActionResource(graphics, caster);
        _ = resource.HasActionCamera ?? throw new NotSupportedException("Native50B2A0 requires its original action camera pointer presence.");
        NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native50B2A0 requires its camera owner.")).Start(resource.ActionCameraBank, selector);
    }

    private void CopyNativeGuardianCpuBank(byte source, byte destination)
    {
        NativeBattleModelCpuResources resources = NativeModelCpuResources ?? throw new NotSupportedException("Native50B2A0 requires its actual retained CPU model-bank bytes.");
        NativeModelCpuResources = resources with { Cpu = resources.Cpu.CopyBank50B2A0(source, destination) };
    }

    private NativeBattleGraphicsState ReapplyNativeGuardianStatus(NativeBattleGraphicsState graphics, BattleModelOwner caster)
    {
        NativeBattleGraphicsModel model = graphics.ActorModels.Get(caster);
        uint flags = model.Flags8 ?? throw new InvalidDataException("Native50B810 requires the caster DWORD8.");
        graphics = graphics with { ActorModels = graphics.ActorModels.Set(caster, model with { Flags8 = flags & ~0x410u }) };
        return ApplyNativeGraphicsStatus509CD0(graphics, caster, flags);
    }

    private NativeBattleGraphicsState RefreshNativeGuardianStandingSequence(NativeBattleGraphicsState graphics, BattleModelOwner caster)
    {
        (NativeBattleGraphicsModel model, byte? immediate) = NativeBattleGraphicsModels.PlanPlayerStanding509C80(graphics.ActorModels.Get(caster));
        return immediate is byte standing ? SelectNativeHeaderSequence(graphics, caster, standing) : graphics with { ActorModels = graphics.ActorModels.Set(caster, model) };
    }
}
