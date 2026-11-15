namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleGraphicsState Graphics, byte Phase, bool Complete) PollNativeIndependentSummonGraphics(NativeBattleGraphicsState graphics, NativeBattleActionGraphicsState context, byte phase)
    {
        context = context with { ActorSelector56 = 255 };
        graphics = graphics with { ActionGraphics = context };
        NativeBattleActionHeaderSlot header = nativeActionPackets.ReadHeader(context.Header);
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
            ushort action = header.Action;
            if (action is < 7 or > 10)
            {
                graphics = PublishNativeOriginalEffectTitle(graphics, context);
                context = graphics.ActionGraphics ?? context;
            }
            NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native50B830 requires its actual camera owner.")) with { ForceCamera = true };
            graphics = PrepareNativeHeaderGeometry(graphics with { Flags = graphics.Flags | 0x10 }, context);
            byte hidden = 0;
            for (int index = 0; index < 3; index++)
            {
                NativeBattleGraphicsModel model = graphics.ActorModels.Get(index);
                if ((model.Flags0 & 2) != 0)
                {
                    graphics = graphics with { ActorModels = graphics.ActorModels.Set(index, NativeBattleGraphicsModels.StartService(model, 11)) };
                    hidden |= (byte)(1 << index);
                }
            }
            PublishNativeModuleSound(new(0x18, 0, null) { Pan = 0x80 });
            PublishNativeModuleSound(new(0x19, 0, null) { Pan = 0x80 });
            graphics = BeginNativeOriginalEffect(graphics, header.Effect);
            context = context with { IndependentSummonPartyMask6C = hidden };
            return (graphics with { ActionGraphics = context, OriginalEffect = graphics.OriginalEffect! with { Parameters = CreateNativeOriginalEffectParameters(graphics, context) } }, 2, false);
        }
        byte mask = context.IndependentSummonPartyMask6C ?? throw new NotSupportedException("Native50B830 requires its actual hidden-party mask BYTE1D99A6C; migrate the standalone snapshot explicitly.");
        if (phase == 2)
        {
            if ((NativeEffectIoResult ?? throw new NotSupportedException("Native50B830 requires its actual signed effect IO result.")) < 0)
            {
                return (graphics, phase, false);
            }
            for (int index = 0; index < 3; index++)
            {
                if ((mask & 1 << index) != 0 && (graphics.ActorModels.Get(index).Flags0 & 4) == 0)
                {
                    return (graphics, phase, false);
                }
            }
            events.Add(new(Frame, "NativeSoundStopAll", null, null, 0, "Native46B440 stops all32 sound-effect voices.") { NativeStopAllSounds = true });
            if ((mask & 4) != 0)
            {
                NativeBattleGraphicsModel model = graphics.ActorModels.Get(2);
                graphics = graphics with { ActorModels = graphics.ActorModels.Set(2, model with { Flags0 = (ushort)(model.Flags0 & ~2), EffectCounter97 = 0 }) };
            }
            NativeEffectMemory = (NativeEffectMemory ?? throw new InvalidDataException("Native50B830 lost its effect memory.")).CopyEntry50B830() with { GuardianCasterBank = 1 };
            return (InvokeNativeOriginalEffect(graphics), 3, false);
        }
        if (phase == 3)
        {
            if (NativeOriginalEffectBusy(graphics))
            {
                return (graphics, phase, false);
            }
            phase = 4;
        }
        if (phase == 4)
        {
            if ((mask & 4) != 0)
            {
                QueueNativeModelResource507080(graphics, graphics.ActorModels.Get(2).ModelId4 ?? throw new InvalidDataException("Native50B830 lost slot2's actual model BYTE4."), 2);
            }
            phase = 5;
        }
        if (phase == 5)
        {
            if (PollNativeModelResource507070(ref graphics) != 0)
            {
                return (graphics, phase, false);
            }
            if ((mask & 4) != 0)
            {
                graphics = ReapplyNativeGuardianStatus(graphics, 2);
                QueueNativeModelResource507080(graphics, (ushort)((graphics.ActorModels.Get(2).ModelId4 ?? throw new InvalidDataException("Native50B830 lost slot2's actual model BYTE4.")) | 0x1000), 2);
            }
            phase = 6;
        }
        if (phase == 6)
        {
            if (PollNativeModelResource507070(ref graphics) != 0)
            {
                return (graphics, phase, false);
            }
            if ((mask & 2) != 0)
            {
                QueueNativeModelResource507080(graphics, (ushort)((graphics.ActorModels.Get(1).ModelId4 ?? throw new InvalidDataException("Native50B830 lost slot1's actual model BYTE4.")) | 0x1000), 1);
            }
            phase = 7;
        }
        if (phase == 7)
        {
            if (PollNativeModelResource507070(ref graphics) != 0)
            {
                return (graphics, phase, false);
            }
            for (byte index = 0; index < 3; index++)
            {
                if ((mask & 1 << index) != 0)
                {
                    NativeBattleGraphicsModel model = graphics.ActorModels.Get(index);
                    graphics = graphics with { ActorModels = graphics.ActorModels.Set(index, model with { Flags0 = (ushort)(model.Flags0 | 2) }) };
                    graphics = SelectNativeHeaderSequence(graphics, index, (ushort)(NativeStandingSequence(model.Flags8 ?? throw new InvalidDataException("Native50B830 requires the restored model DWORD8.")) | 0x1000));
                    graphics = graphics with { ActorModels = graphics.ActorModels.Set(index, NativeBattleGraphicsModels.StartService(graphics.ActorModels.Get(index), 12)) };
                }
            }
            graphics = CompleteNativeHeaderGraphics(graphics);
            NativeEffectMemory = (NativeEffectMemory ?? throw new InvalidDataException("Native50B830 lost its effect memory.")) with { Active = 0 };
            return (graphics with { Flags = graphics.Flags & ~0x10u }, phase, true);
        }
        return (graphics, phase, false);
    }
}
