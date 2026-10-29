using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeAero6C0CB0(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleAeroResources resources = effect.Binding.AeroResources ?? throw new NotSupportedException("Native Aero requires its original resources.");
        RegisterNativeOriginalEffectAnimation(effect, resources.Animation, 0x25215D8);
        return graphics with { OriginalEffect = NativeBattleAeroController.Initialize6C0CB0(effect, nativeActionPackets) };
    }

    private NativeBattleGraphicsState AdvanceNativeAero6C0D20(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleAeroResources resources = effect.Binding.AeroResources ?? throw new NotSupportedException("Native Aero requires its original resources.");
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Aero requires its published projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Aero requires its retained view matrix.");
        NativeBattleActionMatrix billboard = NativeBattleGfMatrices.Normalize67AAE0(view);
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native Aero requires its original depth-cue constants.");
        NativeBattleAeroFrame frame = new(NativeEffectMemory ?? throw new NotSupportedException("Native Aero requires retained effect memory."), effect, nativeCrtRandom ?? throw new NotSupportedException("Native Aero requires its retained CRT random state."));
        frame = NativeBattleAeroController.Advance6C0D20(frame, new(graphics.Flags, nativeActionPackets, actor => graphics.Models[actor].Transform ?? throw new NotSupportedException("Native Aero requires its actual target transform."), actor => ReadNativeModelAnchor(graphics, actor), Execute));
        NativeEffectMemory = frame.Memory;
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleAeroFrame Execute(NativeBattleAeroFrame current, NativeBattleAeroOperation operation)
        {
            NativeEffectMemory = current.Memory;
            switch (operation)
            {
                case NativeBattleAeroContact contact:
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.Contact)] });
                    graphics = graphics with { PendingContacts = pending };
                    return current with { Memory = NativeEffectMemory! };
                case NativeBattleAeroSound sound:
                    PublishNativeModuleSound(resources.Sound, 1, sound.Position);
                    return current with { Memory = NativeEffectMemory! };
                case NativeBattleAeroNode node:
                    NativeBattleAeroNodeDraw draw = NativeBattleAeroDrawing.Node(current.Memory, node.Source, node.Node, node.Config, view, billboard, projection, colors, node.PilotIdentity);
                    return Append(current with { Memory = draw.Memory }, draw.Packets);
                case NativeBattleAeroParticle particle:
                    return Append(current, NativeBattleAeroDrawing.Particle(particle.Task, resources, projection, view, colors));
                default:
                    throw new NotSupportedException("Native Aero operation requires its original callback.");
            }
        }

        NativeBattleAeroFrame Append(NativeBattleAeroFrame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Aero requires its initialized ordering table.");
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, current.Effect.DrawArenaOffset!.Value, primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, Effect = current.Effect with { DrawArenaOffset = written.Cursor } };
        }
    }
}
