using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeFiraga61BFE0(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleFiragaResources resources = effect.Binding.FiragaResources ?? throw new NotSupportedException("Native61BFE0 requires its original Firaga resources.");
        RegisterNativeOriginalEffectAnimation(effect, resources.Animation, 0x24BFB18);
        return graphics with { OriginalEffect = NativeBattleFiragaController.Initialize61BFE0(effect, nativeActionPackets) };
    }

    private NativeBattleGraphicsState AdvanceNativeFiraga61CDA0(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleFiragaResources resources = effect.Binding.FiragaResources ?? throw new NotSupportedException("Native Firaga requires its original executable resources.");
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Firaga requires its published projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Firaga requires its retained view matrix.");
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native Firaga requires its depth-cue tables.");
        NativeBattleFiragaFrame frame = new(NativeEffectMemory ?? throw new NotSupportedException("Native Firaga requires retained effect memory."), effect, nativeCrtRandom ?? throw new NotSupportedException("Native Firaga requires its retained CRT random state."));
        frame = NativeBattleFiragaController.Advance61CDA0(frame, new(graphics.Flags, nativeActionPackets, ReadPosition, model => graphics.Models[model].Transform ?? throw new NotSupportedException("Native Firaga requires its actual model transform."), Execute));
        NativeEffectMemory = frame.Memory;
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleFiraPosition ReadPosition(int model)
        {
            NativeBattleModuleAnchor anchor = ReadNativeModelAnchor(graphics, model);
            NativeBattleModelTransform transform = graphics.Models[model].Transform ?? throw new NotSupportedException("Native571400 requires its actual graphics transform row.");
            return new(anchor.X, anchor.Y, anchor.Z, transform.Angle36);
        }

        NativeBattleFiragaFrame Execute(NativeBattleFiragaFrame current, NativeBattleFiragaOperation operation)
        {
            NativeEffectMemory = current.Memory;
            switch (operation)
            {
                case NativeBattleFiragaContact contact:
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.Contact)] });
                    graphics = graphics with { PendingContacts = pending };
                    return current with { Memory = NativeEffectMemory! };
                case NativeBattleFiragaSfx sound:
                    NativeBattleFiraPosition position = ReadPosition(sound.Owner);
                    PublishNativeModuleSound(resources.Sound, 0, new(position.X, position.Y, position.Z, position.Fourth));
                    return current with { Memory = NativeEffectMemory! };
                case NativeBattleFiragaStage stage:
                    NativeBattleStageState nativeStage = NativeStage ?? throw new NotSupportedException("Native5713E0 requires its retained stage rows.");
                    NativeStage = nativeStage with { Groups = nativeStage.Groups.Select(group => group with { ColorDepth = stage.Value, ColorFlags = stage.ColorFlags }).ToImmutableArray() };
                    return current;
                case NativeBattleFiragaFlash flash:
                    graphics = graphics with { ModuleEffects = graphics.ModuleEffects.AppendFlash(flash.A, flash.B, flash.C, flash.D) };
                    return current;
                case NativeBattleFiragaDraw draw:
                    NativeBattleFiragaDrawResult result = NativeBattleFiragaDrawing.Draw(draw.Task, current.Effect.Firaga ?? throw new NotSupportedException("Native Firaga requires its retained position/alive cells."), resources, projection, view, NativeCamera!.Roll, colors);
                    foreach (NativeBattleModulePacketBatch sprites in result.Sprites)
                    {
                        NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Firaga requires its initialized ordering table.");
                        NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, current.Effect.DrawArenaOffset!.Value, primitive.Pending, sprites, NativeBattleEffectGouraudWrites.FullDword);
                        graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                        current = current with { Memory = written.Memory, Effect = current.Effect with { DrawArenaOffset = written.Cursor } };
                    }
                    if (result.Mesh is { } mesh)
                    {
                        NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Firaga requires its initialized ordering table.");
                        NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, current.Effect.DrawArenaOffset!.Value, primitive.Pending, mesh);
                        graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                        current = current with { Memory = written.Memory, Effect = current.Effect with { DrawArenaOffset = written.Cursor } };
                    }
                    return current;
                default:
                    throw new NotSupportedException("Native Firaga operation requires its original callback.");
            }
        }
    }
}
