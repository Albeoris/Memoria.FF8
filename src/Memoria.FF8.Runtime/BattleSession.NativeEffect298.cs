using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeEffect298(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Effect298 requires its selected original effect.");
        effect = effect with { PresentationPackets = [] };
        NativeBattleEffect298Resources resources = effect.Binding.Effect298Resources ?? throw new NotSupportedException("Effect298 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect298 requires its actual process memory.");
        NativeBattleEffect298Frame frame = new(memory, effect, memory.Effect298 ?? throw new NotSupportedException("Effect298 requires its retained executable storage.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Effect298 requires its current temporary memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Effect298 requires its original arithmetic constants.");
        NativeBattleEffect298Inputs inputs = new(colors, _ => Camera().CapturePackedView1D97778(), _ => Projection(), (_, pointer) => graphics.Models[Actor(pointer)], ReadBone502170, _ => nativeActionPackets, ReadBody, Execute, StartVibration, PollIo) { CapturePresentation = true };
        frame = initialize ? NativeBattleEffect298Controller.Initialize(frame, resources, inputs) : NativeBattleEffect298Controller.Advance(frame, resources, inputs);
        Publish(frame);
        if (initialize)
        {
            NativeCamera = Camera().RegisterControlResource(frame.Memory.Textures!.Animation);
        }
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Effect298 requires its current camera.");
        NativeBattleCameraProjection Projection() => Camera().Projection ?? throw new NotSupportedException("Effect298 requires its current projection.");
        NativeBattlePoseSource PoseSource(uint pointer) => NativeGraphicsPoseResource(graphics, Actor(pointer)).PoseSource ?? throw new NotSupportedException("Effect298 requires its actual current skeleton.");

        int Actor(uint pointer)
        {
            uint delta = unchecked(pointer - 0x1D972C0);
            if (delta % 156 != 0 || delta / 156 >= (uint)graphics.Models.Length)
            {
                throw new InvalidDataException("Effect298 requires an actual physical graphics-model row.");
            }
            return checked((int)(delta / 156));
        }

        NativeBattleEffect298PointStep ReadBone502170(NativeBattleEffect298Frame current, uint pointer, int selector, int distance)
        {
            NativeBattleGraphicsModel model = graphics.Models[Actor(pointer)];
            NativeBattleVector point;
            if ((model.Flags0 & 2) == 0)
            {
                point = (model.Transform ?? throw new NotSupportedException("Effect298 bone query requires its actual transform.")).Position28;
            }
            else
            {
                NativeFileStaging = current.Staging ?? throw new NotSupportedException("Effect298 bone query requires its current temporary memory.");
                NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, PoseSource(pointer), checked((byte)selector), checked((short)distance));
                NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
                point = new(unchecked((short)observed.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)observed.ScriptedColorRegisters.Mac[1]!.Value), unchecked((short)observed.ScriptedColorRegisters.Mac[2]!.Value));
                current = current with { Memory = observed, Staging = NativeFileStaging };
            }
            NativeBattleEffectArena packed = NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z));
            return new(current, packed);
        }

        NativeBattleEffect298BodySource ReadBody(NativeBattleEffect298Frame current, uint pointer)
        {
            int actor = Actor(pointer);
            NativeBattleGraphicsModel model = graphics.Models[actor];
            NativeBattlePoseSource pose = PoseSource(pointer);
            NativeBattleDecodedPose decoded = NativeBattlePoseMatrices.ReadDecodedPose(model.SequenceState ?? throw new NotSupportedException("Effect298 requires its actual decoded pose."), pose);
            ImmutableArray<NativeBattlePackedMatrix32> bones = NativeBattleBodyBoneMatrices.Require(model, pose).Select(matrix => NativeBattlePackedMatrix32.FromMatrix(matrix, null)).ToImmutableArray();
            NativeDeformedBodyBridge bridge = new(current.Memory, current.Effect, nativeCrtRandom ?? throw new NotSupportedException("Effect298 geometry access requires its current process context."), graphics, model, current.Staging ?? throw new NotSupportedException("Effect298 geometry requires its addressed temporary owner."), NativeBattleEffectArena.Clear(), 0);
            NativeBattleResourcePointer selected = NativeDeformedGeometryPointer(bridge, false);
            uint address = selected.Cpu?.PhysicalAddress ?? selected.StagingAddress ?? throw new NotSupportedException("Effect298 requires its actual addressed raw geometry.");
            int scratchLength = checked((NativeModelCpuResources?.ScratchMaximum ?? throw new NotSupportedException("Effect298 body access requires its current loaded projection capacity.")) * 8);
            return new(pose, decoded, bones, address, ReadGeometry) { VertexScratchAddress = checked(0x196B018u - (uint)scratchLength), VertexScratchLength = scratchLength };

            NativeBattleEffectArena ReadGeometry(uint source, int length)
            {
                long delta = (long)source - address;
                if (delta < 0 || delta > int.MaxValue)
                {
                    throw new InvalidDataException("Effect298 geometry read leaves its actual selected resource.");
                }
                return NativeBattleEffectArena.Clear().Write(0, ReadNativeDeformedGeometry(bridge, selected, (int)delta, length));
            }
        }

        void Publish(NativeBattleEffect298Frame current)
        {
            NativeEffectMemory = current.Memory with { Effect298 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Effect298 lost its current temporary memory.");
        }

        NativeBattleEffect298Frame Import(NativeBattleEffect298Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Effect298 service lost its process memory.");
            return current with { Memory = latest, State = latest.Effect298 ?? throw new InvalidDataException("Effect298 service lost its executable storage."), Staging = NativeFileStaging ?? throw new InvalidDataException("Effect298 service lost its temporary memory.") };
        }

        NativeBattleEffect298Frame QueueFile(NativeBattleEffect298Frame current, NativeBattleEffectFileRequest request)
        {
            NativeBattleEffectFileAsset file = resources.CompletionFile;
            if (file.FileId != request.FileId)
            {
                throw new NotSupportedException("Effect298 requires its selected original completion file.");
            }
            NativeBattleEffectArena arena = (current.Memory.Arena ?? throw new NotSupportedException("Effect298 reload requires its actual arena.")).Write(NativeBattleWaterExecutionFields.ArenaOffset(request.DestinationAddress, file.Data.Length), file.Data.AsSpan());
            NativeFileIo = (NativeFileIo ?? throw new NotSupportedException("Effect298 reload requires its current completion scheduler.")).Queue482610(file.Data.Length, request.CompletionCallback, null);
            return current with { Memory = current.Memory with { Arena = arena } };
        }

        NativeBattleEffect298IoPoll PollIo(NativeBattleEffect298Frame current, int index)
        {
            NativeBattleEffectIoPoll poll = NativeBattleEffectIo.Select5342C0(current.Memory, index);
            current = current with { Memory = poll.Memory };
            if (poll.Request is { } request)
            {
                current = QueueFile(current, request);
            }
            return new(current, poll.Busy);
        }

        NativeBattleEffect298VibrationStep StartVibration(NativeBattleEffect298Frame current)
        {
            NativeVibrationEnqueue queued = (NativeVibrationProcess ?? throw new NotSupportedException("Effect298 requires its retained vibration process.")).Enqueue(resources.Vibration.Bank, 1, 128);
            NativeVibrationProcess = queued.State;
            return new(current, queued.Handle);
        }

        NativeBattleEffect298Frame Deliver(NativeBattleEffect298Frame current, NativeBattleGfGeometryBatch batch, uint cursorGlobal = NativeBattleEffect298Profile.CursorGlobal, ushort flags = 0)
        {
            batch = NativeBattleCinematicActorPresentation.NativeDepths(batch);
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect298 requires its current ordering table.");
            if (cursorGlobal == 0x1D8E054)
            {
                NativeBattlePrimitiveProcess process = current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Effect298 requires its current ordinary COMMON packet owner.");
                current = current with { Effect = current.Effect with { PresentationPackets = NativeBattleEffectPresentationPackets.Record(current.Effect.PresentationPackets, process.PacketBank!, process.PacketBuffer, process.ByteCursor!.Value, batch) } };
                NativeBattleCommonPacketWrite ordinary = NativeBattleCommonPacketDelivery.Deliver(process, primitive.Pending, batch, flags);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = ordinary.Ordering } };
                return current with { Memory = current.Memory with { PrimitiveProcess = ordinary.Process } };
            }
            if (cursorGlobal != NativeBattleEffect298Profile.CursorGlobal)
            {
                throw new InvalidDataException("Effect298 selected an unsupported packet cursor.");
            }
            int cursor = NativeBattleWaterExecutionFields.ArenaOffset(current.State.ReadDword(cursorGlobal), 0);
            NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Effect298 requires its actual effect packet arena.");
            NativeBattleGfGeometryBatch resolved = NativeBattlePacketBatchWords.Resolve(batch, cursor, NativeBattleEffectArena.Length, offset => arena.Read(offset, 1)[0]);
            current = current with { Effect = current.Effect with { PresentationPackets = NativeBattleEffectPresentationPackets.Record(current.Effect.PresentationPackets, "20DFAB8", 0, cursor, resolved) } };
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, resolved, flags);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, State = current.State.WriteDword(cursorGlobal, checked(NativeBattleEffect298Profile.ArenaAddress + (uint)written.Cursor)) };
        }

        NativeBattleEffect298Frame ApplyPose(NativeBattleEffect298Frame current, uint pointer, uint offsetAddress)
        {
            int actor = Actor(pointer);
            NativeBattlePoseSource pose = PoseSource(pointer);
            NativeBattleEffectArena offsets = NativeBattleEffect298Controller.ReadBytes(current, offsetAddress, checked(pose.BoneCount * 16));
            NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(current.Staging ?? throw new NotSupportedException("Effect298 offset pose requires its temporary memory."), 96);
            NativeBattleEffect298PosePreparation prepared = NativeBattleEffect298Pose.Prepare662C00(graphics.Models[actor], pose, offsets, NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 96));
            NativeBattleFileStaging staging = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, prepared.Temporary96, 96);
            graphics = graphics with { Models = graphics.Models.SetItem(actor, prepared.Model) };
            return current with { Staging = NativeBattleTemporaryMemory.Release5082D0(staging, 96) };
        }

        NativeBattleEffect298Frame RestorePose(NativeBattleEffect298Frame current, uint pointer)
        {
            int actor = Actor(pointer);
            NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(current.Staging ?? throw new NotSupportedException("Effect298 pose restoration requires its temporary memory."), 88);
            NativeBattleBodyBonePreparation prepared = NativeBattleBodyBoneTemporary.Prepare508C90(graphics.Models[actor], PoseSource(pointer), NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 88));
            NativeBattleFileStaging staging = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, prepared.Temporary58, 88);
            graphics = graphics with { Models = graphics.Models.SetItem(actor, prepared.Model) };
            return current with { Staging = NativeBattleTemporaryMemory.Release5082D0(staging, 88) };
        }

        NativeBattleEffect298Frame DrawMesh(NativeBattleEffect298Frame current, NativeBattleEffect298DrawMesh7043B0 operation)
        {
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Effect298 mesh requires its current temporary owner.");
            NativeBattleEffectArena context = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, operation.TemporaryAddress, 88), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 88), 0, 88);
            NativeBattleStaticMeshResource mesh = resources.ReadGeometry(operation.SourceAddress);
            if (BinaryPrimitives.ReadUInt32LittleEndian(context.Read(4, 4)) != operation.VertexAddress)
            {
                throw new InvalidDataException("Effect298 mesh selected pointer differs from its actual context.");
            }
            NativeBattleEffect298MeshContext88Step drawn = NativeBattleEffect298MeshContext88.DrawLoaded(current.Memory, mesh, Projection(), context, colors, operation.TableBaseBucket, checked((byte)operation.DepthShift), ReadVertex, operation.PilotIdentity);
            current = current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, operation.TemporaryAddress, drawn.Context88, 88) };
            return Deliver(current, drawn.Batch);

            (NativeBattleVector Vertex, short? W) ReadVertex(int offset)
            {
                int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4));
                if (offset < 0 || (offset & 7) != 0 || offset > count * 8 - 8)
                {
                    throw new InvalidDataException("Effect298 selected vertex leaves its actual geometry.");
                }
                NativeBattleEffectArena point;
                if (operation.VertexAddress == NativeBattleEffect298Profile.ArenaAddress + NativeBattleEffect298Profile.ScratchOffset)
                {
                    point = NativeBattleEffectNodeContextMemory.Copy(current.Memory.Arena ?? throw new NotSupportedException("Effect298 morph requires its current arena."), NativeBattleEffect298Profile.ScratchOffset + offset, NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 0, 8);
                }
                else
                {
                    uint delta = unchecked(operation.VertexAddress - mesh.Address);
                    int directory = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan());
                    if (delta < 12 || (delta - 12) % checked((uint)(count * 8)) != 0 || delta + count * 8 > directory)
                    {
                        throw new InvalidDataException("Effect298 selected vertices require its current morph or an actual immutable shape.");
                    }
                    point = NativeBattleEffectArena.Clear().Write(0, mesh.Bytes.AsSpan().Slice(checked((int)delta + offset), 8));
                }
                byte[] xyz = point.Read(0, 6);
                (byte[] w, bool[] known) = point.ReadSnapshot(6, 2);
                return (new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4))), known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(w) : null);
            }
        }

        NativeBattleEffect298Frame DrawSprite(NativeBattleEffect298Frame current, NativeBattleEffect298DrawSprite180 operation)
        {
            NativeBattleModuleResource resource = resources.ReadSprite(operation.SourceAddress);
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Effect298 sprite requires its ordinary temporary owner.");
            NativeBattleEffectArena temporary = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, operation.TemporaryAddress, 180), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 180), 0, 180);
            if (BinaryPrimitives.ReadUInt32LittleEndian(temporary.Read(0, 4)) != operation.SourceAddress)
            {
                throw new InvalidDataException("Effect298 sprite source differs from its current temporary pointer.");
            }
            NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(current.Memory, temporary, pointer => pointer == resource.Address ? resource : throw new InvalidDataException("Effect298 sprite source changed during drawing."), Projection(), Camera().Roll, operation.TableBaseBucket, checked((byte)operation.DepthShift), pilotIdentity: operation.PilotIdentity);
            current = current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, operation.TemporaryAddress, drawn.Temporary180, 180) };
            return Deliver(current, drawn.Batch);
        }

        NativeBattleEffect298Frame DrawBody(NativeBattleEffect298Frame current, uint pointer, uint offsetAddress, NativeBattlePilotVisualIdentity? pilotIdentity)
        {
            int actor = Actor(pointer);
            NativeBattlePoseSource pose = PoseSource(pointer);
            NativeBattleEffectArena offsets = NativeBattleEffect298Controller.ReadBytes(current, offsetAddress, checked(pose.BoneCount * 16));
            int scratchLength = checked((NativeModelCpuResources?.ScratchMaximum ?? throw new NotSupportedException("Effect298 body requires its current loaded projection capacity.")) * 8);
            uint scratchAddress = checked(0x196B018u - (uint)scratchLength);
            NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(current.Staging ?? throw new NotSupportedException("Effect298 body requires its actual temporary memory."), 152);
            NativeBattleEffect298BodyFrame body = new(current.Memory, allocation.Staging, graphics.Models[actor], NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 152));
            NativeBattleEffect298BodyServices services = new(Shadow, OffsetPose, DrawSource, Restore);
            body = NativeBattleEffect298BodyDrawing.DrawOuter(body, Camera().CapturePackedView1D97778(), offsetAddress, offsets, scratchAddress, services);
            graphics = graphics with { Models = graphics.Models.SetItem(actor, body.Model) };
            NativeBattleFileStaging staging = NativeBattleTemporaryMemory.PublishView(body.Staging, allocation.Address, body.Temporary152, 152);
            return current with { Memory = body.Memory, Staging = NativeBattleTemporaryMemory.Release5082D0(staging, 152) };

            NativeBattleEffect298BodyFrame Shadow(NativeBattleEffect298BodyFrame selected)
            {
                NativeBattleShadowDrawStep shadow = NativeBattleShadowDrawing.Prepare5088A0(selected.Staging, new(selected.Memory.GeometryRegisters ?? throw new NotSupportedException("Effect298 shadow requires its current GTE matrix."), selected.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect298 shadow requires its current GTE data.")), selected.Model, Projection(), selected.Memory.ScriptedGraphics ?? throw new NotSupportedException("Effect298 shadow requires its current depth controls."), definition.NativeGraphics?.Rules.ShadowSource ?? throw new NotSupportedException("Effect298 shadow requires its original seeds."), definition.NativeGraphics?.PrimitiveCapabilities?.GouraudAlpha ?? throw new NotSupportedException("Effect298 shadow requires its actual primitive capabilities."), 4112, 16, pilotIdentity);
                selected = selected with { Memory = selected.Memory with { GeometryRegisters = shadow.Registers.Geometry, ScriptedColorRegisters = shadow.Registers.Colors }, Staging = NativeBattleTemporaryMemory.PublishView(shadow.Staging, shadow.TemporaryAddress, shadow.Temporary78, 0x78) };
                NativeBattleEffect298Frame delivered = Deliver(current with { Memory = selected.Memory, Staging = selected.Staging }, shadow.Batch, 0x1D8E054);
                current = current with { Effect = delivered.Effect };
                return selected with { Memory = delivered.Memory, Staging = NativeBattleTemporaryMemory.Release5082D0(selected.Staging, 0x78) };
            }

            NativeBattleEffect298BodyFrame OffsetPose(NativeBattleEffect298BodyFrame selected)
            {
                graphics = graphics with { Models = graphics.Models.SetItem(actor, selected.Model) };
                NativeBattleEffect298Frame prepared = ApplyPose(current with { Memory = selected.Memory, Staging = selected.Staging }, pointer, offsetAddress);
                return selected with { Memory = prepared.Memory, Staging = prepared.Staging!, Model = graphics.Models[actor] };
            }

            NativeBattleEffect298BodyFrame DrawSource(NativeBattleEffect298BodyFrame selected)
            {
                graphics = graphics with { Models = graphics.Models.SetItem(actor, selected.Model) };
                NativeBattleEffect298BodySource source = ReadBody(current with { Memory = selected.Memory, Staging = selected.Staging }, pointer);
                NativeBattleEffectArena scratch = NativeBattleTemporaryMemory.ReadView(selected.Staging, scratchAddress, scratchLength);
                NativeBattleEffect298BodyStep drawn = NativeBattleEffect298BodyDrawing.DrawBody(selected.Memory, selected.Temporary152, scratch, source.ReadGeometry, source.GeometryAddress, source.CurrentBones, offsets, Projection(), selected.Memory.ScriptedGraphics ?? throw new NotSupportedException("Effect298 body requires its actual projection controls."), scratchAddress, scratchLength, 17, 0, pilotIdentity);
                selected = selected with { Memory = drawn.Memory, Temporary152 = drawn.Temporary152, Staging = NativeBattleTemporaryMemory.PublishView(selected.Staging, scratchAddress, drawn.VertexScratch, scratchLength) };
                selected = selected with { Staging = NativeBattleTemporaryMemory.PublishView(selected.Staging, allocation.Address, selected.Temporary152, 152) };
                NativeBattleEffect298Frame delivered = Deliver(current with { Memory = selected.Memory, Staging = selected.Staging }, drawn.Batch, 0x1D8E054);
                current = current with { Effect = delivered.Effect };
                return selected with { Memory = delivered.Memory };
            }

            NativeBattleEffect298BodyFrame Restore(NativeBattleEffect298BodyFrame selected)
            {
                graphics = graphics with { Models = graphics.Models.SetItem(actor, selected.Model) };
                NativeBattleEffect298Frame restored = RestorePose(current with { Memory = selected.Memory, Staging = selected.Staging }, pointer);
                return selected with { Memory = restored.Memory, Staging = restored.Staging!, Model = graphics.Models[actor] };
            }
        }

        NativeBattleEffect298Frame Execute(NativeBattleEffect298Frame current, NativeBattleEffect298Operation operation)
        {
            switch (operation)
            {
                case NativeBattleEffect298Music47E3C0 music:
                    if ((ResolvedNativeFlags & 2) == 0)
                    {
                        ushort channel = NativeMusicProcess?.ReturnChannel ?? throw new NotSupportedException("Effect298 requires its actual music return channel.");
                        NativeGlobalSoundProcess owner = NativeGlobalSoundProcess ?? throw new NotSupportedException("Effect298 requires its actual sound process.");
                        NativeMusicPlaybackState playback = owner.MusicPlayback ?? throw new NotSupportedException("Effect298 requires its current music playback.");
                        NativeGlobalSoundProcess = owner with { MusicPlayback = playback.FadeGain46BBC0(unchecked((uint)(short)channel), unchecked((byte)music.Duration), 0) };
                    }
                    return current;
                case NativeBattleEffect298StopVibration4A2940 vibration:
                    NativeVibrationProcess = (NativeVibrationProcess ?? throw new NotSupportedException("Effect298 requires its current vibration process.")).Stop4A2940(vibration.Handle);
                    return current;
                case NativeBattleEffect298Sound501330 sound:
                    Publish(current);
                    PublishNativeModuleSound(new(sound.Packed, sound.Flags, null) { Pan = sound.Pan });
                    return Import(current);
                case NativeBattleEffect298ModelFields fields:
                    int actor = Actor(fields.ModelAddress);
                    NativeBattleGraphicsModel model = graphics.Models[actor];
                    model = model with { Flags0 = unchecked((ushort)((model.Flags0 & ~fields.ClearFlags0) | fields.SetFlags0)) };
                    if (fields.PositionZ is short z)
                    {
                        NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Effect298 requires its actual position owner.");
                        model = model with { Transform = transform with { Position28 = transform.Position28 with { Z = z } } };
                    }
                    graphics = graphics with { Models = graphics.Models.SetItem(actor, model) };
                    return current;
                case NativeBattleEffect298InitializeIo534110 io:
                    return current with { Memory = NativeBattleEffectIo.Initialize534110(current.Memory, io.ArenaOffset) };
                case NativeBattleEffect298QueueFile5341D0 file:
                    return current with { Memory = NativeBattleEffectIo.Queue5341D0(current.Memory, file.FileId, file.DestinationAddress, file.Type) };
                case NativeBattleEffect298AdvanceIo534210:
                    NativeBattleEffectIoAdvance advanced = NativeBattleEffectIo.Advance534210(current.Memory);
                    current = current with { Memory = advanced.Memory };
                    return advanced.Request is { } request ? QueueFile(current, request) : current;
                case NativeBattleEffect298ApplyOffsetPose662C00 pose:
                    return ApplyPose(current, pose.ModelAddress, pose.OffsetRowsAddress);
                case NativeBattleEffect298RestorePose508C90 restore:
                    return RestorePose(current, restore.ModelAddress);
                case NativeBattleEffect298DrawOffsetBody672330 body:
                    return DrawBody(current, body.ModelAddress, body.OffsetRowsAddress, body.PilotIdentity);
                case NativeBattleEffect298DeliverBatch packets:
                    return Deliver(current, packets.Batch, packets.CursorGlobal, packets.Flags);
                case NativeBattleEffect298DrawMesh7043B0 mesh:
                    return DrawMesh(current, mesh);
                case NativeBattleEffect298DrawSprite180 sprite:
                    return DrawSprite(current, sprite);
                default:
                    throw new NotSupportedException("Effect298 emitted an unsupported native operation.");
            }
        }
    }
}
