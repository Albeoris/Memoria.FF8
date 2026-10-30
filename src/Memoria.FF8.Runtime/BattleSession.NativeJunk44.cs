using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeJunk44(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Junk44 requires its selected original effect.");
        NativeBattleJunk44Resources resources = effect.Binding.Junk44Resources ?? throw new NotSupportedException("Junk44 requires its original node resources and controls.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Junk44 requires its actual process memory.");
        NativeBattleJunk44Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Junk44 requires its actual CRT stream."), (memory.Junk44 ?? throw new NotSupportedException("Junk44 requires its retained executable storage."))) { Staging = NativeFileStaging ?? throw new NotSupportedException("Junk44 requires its actual temporary memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Junk44 requires its original arithmetic constants.");
        NativeBattleJunk44Inputs inputs = new(nativeActionPackets, colors, graphics.Flags, _ => Camera().CapturePackedView1D97778(), _ => Projection(), (_, pointer) => graphics.Models[Actor(pointer)], Anchor, Bone, _ => nativeActionPackets, ReloadComplete, Execute) { CapturePresentation = true };
        frame = initialize ? NativeBattleJunk44Controller.Initialize(frame, resources, inputs) : NativeBattleJunk44Controller.Advance(frame, resources, inputs);
        Publish(frame);
        if (initialize)
        {
            NativeCamera = Camera().RegisterControlResource(frame.Memory.Textures!.Animation);
        }
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Junk44 requires its current camera.");
        NativeBattleCameraProjection Projection() => Camera().Projection ?? throw new NotSupportedException("Junk44 requires its current projection.");
        NativeBattlePoseSource PoseSource(int actor) => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Junk44 requires its actual current actor skeleton.");

        void Publish(NativeBattleJunk44Frame current)
        {
            NativeEffectMemory = current.Memory with { Junk44 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Junk44 lost its addressed temporary memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleJunk44Frame Import(NativeBattleJunk44Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Junk44 service lost its process memory.");
            return current with { Memory = latest, State = latest.Junk44 ?? throw new InvalidDataException("Junk44 service lost its executable storage."), Staging = NativeFileStaging ?? throw new InvalidDataException("Junk44 service lost its temporary memory."), Random = nativeCrtRandom ?? throw new InvalidDataException("Junk44 service lost its CRT state.") };
        }

        NativeBattleJunk44Frame Observe(NativeBattleJunk44Frame current, NativeBattleBoneAnchorProducer producer)
        {
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Junk44 bone query requires its current temporary memory.");
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            return current with { Memory = observed, Staging = NativeFileStaging };
        }

        NativeBattleJunk44PointStep Bone(NativeBattleJunk44Frame current, uint pointer, int selector, int distance)
        {
            int actor = Actor(pointer);
            NativeBattleGraphicsModel model = graphics.Models[actor];
            if ((model.Flags0 & 2) == 0)
            {
                return new(current, Point((model.Transform ?? throw new NotSupportedException("Junk44 disabled bone query requires its actual transform.")).Position28));
            }
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, PoseSource(actor), checked((byte)selector), checked((short)distance));
            current = Observe(current, producer);
            NativeBattleScriptedColorRegisters registers = current.Memory.ScriptedColorRegisters ?? throw new InvalidDataException("Junk44 bone query lost its current arithmetic registers.");
            return new(current, Point(new(unchecked((short)registers.Mac[0]!.Value), unchecked((short)registers.Mac[1]!.Value), unchecked((short)registers.Mac[2]!.Value))));
        }

        int Actor(uint pointer)
        {
            uint delta = unchecked(pointer - 0x1D972C0);
            if (delta % 156 != 0 || delta / 156 >= (uint)graphics.Models.Length)
            {
                throw new InvalidDataException("Junk requires an actual physical graphics-model row.");
            }
            return checked((int)(delta / 156));
        }

        NativeBattleJunk44PointStep Anchor(NativeBattleJunk44Frame current, uint pointer)
        {
            NativeBattleGraphicsModel model = graphics.Models[Actor(pointer)];
            if ((model.Flags0 & 2) == 0)
            {
                return new(current, Point((model.Transform ?? throw new NotSupportedException("Junk anchor requires its actual transform.")).Position28));
            }
            NativeBattleJunk44PointStep first = Bone(current, pointer, 0xF0, 0);
            NativeBattleJunk44PointStep second = Bone(first.Frame, pointer, 0xF1, 0);
            short Average(int offset) => unchecked((short)((BinaryPrimitives.ReadInt16LittleEndian(first.Point8.Read(offset, 2)) + BinaryPrimitives.ReadInt16LittleEndian(second.Point8.Read(offset, 2))) / 2));
            return new(second.Frame, Point(new(Average(0), Average(2), Average(4))));
        }

        NativeBattleJunk44Frame Deliver(NativeBattleJunk44Frame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Junk requires its current ordering table.");
            uint address = current.State.ReadDword(0x2545174);
            if (address < NativeBattleEffectNodeMemory.ArenaAddress || address - NativeBattleEffectNodeMemory.ArenaAddress > NativeBattleEffectArena.Length)
            {
                throw new InvalidDataException("Junk packet cursor leaves its actual effect arena.");
            }
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, checked((int)(address - NativeBattleEffectNodeMemory.ArenaAddress)), primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, State = current.State.WriteDword(0x2545174, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)) };
        }

        NativeBattleJunk44Frame QueueFile(NativeBattleJunk44Frame current, NativeBattleEffectFileRequest request)
        {
            NativeBattleEffectFileAsset file = resources.Files.Single(file => file.FileId == request.FileId);
            NativeBattleEffectArena arena = (current.Memory.Arena ?? throw new NotSupportedException("Junk reload requires its actual arena.")).Write(NativeBattleWaterExecutionFields.ArenaOffset(request.DestinationAddress, file.Data.Length), file.Data.AsSpan());
            NativeFileIo = (NativeFileIo ?? throw new NotSupportedException("Junk reload requires its original file scheduler.")).Queue482610(file.Data.Length, request.CompletionCallback, null);
            return current with { Memory = current.Memory with { Arena = arena } };
        }

        NativeBattleJunk44IoStep ReloadComplete(NativeBattleJunk44Frame current)
        {
            NativeBattleEffectIoPoll poll = NativeBattleEffectIo.Select5342C0(current.Memory, 1);
            current = current with { Memory = poll.Memory };
            if (poll.Request is { } request)
            {
                current = QueueFile(current, request);
            }
            return new(current, !poll.Busy);
        }

        NativeBattleJunk44Frame Execute(NativeBattleJunk44Frame current, NativeBattleJunk44Operation operation)
        {
            switch (operation)
            {
                case NativeBattleJunk44Sound501330 sound:
                    Publish(current);
                    PublishNativeModuleSound(new(sound.Packed, sound.Flags, null) { Pan = sound.Pan });
                    return Import(current);
                case NativeBattleJunk44Contact506690 contact:
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.ContactIndex)] });
                    graphics = graphics with { PendingContacts = pending };
                    return Import(current);
                case NativeBattleJunk44SelectSequence505C00 sequence:
                    Publish(current);
                    graphics = ScheduleNativeRingSequence505C00(graphics, checked((byte)Actor(sequence.ModelAddress)), checked((byte)sequence.Sequence));
                    return Import(current);
                case NativeBattleJunk44ModelPositionY position:
                    int yActor = Actor(position.ModelAddress);
                    NativeBattleGraphicsModel moved = graphics.Models[yActor];
                    NativeBattleModelTransform yTransform = moved.Transform ?? throw new NotSupportedException("Junk requires its current recipient transform.");
                    graphics = graphics with { Models = graphics.Models.SetItem(yActor, moved with { Transform = yTransform with { Position28 = yTransform.Position28 with { Y = position.Y } } }) };
                    return current;
                case NativeBattleJunk44RestoreModelPosition restore:
                    int actor = Actor(restore.ModelAddress);
                    NativeBattleGraphicsModel model = graphics.Models[actor];
                    NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Junk requires its saved recipient transform owner.");
                    byte[] packed = restore.Point8.Read(0, 8);
                    NativeBattleVector xyz = new(BinaryPrimitives.ReadInt16LittleEndian(packed), BinaryPrimitives.ReadInt16LittleEndian(packed.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(packed.AsSpan(4)));
                    graphics = graphics with { Models = graphics.Models.SetItem(actor, model with { Transform = transform with { Position28 = xyz, Vertical34 = BinaryPrimitives.ReadInt16LittleEndian(packed.AsSpan(6)) } }) };
                    return current;
                case NativeBattleJunk44CameraWord1D97712 cameraWord:
                    NativeBattleCameraState camera = Camera();
                    NativeBattleCameraViewTranslationScratch scratch = camera.ViewTranslationScratch ?? throw new NotSupportedException("Junk requires its original camera view-translation scratch owner.");
                    NativeCamera = camera with { ViewTranslationScratch = scratch with { Y = unchecked((short)cameraWord.Value) } };
                    return current;
                case NativeBattleJunk44AdvanceIo534210:
                    NativeBattleEffectIoAdvance io = NativeBattleEffectIo.Advance534210(current.Memory);
                    current = current with { Memory = io.Memory };
                    return io.Request is { } request ? QueueFile(current, request) : current;
                case NativeBattleJunk44DrawStatic88 mesh:
                    return DrawMesh(current, mesh);
                case NativeBattleJunk44DrawSprite180 sprite:
                    return DrawSprite(current, sprite);
                default:
                    throw new NotSupportedException("Junk44 emitted an unsupported native operation.");
            }
        }

        NativeBattleJunk44Frame DrawMesh(NativeBattleJunk44Frame current, NativeBattleJunk44DrawStatic88 operation)
        {
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Junk mesh requires its ordinary temporary owner.");
            NativeBattleEffectArena context = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, operation.TemporaryAddress, 88), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 88), 0, 88);
            uint flags = BinaryPrimitives.ReadUInt32LittleEndian(context.Read(0x1C, 4));
            bool selected = flags is 0x2000 or 0x20C0;
            NativeBattleStaticMeshResource mesh = selected ? resources.ReadGeometry(operation.SourceAddress) : resources.ReadMesh(operation.SourceAddress);
            uint vertexAddress = selected ? BinaryPrimitives.ReadUInt32LittleEndian(context.Read(4, 4)) : 0;
            NativeBattleStaticMeshContext88Step drawn = NativeBattleStaticMeshContext88.DrawLoaded(current.Memory, mesh, Projection(), context, colors, operation.TableBaseBucket, checked((byte)operation.DepthShift), selectedVerticesAddress: selected ? vertexAddress : null, readSelectedVertex: selected ? ReadVertex : null, pilotIdentity: operation.PilotIdentity);
            current = current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, operation.TemporaryAddress, drawn.Context, 88) };
            return Deliver(current, drawn.Batch);

            (NativeBattleVector Vertex, short? W) ReadVertex(int offset)
            {
                int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4));
                if (offset < 0 || (offset & 7) != 0 || offset > count * 8 - 8)
                {
                    throw new InvalidDataException("Junk selected vertex leaves its original geometry.");
                }
                NativeBattleEffectArena point;
                if (vertexAddress == 0x2100D8C)
                {
                    point = NativeBattleEffectNodeContextMemory.Copy(current.Memory.Arena ?? throw new NotSupportedException("Junk morph requires its actual arena."), NativeBattleWaterExecutionFields.ArenaOffset(vertexAddress + (uint)offset, 8), NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 0, 8);
                }
                else
                {
                    if (vertexAddress != mesh.Address + 12)
                    {
                        throw new InvalidDataException("Junk selected vertices require its original shape or current morph scratch.");
                    }
                    point = NativeBattleEffectArena.Clear().Write(0, mesh.Bytes.AsSpan().Slice(12 + offset, 8));
                }
                byte[] xyz = point.Read(0, 6);
                (byte[] w, bool[] known) = point.ReadSnapshot(6, 2);
                return (new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4))), known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(w) : null);
            }
        }

        NativeBattleJunk44Frame DrawSprite(NativeBattleJunk44Frame current, NativeBattleJunk44DrawSprite180 operation)
        {
            NativeBattleModuleResource resource = resources.ReadSprite(operation.SourceAddress);
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Junk sprite requires its ordinary temporary owner.");
            NativeBattleEffectArena temporary = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, operation.TemporaryAddress, 180), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 180), 0, 180);
            if (BinaryPrimitives.ReadUInt32LittleEndian(temporary.Read(0, 4)) != operation.SourceAddress)
            {
                throw new InvalidDataException("Junk sprite source differs from its current temporary pointer.");
            }
            NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(current.Memory, temporary, pointer => pointer == resource.Address ? resource : throw new InvalidDataException("Junk sprite source changed during drawing."), Projection(), Camera().Roll, operation.TableBaseBucket, checked((byte)operation.DepthShift), pilotIdentity: operation.PilotIdentity);
            current = current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, operation.TemporaryAddress, drawn.Temporary180, 180) };
            return Deliver(current, drawn.Batch);
        }

        static NativeBattleEffectArena Point(NativeBattleVector point) => NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z));
    }
}
