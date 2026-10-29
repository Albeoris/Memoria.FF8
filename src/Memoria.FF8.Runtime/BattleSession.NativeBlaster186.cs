using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeBlaster186(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Blaster requires its selected original effect.");
        NativeBattleBlaster186Resources resources = effect.Binding.Blaster186Resources ?? throw new NotSupportedException("Blaster requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Blaster requires its actual process memory.");
        NativeBattleBlaster186Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Blaster requires its actual CRT stream."), memory.Blaster186 ?? throw new NotSupportedException("Blaster requires its retained executable storage.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Blaster requires its physical temporary memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Blaster requires its interpolation constants.");
        NativeBattleBlaster186Inputs inputs = new(nativeActionPackets, colors, graphics.Flags, _ => Camera().CapturePackedView1D97778(), _ => Camera().Projection ?? throw new NotSupportedException("Blaster requires its current projection."), (_, actor) => graphics.Models[actor], Bone, Anchor, Execute);
        frame = initialize ? NativeBattleBlaster186Controller.Initialize(frame, resources, inputs) : NativeBattleBlaster186Controller.Advance(frame, resources, inputs);
        Publish(frame);
        if (initialize)
        {
            NativeCamera = Camera().RegisterControlResource(frame.Memory.Textures!.Animation);
        }
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Blaster requires its actual camera.");

        void Publish(NativeBattleBlaster186Frame current)
        {
            NativeEffectMemory = current.Memory with { Blaster186 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Blaster lost its actual temporary memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleBlaster186Frame Import(NativeBattleBlaster186Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Blaster external service lost its process memory.");
            return current with { Memory = latest, State = latest.Blaster186 ?? throw new InvalidDataException("Blaster external service lost its executable storage."), Staging = NativeFileStaging ?? throw new InvalidDataException("Blaster external service lost its temporary memory."), Random = nativeCrtRandom ?? throw new InvalidDataException("Blaster external service lost its CRT stream.") };
        }

        NativeBattleBlaster186PointStep Bone(NativeBattleBlaster186Frame current, int actor, int selector, int distance)
        {
            NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Blaster requires its current actor skeleton.");
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[actor], source, checked((byte)selector), checked((short)distance));
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Blaster bone query requires its actual temporary memory.");
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)observed.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[1]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[2]!.Value)) : producer.DisabledPosition;
            return new(current with { Memory = observed, Staging = NativeFileStaging }, Point(point));
        }

        NativeBattleBlaster186PointStep Anchor(NativeBattleBlaster186Frame current, int actor)
        {
            return new(current, Point(ReadNativeDefaultPosition571400(graphics, actor)));
        }

        static NativeBattleEffectArena Point(NativeBattleVector point) => NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z));

        NativeBattleBlaster186Frame Deliver(NativeBattleBlaster186Frame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Blaster requires its pending ordering table.");
            uint cursor = current.State.ReadDword(NativeBattleBlaster186Profile.CursorGlobal);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, checked((int)(cursor - NativeBattleEffectNodeMemory.ArenaAddress)), primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, State = current.State.WriteDword(NativeBattleBlaster186Profile.CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)) };
        }

        NativeBattleBlaster186Frame Execute(NativeBattleBlaster186Frame current, NativeBattleBlaster186Operation operation)
        {
            switch (operation)
            {
                case NativeBattleBlaster186Sound sound:
                    Publish(current);
                    PublishNativeModuleSound(new(sound.Packed, sound.Flags, null) { Pan = sound.Pan });
                    return Import(current);
                case NativeBattleBlaster186ContactsAll contacts:
                    Publish(current);
                    graphics = ApplyNativeHeaderContacts(graphics, contacts.HeaderIndex);
                    return Import(current);
                case NativeBattleBlaster186SetActorZWord position:
                    NativeBattleGraphicsModel model = graphics.Models[position.Actor];
                    NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Blaster requires its actual actor transform.");
                    graphics = graphics with { Models = graphics.Models.SetItem(position.Actor, model with { Transform = transform with { Position28 = transform.Position28 with { Z = position.Value } } }) };
                    return current;
                case NativeBattleBlaster186Stage5713E0 stage:
                    NativeBattleStageState retained = NativeStage ?? throw new NotSupportedException("Blaster requires its retained stage groups.");
                    NativeStage = retained with { Groups = retained.Groups.Select(group => group with { ColorDepth = unchecked((short)stage.Value), ColorFlags = stage.Flags }).ToImmutableArray() };
                    return current;
                case NativeBattleBlaster186DrawCommonRectangle571250 rectangle:
                    Publish(current);
                    NativeBattleModulePacketBatch rectangleBatch = NativeBattleFlashPackets.Emit571250(rectangle.Red, rectangle.Green, rectangle.Blue);
                    if (rectangle.PilotIdentity is { } identity)
                    {
                        rectangleBatch = rectangleBatch with { Primitives = rectangleBatch.Primitives.Select(allocation => allocation with { Primitive = allocation.Primitive with { PilotGeometry = allocation.Primitive.PilotGeometry is { } geometry ? geometry with { Identity = identity } : null } }).ToImmutableArray() };
                    }
                    graphics = AppendNativeModulePacketBatch(graphics, rectangleBatch, null, null);
                    return Import(current);
                case NativeBattleBlaster186ReleaseCommon release:
                    (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(release.SourceAddress, checked(release.TaskAddress + 14), (address, value) => current = current with { Effect = NativeBattleBlaster186Controller.WriteCompletionByte(current.Effect, address, value) });
                    graphics = graphics with { Auxiliary = auxiliary };
                    return current;
                case NativeBattleBlaster186DrawSprite180 sprite:
                    return DrawSprite(current, sprite.TemporaryAddress, sprite.SourceAddress, sprite.TableBaseBucket, unchecked((byte)(sprite.DepthShift & 31)), pilotIdentity: sprite.PilotIdentity);
                case NativeBattleBlaster186DrawStatic88 mesh:
                    return DrawMesh(current, mesh.TemporaryAddress, mesh.SourceAddress, mesh.TableBaseBucket, unchecked((byte)(mesh.DepthShift & 31)), pilotIdentity: mesh.PilotIdentity);
                case NativeBattleBlaster186DrawCommonSprite180 sprite:
                    return DrawSprite(current, sprite.TemporaryAddress, sprite.SourceAddress, sprite.TableBaseBucket, unchecked((byte)(sprite.DepthShift & 31)), true, pilotIdentity: sprite.PilotIdentity);
                default:
                    throw new NotSupportedException("Blaster emitted an unsupported native operation.");
            }
        }

        NativeBattleBlaster186Frame DrawSprite(NativeBattleBlaster186Frame current, uint address, uint sourceAddress, int bucket, byte shift, bool common = false, NativeBattlePilotVisualIdentity? pilotIdentity = null)
        {
            NativeBattleModuleResource resource = resources.ReadSprite(sourceAddress);
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Blaster sprite requires its actual temporary memory.");
            NativeBattleEffectArena temporary = Temporary(staging, address, 180);
            if (BinaryPrimitives.ReadUInt32LittleEndian(temporary.Read(0, 4)) != sourceAddress)
            {
                throw new InvalidDataException("Blaster sprite arguments differ from its physical temporary.");
            }
            NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(current.Memory, temporary, selected => selected == resource.Address ? resource : throw new InvalidDataException("Blaster sprite dispatcher changed its original source."), Camera().Projection ?? throw new NotSupportedException("Blaster sprite requires its projection."), Camera().Roll, bucket, shift, pilotIdentity: pilotIdentity);
            current = current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, address, drawn.Temporary180, 180) };
            if (!common)
            {
                return Deliver(current, drawn.Batch);
            }
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Blaster requires its pending common ordering table.");
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Blaster requires its common particle packet owner."), primitive.Pending, drawn.Batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = current.Memory with { PrimitiveProcess = written.Process } };
        }

        NativeBattleBlaster186Frame DrawMesh(NativeBattleBlaster186Frame current, uint address, uint sourceAddress, int bucket, byte shift, NativeBattlePilotVisualIdentity? pilotIdentity = null)
        {
            NativeBattleStaticMeshResource mesh = resources.ReadMesh(sourceAddress);
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Blaster mesh requires its actual temporary memory.");
            NativeBattleEffectArena temporary = Temporary(staging, address, 88);
            uint flags = BinaryPrimitives.ReadUInt32LittleEndian(temporary.Read(0x1C, 4));
            uint? selected = (flags & 0x2000) != 0 ? BinaryPrimitives.ReadUInt32LittleEndian(temporary.Read(4, 4)) : null;
            NativeBattleStaticMeshContext88Step drawn = NativeBattleStaticMeshContext88.DrawLoaded(current.Memory, mesh, Camera().Projection ?? throw new NotSupportedException("Blaster requires its current projection."), temporary, colors, bucket, shift, selected, selected is uint vertexAddress ? offset => SelectedVertex(current, vertexAddress, offset) : null, pilotIdentity: pilotIdentity);
            current = current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, address, drawn.Context, 88) };
            return Deliver(current, drawn.Batch);
        }

        (NativeBattleVector Vertex, short? W) SelectedVertex(NativeBattleBlaster186Frame current, uint vertexAddress, int offset)
        {
            if (offset < 0 || (offset & 7) != 0 || offset > NativeBattleBlaster186Profile.VertexFrameBytes - 8)
            {
                throw new InvalidDataException("Blaster selected vertex leaves its packed frame.");
            }
            NativeBattleEffectArena vertices = NativeBattleBlaster186Profile.SourceAddresses.Contains(vertexAddress) ? resources.ReadSourceVertices(vertexAddress) : NativeBattleBlaster186Profile.MutableFrameAddresses.Contains(vertexAddress) ? current.State.ReadView(vertexAddress, NativeBattleBlaster186Profile.VertexFrameBytes) : throw new NotSupportedException("Blaster selected an unsupported vertex owner.");
            byte[] xyz = vertices.Read(offset, 6);
            (byte[] bytes, bool[] known) = vertices.ReadSnapshot(offset + 6, 2);
            return (new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4))), known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null);
        }

        static NativeBattleEffectArena Temporary(NativeBattleFileStaging staging, uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, address, length), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    }
}
