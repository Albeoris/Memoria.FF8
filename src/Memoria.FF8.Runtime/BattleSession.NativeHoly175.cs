using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeHoly175(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new InvalidDataException("Native Holy requires its selected original effect.");
        NativeBattleHoly175Resources resources = effect.Binding.Holy175Resources ?? throw new NotSupportedException("Native Holy requires its original resources.");
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native Holy requires its authenticated color arithmetic.");
        NativeBattleHoly175Frame frame = new(NativeEffectMemory ?? throw new NotSupportedException("Native Holy requires its retained process memory."), effect, nativeCrtRandom ?? throw new NotSupportedException("Native Holy requires its retained CRT state."));
        NativeBattleHoly175Inputs inputs = new(graphics.Flags, nativeActionPackets, Position, actor => graphics.Models[actor].Transform?.Scale38 ?? throw new NotSupportedException("Native Holy requires its actual actor scale."), actor => graphics.Models[actor].Flags0, AdvanceColors, AdvanceWarp, DrawSprite31, Execute);
        frame = initialize ? NativeBattleHoly175Controller.Initialize5D1AD0(frame, inputs, resources) : NativeBattleHoly175Controller.Advance5D44C0(frame, inputs, resources);
        NativeEffectMemory = frame.Memory;
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraProjection Projection() => NativeCamera?.Projection ?? throw new NotSupportedException("Native Holy requires its published projection.");
        NativeBattleActionMatrix View() => Projection().View ?? throw new NotSupportedException("Native Holy requires its current view matrix.");
        ImmutableArray<byte> Position(int actor)
        {
            NativeBattleModuleAnchor anchor = ReadNativeModelAnchor(graphics, actor);
            byte[] bytes = new byte[8];
            BinaryPrimitives.WriteInt16LittleEndian(bytes, anchor.X);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2), anchor.Y);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), anchor.Z);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(6), anchor.W);
            return bytes.ToImmutableArray();
        }

        NativeBattleHoly175TaskStep AdvanceColors(NativeBattleHoly175Frame current, NativeBattleModulePoolTask task)
        {
            NativeBattleStageColors globals = NativeStage?.ModelColors ?? throw new NotSupportedException("Native Holy requires its actual stage model-color globals.");
            NativeBattleHoly175ModelColorStep step = NativeBattleHoly175ModelColors.Advance(task, graphics.Models, graphics.Flags, globals.Color44, globals.Color40, colors);
            graphics = graphics with { Models = step.Models };
            return new(current, step.Task, step.Result);
        }

        NativeBattleHoly175Frame AdvanceWarp(NativeBattleHoly175Frame current, int animationFrame, uint color)
        {
            NativeBattleHoly175State state = current.Memory.Holy175!;
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Holy warp requires its current ordering table.");
            NativeBattleHoly175WarpFrame source = new(state.Warp, current.Memory.Arena ?? throw new NotSupportedException("Native Holy warp requires its private packet arena."), current.Effect.DrawArenaOffset ?? throw new NotSupportedException("Native Holy warp requires its current private cursor."), primitive.Pending);
            NativeBattleHoly175WarpDraw step = NativeBattleHoly175WarpDrawing.Advance5D2BC0(source, animationFrame, current.Memory.PrimitiveProcess?.Buffer80 ?? throw new NotSupportedException("Native Holy warp requires its actual buffer selector."), color, graphics.Flags, unchecked((short)(Projection().FocalLength ?? throw new NotSupportedException("Native Holy warp requires its retained H WORD."))), state.WarpTextured ?? throw new NotSupportedException("Native Holy warp requires its actual textured-draw gate."), state.WarpAlive ?? throw new NotSupportedException("Native Holy warp requires its actual alive gate."), Projection());
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = step.Frame.Ordering } };
            return current with { Memory = current.Memory with { Arena = step.Frame.PrivateArena, Holy175 = state with { Warp = step.Frame.State, WarpAlive = step.WarpAlive } }, Effect = current.Effect with { DrawArenaOffset = step.Frame.PrivateCursor } };
        }

        NativeBattleHoly175Frame DrawTask(NativeBattleHoly175Frame current, NativeBattleModulePoolTask task)
        {
            NativeBattleVector eye = default;
            if (task.Callback == 0x5D3FB0)
            {
                NativeBattleCameraPoint currentEye = NativeCamera?.CurrentPose?.Eye ?? throw new NotSupportedException("Native Holy rays require the actual retained camera eye.");
                eye = new(currentEye.X, currentEye.Y, currentEye.Z);
            }
            NativeBattleHoly175DrawingResult drawn = NativeBattleHoly175Drawing.DrawTask(task, current.Memory.Holy175!, resources, View(), Projection(), NativeCamera!.Roll, eye, colors);
            return AppendDrawing(current, drawn);
        }

        NativeBattleHoly175Sprite31DrawStep DrawSprite31(NativeBattleHoly175Frame current, NativeBattleModulePoolTask task, NativeBattleHoly175Sprite31Row row)
        {
            NativeBattleHoly175DrawingResult drawn = NativeBattleHoly175Drawing.DrawSprite31(task, row, current.Memory.Holy175!, resources, View(), Projection(), NativeCamera!.Roll);
            return new(AppendDrawing(current, drawn), drawn.NextOffset ?? throw new NotSupportedException("Native Holy sprite row requires its actual frame continuation offset."));
        }

        NativeBattleHoly175Frame DrawSprite4(NativeBattleHoly175Frame current, NativeBattleHoly175Sprite4Draw row)
        {
            NativeBattleHoly175DrawingResult drawn = NativeBattleHoly175Drawing.DrawSprite4(row, current.Memory.Holy175!, resources, Projection(), NativeCamera!.Roll);
            return AppendDrawing(current, drawn);
        }

        NativeBattleHoly175Frame AppendDrawing(NativeBattleHoly175Frame current, NativeBattleHoly175DrawingResult drawn)
        {
            if (drawn.Geometry is not null && drawn.Sprites is not null)
            {
                throw new InvalidDataException("Native Holy drawing requires one physical batch representation.");
            }
            current = current with { Memory = current.Memory with { Holy175 = drawn.State } };
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Holy drawing requires its initialized ordering table.");
            NativeBattleGfGeometryBatch? geometry = drawn.Geometry ?? drawn.Sprites?.CapturedWrites;
            if (geometry is not null)
            {
                if (drawn.Common)
                {
                    NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Holy drawing requires its common packet cursor."), primitive.Pending, geometry);
                    graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                    return current with { Memory = current.Memory with { PrimitiveProcess = written.Process } };
                }
                NativeBattleEffectPacketWrite packets = NativeBattleEffectPacketArena.Append(current.Memory, current.Effect.DrawArenaOffset ?? throw new NotSupportedException("Native Holy drawing requires its private packet cursor."), primitive.Pending, geometry);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = packets.Ordering } };
                return current with { Memory = packets.Memory, Effect = current.Effect with { DrawArenaOffset = packets.Cursor } };
            }
            if (drawn.Sprites is { } sprites)
            {
                if (drawn.Common)
                {
                    NativeEffectMemory = current.Memory;
                    graphics = AppendNativeModulePacketBatch(graphics, sprites, null, null);
                    return current with { Memory = NativeEffectMemory! };
                }
                NativeBattleEffectPacketWrite packets = NativeBattleEffectPacketArena.Append(current.Memory, current.Effect.DrawArenaOffset ?? throw new NotSupportedException("Native Holy sprites require their private packet cursor."), primitive.Pending, sprites, NativeBattleEffectGouraudWrites.FullDword);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = packets.Ordering } };
                return current with { Memory = packets.Memory, Effect = current.Effect with { DrawArenaOffset = packets.Cursor } };
            }
            return current;
        }

        NativeBattleHoly175Frame Execute(NativeBattleHoly175Frame current, NativeBattleHoly175Operation operation)
        {
            NativeEffectMemory = current.Memory;
            switch (operation)
            {
                case NativeBattleHoly175InitializeTextures:
                    RegisterNativeOriginalEffectAnimation(current.Effect, resources.Animation, 0x2392CE0);
                    return current with { Memory = NativeEffectMemory! };
                case NativeBattleHoly175CameraControl50A730:
                    NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native Holy entry requires its actual camera process.")).Apply50A730();
                    return current;
                case NativeBattleHoly175Capture:
                {
                    NativeBattleHoly175State state = current.Memory.Holy175!;
                    NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Holy capture requires its initialized ordering table.");
                    NativeBattleHoly175CaptureMemory retained = new(state.BlackPackets.Read(0, 48).ToImmutableArray(), state.CaptureOffsetX, state.CaptureOffsetY);
                    NativeBattleHoly175CaptureWrite captured = NativeBattleHoly175CapturePackets.Write5D29E0(current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Holy capture requires its common packet owner."), primitive.Pending, NativeStage ?? throw new NotSupportedException("Native Holy capture requires its current stage."), definition.NativeStageSource?.Geometry ?? throw new NotSupportedException("Native Holy capture requires its original stage geometry."), View(), Projection(), colors, resources, retained);
                    NativeStage = captured.Stage;
                    graphics = graphics with { PrimitiveFrame = primitive with { Pending = captured.Ordering } };
                    return current with { Memory = current.Memory with { PrimitiveProcess = captured.Process, Holy175 = state with { BlackPackets = state.BlackPackets.Write(0, captured.Memory.BlackPackets.AsSpan()), CaptureOffsetX = captured.Memory.OffsetX, CaptureOffsetY = captured.Memory.OffsetY } } };
                }
                case NativeBattleHoly175DrawTask draw:
                    return DrawTask(current, draw.Task);
                case NativeBattleHoly175Sprite4Draw draw:
                    return DrawSprite4(current, draw);
                case NativeBattleHoly175StageFlags flags:
                {
                    NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native Holy requires its actual stage flag owners.");
                    NativeStage = stage with { Groups = stage.Groups.Select(group => group with { Flags = unchecked((byte)((group.Flags & ~flags.ClearMask) | flags.SetMask)) }).ToImmutableArray() };
                    return current;
                }
                case NativeBattleHoly175StageFlash flash:
                {
                    NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native Holy requires its stage color-depth owners.");
                    NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = unchecked((short)flash.Amount), ColorFlags = 0 }).ToImmutableArray() };
                    return current;
                }
                case NativeBattleHoly175UploadTim:
                    return current with { Memory = current.Memory with { Textures = (current.Memory.Textures ?? throw new NotSupportedException("Native Holy requires its actual TIM upload queue.")).EnqueueTim505E30(resources.Tim).State } };
                case NativeBattleHoly175Sound sound:
                {
                    NativeBattleModuleAnchor position = new(BinaryPrimitives.ReadInt16LittleEndian(sound.Position.AsSpan()), BinaryPrimitives.ReadInt16LittleEndian(sound.Position.AsSpan().Slice(2)), BinaryPrimitives.ReadInt16LittleEndian(sound.Position.AsSpan().Slice(4)), BinaryPrimitives.ReadInt16LittleEndian(sound.Position.AsSpan().Slice(6)));
                    PublishNativeModuleSound(new(resources.Sound, 1, position) { Pan = Projection().ReadPan(new(position.X, position.Y, position.Z)) });
                    return current;
                }
                case NativeBattleHoly175Contact contact:
                {
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.Contact)] });
                    graphics = graphics with { PendingContacts = pending };
                    return current with { Memory = NativeEffectMemory! };
                }
                case NativeBattleHoly175LoadCommon load:
                    (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(load.DestinationAddress, load.CompletionWordAddress, (address, value) => current = current with { Effect = NativeBattleHoly175Controller.WriteCompletionWord(current.Effect, address, value) });
                    graphics = graphics with { Auxiliary = auxiliary };
                    return current;
                default:
                    throw new NotSupportedException("Native Holy operation requires its original callback contract.");
            }
        }
    }

    private bool TryWriteNativeHoly175Completion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState updated)
    {
        updated = graphics;
        uint delta = unchecked(address - NativeBattleHoly175Controller.MainBank);
        if (delta >= 2 * 0x10 || delta % 0x10 != 0xE)
        {
            return false;
        }
        if (NativeEffectMemory?.DetachedReturnedTask is { Binding.Entry: 0x5D1AD0 } detached)
        {
            NativeEffectMemory = NativeEffectMemory with { DetachedReturnedTask = NativeBattleHoly175Controller.WriteCompletionWord(detached, address, value) };
        }
        else
        {
            NativeBattleOriginalEffectState current = graphics.OriginalEffect is { Binding.Entry: 0x5D1AD0 } selected ? selected : throw new NotSupportedException("Native Holy completion lost its physical final-task owner.");
            updated = graphics with { OriginalEffect = NativeBattleHoly175Controller.WriteCompletionWord(current, address, value) };
        }
        return true;
    }
}
