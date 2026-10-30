using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeMorph172(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Morph172 requires its selected original effect.");
        NativeBattleMorph172Resources resources = effect.Binding.Morph172Resources ?? throw new NotSupportedException("Morph172 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Morph172 requires its actual process memory.");
        NativeBattleMorph172Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Morph172 requires its actual CRT stream."), memory.Morph172 ?? throw new NotSupportedException("Morph172 requires its retained executable storage.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Morph172 requires its physical temporary memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Morph172 requires its interpolation constants.");
        NativeBattleMorph172Inputs inputs = new(nativeActionPackets, colors, graphics.Flags, _ => Camera().CapturePackedView1D97778(), _ => Camera().Projection ?? throw new NotSupportedException("Morph172 requires its current projection."), (_, actor) => graphics.Models[actor], Anchor, (_, actor) => (NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Morph172 requires its actual current skeleton byte owner.")).BoneCount, Bone, Execute) { CapturePresentation = true };
        frame = initialize ? NativeBattleMorph172Controller.Initialize(frame, resources, inputs) : NativeBattleMorph172Controller.Advance(frame, resources, inputs);
        Publish(frame);
        if (initialize)
        {
            NativeCamera = Camera().RegisterControlResource(frame.Memory.Textures!.Animation);
        }
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Morph172 requires its actual camera.");

        void Publish(NativeBattleMorph172Frame current)
        {
            NativeEffectMemory = current.Memory with { Morph172 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Morph172 lost its actual temporary memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleMorph172Frame Import(NativeBattleMorph172Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Morph172 external service lost its process memory.");
            return current with { Memory = latest, State = latest.Morph172 ?? throw new InvalidDataException("Morph172 external service lost its executable storage."), Staging = NativeFileStaging ?? throw new InvalidDataException("Morph172 external service lost its temporary memory."), Random = nativeCrtRandom ?? throw new InvalidDataException("Morph172 external service lost its CRT stream.") };
        }

        NativeBattleMorph172PointStep Bone(NativeBattleMorph172Frame current, int actor, int selector, int distance)
        {
            NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Morph172 requires its current actor skeleton.");
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[actor], source, checked((byte)selector), checked((short)distance));
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Morph172 bone query requires its addressed temporary memory.");
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)observed.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[1]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[2]!.Value)) : producer.DisabledPosition;
            return new(current with { Memory = observed, Staging = NativeFileStaging }, Point(point));
        }

        NativeBattleMorph172PointStep Anchor(NativeBattleMorph172Frame current, int actor)
        {
            NativeBattleGraphicsModel model = graphics.Models[actor];
            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Morph172 anchor requires its actual model transform.");
            if ((model.Flags0 & 2) == 0)
            {
                return new(current, Point(transform.Position28).Write(6, BitConverter.GetBytes(transform.Angle36)));
            }
            NativeBattleMorph172PointStep first = Bone(current, actor, 0xF0, 0);
            NativeBattleMorph172PointStep second = Bone(first.Frame, actor, 0xF1, 0);
            short Average(int offset) => unchecked((short)((BinaryPrimitives.ReadInt16LittleEndian(first.Point8.Read(offset, 2)) + BinaryPrimitives.ReadInt16LittleEndian(second.Point8.Read(offset, 2))) / 2));
            return new(second.Frame, Point(new(Average(0), Average(2), Average(4))).Write(6, BitConverter.GetBytes(transform.Angle36)));
        }

        static NativeBattleEffectArena Point(NativeBattleVector point) => NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z));

        NativeBattleMorph172Frame Deliver(NativeBattleMorph172Frame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Morph172 requires its pending ordering table.");
            NativeBattleMorph172PacketWrite written = NativeBattleMorph172PacketArena.Append(current.State, primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { State = written.State };
        }

        NativeBattleMorph172Frame Execute(NativeBattleMorph172Frame current, NativeBattleMorph172Operation operation)
        {
            switch (operation)
            {
                case NativeBattleMorph172Sound5013A0 sound:
                    Publish(current);
                    PublishNativeModuleSound(sound.Packed, sound.Flags, new(BinaryPrimitives.ReadInt16LittleEndian(sound.Point8.Read(0, 2)), BinaryPrimitives.ReadInt16LittleEndian(sound.Point8.Read(2, 2)), BinaryPrimitives.ReadInt16LittleEndian(sound.Point8.Read(4, 2)), 0));
                    return Import(current);
                case NativeBattleMorph172ActorFlags flags:
                    graphics = graphics with { Models = graphics.Models.SetItem(flags.Actor, graphics.Models[flags.Actor] with { Flags0 = unchecked((ushort)((graphics.Models[flags.Actor].Flags0 & ~flags.ClearMask) | flags.SetMask)) }) };
                    return current;
                case NativeBattleMorph172DrawBody220 body:
                    Publish(current);
                    var drawnBody = DrawNativeMorph172Body(current, graphics, checked((byte)body.Actor), body.TemporaryAddress, body.PilotIdentity);
                    graphics = drawnBody.Graphics;
                    return drawnBody.Frame;
                case NativeBattleMorph172DrawSprite180 sprite:
                    return DrawSprite(current, sprite.TemporaryAddress, sprite.SourceAddress, sprite.TableBaseBucket, unchecked((byte)(sprite.DepthShift & 31)), sprite.PilotIdentity);
                case NativeBattleMorph172DrawStatic88 mesh:
                    return DrawMesh(current, mesh.TemporaryAddress, mesh.SourceAddress, mesh.TableBaseBucket, unchecked((byte)(mesh.DepthShift & 31)), mesh.PilotIdentity);
                default:
                    throw new NotSupportedException("Morph172 emitted an unsupported native operation.");
            }
        }

        NativeBattleMorph172Frame DrawSprite(NativeBattleMorph172Frame current, uint address, uint sourceAddress, int bucket, byte shift, NativeBattlePilotVisualIdentity? pilotIdentity = null)
        {
            NativeBattleModuleResource resource = resources.ReadSprite(sourceAddress);
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Morph172 sprite requires its actual temporary memory.");
            NativeBattleEffectArena temporary = Temporary(staging, address, 180);
            if (BinaryPrimitives.ReadUInt32LittleEndian(temporary.Read(0, 4)) != sourceAddress)
            {
                throw new InvalidDataException("Morph172 sprite arguments differ from its physical temporary.");
            }
            NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(current.Memory, temporary, selected => selected == resource.Address ? resource : throw new InvalidDataException("Morph172 sprite dispatcher changed its original source."), Camera().Projection ?? throw new NotSupportedException("Morph172 sprite requires its projection."), Camera().Roll, bucket, shift, allowCallerScale: true, pilotIdentity: pilotIdentity);
            current = current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, address, drawn.Temporary180, 180) };
            return Deliver(current, pilotIdentity.HasValue ? NativeBattleMorph172Presentation.PreserveNativeDepths(drawn.Batch) : drawn.Batch);
        }

        NativeBattleMorph172Frame DrawMesh(NativeBattleMorph172Frame current, uint address, uint sourceAddress, int bucket, byte shift, NativeBattlePilotVisualIdentity? pilotIdentity = null)
        {
            NativeBattleStaticMeshResource mesh = resources.ReadMesh(sourceAddress);
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Morph172 mesh requires its actual temporary memory.");
            NativeBattleEffectArena temporary = Temporary(staging, address, 88);
            uint flags = BinaryPrimitives.ReadUInt32LittleEndian(temporary.Read(0x1C, 4));
            uint? selected = (flags & 0x2000) != 0 ? BinaryPrimitives.ReadUInt32LittleEndian(temporary.Read(4, 4)) : null;
            NativeBattleStaticMeshContext88Step drawn = NativeBattleStaticMeshContext88.DrawLoaded(current.Memory, mesh, Camera().Projection ?? throw new NotSupportedException("Morph172 requires its current projection."), temporary, colors, bucket, shift, selected, selected is uint vertexAddress ? offset => SelectedVertex(current, vertexAddress, offset) : null, pilotIdentity: pilotIdentity);
            current = current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, address, drawn.Context, 88) };
            return Deliver(current, pilotIdentity.HasValue ? NativeBattleMorph172Presentation.PreserveNativeDepths(drawn.Batch) : drawn.Batch);
        }

        (NativeBattleVector Vertex, short? W) SelectedVertex(NativeBattleMorph172Frame current, uint address, int offset)
        {
            if (offset < 0 || (offset & 7) != 0 || offset > NativeBattleMorph172Profile.MutableVerticesLength - 8 || address is not (NativeBattleMorph172Profile.MutableVerticesA or NativeBattleMorph172Profile.MutableVerticesB))
            {
                throw new InvalidDataException("Morph172 selected vertex leaves its actual mutable bank.");
            }
            byte[] xyz = current.State.Read(address + (uint)offset, 6);
            (byte[] bytes, bool[] known) = current.State.ReadView(address, NativeBattleMorph172Profile.MutableVerticesLength).ReadSnapshot(offset + 6, 2);
            return (new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4))), known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null);
        }

        static NativeBattleEffectArena Temporary(NativeBattleFileStaging staging, uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, address, length), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    }
}
