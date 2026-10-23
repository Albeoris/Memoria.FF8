using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeEffect303(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Effect303 requires its selected original effect.");
        NativeBattleEffect303Resources resources = effect.Binding.Effect303Resources ?? throw new NotSupportedException("Effect303 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect303 requires its actual process memory.");
        NativeBattleEffect303Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect303 requires its current CRT random owner."), memory.Effect303 ?? throw new NotSupportedException("Effect303 requires its retained executable storage.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Effect303 requires its current temporary memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Effect303 requires its original arithmetic constants.");
        NativeBattleEffect303Inputs inputs = new(colors, _ => Camera().CapturePackedView1D97778(), _ => Projection(), (_, pointer) => graphics.Models[Actor(pointer)], ReadModelWorld, ReadCurrentBone, _ => nativeActionPackets, Execute, StartVibration, PollIo);
        frame = initialize ? NativeBattleEffect303Controller.Initialize(frame, resources, inputs) : NativeBattleEffect303Controller.Advance(frame, resources, inputs);
        Publish(frame);
        if (initialize)
        {
            NativeCamera = Camera().RegisterControlResource(frame.Memory.Textures!.Animation);
        }
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Effect303 requires its current camera.");
        NativeBattleCameraProjection Projection() => Camera().Projection ?? throw new NotSupportedException("Effect303 requires its current projection.");
        NativeBattlePoseSource PoseSource(uint pointer) => NativeGraphicsPoseResource(graphics, Actor(pointer)).PoseSource ?? throw new NotSupportedException("Effect303 requires its actual current skeleton.");

        int Actor(uint pointer)
        {
            uint delta = unchecked(pointer - 0x1D972C0);
            if (delta % 156 != 0 || delta / 156 >= (uint)graphics.Models.Length)
            {
                throw new InvalidDataException("Effect303 requires an actual physical graphics-model row.");
            }
            return checked((int)(delta / 156));
        }

        NativeBattlePackedMatrix32 ReadModelWorld(NativeBattleEffect303Frame current, uint pointer)
        {
            NativeBattleGraphicsModel model = graphics.Models[Actor(pointer)];
            return NativeBattlePackedMatrix32.FromMatrix(model.GeometryWorldMatrix ?? throw new NotSupportedException("Effect303 requires its current model world matrix."), null);
        }

        NativeBattlePackedMatrix32 ReadCurrentBone(NativeBattleEffect303Frame current, uint pointer, int bone)
        {
            NativeBattleGraphicsModel model = graphics.Models[Actor(pointer)];
            ImmutableArray<NativeBattleActionMatrix> bones = NativeBattleBodyBoneMatrices.Require(model, PoseSource(pointer));
            if ((uint)bone >= (uint)bones.Length)
            {
                throw new InvalidDataException("Effect303 bone selector leaves its actual current rig.");
            }
            return NativeBattlePackedMatrix32.FromMatrix(bones[bone], null);
        }

        void Publish(NativeBattleEffect303Frame current)
        {
            NativeEffectMemory = current.Memory with { Effect303 = current.State };
            nativeCrtRandom = current.Random;
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Effect303 lost its current temporary memory.");
        }

        NativeBattleEffect303Frame Import(NativeBattleEffect303Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Effect303 service lost its process memory.");
            return current with { Memory = latest, State = latest.Effect303 ?? throw new InvalidDataException("Effect303 service lost its executable storage."), Random = nativeCrtRandom ?? throw new InvalidDataException("Effect303 service lost its CRT owner."), Staging = NativeFileStaging ?? throw new InvalidDataException("Effect303 service lost its temporary memory.") };
        }

        NativeBattleEffect303Frame QueueFile(NativeBattleEffect303Frame current, NativeBattleEffectFileRequest request)
        {
            NativeBattleEffectFileAsset file = resources.CompletionFile;
            if (file.FileId != request.FileId)
            {
                throw new NotSupportedException("Effect303 requires its selected original completion file.");
            }
            NativeBattleEffectArena arena = (current.Memory.Arena ?? throw new NotSupportedException("Effect303 reload requires its actual arena.")).Write(NativeBattleWaterExecutionFields.ArenaOffset(request.DestinationAddress, file.Data.Length), file.Data.AsSpan());
            NativeFileIo = (NativeFileIo ?? throw new NotSupportedException("Effect303 reload requires its current completion scheduler.")).Queue482610(file.Data.Length, request.CompletionCallback, null);
            return current with { Memory = current.Memory with { Arena = arena } };
        }

        NativeBattleEffect303IoPoll PollIo(NativeBattleEffect303Frame current, int index)
        {
            NativeBattleEffectIoPoll poll = NativeBattleEffectIo.Select5342C0(current.Memory, index);
            current = current with { Memory = poll.Memory };
            if (poll.Request is { } request)
            {
                current = QueueFile(current, request);
            }
            return new(current, poll.Busy);
        }

        NativeBattleEffect303VibrationStep StartVibration(NativeBattleEffect303Frame current)
        {
            NativeVibrationEnqueue queued = (NativeVibrationProcess ?? throw new NotSupportedException("Effect303 requires its retained vibration process.")).Enqueue(resources.Vibration.Bank, 1, 128);
            NativeVibrationProcess = queued.State;
            return new(current, queued.Handle);
        }

        NativeBattleEffect303Frame Deliver(NativeBattleEffect303Frame current, NativeBattleGfGeometryBatch batch, uint cursorGlobal = NativeBattleEffect303Profile.CursorGlobal, ushort flags = 0)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect303 requires its current ordering table.");
            if (cursorGlobal == 0x1D8E054)
            {
                NativeBattlePrimitiveProcess process = current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Effect303 requires its current ordinary COMMON packet owner.");
                NativeBattleCommonPacketWrite ordinary = NativeBattleCommonPacketDelivery.Deliver(process, primitive.Pending, batch, flags);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = ordinary.Ordering } };
                return current with { Memory = current.Memory with { PrimitiveProcess = ordinary.Process } };
            }
            if (cursorGlobal != NativeBattleEffect303Profile.CursorGlobal)
            {
                throw new InvalidDataException("Effect303 selected an unsupported packet cursor.");
            }
            int cursor = NativeBattleWaterExecutionFields.ArenaOffset(current.State.ReadDword(cursorGlobal), 0);
            NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Effect303 requires its actual effect packet arena.");
            NativeBattleGfGeometryBatch resolved = NativeBattlePacketBatchWords.Resolve(batch, cursor, NativeBattleEffectArena.Length, offset => arena.Read(offset, 1)[0]);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, resolved, flags);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, State = current.State.WriteDword(cursorGlobal, checked(NativeBattleEffect303Profile.ArenaAddress + (uint)written.Cursor)) };
        }

        NativeBattleEffect303Frame RestorePose(NativeBattleEffect303Frame current, uint pointer)
        {
            int actor = Actor(pointer);
            NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(current.Staging ?? throw new NotSupportedException("Effect303 pose restoration requires its temporary memory."), 88);
            NativeBattleBodyBonePreparation prepared = NativeBattleBodyBoneTemporary.Prepare508C90(graphics.Models[actor], PoseSource(pointer), NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 88));
            NativeBattleFileStaging staging = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, prepared.Temporary58, 88);
            graphics = graphics with { Models = graphics.Models.SetItem(actor, prepared.Model) };
            return current with { Staging = NativeBattleTemporaryMemory.Release5082D0(staging, 88) };
        }

        NativeBattleEffect303Frame DrawMesh(NativeBattleEffect303Frame current, NativeBattleEffect303DrawMesh7043B0 operation)
        {
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Effect303 mesh requires its current temporary owner.");
            NativeBattleEffectArena context = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, operation.TemporaryAddress, 88), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 88), 0, 88);
            NativeBattleStaticMeshResource mesh = resources.ReadGeometry(operation.SourceAddress);
            if (BinaryPrimitives.ReadUInt32LittleEndian(context.Read(4, 4)) != operation.VertexAddress)
            {
                throw new InvalidDataException("Effect303 mesh selected pointer differs from its actual context.");
            }
            NativeBattleEffect303MeshContext88Step drawn = NativeBattleEffect303MeshContext88.DrawLoaded(current.Memory, mesh, Projection(), context, colors, operation.TableBaseBucket, checked((byte)operation.DepthShift), ReadVertex);
            current = current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, operation.TemporaryAddress, drawn.Context88, 88) };
            return Deliver(current, drawn.Batch);

            (NativeBattleVector Vertex, short? W) ReadVertex(int offset)
            {
                int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4));
                if (offset < 0 || (offset & 7) != 0 || offset > count * 8 - 8)
                {
                    throw new InvalidDataException("Effect303 selected vertex leaves its actual geometry.");
                }
                NativeBattleEffectArena point;
                if (operation.VertexAddress == NativeBattleEffect303Profile.ArenaAddress + NativeBattleEffect303Profile.ScratchOffset)
                {
                    point = NativeBattleEffectNodeContextMemory.Copy(current.Memory.Arena ?? throw new NotSupportedException("Effect303 morph requires its current arena."), NativeBattleEffect303Profile.ScratchOffset + offset, NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 0, 8);
                }
                else
                {
                    uint delta = unchecked(operation.VertexAddress - mesh.Address);
                    int directory = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan());
                    if (delta < 12 || (delta - 12) % checked((uint)(count * 8)) != 0 || delta + count * 8 > directory)
                    {
                        throw new InvalidDataException("Effect303 selected vertices require its current morph or an actual immutable shape.");
                    }
                    point = NativeBattleEffectArena.Clear().Write(0, mesh.Bytes.AsSpan().Slice(checked((int)delta + offset), 8));
                }
                byte[] xyz = point.Read(0, 6);
                (byte[] w, bool[] known) = point.ReadSnapshot(6, 2);
                return (new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4))), known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(w) : null);
            }
        }

        NativeBattleEffect303Frame DrawSprite(NativeBattleEffect303Frame current, NativeBattleEffect303DrawSprite180 operation)
        {
            NativeBattleModuleResource resource = resources.ReadSprite(operation.SourceAddress);
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Effect303 sprite requires its ordinary temporary owner.");
            NativeBattleEffectArena temporary = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, operation.TemporaryAddress, 180), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 180), 0, 180);
            if (BinaryPrimitives.ReadUInt32LittleEndian(temporary.Read(0, 4)) != operation.SourceAddress)
            {
                throw new InvalidDataException("Effect303 sprite source differs from its current temporary pointer.");
            }
            NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(current.Memory, temporary, pointer => pointer == resource.Address ? resource : throw new InvalidDataException("Effect303 sprite source changed during drawing."), Projection(), Camera().Roll, operation.TableBaseBucket, checked((byte)operation.DepthShift));
            current = current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, operation.TemporaryAddress, drawn.Temporary180, 180) };
            return Deliver(current, drawn.Batch);
        }

        NativeBattleEffect303Frame DrawStatic(NativeBattleEffect303Frame current, NativeBattleEffect303DrawStaticMesh88 operation)
        {
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Effect303 static mesh requires its actual temporary memory.");
            NativeBattleEffectArena context = NativeBattleTemporaryMemory.ReadView(staging, operation.TemporaryAddress, 88);
            NativeBattleStaticMeshResource mesh = resources.ReadMesh(operation.SourceAddress);
            NativeBattleStaticMeshContext88Step drawn = NativeBattleStaticMeshContext88.DrawLoaded(current.Memory, mesh, Projection(), context, colors, operation.TableBaseBucket, checked((byte)operation.DepthShift));
            current = current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, operation.TemporaryAddress, drawn.Context, 88) };
            return Deliver(current, drawn.Batch);
        }

        NativeBattleEffect303Frame Execute(NativeBattleEffect303Frame current, NativeBattleEffect303Operation operation)
        {
            switch (operation)
            {
                case NativeBattleEffect303CopyPackedPosition copy:
                    NativeBattleModelTransform source = graphics.Models[Actor(copy.SourceModel)].Transform ?? throw new NotSupportedException("Effect303 requires the source packed position owner.");
                    int targetIndex = Actor(copy.TargetModel);
                    NativeBattleGraphicsModel target = graphics.Models[targetIndex];
                    NativeBattleModelTransform destination = target.Transform ?? throw new NotSupportedException("Effect303 requires the target packed position owner.");
                    target = target with { Transform = destination with { Position28 = source.Position28, Vertical34 = source.Vertical34 } };
                    graphics = graphics with { Models = graphics.Models.SetItem(targetIndex, target) };
                    return current;
                case NativeBattleEffect303SelectSequence505C00 sequence:
                    graphics = ScheduleNativeRingSequence505C00(graphics, checked((byte)Actor(sequence.ModelAddress)), sequence.Sequence);
                    return current;
                case NativeBattleEffect303EnterSequence508F90 enter:
                    int enteringActor = Actor(enter.ModelAddress);
                    NativeBattleGraphicsModel entering = graphics.Models[enteringActor];
                    NativeBattleSequenceState control = entering.SequenceState ?? throw new NotSupportedException("Effect303 requires the recipient's actual animation control.");
                    byte counter = control.PoseFrame114 ?? throw new NotSupportedException("Effect303 requires the actual decoded-frame counter.");
                    byte limit = control.PoseLimit115 ?? throw new NotSupportedException("Effect303 requires the actual animation limit.");
                    if (counter < limit)
                    {
                        NativeBattlePoseProgress progress = new NativeBattlePoseProgress(control.Pose ?? throw new NotSupportedException("Effect303 requires the current animation selector."), counter, limit, control.PoseFlags109).Advance();
                        control = control with { PoseFrame114 = progress.Frame, PoseFlags109 = progress.Flags, DecodedPose = PoseSource(enter.ModelAddress).Sample(progress.Pose, progress.Frame, progress.Flags) };
                        graphics = graphics with { Models = graphics.Models.SetItem(enteringActor, entering with { SequenceState = control }) };
                    }
                    return current;
                case NativeBattleEffect303Activate487670 activation:
                    graphics = QueueNativeDescriptor(graphics, 113, 0x487670, checked((ushort)Actor(activation.ModelAddress)));
                    return current;
                case NativeBattleEffect303ModelFields fields:
                    int actor = Actor(fields.ModelAddress);
                    NativeBattleGraphicsModel model = graphics.Models[actor];
                    graphics = graphics with { Models = graphics.Models.SetItem(actor, model with { Flags0 = unchecked((ushort)((model.Flags0 & ~fields.ClearFlags0) | fields.SetFlags0)) }) };
                    return current;
                case NativeBattleEffect303RestorePose508C90 restore:
                    return RestorePose(current, restore.ModelAddress);
                case NativeBattleEffect303DrawBody188 body:
                    (graphics, current) = DrawNativeEffect303Body(graphics, current, body);
                    return current;
                case NativeBattleEffect303Sound501330 sound:
                    Publish(current);
                    PublishNativeModuleSound(new(sound.Packed, sound.Flags, null) { Pan = sound.Pan });
                    return Import(current);
                case NativeBattleEffect303StopVibration4A2940 vibration:
                    NativeVibrationProcess = (NativeVibrationProcess ?? throw new NotSupportedException("Effect303 requires its actual vibration process.")).Stop4A2940(vibration.Handle);
                    return current;
                case NativeBattleEffect303InitializeIo534110 io:
                    return current with { Memory = NativeBattleEffectIo.Initialize534110(current.Memory, io.ArenaOffset) };
                case NativeBattleEffect303QueueFile5341D0 file:
                    return current with { Memory = NativeBattleEffectIo.Queue5341D0(current.Memory, file.FileId, file.DestinationAddress, file.Type) };
                case NativeBattleEffect303AdvanceIo534210:
                    NativeBattleEffectIoAdvance advanced = NativeBattleEffectIo.Advance534210(current.Memory);
                    current = current with { Memory = advanced.Memory };
                    return advanced.Request is { } request ? QueueFile(current, request) : current;
                case NativeBattleEffect303DrawMesh7043B0 mesh:
                    return DrawMesh(current, mesh);
                case NativeBattleEffect303DrawStaticMesh88 mesh:
                    return DrawStatic(current, mesh);
                case NativeBattleEffect303DrawSprite180 sprite:
                    return DrawSprite(current, sprite);
                case NativeBattleEffect303DeliverBatch packets:
                    return Deliver(current, packets.Batch, packets.CursorGlobal, packets.Flags);
                default:
                    throw new NotSupportedException("Effect303 emitted an unsupported native operation.");
            }
        }
    }
}
