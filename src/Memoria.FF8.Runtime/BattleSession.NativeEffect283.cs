using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeEffect283(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Effect283 requires its selected original effect.");
        NativeBattleEffect283Resources resources = effect.Binding.Effect283Resources ?? throw new NotSupportedException("Effect283 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect283 requires its actual process memory.");
        NativeBattleEffect283Frame frame = new(memory, effect, memory.Effect283 ?? throw new NotSupportedException("Effect283 requires its retained executable storage.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Effect283 requires its current temporary memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Effect283 requires its original arithmetic constants.");
        NativeBattleEffect283Inputs? inputs = null;
        inputs = new(colors, _ => Camera().CapturePackedView1D97778(), _ => Projection(), (_, pointer) => graphics.Models[Actor(pointer)], _ => nativeActionPackets, Anchor, Execute, DrawBody, (current, source, node, caller) => NativeBattleEffect283Controller.DrawProgrammeNode67C8A0(current, source, node, caller, resources, inputs!));
        frame = initialize ? NativeBattleEffect283Controller.Initialize(frame, resources, inputs) : NativeBattleEffect283Controller.Advance(frame, resources, inputs);
        Publish(frame);
        if (initialize)
        {
            NativeCamera = Camera().RegisterControlResource(frame.Memory.Textures!.Animation);
        }
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Effect283 requires its current camera.");
        NativeBattleCameraProjection Projection() => Camera().Projection ?? throw new NotSupportedException("Effect283 requires its current projection.");
        int Actor(uint pointer)
        {
            uint delta = unchecked(pointer - 0x1D972C0);
            if (delta % 156 != 0 || delta / 156 >= (uint)graphics.Models.Length)
            {
                throw new InvalidDataException("Effect283 requires an actual physical graphics-model row.");
            }
            return checked((int)(delta / 156));
        }
        void Publish(NativeBattleEffect283Frame current)
        {
            NativeEffectMemory = current.Memory with { Effect283 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Effect283 lost its current temporary memory.");
        }
        NativeBattleEffect283PointStep Bone(NativeBattleEffect283Frame current, int actor, byte selector)
        {
            NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect283 requires its current actor skeleton.");
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[actor], source, selector, 0);
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Effect283 bone query requires its current temporary memory.");
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)observed.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[1]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[2]!.Value)) : producer.DisabledPosition;
            return new(current with { Memory = observed, Staging = NativeFileStaging }, NativeBattleEffect283Controller.PointView(point));
        }
        NativeBattleEffect283PointStep Anchor(NativeBattleEffect283Frame current, uint pointer)
        {
            int actor = Actor(pointer);
            NativeBattleGraphicsModel model = graphics.Models[actor];
            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Effect283 anchor requires its actual model transform.");
            if ((model.Flags0 & 2) == 0)
            {
                return new(current, NativeBattleEffect283Controller.PointView(transform.Position28, transform.Angle36));
            }
            NativeBattleEffect283PointStep first = Bone(current, actor, 0xF0);
            NativeBattleEffect283PointStep second = Bone(first.Frame, actor, 0xF1);
            short Average(int offset) => unchecked((short)((BinaryPrimitives.ReadInt16LittleEndian(first.Point8.Read(offset, 2)) + BinaryPrimitives.ReadInt16LittleEndian(second.Point8.Read(offset, 2))) / 2));
            return new(second.Frame, NativeBattleEffect283Controller.PointView(new(Average(0), Average(2), Average(4)), transform.Angle36));
        }
        NativeBattleEffect283BodyResult DrawBody(NativeBattleEffect283Frame current, uint model, short centerX, short centerY, short threshold, int bank, int fade)
        {
            (graphics, current, short maximum) = DrawNativeEffect283Body(graphics, current, model, centerX, centerY, threshold, bank, fade, resources);
            return new(current, maximum);
        }
        NativeBattleEffect283Frame Deliver(NativeBattleEffect283Frame current, NativeBattleGfGeometryBatch batch, uint cursorGlobal = NativeBattleEffect283Profile.CursorGlobal, ushort flags = 0)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect283 requires its current ordering table.");
            if (cursorGlobal == 0x1D8E054)
            {
                NativeBattlePrimitiveProcess process = current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Effect283 requires its current ordinary COMMON packet owner.");
                NativeBattleCommonPacketWrite ordinary = NativeBattleCommonPacketDelivery.Deliver(process, primitive.Pending, batch, flags);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = ordinary.Ordering } };
                return current with { Memory = current.Memory with { PrimitiveProcess = ordinary.Process } };
            }
            if (cursorGlobal != NativeBattleEffect283Profile.CursorGlobal)
            {
                throw new InvalidDataException("Effect283 selected an unsupported packet cursor.");
            }
            int cursor = NativeBattleWaterExecutionFields.ArenaOffset(current.State.ReadDword(cursorGlobal), 0);
            NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Effect283 requires its actual effect packet arena.");
            NativeBattleGfGeometryBatch resolved = NativeBattlePacketBatchWords.Resolve(batch, cursor, NativeBattleEffectArena.Length, offset => arena.Read(offset, 1)[0]);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, resolved, flags);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, State = current.State.WriteDword(cursorGlobal, checked(NativeBattleEffect283Profile.ArenaAddress + (uint)written.Cursor)) };
        }

        NativeBattleEffect283Frame DrawMesh(NativeBattleEffect283Frame current, NativeBattleEffect283DrawWindow108 operation)
        {
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Effect283 mesh requires its current temporary owner.");
            NativeBattleEffectArena context = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, operation.TemporaryAddress, 108), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 108), 0, 108);
            NativeBattleStaticMeshResource mesh = resources.ReadGeometry(operation.SourceAddress);
            if (BinaryPrimitives.ReadUInt32LittleEndian(context.Read(4, 4)) != operation.VertexAddress)
            {
                throw new InvalidDataException("Effect283 mesh selected pointer differs from its actual context.");
            }
            uint start = current.State.ReadDword(NativeBattleEffect283Profile.CursorGlobal);
            NativeBattleEffect283MeshContext108Step drawn = NativeBattleEffect283MeshContext108.DrawLoaded(current.Memory, mesh, Projection(), context, colors, operation.TableBaseBucket, checked((byte)operation.DepthShift), ReadVertex, DeliverAttempt);
            return current with { Memory = drawn.Memory with { Arena = current.Memory.Arena }, State = current.State.WriteDword(NativeBattleEffect283Profile.CursorGlobal, checked(start + (uint)drawn.Batch.AllocationBytes)), Staging = NativeBattleTemporaryMemory.PublishView(staging, operation.TemporaryAddress, drawn.Context108, 108) };

            void DeliverAttempt(NativeBattleGfGeometryAttempt attempt, int allocation)
            {
                int cursor = checked(NativeBattleWaterExecutionFields.ArenaOffset(start, 0) + attempt.ByteOffsetRelative);
                NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Effect283 requires its current overlapping packet and morph arena.");
                NativeBattleGfGeometryBatch batch = new([attempt with { ByteOffsetRelative = 0 }], allocation);
                NativeBattleGfGeometryBatch resolved = NativeBattlePacketBatchWords.Resolve(batch, cursor, NativeBattleEffectArena.Length, offset => arena.Read(offset, 1)[0]);
                NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect283 requires its current ordering table.");
                NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, resolved);
                current = current with { Memory = written.Memory };
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            }

            (NativeBattleVector Vertex, short? W) ReadVertex(int offset)
            {
                int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4));
                if (offset < 0 || (offset & 7) != 0 || offset > count * 8 - 8)
                {
                    throw new InvalidDataException("Effect283 selected vertex leaves its actual geometry.");
                }
                NativeBattleEffectArena point;
                if (operation.VertexAddress == NativeBattleEffect283Profile.ArenaAddress + NativeBattleEffect283Profile.ScratchOffset)
                {
                    point = NativeBattleEffectNodeContextMemory.Copy(current.Memory.Arena ?? throw new NotSupportedException("Effect283 morph requires its current arena."), NativeBattleEffect283Profile.ScratchOffset + offset, NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 0, 8);
                }
                else
                {
                    uint delta = unchecked(operation.VertexAddress - mesh.Address);
                    int directory = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan());
                    if (delta < 12 || (delta - 12) % checked((uint)(count * 8)) != 0 || delta + count * 8 > directory)
                    {
                        throw new InvalidDataException("Effect283 selected vertices require its current morph or an actual immutable shape.");
                    }
                    point = NativeBattleEffectArena.Clear().Write(0, mesh.Bytes.AsSpan().Slice(checked((int)delta + offset), 8));
                }
                byte[] xyz = point.Read(0, 6);
                (byte[] w, bool[] known) = point.ReadSnapshot(6, 2);
                return (new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4))), known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(w) : null);
            }
        }

        NativeBattleEffect283Frame Execute(NativeBattleEffect283Frame current, NativeBattleEffect283Operation operation)
        {
            switch (operation)
            {
                case NativeBattleEffect283ModelFlags fields:
                    int actor = Actor(fields.ModelAddress);
                    NativeBattleGraphicsModel model = graphics.Models[actor];
                    graphics = graphics with { Models = graphics.Models.SetItem(actor, model with { Flags0 = unchecked((ushort)((model.Flags0 & ~fields.ClearFlags0) | fields.SetFlags0)) }) };
                    return current;
                case NativeBattleEffect283ModelColor44 recolor:
                    int target = Actor(recolor.ModelAddress);
                    graphics = graphics with { Models = graphics.Models.SetItem(target, graphics.Models[target] with { Color44 = recolor.Value }) };
                    return current;
                case NativeBattleEffect283Stage5713E0 stage:
                    NativeBattleStageState retained = NativeStage ?? throw new NotSupportedException("Effect283 requires its retained stage groups.");
                    NativeStage = retained with { Groups = retained.Groups.Select(group => group with { ColorDepth = unchecked((short)stage.Value), ColorFlags = stage.Flags }).ToImmutableArray() };
                    return current;
                case NativeBattleEffect283Sound501330 sound:
                    Publish(current);
                    PublishNativeModuleSound(new(sound.Packed, sound.Flags, null) { Pan = sound.Pan });
                    return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Effect283 sound lost its process memory."), Staging = NativeFileStaging };
                case NativeBattleEffect283DrawWindow108 window:
                    return DrawMesh(current, window);
                case NativeBattleEffect283DeliverBatch packets:
                    return Deliver(current, packets.Batch, packets.CursorGlobal, packets.Flags);
                default:
                    throw new NotSupportedException("Effect283 emitted an unsupported native operation.");
            }
        }
    }
}
