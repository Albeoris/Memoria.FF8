using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeThundara6DC2E0(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleThundaraResources resources = effect.Binding.ThundaraResources ?? throw new NotSupportedException("Native6DC2E0 requires its original Thundara resources.");
        RegisterNativeOriginalEffectAnimation(effect, resources.Animation, 0x254405C);
        return graphics with { OriginalEffect = NativeBattleThundaraController.Initialize6DC2E0(effect, nativeActionPackets) };
    }

    private NativeBattleGraphicsState AdvanceNativeThundara6DC390(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleThundaraResources resources = effect.Binding.ThundaraResources ?? throw new NotSupportedException("Native Thundara requires its original executable resources.");
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Thundara requires its published projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Thundara requires its retained view matrix.");
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native Thundara requires its depth-cue tables.");
        NativeBattleThundaraFrame frame = new(NativeEffectMemory ?? throw new NotSupportedException("Native Thundara requires retained effect memory."), effect, nativeCrtRandom ?? throw new NotSupportedException("Native Thundara requires its retained CRT random state."));
        frame = NativeBattleThundaraController.Advance6DC390(frame, new(graphics.Flags, nativeActionPackets, model => ReadNativeGraphicsBone(graphics, model, 241, 0), model => graphics.Models[model].Transform ?? throw new NotSupportedException("Native Thundara requires its actual model transform."), Execute));
        NativeEffectMemory = frame.Memory;
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleThundaraFrame Execute(NativeBattleThundaraFrame current, NativeBattleThundaraOperation operation)
        {
            NativeEffectMemory = current.Memory;
            switch (operation)
            {
                case NativeBattleThundaraContact contact:
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.Contact)] });
                    graphics = graphics with { PendingContacts = pending };
                    return current with { Memory = NativeEffectMemory! };
                case NativeBattleThundaraSound sound:
                    PublishNativeModuleSound(resources.Sound, 0x101, sound.Position);
                    return current with { Memory = NativeEffectMemory! };
                case NativeBattleThundaraSprite sprite:
                    return Append(current, NativeBattleThundaraDrawing.Sprite(sprite, resources, projection, view, NativeCamera!.Roll));
                case NativeBattleThundaraFlash:
                    return Append(current, NativeBattleThundaraDrawing.Flash(resources, current.Effect.DrawArenaOffset!.Value));
                case NativeBattleThundaraGeometry geometry:
                    NativeBattleGfGeometryBatch mesh = NativeBattleThundaraDrawing.Geometry(geometry, resources, projection, view, colors);
                    NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Thundara requires its initialized ordering table.");
                    NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, current.Effect.DrawArenaOffset!.Value, primitive.Pending, mesh);
                    graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                    return current with { Memory = written.Memory, Effect = current.Effect with { DrawArenaOffset = written.Cursor } };
                default:
                    throw new NotSupportedException("Native Thundara operation requires its original callback.");
            }
        }

        NativeBattleThundaraFrame Append(NativeBattleThundaraFrame current, NativeBattleModulePacketBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Thundara requires its initialized ordering table.");
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, current.Effect.DrawArenaOffset!.Value, primitive.Pending, batch, NativeBattleEffectGouraudWrites.FullDword);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, Effect = current.Effect with { DrawArenaOffset = written.Cursor } };
        }
    }
}
