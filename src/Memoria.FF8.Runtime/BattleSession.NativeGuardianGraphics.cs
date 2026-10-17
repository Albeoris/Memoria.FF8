namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    /// <summary>50B2A0: the descriptor owns the phase; this context retains only the original action globals.</summary>
    private (NativeBattleGraphicsState Graphics, byte Phase, bool Complete) PollNativeGuardianGraphics(NativeBattleGraphicsState graphics, NativeBattleActionGraphicsState context, byte phase)
    {
        NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[context.HeaderIndex];
        byte caster = context.ActorIndex;
        if (caster >= 3)
        {
            throw new NotSupportedException("Native50B2A0 requires a physical party caster and its CPU model bank.");
        }
        if (phase == 0)
        {
            if (graphics.Models.Any(model => (model.Flags0 & 2) != 0 && NativeBattleActionGraphics.IsModelBusy(model, 0x40)) || NativeOriginalSoundBusy())
            {
                return (graphics, phase, false);
            }
            phase = 1;
        }
        if (phase == 1)
        {
            NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native50B2A0 requires its actual camera owner.")) with { ForceCamera = true };
            graphics = PrepareNativeHeaderGeometry(graphics with { Flags = graphics.Flags | 0x10 }, context);
            NativeBattleGraphicsModel actor = graphics.Models[caster];
            actor = actor with { Flags8 = (actor.Flags8 ?? throw new InvalidDataException("Native50B2A0 requires the caster DWORD8.")) & ~0x800000u };
            graphics = SelectNativeHeaderSequence(graphics with { Models = graphics.Models.SetItem(caster, actor) }, caster, header.ReadByte(2));
            byte selector = (byte)(NextNativeGlobalRandom() % 3 | 0x10);
            StartNativeGuardianCamera(graphics, caster, selector);
            byte hidden = 0;
            for (int index = 0; index < 3; index++)
            {
                NativeBattleGraphicsModel model = graphics.Models[index];
                if (index != caster && (model.Flags0 & 2) != 0)
                {
                    graphics = graphics with { Models = graphics.Models.SetItem(index, NativeBattleGraphicsModels.StartService(model, 11)) };
                    hidden |= (byte)(1 << index);
                }
            }
            graphics = BeginNativeOriginalEffect(graphics, NativeBattleActionGraphics.Word(header, 6));
            context = context with { Guardian = new(hidden, selector, 0) };
            return (graphics with { ActionGraphics = context, OriginalEffect = graphics.OriginalEffect! with { Parameters = CreateNativeOriginalEffectParameters(context) } }, 2, false);
        }
        NativeBattleGuardianServiceState guardian = context.Guardian ?? throw new InvalidDataException("Native50B2A0 lost its initialized action globals.");
        if (phase == 2)
        {
            if ((NativeEffectIoResult ?? throw new NotSupportedException("Native50B2A0 requires the actual signed effect IO result.")) < 0 || NativeCameraTaskMask != 0)
            {
                return (graphics, phase, false);
            }
            graphics = SetNativeCastingResourceFlags(graphics, caster, 0x10);
            NativeEffectMemory = (NativeEffectMemory ?? throw new InvalidDataException("Native50B2A0 lost its effect memory.")) with { GuardianCasterBank = Math.Min(caster, (byte)1) };
            return (graphics, 3, false);
        }
        if (phase == 3)
        {
            NativeBattleGraphicsModel actor = graphics.Models[caster];
            if (graphics.ModuleEffects.PublishedCount != 0 || (actor.Flags0 & 4) == 0)
            {
                return (graphics, phase, false);
            }
            events.Add(new(Frame, "NativeSoundStopAll", null, null, 0, "Native46B440 stops all32 sound-effect voices.") { NativeStopAllSounds = true });
            actor = actor with { Flags0 = (ushort)((actor.Flags0 & 0xFFF5) | 8), EffectCounter97 = 0, ResourceFlags44 = (ushort)(NativeCastingResourceFlags(graphics, caster) & ~0x20) };
            graphics = graphics with { Models = graphics.Models.SetItem(caster, actor) };
            if (caster != 2 && (guardian.HiddenPartyMask47 & 4) != 0)
            {
                CopyNativeGuardianCpuBank(2, caster);
                NativeBattleGraphicsModel row2 = graphics.Models[2];
                graphics = graphics with { Models = graphics.Models.SetItem(2, row2 with { Flags0 = (ushort)(row2.Flags0 & ~2) }) };
            }
            graphics = InvokeNativeOriginalEffect(graphics);
            if (header.ReadByte(1) == 0xFE)
            {
                graphics = RegisterNativeGuardianBoost(graphics, NativeBattleActionGraphics.Word(header, 4));
            }
            phase = 4;
        }
        if (phase == 4)
        {
            if (NativeOriginalEffectBusy(graphics))
            {
                return (graphics, phase, false);
            }
            if (caster != 2 && (guardian.HiddenPartyMask47 & 4) != 0)
            {
                CopyNativeGuardianCpuBank(caster, 2);
                NativeBattleGraphicsModel row2 = graphics.Models[2];
                graphics = graphics with { Models = graphics.Models.SetItem(2, row2 with { Flags0 = (ushort)(row2.Flags0 | 2) }) };
            }
            byte model = graphics.Models[caster].ModelId4 ?? throw new InvalidDataException("Native50B2A0 lost the caster model BYTE4.");
            QueueNativeModelResource507080(graphics, model, caster);
            phase = 5;
        }
        if (phase == 5)
        {
            if (PollNativeModelResource507070(ref graphics) != 0)
            {
                return (graphics, phase, false);
            }
            byte model = graphics.Models[caster].ModelId4 ?? throw new InvalidDataException("Native50B2A0 lost the caster model BYTE4.");
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
            byte neighbor = (byte)(caster >= 2 ? caster - 1 : caster + 1);
            context = context with { Guardian = guardian with { Neighbor70 = neighbor } };
            guardian = context.Guardian!;
            graphics = graphics with { ActionGraphics = context };
            NativeBattleGraphicsModel model = graphics.Models[neighbor];
            if ((model.Flags0 & 2) != 0)
            {
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
            byte neighbor = guardian.Neighbor70 ?? throw new InvalidDataException("Native50B2A0 lost its selected neighboring row.");
            NativeBattleGraphicsModel adjacent = graphics.Models[neighbor];
            if ((adjacent.Flags0 & 2) != 0)
            {
                adjacent = adjacent with { Flags2 = (ushort)((adjacent.Flags2 ?? throw new InvalidDataException("Native50B2A0 requires the neighbor WORD2.")) | 4) };
                graphics = graphics with { Models = graphics.Models.SetItem(neighbor, adjacent) };
                graphics = SelectNativeHeaderSequence(graphics, neighbor, (ushort)(NativeStandingSequence(adjacent.Flags8 ?? throw new InvalidDataException("Native509C10 requires the neighbor DWORD8.")) | 0x1000));
            }
            byte parameter81 = graphics.OriginalEffect?.Parameters?.Parameter81 ?? throw new InvalidDataException("Native50B2A0 lost its module parameter BYTE81.");
            for (int index = 0; index < 3; index++)
            {
                NativeBattleGraphicsModel model = graphics.Models[index];
                if (index != caster && (model.Flags0 & 2) != 0)
                {
                    model = model with { Flags0 = (ushort)(model.Flags0 & ~4) };
                    model = parameter81 == 2 ? model with { Color44 = NativeStage?.ModelColors?.Color44 ?? throw new NotSupportedException("Native50B2A0 requires the actual stage normal color DWORDB8B9A8.") } : NativeBattleGraphicsModels.StartService(model, 12);
                    graphics = graphics with { Models = graphics.Models.SetItem(index, model) };
                }
            }
            NativeBattleGraphicsModel actor = graphics.Models[caster];
            actor = actor with { Flags0 = (ushort)((actor.Flags0 & 0xFFF1) | 2), Flags2 = (ushort)((actor.Flags2 ?? throw new InvalidDataException("Native50B2A0 requires the caster WORD2.")) | 4) };
            if (parameter81 == 2)
            {
                actor = actor with { SequenceState = (actor.SequenceState ?? throw new InvalidDataException("Native50B2A0 requires the caster resource WORD40.")) with { Header40 = 0 } };
                graphics = SelectNativeHeaderSequence(graphics with { Models = graphics.Models.SetItem(caster, actor) }, caster, 1);
                graphics = RefreshNativeGuardianStandingSequence(graphics, caster);
            }
            else
            {
                graphics = SelectNativeHeaderSequence(graphics with { Models = graphics.Models.SetItem(caster, actor) }, caster, 20);
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
            if (graphics.OriginalEffect?.Parameters?.Parameter81 == 3)
            {
                PublishNativeModuleSound(new(0x87, 0, null) { Pan = 0x80 });
                PublishNativeModuleSound(new(0x88, 0, null) { Pan = 0x80 });
                graphics = ApplyNativeActiveHeaderContacts(graphics);
            }
            NativeEffectMemory = (NativeEffectMemory ?? throw new InvalidDataException("Native50B2A0 lost its effect memory owner.")) with { Active = 0 };
            return (graphics with { Flags = graphics.Flags & ~0x10u }, phase, true);
        }
        return (graphics, phase, false);
    }

    private void StartNativeGuardianCamera(NativeBattleGraphicsState graphics, byte caster, byte selector)
    {
        NativeBattleGraphicsResource resource = NativeActionResource(graphics, caster);
        _ = resource.HasActionCamera ?? throw new NotSupportedException("Native50B2A0 requires its original action camera pointer presence.");
        NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native50B2A0 requires its camera owner.")).Start(resource.ActionCameraBank, selector);
    }

    private void CopyNativeGuardianCpuBank(byte source, byte destination)
    {
        NativeBattleModelCpuResources resources = NativeModelCpuResources ?? throw new NotSupportedException("Native50B2A0 requires its actual retained CPU model-bank bytes.");
        NativeModelCpuResources = resources with { Cpu = resources.Cpu.Copy64K50B2A0(source, destination) };
    }

    private NativeBattleGraphicsState ReapplyNativeGuardianStatus(NativeBattleGraphicsState graphics, byte caster)
    {
        NativeBattleGraphicsModel model = graphics.Models[caster];
        if (model.HasLinkedModelRing140 ?? throw new NotSupportedException("Native50B810 requires the caster's actual linked-ring pointer."))
        {
            throw new NotSupportedException("Native50B810 linked-ring status projection requires its original50A000 auxiliary callback.");
        }
        uint flags = model.Flags8 ?? throw new InvalidDataException("Native50B810 requires the caster DWORD8.");
        model = SetNativeGraphicsFlags(model with { Flags8 = flags & ~0x410u }, flags, graphics);
        return graphics with { Models = graphics.Models.SetItem(caster, model) };
    }

    private NativeBattleGraphicsState RefreshNativeGuardianStandingSequence(NativeBattleGraphicsState graphics, byte caster)
    {
        NativeBattleGraphicsModel model = graphics.Models[caster];
        byte standing = NativeStandingSequence(model.Flags8 ?? throw new InvalidDataException("Native509C80 requires the caster DWORD8."));
        NativeBattleSequenceState state = model.SequenceState ?? throw new InvalidDataException("Native509C80 requires the caster resource header.");
        if (state.Pending3 != 0)
        {
            return graphics with { Models = graphics.Models.SetItem(caster, model with { SequenceState = state with { Pending3 = model.Sequence != standing ? standing : (byte)0 } }) };
        }
        if (model.Sequence == standing)
        {
            return graphics;
        }
        ushort flags = model.ResourceFlags44 ?? throw new InvalidDataException("Native505CB0 requires the caster resource WORD44.");
        return (flags & 0x40) != 0 ? SelectNativeHeaderSequence(graphics, caster, standing) : graphics with { Models = graphics.Models.SetItem(caster, model with { SequenceState = state with { Pending3 = standing } }) };
    }
}
