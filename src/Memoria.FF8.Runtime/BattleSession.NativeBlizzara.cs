using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeBlizzara6DA310(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleBlizzaraResources resources = effect.Binding.BlizzaraResources ?? throw new NotSupportedException("Native Blizzara requires its original resources.");
        RegisterNativeOriginalEffectAnimation(effect, resources.Animation, 0x2543E90);
        return graphics with { OriginalEffect = NativeBattleBlizzaraController.Initialize6DA310(effect, nativeActionPackets) };
    }

    private NativeBattleGraphicsState AdvanceNativeBlizzara6DA3A0(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleBlizzaraResources resources = effect.Binding.BlizzaraResources ?? throw new NotSupportedException("Native Blizzara requires its original resources.");
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Blizzara requires its published projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Blizzara requires its retained view matrix.");
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native Blizzara requires its depth-cue tables.");
        NativeBattleBlizzaraFrame frame = new(NativeEffectMemory ?? throw new NotSupportedException("Native Blizzara requires retained effect memory."), effect, nativeCrtRandom ?? throw new NotSupportedException("Native Blizzara requires its retained CRT random state."));
        frame = NativeBattleBlizzaraController.Advance6DA3A0(frame, new(graphics.Flags, nativeActionPackets, model => ReadNativeGraphicsBone(graphics, model, 241, 0), model => graphics.Models[model].Transform ?? throw new NotSupportedException("Native Blizzara requires its actual model transform."), Execute));
        NativeEffectMemory = frame.Memory;
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleBlizzaraFrame Execute(NativeBattleBlizzaraFrame current, NativeBattleBlizzaraOperation operation)
        {
            NativeEffectMemory = current.Memory;
            switch (operation)
            {
                case NativeBattleBlizzaraContact contact:
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.Contact)] });
                    graphics = graphics with { PendingContacts = pending };
                    return current with { Memory = NativeEffectMemory! };
                case NativeBattleBlizzaraSound sound:
                    PublishNativeModuleSound(resources.Sound, 0x101, sound.Position);
                    return current with { Memory = NativeEffectMemory! };
                case NativeBattleBlizzaraMainDraw draw:
                    NativeBattleActionMatrix world = new([-4096, 0, 0, 0, draw.YScale, 0, 0, 0, -4096], unchecked((short)draw.Task.ReadWord(28)), unchecked((short)draw.Task.ReadWord(30)), unchecked((short)draw.Task.ReadWord(32)), 4096);
                    NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.Compose56C2F0(view, world);
                    return AppendGeometry(current, NativeBattleBlizzaraDrawing.EmitMainMesh(NativeBattleBlizzaraFaces.ReadPrimaryMesh(current.Memory, draw.Sequence), NativeBattleBlizzaraFaces.ExpandedVertices(draw.Task), matrix, projection, draw.Flags, NativeBattlePilotVisualLifetimes.Identity(103, draw.Task)));
                case NativeBattleBlizzaraDraw draw:
                    if (draw.Task.Callback is 0x6DBB20 or 0x6DBD20)
                    {
                        NativeBattleGfGeometryBatch? mesh = NativeBattleBlizzaraDrawing.Mesh(draw.Task, resources, projection, view, colors);
                        return mesh is null ? current : AppendGeometry(current, mesh);
                    }
                    NativeBattleModulePacketBatch sprites = NativeBattleBlizzaraDrawing.Sprite(draw.Task, resources, projection, view, NativeCamera!.Roll);
                    NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Blizzara requires its initialized ordering table.");
                    NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, current.Effect.DrawArenaOffset!.Value, primitive.Pending, sprites, NativeBattleEffectGouraudWrites.FullDword);
                    graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                    return current with { Memory = written.Memory, Effect = current.Effect with { DrawArenaOffset = written.Cursor } };
                default:
                    throw new NotSupportedException("Native Blizzara operation requires its original callback.");
            }
        }

        NativeBattleBlizzaraFrame AppendGeometry(NativeBattleBlizzaraFrame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Blizzara requires its initialized ordering table.");
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, current.Effect.DrawArenaOffset!.Value, primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, Effect = current.Effect with { DrawArenaOffset = written.Cursor } };
        }
    }
}
