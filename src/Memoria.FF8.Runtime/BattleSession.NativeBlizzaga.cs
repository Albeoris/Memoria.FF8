using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeBlizzaga6D6320(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleBlizzagaResources resources = effect.Binding.BlizzagaResources ?? throw new NotSupportedException("Native Blizzaga requires its original resources.");
        RegisterNativeOriginalEffectAnimation(effect, resources.Animation, 0x2543DF8);
        return graphics with { OriginalEffect = NativeBattleBlizzagaController.Initialize6D6320(effect, nativeActionPackets) };
    }

    private NativeBattleGraphicsState AdvanceNativeBlizzaga6D6400(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleBlizzagaResources resources = effect.Binding.BlizzagaResources ?? throw new NotSupportedException("Native Blizzaga requires its original resources.");
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Blizzaga requires its published projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Blizzaga requires its retained camera matrix.");
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native Blizzaga requires its original depth-cue constants.");
        NativeBattleBlizzagaFrame frame = new(NativeEffectMemory ?? throw new NotSupportedException("Native Blizzaga requires retained effect memory."), effect, nativeCrtRandom ?? throw new NotSupportedException("Native Blizzaga requires its retained CRT random state."));
        frame = NativeBattleBlizzagaController.Advance6D6400(frame, new(graphics.Flags, nativeActionPackets, effect.Parameters!.Actor78, view, model => ReadNativeGraphicsBone(graphics, model, 241, 0), model => graphics.Models[model].Transform ?? throw new NotSupportedException("Native Blizzaga requires its actual model transform."), model => ReadNativeModelAnchor(graphics, model), ModelBand, ProcessRibbon, Execute));
        NativeEffectMemory = frame.Memory;
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleBlizzagaModelBandStep ModelBand(NativeBattleBlizzagaFrame current, NativeBattleBlizzagaModelBandRequest request)
        {
            NativeEffectMemory = current.Memory;
            graphics = SetNativeBlizzagaModelFlags(graphics, request.Actor, request.Enabled, request.BandEnabled);
            if (!request.Enabled)
            {
                return new(current, request.LowY, request.HighY);
            }
            (graphics, short minimum, short maximum) = DrawNativeBlizzagaModelBand(graphics, request.Actor, request.LowY, request.HighY);
            return new(current with { Memory = NativeEffectMemory! }, minimum, maximum);
        }

        NativeBattleBlizzagaRibbonStep ProcessRibbon(NativeBattleBlizzagaFrame current, NativeBattleModulePoolTask task, bool second, sbyte delta, NativeBattleActionMatrix matrix)
        {
            int countOffset = second ? 24 : 32;
            int nodesOffset = second ? 64 : 44;
            int count = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(task.ReadRawPayloadSpan().Slice(countOffset - 12)));
            ImmutableArray<byte> nodes = task.ReadRawPayloadSpan().Slice(nodesOffset - 12, 30 * 36).ToArray().ToImmutableArray();
            NativeBattleBlizzagaRibbonResult ribbon = NativeBattleBlizzagaRibbon.Emit6D9A40(count, nodes, delta, matrix, projection);
            task.WriteDword(countOffset, (uint)ribbon.Count);
            task.WriteRetainedBytes(nodesOffset, ribbon.Nodes.AsSpan());
            return new(AppendGeometry(current, ribbon.Packets), task);
        }

        NativeBattleBlizzagaFrame Execute(NativeBattleBlizzagaFrame current, NativeBattleBlizzagaOperation operation)
        {
            NativeEffectMemory = current.Memory;
            switch (operation)
            {
                case NativeBattleBlizzagaContact contact:
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.Contact)] });
                    graphics = graphics with { PendingContacts = pending };
                    return current with { Memory = NativeEffectMemory! };
                case NativeBattleBlizzagaSound sound:
                    PublishNativeModuleSound(resources.Sound, 0x101, sound.Position);
                    return current with { Memory = NativeEffectMemory! };
                case NativeBattleBlizzagaStage stage:
                    NativeBattleStageState nativeStage = NativeStage ?? throw new NotSupportedException("Native Blizzaga requires its actual retained stage rows.");
                    NativeStage = nativeStage with { Groups = nativeStage.Groups.Select(group => group with { ColorDepth = stage.Value, ColorFlags = 0 }).ToImmutableArray() };
                    return current;
                case NativeBattleBlizzagaMainDraw draw:
                    NativeBattleActionMatrix local = NativeBattleBlizzagaController.ScaledYaw(unchecked((short)draw.Task.ReadWord(16)), 4096, 4096) with { X = unchecked((short)draw.Task.ReadWord(32)), Y = unchecked((short)draw.Task.ReadWord(34)), Z = unchecked((short)draw.Task.ReadWord(36)) };
                    NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.Compose56C2F0(view, local);
                    return AppendGeometry(current, NativeBattleBlizzagaDrawing.EmitMainMesh(NativeBattleBlizzagaFaces.ReadPrimaryMesh(current.Memory, draw.Sequence), NativeBattleBlizzagaFaces.Companion(current.Memory), NativeBattleBlizzagaFaces.ExpandedVertices(draw.Task, draw.Phase), draw.Phase, matrix, projection));
                case NativeBattleBlizzagaAuxiliaryDraw draw:
                    return AppendGeometry(current, draw.First ? NativeBattleBlizzagaDrawing.AuxiliaryA(draw.Task, draw.Phase, resources, projection, view, colors) : NativeBattleBlizzagaDrawing.AuxiliaryB(draw.Task, draw.Phase, resources, projection, view, colors));
                case NativeBattleBlizzagaSpark spark:
                    return AppendSprites(current, NativeBattleBlizzagaDrawing.Sprite(spark.Task, resources, projection, view, NativeCamera!.Roll, spark.Matrix));
                case NativeBattleBlizzagaSprite sprite:
                    return AppendSprites(current, NativeBattleBlizzagaDrawing.Sprite(sprite.Task, resources, projection, view, NativeCamera!.Roll));
                default:
                    throw new NotSupportedException("Native Blizzaga operation requires its original callback.");
            }
        }

        NativeBattleBlizzagaFrame AppendGeometry(NativeBattleBlizzagaFrame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Blizzaga requires its initialized ordering table.");
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, current.Effect.DrawArenaOffset!.Value, primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, Effect = current.Effect with { DrawArenaOffset = written.Cursor } };
        }

        NativeBattleBlizzagaFrame AppendSprites(NativeBattleBlizzagaFrame current, NativeBattleModulePacketBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Blizzaga requires its initialized ordering table.");
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, current.Effect.DrawArenaOffset!.Value, primitive.Pending, batch, NativeBattleEffectGouraudWrites.FullDword);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, Effect = current.Effect with { DrawArenaOffset = written.Cursor } };
        }
    }
}
