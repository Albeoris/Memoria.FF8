using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeStatusCureItem(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native status-cure item requires its selected original module.");
        NativeBattleStatusCureItemResources resources = effect.Binding.StatusCureItemResources ?? throw new NotSupportedException("Native status-cure item requires its original ACT resources.");
        NativeBattleStatusCureItemFrame frame = NativeBattleStatusCureItemController.Initialize(NativeEffectMemory ?? throw new NotSupportedException("Native status-cure item requires its retained arena and texture owners."), effect, resources, 0);
        NativeEffectMemory = frame.Memory;
        return graphics with { OriginalEffect = frame.Effect };
    }

    private NativeBattleGraphicsState AdvanceNativeStatusCureItem(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native status-cure item callback requires its current module.");
        NativeBattleStatusCureItemResources resources = effect.Binding.StatusCureItemResources ?? throw new NotSupportedException("Native status-cure item callback requires its original ACT resources.");
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native status-cure item requires its current projection registers.");
        NativeBattleStatusCureItemFrame frame = new(NativeEffectMemory ?? throw new NotSupportedException("Native status-cure item requires its current effect memory."), effect, effect.StatusCureItem ?? throw new NotSupportedException("Native status-cure item requires its executed setup."));
        NativeBattleStatusCureItemInputs inputs = new(nativeActionPackets, projection.View ?? throw new NotSupportedException("Native status-cure item requires its published camera matrix."), ReadAnchor, ReadModelY, Draw) { DrawWithPilotIdentity = resources.Effect is 292 or 293 or 294 ? DrawWithIdentity : null };
        NativeBattleStatusCureItemStep step = NativeBattleStatusCureItemController.Advance(frame, resources, inputs);
        NativeEffectMemory = step.Frame.Memory;
        foreach (int contact in step.Contacts)
        {
            ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
            graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact)] });
            graphics = graphics with { PendingContacts = pending };
        }
        foreach (NativeBattleModuleSound sound in step.Sounds)
        {
            PublishNativeModuleSound(sound);
        }
        return graphics with { OriginalEffect = step.Frame.Effect };

        short ReadModelY(int index)
        {
            return (graphics.Models[index].Transform ?? throw new NotSupportedException("Native status-cure item requires its actual target position.")).Position28.Y;
        }

        NativeBattleVector ReadAnchor(int index)
        {
            if (resources.Effect == 293)
            {
                return ReadNativeGraphicsBone(graphics, index, 240, 0);
            }
            NativeBattleModuleAnchor anchor = ReadNativeModelAnchor(graphics, index);
            NativeBattleVector position = new(anchor.X, anchor.Y, anchor.Z);
            if (resources.Effect == 295)
            {
                return position;
            }
            short height = graphics.Models[index].MinimumY146 ?? throw new NotSupportedException("Native status-cure item requires its original target WORD92h.");
            return NativeBattleStatusCureItemGeometry.Anchor(resources.Effect, position, ReadModelY(index), height);
        }

        NativeBattleStatusCureItemFrame Draw(NativeBattleStatusCureItemFrame current, NativeBattleGfAnimationNode node, NativeBattleVector anchor) => DrawWithIdentity(current, node, anchor, null);

        NativeBattleStatusCureItemFrame DrawWithIdentity(NativeBattleStatusCureItemFrame current, NativeBattleGfAnimationNode node, NativeBattleVector anchor, NativeBattlePilotVisualIdentity? pilotIdentity)
        {
            NativeBattleStatusCureItemGeometryStep geometry = NativeBattleStatusCureItemGeometry.Emit(current, resources, node, anchor, projection, definition.NativeGraphics!.Rules.SummonColors, pilotIdentity);
            if (geometry.Batch.Attempts.IsEmpty)
            {
                return geometry.Frame;
            }
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native status-cure item requires its initialized ordering table.");
            int cursor = geometry.Frame.Effect.DrawArenaOffset ?? throw new NotSupportedException("Native status-cure item requires its current packet cursor.");
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(geometry.Frame.Memory, cursor, primitive.Pending, geometry.Batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return geometry.Frame with { Memory = written.Memory, Effect = geometry.Frame.Effect with { DrawArenaOffset = written.Cursor } };
        }
    }
}
