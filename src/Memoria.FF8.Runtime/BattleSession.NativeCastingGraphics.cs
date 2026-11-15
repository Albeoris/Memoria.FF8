namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    /// <summary>50A9A0's eight physical descriptor phases, including its resourceWORD44 casting handshake.</summary>
    private (NativeBattleGraphicsState Graphics, byte Phase, bool Complete) PollNativeCastingGraphics(NativeBattleGraphicsState graphics, NativeBattleActionGraphicsState context, byte phase)
    {
        NativeBattleActionHeaderSlot header = nativeActionPackets.ReadHeader(context.Header);
        if (phase == 0)
        {
            (graphics, bool selected) = ChooseNativeActionCamera(graphics, context);
            context = context with { UseActionCamera = selected };
            graphics = graphics with { ActionGraphics = context };
            phase = 1;
        }
        if (phase == 1)
        {
            if (NativeActionModelsBusy(graphics, context))
            {
                return (graphics, phase, false);
            }
            phase = 2;
        }
        if (phase == 2)
        {
            graphics = graphics with { Flags = graphics.Flags | 0x10 };
            graphics = PrepareNativeHeaderGeometry(graphics, context);
            if (header.Command == 14 || header.Command == 11 && header.Action == 65531)
            {
                NativeEffectMemory = (NativeEffectMemory ?? throw new NotSupportedException("Native50A9A0 requires its retained parameterBYTE80 owner.")) with { Parameter80 = 0 };
            }
            NativeBattleEffectParameters parameters = CreateNativeOriginalEffectParameters(graphics, context);
            NativeBattleGraphicsModel actor = graphics.ActorModels.Get(context.Actor);
            graphics = graphics with { ActorModels = graphics.ActorModels.Set(context.Actor, actor with { Flags8 = (actor.Flags8 ?? throw new InvalidDataException("Native50A9A0 requires its loaded model DWORD8.")) & ~0x800000u }) };
            graphics = SelectNativeHeaderSequence(graphics, context.Actor, header.Sequence);
            graphics = StartNativeHeaderCamera(graphics, context);
            graphics = BeginNativeOriginalEffect(graphics, header.Effect);
            return (graphics with { OriginalEffect = graphics.OriginalEffect! with { Parameters = parameters } }, 3, false);
        }
        if (phase == 3)
        {
            if (NativeCastingModulesBusy(graphics) || (NativeEffectIoResult ?? throw new NotSupportedException("Native50A9A0 requires the actual signed effect IO result.")) < 0 || NativeCameraTaskMask != 0)
            {
                return (graphics, phase, false);
            }
            graphics = SetNativeCastingResourceFlags(graphics, context.Actor, 0x10);
            return (graphics, 4, false);
        }
        if (phase == 4)
        {
            if (NativeCastingModulesBusy(graphics) || (NativeCastingResourceFlags(graphics, context.Actor) & 0x20) == 0)
            {
                return (graphics, phase, false);
            }
            if (!context.CoverActors.IsEmpty)
            {
                graphics = graphics with { ActorModels = NativeBattleCastingCover.Enter(graphics.ActorModels, graphics.NativeBindings, context) };
                graphics = SelectNativeHeaderSequence(graphics, NativeBattleCastingCover.Recipient(graphics.ActorModels, context.TargetActors), 27);
                return (graphics, 5, false);
            }
            //50A9A0 advances to phase5, then falls directly into its non-cover entry call.
            phase = 5;
        }
        if (phase == 5)
        {
            AdmitDiagnosticVisualEntry(header);
            NativeBattleGraphicsModel actor = graphics.ActorModels.Get(context.Actor);
            graphics = graphics with { ActorModels = graphics.ActorModels.Set(context.Actor, actor with { ResourceFlags44 = (ushort)(NativeCastingResourceFlags(graphics, context.Actor) & ~0x20) }) };
            if ((graphics.Flags & 0x20000000) != 0)
            {
                graphics = graphics with { Flags = graphics.Flags & ~0x20000000u };
                NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native50A9A0 requires its action camera selector owner.");
                if (camera.ReadControlWord(0).RequireScalar() >= 0)
                {
                    NativeCamera = camera with { Mask = (ushort)(camera.Mask | 0x8000) };
                }
            }
            if (header.Command is 2 or 6 && (actor.ModelId4 ?? throw new InvalidDataException("Native50A9A0 requires its actor modelBYTE4.")) >= 16)
            {
                graphics = graphics with { Flags = graphics.Flags | 0x20 };
            }
            graphics = InvokeNativeOriginalEffect(graphics);
            if (header.Command == 0xFE)
            {
                graphics = RegisterNativeGuardianBoost(graphics, header.Action);
            }
            return (graphics with { ActionGraphics = context with { CastingWatchdog8E = 0 } }, 6, false);
        }
        if (phase == 6)
        {
            if (header.Action == 65534)
            {
                return (graphics, NativeOriginalEffectDescriptorReleased(graphics) ? (byte)7 : phase, false);
            }
            NativeBattleEffectParameters parameters = graphics.OriginalEffect?.Parameters ?? throw new InvalidDataException("Native50A9A0 lost its parameter block.");
            if (header.Command is 11 or 14)
            {
                return (graphics, parameters.Parameter81 == 1 ? (byte)7 : phase, false);
            }
            if (NativeOriginalEffectBusy(graphics))
            {
                if (NativeOriginalEffectDescriptorReleased(graphics))
                {
                    ushort watchdog = AdvanceNativeEffectSoundWatchdog(context.CastingWatchdog8E ?? throw new InvalidDataException("Native50A9A0 lost its initialized watchdogWORD8E."));
                    graphics = graphics with { ActionGraphics = context with { CastingWatchdog8E = watchdog } };
                }
                return (graphics, phase, false);
            }
            graphics = CompleteNativeHeaderGraphics(graphics);
            if ((parameters.Flags79 & 2) == 0)
            {
                graphics = graphics with { Flags = graphics.Flags & ~0x10u };
            }
            if (!context.CoverActors.IsEmpty)
            {
                graphics = graphics with { ActorModels = NativeBattleCastingCover.Complete(graphics.ActorModels, graphics.NativeBindings, context) };
            }
            return (graphics, 7, false);
        }
        if (phase == 7)
        {
            graphics = SetNativeCastingResourceFlags(graphics with { Flags = graphics.Flags & ~0x20u }, context.Actor, 0x200);
            NativeEffectMemory = (NativeEffectMemory ?? throw new InvalidDataException("Native50A9A0 lost its effect memory owner.")) with { Active = 0 };
            return (graphics, phase, true);
        }
        return (graphics, phase, false);
    }

    private static bool NativeCastingModulesBusy(NativeBattleGraphicsState graphics) => graphics.ModuleEffects.PublishedCount != 0 && (graphics.Flags & 0x20000000) == 0;

    private ushort AdvanceNativeEffectSoundWatchdog(ushort counter)
    {
        (ushort next, bool stop) = NativeBattleEffectSoundWatchdog.Advance(counter);
        if (stop)
        {
            PublishNativeStopAllSounds("Native46B440 stops all32 sound-effect voices.");
        }
        return next;
    }

    private static ushort NativeCastingResourceFlags(NativeBattleGraphicsState graphics, BattleModelOwner actor) => graphics.ActorModels.Get(actor).ResourceFlags44 ?? throw new NotSupportedException("Native50A9A0 requires the actual casting resource WORD44.");

    private static NativeBattleGraphicsState SetNativeCastingResourceFlags(NativeBattleGraphicsState graphics, BattleModelOwner index, ushort flags)
    {
        NativeBattleGraphicsModel model = graphics.ActorModels.Get(index);
        if (model.ReadLinkedModel() is not null)
        {
            (NativeBattleAuxiliaryState auxiliary, bool allocated) = graphics.Auxiliary.AppendModelRing(NativeBattleAuxiliaryKind.ModelResourceFlags50B080, index, graphics.NativeBindings.CaptureAliases(graphics.ActorModels), flags);
            return allocated ? graphics with { Auxiliary = auxiliary } : throw new InvalidOperationException("Native50A9A0 cannot write its50B080 payload after auxiliary allocation fails.");
        }
        return graphics with { ActorModels = graphics.ActorModels.Set(index, model with { ResourceFlags44 = (ushort)(NativeCastingResourceFlags(graphics, index) | flags) }) };
    }
}
