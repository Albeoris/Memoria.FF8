using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect338Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect338Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeEffect338(NativeBattleGraphicsState graphics) => RunNativeEffect338(graphics, true);
    private NativeBattleGraphicsState AdvanceNativeEffect338(NativeBattleGraphicsState graphics) => RunNativeEffect338(graphics, false);

    private NativeBattleGraphicsState RunNativeEffect338(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Effect338 requires its selected original effect.");
        NativeBattleEffect338Resources resources = effect.Binding.Effect338Resources ?? throw new NotSupportedException("Effect338 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect338 requires its current process memory.");
        NativeBattleEffect338Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect338 requires its current CRT random owner."), memory.Effect338 ?? throw new NotSupportedException("Effect338 requires its retained executable storage; explicitly migrate a needed older snapshot.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Effect338 requires its current temporary memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Effect338 requires its actual arithmetic constants.");
        NativeBattleEffect338Inputs inputs = new(colors, _ => Camera().CapturePackedView1D97778(), _ => Projection(), (_, pointer) => graphics.Models[Actor(pointer)], _ => nativeActionPackets, Execute, StartVibration, PollIo, (_, pointer) => PoseSource(pointer).BoneCount) { CapturePresentation = true };
        frame = initialize ? NativeBattleEffect338Controller.Initialize(frame, resources, inputs) : NativeBattleEffect338Controller.Advance(frame, resources, inputs);
        Publish(frame);
        if (initialize)
        {
            NativeCamera = Camera().RegisterControlResource(frame.Memory.Textures!.Animation);
        }
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Effect338 requires its current camera.");
        NativeBattleCameraProjection Projection() => Camera().Projection ?? throw new NotSupportedException("Effect338 requires its current projection.");
        NativeBattlePoseSource PoseSource(uint pointer) => NativeGraphicsPoseResource(graphics, Actor(pointer)).PoseSource ?? throw new NotSupportedException("Effect338 requires its actual current actor skeleton.");

        int Actor(uint pointer)
        {
            uint delta = unchecked(pointer - 0x1D972C0);
            if (delta % 156 != 0 || delta / 156 >= (uint)graphics.Models.Length)
            {
                throw new InvalidDataException("Effect338 requires an actual physical graphics-model row.");
            }
            return checked((int)(delta / 156));
        }

        void Publish(NativeBattleEffect338Frame current)
        {
            NativeEffectMemory = current.Memory with { Effect338 = current.State };
            nativeCrtRandom = current.Random;
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Effect338 lost its physical temporary memory.");
        }

        NativeBattleEffect338Frame Import(NativeBattleEffect338Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Effect338 service lost its process memory.");
            return current with { Memory = latest, State = latest.Effect338 ?? throw new InvalidDataException("Effect338 service lost its executable storage."), Random = nativeCrtRandom ?? throw new InvalidDataException("Effect338 service lost its CRT owner."), Staging = NativeFileStaging ?? throw new InvalidDataException("Effect338 service lost its temporary memory.") };
        }

        NativeBattleEffect338Frame QueueFile(NativeBattleEffect338Frame current, NativeBattleEffectFileRequest request)
        {
            NativeBattleEffectFileAsset file = resources.CompletionFile;
            if (file.FileId != request.FileId)
            {
                throw new NotSupportedException("Effect338 requires its selected original completion file.");
            }
            NativeBattleEffectArena arena = (current.Memory.Arena ?? throw new NotSupportedException("Effect338 reload requires its actual arena.")).Write(NativeBattleWaterExecutionFields.ArenaOffset(request.DestinationAddress, file.Data.Length), file.Data.AsSpan());
            NativeFileIo = (NativeFileIo ?? throw new NotSupportedException("Effect338 reload requires its current completion scheduler.")).Queue482610(file.Data.Length, request.CompletionCallback, null);
            return current with { Memory = current.Memory with { Arena = arena } };
        }

        NativeBattleEffect338IoPoll PollIo(NativeBattleEffect338Frame current, int index)
        {
            NativeBattleEffectIoPoll poll = NativeBattleEffectIo.Select5342C0(current.Memory, index);
            current = current with { Memory = poll.Memory };
            if (poll.Request is { } request)
            {
                current = QueueFile(current, request);
            }
            return new(current, poll.Busy);
        }

        NativeBattleEffect338VibrationStep StartVibration(NativeBattleEffect338Frame current)
        {
            NativeVibrationEnqueue queued = (NativeVibrationProcess ?? throw new NotSupportedException("Effect338 requires its actual vibration process.")).Enqueue(resources.Vibration, 1, 128);
            NativeVibrationProcess = queued.State;
            return new(current, queued.Handle);
        }

        NativeBattleEffect338Frame Deliver(NativeBattleEffect338Frame current, NativeBattleGfGeometryBatch batch, uint cursorGlobal, ushort flags)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect338 requires its current ordering table.");
            if (cursorGlobal == 0x1D8E054)
            {
                NativeBattleCommonPacketWrite ordinary = NativeBattleCommonPacketDelivery.Deliver(current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Effect338 requires its current ordinary COMMON process."), primitive.Pending, batch, flags);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = ordinary.Ordering } };
                return current with { Memory = current.Memory with { PrimitiveProcess = ordinary.Process } };
            }
            if (cursorGlobal != CursorGlobal)
            {
                throw new InvalidDataException("Effect338 selected an unsupported packet cursor owner.");
            }
            uint pointer = current.State.ReadDword(cursorGlobal);
            uint end = current.State.ReadDword(EndGlobal);
            if (pointer > end || (uint)batch.AllocationBytes > end - pointer)
            {
                throw new NotSupportedException("Effect338 packet allocation leaves its current physical bank.");
            }
            int cursor = NativeBattleWaterExecutionFields.ArenaOffset(pointer, batch.AllocationBytes);
            NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Effect338 requires its retained private packet arena.");
            NativeBattleGfGeometryBatch resolved = NativeBattlePacketBatchWords.ResolveWithKnownness(batch, cursor, NativeBattleEffectArena.Length, offset =>
            {
                (byte[] data, bool[] known) = arena.ReadSnapshot(offset, 1);
                return (data[0], known[0]);
            });
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, resolved, flags);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, State = current.State.WriteDword(cursorGlobal, checked(ArenaAddress + (uint)written.Cursor)), Effect = current.Effect with { DrawArenaOffset = written.Cursor } };
        }

        NativeBattleEffect338Frame ReadBone(NativeBattleEffect338Frame current, NativeBattleEffect338ReadBone502170 read)
        {
            NativeBattleGraphicsModel model = graphics.Models[Actor(read.ModelAddress)];
            NativeBattleVector point;
            if ((model.Flags0 & 2) == 0)
            {
                point = (model.Transform ?? throw new NotSupportedException("Effect338 bone query requires its actual placement.")).Position28;
            }
            else
            {
                NativeFileStaging = RequireStaging(current);
                NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, PoseSource(read.ModelAddress), checked((byte)read.Bone), 0);
                NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
                point = new(unchecked((short)observed.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)observed.ScriptedColorRegisters.Mac[1]!.Value), unchecked((short)observed.ScriptedColorRegisters.Mac[2]!.Value));
                current = current with { Memory = observed, Staging = NativeFileStaging };
            }
            return WriteBytes(current, read.Destination, PointView(point), 6);
        }

        NativeBattleEffect338Frame Bounds(NativeBattleEffect338Frame current, NativeBattleEffect338CaptureBounds6DFD90 capture)
        {
            int actor = Actor(capture.ModelAddress);
            NativeBattleGraphicsModel model = graphics.Models[actor];
            NativeBattlePoseSource source = PoseSource(capture.ModelAddress);
            model = NativeBattleBodyBoneMatrices.Compose5095B0(model, model.GeometryWorldMatrix ?? throw new NotSupportedException("Effect338 bounds require their current recipient world matrix."));
            (current, uint temporary) = Allocate(current, 24);
            NativeDeformedBodyBridge bridge = new(current.Memory, current.Effect, current.Random, graphics, model, RequireStaging(current), Context(current, temporary, 24), temporary);
            NativeBattleResourcePointer pointer = NativeDeformedGeometryPointer(bridge, false);
            NativeBattleEffect70BoundsStep captured = NativeBattleEffect70BoundsCapture.Capture6DFD90(current.Memory, bridge.Temporary, NativeBattleBodyBoneMatrices.Require(model, source).Select(matrix => NativeBattlePackedMatrix32.FromMatrix(matrix, null)).ToImmutableArray(), (offset, length) => ReadNativeDeformedGeometry(bridge, pointer, offset, length), model.SequenceState?.MeshMask124 ?? throw new NotSupportedException("Effect338 bounds require their current recipient object mask."), Projection());
            current = PublishContext(current with { Memory = captured.Memory }, temporary, captured.Temporary24, 24);
            current = Release(current, 24);
            (current, uint restore) = Allocate(current, 88);
            NativeBattleBodyBonePreparation restored = NativeBattleBodyBoneTemporary.Prepare508C90(model, source, Context(current, restore, 88));
            current = Release(PublishContext(current, restore, restored.Temporary58, 88), 88);
            graphics = graphics with { Models = graphics.Models.SetItem(actor, restored.Model) };
            NativeBattleEffectArena limits = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(captured.Maximum)).Write(2, BitConverter.GetBytes(captured.Minimum));
            return WriteBytes(current, capture.Destination, limits, 4);
        }

        NativeBattleEffect338Frame DrawModel(NativeBattleEffect338Frame current, uint address, NativeBattlePilotVisualIdentity? pilotIdentity = null)
        {
            NativeBattleModulePoolTask task = Read(current, address);
            NativeBattleEffect338Skeleton skeleton = current.State.Skeleton ?? throw new NotSupportedException("Effect338 model drawing requires its current embedded skeleton.");
            int count = skeleton.Storage.Read(0, 1)[0];
            NativeBattleDecodedPose pose = NativeBattleArenaPose.ReadCurrentPose(skeleton.Storage, 0, count);
            ImmutableArray<NativeBattleActionMatrix> local = Enumerable.Range(0, count).Select(index => NativeBattleArenaPose.ReadMatrix(skeleton.Storage, 32 + index * 48)).ToImmutableArray();
            NativeBattleActionMatrix world = new(Enumerable.Range(0, 9).Select(index => unchecked((short)task.ReadWord(0x64 + index * 2))).ToImmutableArray(), unchecked((int)task.ReadDword(0x78)), unchecked((int)task.ReadDword(0x7C)), unchecked((int)task.ReadDword(0x80)), 4096);
            NativeBattleActionMatrix basis = NativeBattleGfMatrices.Compose56C2F0(Camera().CapturePackedView1D97778().ReadMatrix(), world);
            ImmutableArray<NativeBattlePackedMatrix32> bones = NativeBattlePoseMatrices.ComposeStageCamera(local, basis, 0).Select(matrix => NativeBattlePackedMatrix32.FromMatrix(matrix, null)).ToImmutableArray();
            int scratchLength = checked(resources.Model.Geometry.Max(mesh => mesh.Vertices.Length) * 8);
            if (scratchLength > 0x4000)
            {
                throw new InvalidDataException("Effect338 body projection leaves its original scratch before IO.");
            }
            (current, uint temporary) = Allocate(current, 76);
            NativeBattleEffectArena temporary76 = NativeBattleEffectOwnedModelRendering.StoreMatrix(Context(current, temporary, 76), 0, NativeBattlePackedMatrix32.FromMatrix(basis, null));
            temporary76 = temporary76.Write(0x24, BitConverter.GetBytes(ArenaAddress + ScratchOffset)).Write(0x30, BitConverter.GetBytes(current.State.ReadDword(EndGlobal))).Write(0x34, new byte[4]).Write(0x38, BitConverter.GetBytes((short)320)).Write(0x3A, BitConverter.GetBytes((short)216)).Write(0x3C, BitConverter.GetBytes(task.ReadDword(0x4C))).Write(0x40, BitConverter.GetBytes(task.ReadDword(0xA0))).Write(0x44, new byte[2]).Write(0x48, new byte[4]);
            uint geometry = checked(resources.Model.Address + (uint)resources.Model.Section(1));
            NativeBattleEffectArena context = NativeBattleEffectNodeContextMemory.Copy(temporary76, 0x20, NativeBattleEffectArena.Clear().MarkUnknown(0, 44), 0, 44);
            NativeBattleModelContext44Step rendered = NativeBattleModelContext44.Draw(current.Memory, context, bones, ReadGeometry, geometry, ReadBytes(current, ArenaAddress + ScratchOffset, scratchLength), scratchLength, Projection(), colors, definition.NativeGraphics?.PrimitiveCapabilities?.HardwareEnabled ?? throw new NotSupportedException("Effect338 model drawing requires its actual hardware capability."), 17, 4, current.State.ReadDword(CursorGlobal), pilotIdentity: pilotIdentity);
            current = current with { Memory = rendered.Memory };
            temporary76 = NativeBattleEffectNodeContextMemory.Copy(rendered.Context44, 0, temporary76, 0x20, 44);
            current = PublishContext(current, temporary, temporary76, 76);
            current = WriteBytes(current, ArenaAddress + ScratchOffset, rendered.ProjectedScratch, scratchLength);
            current = Deliver(current, pilotIdentity.HasValue ? NativeBattleEffect338Presentation.PreserveNativeDepths(rendered.Batch) : rendered.Batch, CursorGlobal, 0);
            (current, uint restore) = Allocate(current, 88);
            skeleton = skeleton.Publish(pose);
            ImmutableArray<NativeBattleActionMatrix> restoredBones = Enumerable.Range(0, count).Select(index => NativeBattleArenaPose.ReadMatrix(skeleton.Storage, 32 + index * 48)).ToImmutableArray();
            NativeBattleEffectArena restored = NativeBattleBodyBoneTemporary.Prepare508C90(resources.Model.PoseSource, pose, restoredBones, Context(current, restore, 88));
            current = PublishContext(current, restore, restored, 88);
            current = current with { State = current.State with { Skeleton = skeleton } };
            return Release(Release(current, 88), 76);
        }

        byte[] ReadGeometry(int offset, int length)
        {
            if (offset < 0 || length < 0 || offset > 0x3730 - 0x568 - length)
            {
                throw new InvalidDataException("Effect338 body read leaves its current original geometry section.");
            }
            return resources.Model.Bytes.AsSpan().Slice(0x568 + offset, length).ToArray();
        }

        NativeBattleEffect338Frame Execute(NativeBattleEffect338Frame current, NativeBattleEffect338Operation operation)
        {
            switch (operation)
            {
                case NativeBattleEffect338Stage5713E0 intensity:
                    NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Effect338 requires its actual stage.");
                    NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = unchecked((short)intensity.Intensity), ColorFlags = 0 }).ToImmutableArray() };
                    return current;
                case NativeBattleEffect338SelectSequence505C00 sequence:
                    graphics = ScheduleNativeRingSequence505C00(graphics, checked((byte)Actor(sequence.ModelAddress)), sequence.Sequence);
                    return current;
                case NativeBattleEffect338Contact506690 contact:
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.ContactIndex)] });
                    graphics = graphics with { PendingContacts = pending };
                    return Import(current);
                case NativeBattleEffect338ReadBone502170 read:
                    return ReadBone(current, read);
                case NativeBattleEffect338CaptureBounds6DFD90 capture:
                    return Bounds(current, capture);
                case NativeBattleEffect338DrawModel6A79B0 body:
                    return DrawModel(current, body.TaskAddress, body.PilotIdentity);
                case NativeBattleEffect338Sound501330 sound:
                    Publish(current);
                    PublishNativeModuleSound(new(sound.Packed, sound.Flags, null) { Pan = sound.Pan });
                    return Import(current);
                case NativeBattleEffect338StopVibration4A2940 vibration:
                    NativeVibrationProcess = (NativeVibrationProcess ?? throw new NotSupportedException("Effect338 requires its retained vibration process.")).Stop4A2940(vibration.Handle);
                    return current;
                case NativeBattleEffect338InitializeIo534110 io:
                    return current with { Memory = NativeBattleEffectIo.Initialize534110(current.Memory, io.ArenaOffset) };
                case NativeBattleEffect338QueueFile5341D0 file:
                    return current with { Memory = NativeBattleEffectIo.Queue5341D0(current.Memory, file.FileId, file.DestinationAddress, file.Type) };
                case NativeBattleEffect338AdvanceIo534210:
                    NativeBattleEffectIoAdvance advanced = NativeBattleEffectIo.Advance534210(current.Memory);
                    current = current with { Memory = advanced.Memory };
                    return advanced.Request is { } request ? QueueFile(current, request) : current;
                case NativeBattleEffect338DeliverBatch packets:
                    return Deliver(current, packets.Batch, packets.CursorGlobal, packets.Flags);
                default:
                    throw new NotSupportedException("Effect338 emitted an unsupported native operation.");
            }
        }
    }
}
