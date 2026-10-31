using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using static Memoria.FF8.Runtime.NativeBattleEffect314Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeEffect314(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Effect314 requires its current effect.");
        effect = effect with { PresentationPackets = [] };
        NativeBattleEffect314Resources resources = effect.Binding.Effect314Resources ?? throw new NotSupportedException("Effect314 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect314 requires its current process memory.");
        NativeBattleEffect314Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect314 requires its current CRT stream."), memory.Effect314 ?? throw new NotSupportedException("Effect314 requires its retained state.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Effect314 requires its current addressed memory."), CapturePresentation = true };
        Dictionary<uint, long> presentationResources = [];
        NativeBattleEffect314Inputs inputs = new(definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Effect314 requires its arithmetic constants."), _ => nativeActionPackets, (_, address) => graphics.Models[NativeEffect314Actor(graphics, address)], _ => Camera().CapturePackedView1D97778(), _ => Camera().Projection ?? throw new NotSupportedException("Effect314 requires its current projection."), _ => Camera().Roll, ReadArena, GeometryAddress, BoneAddress, Allocate, Release, Execute) { PresentationResourceGeneration = pointer => presentationResources.TryGetValue(pointer, out long generation) ? generation : null, ReadBoneCount = (_, pointer) => Pose(NativeEffect314Actor(graphics, pointer)).BoneCount, ReadBody = ReadBody, PollIo = PollIo };
        frame = initialize ? NativeBattleEffect314Controller.Initialize(frame, resources, inputs) : NativeBattleEffect314Controller.Advance(frame, resources, inputs);
        PublishNativeEffect314Frame(frame);
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Effect314 requires its current camera.");
        NativeBattlePoseSource Pose(int actor)
        {
            var resource = NativeGraphicsPoseResource(graphics, actor);
            return resource.PoseSource ?? throw new NotSupportedException("Effect314 requires its current physical skeleton.");
        }
        NativeBattleModelCpuResources Cpu() => NativeModelCpuResources ?? throw new NotSupportedException("Effect314 requires its current CPU resource owner.");
        NativeBattleModelCpuResourceRow Row(int actor) => Cpu().Rows[graphics.Models[actor].ActiveResourceRow100 ?? throw new NotSupportedException("Effect314 requires its current resource row.")];
        NativeBattleEffect314AddressStep Allocate(NativeBattleEffect314Frame current, int length)
        {
            NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(current.Staging!, length);
            return new(current with { Staging = allocation.Staging }, allocation.Address);
        }
        NativeBattleEffect314Frame Release(NativeBattleEffect314Frame current, int length) => current with { Staging = NativeBattleTemporaryMemory.Release5082D0(current.Staging!, length) };
        uint GeometryAddress(NativeBattleEffect314Frame current, uint address)
        {
            int actor = NativeEffect314Actor(graphics, address);
            NativeDeformedBodyBridge bridge = new(current.Memory, current.Effect, current.Random, graphics, graphics.Models[actor], current.Staging!, NativeBattleEffectArena.Clear(), 0);
            NativeBattleResourcePointer pointer = NativeDeformedGeometryPointer(bridge, false);
            presentationResources[address] = NativeBattleEffect32Presentation.ResourceEpoch(pointer);
            return pointer.Cpu?.PhysicalAddress ?? pointer.StagingAddress ?? throw new NotSupportedException("Effect314 requires its actual addressed geometry.");
        }
        uint BoneAddress(NativeBattleEffect314Frame current, uint address, int bone)
        {
            int actor = NativeEffect314Actor(graphics, address);
            ImmutableArray<NativeBattleActionMatrix> matrices = NativeBattleBodyBoneMatrices.Require(graphics.Models[actor], Pose(actor));
            if ((uint)bone >= matrices.Length)
            {
                throw new InvalidDataException("Effect314 bone index leaves its current skeleton.");
            }
            return checked(Row(actor).Require(12).PhysicalAddress + 32 + (uint)bone * 48);
        }
        NativeBattleEffect314Frame QueueFile(NativeBattleEffect314Frame current, NativeBattleEffectFileRequest request)
        {
            NativeBattleEffectFileAsset file = resources.CompletionFiles.Single(file => file.FileId == request.FileId);
            NativeBattleEffectArena arena = (current.Memory.Arena ?? throw new NotSupportedException("Effect314 requires its current file destination owner.")).Write(NativeBattleWaterExecutionFields.ArenaOffset(request.DestinationAddress, file.Data.Length), file.Data.AsSpan());
            NativeFileIo = (NativeFileIo ?? throw new NotSupportedException("Effect314 requires its current file completion scheduler.")).Queue482610(file.Data.Length, request.CompletionCallback, null);
            return current with { Memory = current.Memory with { Arena = arena } };
        }
        NativeBattleEffect314IoStep PollIo(NativeBattleEffect314Frame current, int index)
        {
            NativeBattleEffectIoPoll poll = NativeBattleEffectIo.Select5342C0(current.Memory, index);
            current = current with { Memory = poll.Memory };
            return new(poll.Request is { } request ? QueueFile(current, request) : current, poll.Busy);
        }
        NativeBattleEffect298BodySource ReadBody(NativeBattleEffect314Frame current, uint pointer)
        {
            int actor = NativeEffect314Actor(graphics, pointer);
            NativeBattlePoseSource pose = Pose(actor);
            NativeBattleDecodedPose decoded = NativeBattlePoseMatrices.ReadDecodedPose(graphics.Models[actor].SequenceState ?? throw new NotSupportedException("Effect314 requires its current decoded pose."), pose);
            ImmutableArray<NativeBattlePackedMatrix32>.Builder bones = ImmutableArray.CreateBuilder<NativeBattlePackedMatrix32>(pose.BoneCount);
            for (int bone = 0; bone < pose.BoneCount; bone++)
            {
                NativeBattleEffectArena record = ReadArena(current, BoneAddress(current, pointer, bone), 32);
                (byte[] bytes, bool[] known) = record.ReadSnapshot(0, 32);
                bones.Add(new(bytes.ToImmutableArray(), known.ToImmutableArray()));
            }
            uint geometry = GeometryAddress(current, pointer);
            int length = checked((Cpu().ScratchMaximum ?? throw new NotSupportedException("Effect314 requires its current loaded projection capacity.")) * 8);
            return new(pose, decoded, bones.MoveToImmutable(), geometry, (address, count) => ReadArena(current, address, count)) { VertexScratchAddress = checked(0x196B018u - (uint)length), VertexScratchLength = length, PresentationResourceGeneration = presentationResources.TryGetValue(pointer, out long epoch) ? epoch : null };
        }
        NativeBattleEffect314Frame WriteArena(NativeBattleEffect314Frame current, uint address, NativeBattleEffectArena data, int length)
        {
            long modelRelative = (long)address - 0x1D972C0;
            if (modelRelative >= 0 && modelRelative < graphics.Models.Length * 156L)
            {
                int actor = checked((int)(modelRelative / 156));
                int offset = checked((int)(modelRelative % 156));
                if (length > 156 - offset)
                {
                    throw new InvalidDataException("Effect314 model store crosses its physical row.");
                }
                NativeBattleGraphicsModel model = graphics.Models[actor];
                if (offset >= 0x28 && offset + length <= 0x2C || offset >= 0x2C && offset + length <= 0x30)
                {
                    bool firstColor = offset < 0x2C;
                    int colorOffset = firstColor ? 0x28 : 0x2C;
                    uint color = length == 4 ? 0 : (firstColor ? model.Color40 : model.Color44) ?? throw new NotSupportedException("Effect314 partial color store requires its current packed color.");
                    byte[] bytes = BitConverter.GetBytes(color);
                    data.Read(0, length).CopyTo(bytes, offset - colorOffset);
                    uint updated = BinaryPrimitives.ReadUInt32LittleEndian(bytes);
                    model = firstColor ? model with { Color40 = updated } : model with { Color44 = updated };
                }
                else
                {
                    model = ApplyNativeEffect301ModelBytes(model, offset, data, length);
                }
                graphics = graphics with { Models = graphics.Models.SetItem(actor, model) };
                return current;
            }
            for (int actor = 0; actor < graphics.Models.Length; actor++)
            {
                NativeBattleGraphicsModel model = graphics.Models[actor];
                if (model.CurrentBodyBoneMatrices is not { } bones || model.ActiveResourceRow100 is null)
                {
                    continue;
                }
                uint first = checked(Row(actor).Require(12).PhysicalAddress + 32);
                long relative = (long)address - first;
                if (relative >= 0 && relative < bones.Matrices.Length * 48 && relative % 48 == 0 && length == 32)
                {
                    (byte[] bytes, bool[] known) = data.ReadSnapshot(0, 32);
                    NativeBattleActionMatrix matrix = new NativeBattlePackedMatrix32(bytes.ToImmutableArray(), known.ToImmutableArray()).ReadMatrix();
                    graphics = graphics with { Models = graphics.Models.SetItem(actor, model with { CurrentBodyBoneMatrices = bones with { Matrices = bones.Matrices.SetItem(checked((int)(relative / 48)), matrix) } }) };
                    break;
                }
            }
            return WriteNativeEffect314Arena(current, address, data, length);
        }
        NativeBattleEffectArena ReadArena(NativeBattleEffect314Frame current, uint address, int length) => ReadNativeEffect314Arena(graphics, current, address, length);
        NativeBattleEffect314Frame Execute(NativeBattleEffect314Frame current, NativeBattleEffect314Operation operation)
        {
            switch (operation)
            {
                case NativeBattleEffect314UploadTim505E30 upload:
                    NativeBattleEffectHeapAllocation tim = new(resources.MainTim.Name, NativeBattleEffect314Controller.ReadBytes(current, upload.SourceAddress, resources.MainTim.Data.Length).Read(0, resources.MainTim.Data.Length).ToImmutableArray());
                    return current with { Memory = current.Memory with { Textures = (current.Memory.Textures ?? throw new NotSupportedException("Effect314 requires its texture scheduler.")).EnqueueTim505E30(tim).State } };
                case NativeBattleEffect314RegisterCamera5099A0 registration when registration.SourceAddress == CameraAddress:
                    NativeBattleEffectTextureState textures = (current.Memory.Textures ?? throw new NotSupportedException("Effect314 requires its texture scheduler.")).Register5099A0(resources.Camera);
                    NativeCamera = Camera().RegisterControlResource(textures.Animation);
                    return current with { Memory = current.Memory with { Textures = textures } };
                case NativeBattleEffect314WriteModel write:
                    return WriteArena(current, checked(write.ModelAddress + (uint)write.Offset), write.Data, write.Length);
                case NativeBattleEffect314ModelFlags flags:
                    int physical = NativeEffect314Actor(graphics, flags.ModelAddress);
                    NativeBattleGraphicsModel selected = graphics.Models[physical];
                    ushort updated = flags.Width switch
                    {
                        1 => unchecked((ushort)((selected.Flags0 & 0xFF00) | (byte)(((byte)selected.Flags0 & (byte)flags.AndMask) | (byte)flags.OrMask))),
                        2 => unchecked((ushort)((selected.Flags0 & flags.AndMask) | flags.OrMask)),
                        _ => throw new InvalidDataException("Effect314 requires its actual model flags store width.")
                    };
                    graphics = graphics with { Models = graphics.Models.SetItem(physical, selected with { Flags0 = updated }) };
                    return current;
                case NativeBattleEffect314WriteArena write:
                    return WriteArena(current, write.Address, write.Data, write.Length);
                case NativeBattleEffect314PublishCursors cursors:
                    NativeBattlePrimitiveProcess process = current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Effect314 requires its current packet process.");
                    int offset = checked((int)(cursors.CommonCursor - NativeEffect314CommonBase(process)));
                    if (offset < 0 || offset > process.CommonPacketWriteCapacity)
                    {
                        throw new InvalidDataException("Effect314 COMMON cursor leaves its current physical bank.");
                    }
                    return current with { Memory = current.Memory with { PrimitiveProcess = process with { ByteCursor = offset } }, State = current.State.WriteDword(CursorGlobal, cursors.PrivateCursor) };
                case NativeBattleEffect314Prepare5095B0 prepare:
                    int preparedActor = NativeEffect314Actor(graphics, prepare.ModelAddress);
                    NativeBattleGraphicsModel preparedModel = graphics.Models[preparedActor];
                    graphics = graphics with { Models = graphics.Models.SetItem(preparedActor, NativeBattleBodyBoneMatrices.Compose5095B0(preparedModel, preparedModel.GeometryWorldMatrix ?? throw new NotSupportedException("Effect314 requires its current model world matrix."))) };
                    return current;
                case NativeBattleEffect314Restore508C90 restore:
                    int restoredActor = NativeEffect314Actor(graphics, restore.ModelAddress);
                    NativeBattleEffect314AddressStep allocation = Allocate(current, 88);
                    NativeBattleBodyBonePreparation restored = NativeBattleBodyBoneTemporary.Prepare508C90(graphics.Models[restoredActor], Pose(restoredActor), ReadArena(allocation.Frame, allocation.Address, 88));
                    graphics = graphics with { Models = graphics.Models.SetItem(restoredActor, restored.Model) };
                    return Release(WriteNativeEffect314Arena(allocation.Frame, allocation.Address, restored.Temporary58, 88), 88);
                case NativeBattleEffect314ClearGeometryMarkers6E5D20 clear:
                    NativeBattleEffect314AddressStep markerAllocation = Allocate(current, 8);
                    current = markerAllocation.Frame;
                    uint geometry = GeometryAddress(current, clear.ModelAddress);
                    NativeBattleEffectArena temporary = NativeBattleEffect64GeometryCleanup.Clear(geometry, (address, length) => ReadArena(current, address, length), (address, value) => current = WriteNativeEffect314Arena(current, address, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(value)), 2), ReadArena(current, markerAllocation.Address, 8));
                    return Release(WriteNativeEffect314Arena(current, markerAllocation.Address, temporary, 8), 8);
                case NativeBattleEffect314Shadow5088A0 shadow:
                    int shadowActor = NativeEffect314Actor(graphics, shadow.ModelAddress);
                    var shadowSource = definition.NativeGraphics?.Rules.ShadowSource ?? throw new NotSupportedException("Effect314 requires its original shadow template.");
                    NativeBattleShadowDrawStep drawn = NativeBattleShadowDrawing.Prepare5088A0(current.Staging!, new(current.Memory.GeometryRegisters ?? throw new NotSupportedException("Effect314 shadow requires its current geometry registers."), current.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect314 shadow requires its current arithmetic registers.")), graphics.Models[shadowActor], Camera().Projection ?? throw new NotSupportedException("Effect314 requires its current projection."), current.Memory.ScriptedGraphics ?? throw new NotSupportedException("Effect314 shadow requires its current depth controls."), shadowSource, definition.NativeGraphics?.PrimitiveCapabilities?.GouraudAlpha ?? throw new NotSupportedException("Effect314 requires its primitive capabilities."), shadow.OrderingBucket, checked((byte)shadow.Count), current.PilotIdentity is { } owner ? owner with { ResourceGeneration = NativeBattlePilotVisualLifetimes.EpochFor(shadowSource), Primitive = unchecked(owner.Primitive * 397 + 0x5088) } : null);
                    current = current with { Memory = current.Memory with { GeometryRegisters = drawn.Registers.Geometry, ScriptedColorRegisters = drawn.Registers.Colors }, Staging = NativeBattleTemporaryMemory.PublishView(drawn.Staging, drawn.TemporaryAddress, drawn.Temporary78, 0x78) };
                    current = DeliverNativeEffect314Batch(ref graphics, current, drawn.Batch, 0x1D8E054);
                    return Release(current, 0x78);
                case NativeBattleEffect314Blend56CC00 blend:
                    byte[] first = ReadArena(current, blend.Source1Address, 3).Read(0, 3);
                    byte[] second = ReadArena(current, blend.Source2Address, 3).Read(0, 3);
                    uint firstRgb = (uint)(first[0] | first[1] << 8 | first[2] << 16);
                    uint secondRgb = (uint)(second[0] | second[1] << 8 | second[2] << 16);
                    NativeBattleWeightedColorStep color = NativeBattleWeightedColor56CC00.Blend(current.Memory, firstRgb, secondRgb, unchecked((short)blend.Weight1), unchecked((short)blend.Weight2), 0, definition.NativeGraphics!.Rules.SummonColors);
                    current = current with { Memory = color.Memory };
                    return WriteArena(current, blend.DestinationAddress, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(color.Color).AsSpan(0, 3)), 3);
                case NativeBattleEffect314Music47E3C0 music:
                    if ((ResolvedNativeFlags & 2) == 0)
                    {
                        ushort channel = NativeMusicProcess?.ReturnChannel ?? throw new NotSupportedException("Effect314 requires its music return channel.");
                        NativeGlobalSoundProcess sound = NativeGlobalSoundProcess ?? throw new NotSupportedException("Effect314 requires its sound process.");
                        NativeGlobalSoundProcess = sound with { MusicPlayback = (sound.MusicPlayback ?? throw new NotSupportedException("Effect314 requires its music playback.")).FadeGain46BBC0(unchecked((uint)(short)channel), unchecked((byte)music.Duration), 0) };
                    }
                    return current;
                case NativeBattleEffect314Vibration4A29A0 vibration when vibration.SourceAddress == VibrationAddress:
                    NativeVibrationBank bank = new(Convert.ToHexString(SHA256.HashData(resources.Vibration.Data.AsSpan())), [checked((int)BinaryPrimitives.ReadUInt32LittleEndian(resources.Vibration.Data.AsSpan())), checked((int)BinaryPrimitives.ReadUInt32LittleEndian(resources.Vibration.Data.AsSpan().Slice(4)))], resources.Vibration.Data);
                    NativeVibrationEnqueue queued = (NativeVibrationProcess ?? throw new NotSupportedException("Effect314 requires its vibration process.")).Enqueue(bank, vibration.Mode, vibration.Magnitude);
                    NativeVibrationProcess = queued.State;
                    return NativeBattleEffect314Controller.WriteBytes(current, vibration.OutputAddress, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(queued.Handle)), 4);
                case NativeBattleEffect314StopVibration4A2940 vibration:
                    NativeVibrationProcess = (NativeVibrationProcess ?? throw new NotSupportedException("Effect314 requires its vibration process.")).Stop4A2940(vibration.Handle);
                    return current;
                case NativeBattleEffect314Stage5713E0 stage:
                    NativeBattleStageState stageState = NativeStage ?? throw new NotSupportedException("Effect314 requires its current stage.");
                    NativeStage = stageState with { Groups = stageState.Groups.Select(group => group with { ColorDepth = unchecked((short)stage.Amount), ColorFlags = stage.Flags }).ToImmutableArray() };
                    return current;
                case NativeBattleEffect314Sound501330 sound:
                    PublishNativeEffect314Frame(current);
                    PublishNativeModuleSound(new(BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(sound.SourceAddress, 4)), sound.Flags, null) { Pan = sound.Pan });
                    return ImportNativeEffect314Frame(current);
                case NativeBattleEffect314DrawBatch draw:
                    return DeliverNativeEffect314Batch(ref graphics, current, draw.Batch, draw.CursorGlobal, draw.Flags);
                case NativeBattleEffect314Prepare662C00 pose:
                    int poseActor = NativeEffect314Actor(graphics, pose.ModelAddress);
                    NativeBattlePoseSource poseSource = Pose(poseActor);
                    NativeBattleEffectArena offsets = ReadArena(current, pose.BoneOffsetsAddress, checked(poseSource.BoneCount * 16));
                    NativeBattleEffect314AddressStep poseAllocation = Allocate(current, 96);
                    NativeBattleEffect298PosePreparation posePrepared = NativeBattleEffect298Pose.Prepare662C00(graphics.Models[poseActor], poseSource, offsets, ReadArena(poseAllocation.Frame, poseAllocation.Address, 96));
                    graphics = graphics with { Models = graphics.Models.SetItem(poseActor, posePrepared.Model) };
                    return Release(WriteArena(poseAllocation.Frame, poseAllocation.Address, posePrepared.Temporary96, 96), 96);
                case NativeBattleEffect314BonePoint502170 bone:
                    int boneActor = NativeEffect314Actor(graphics, bone.ModelAddress);
                    NativeFileStaging = current.Staging;
                    NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[boneActor], Pose(boneActor), checked((byte)bone.Bone), checked((short)bone.Angle));
                    NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
                    current = current with { Memory = observed, Staging = NativeFileStaging };
                    NativeBattleVector point = producer.Enabled ? new(unchecked((short)observed.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[1]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[2]!.Value)) : producer.DisabledPosition;
                    return WriteArena(current, bone.DestinationAddress, NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z)), 6);
                case NativeBattleEffect314InitializeIo534110 io:
                    return current with { Memory = NativeBattleEffectIo.Initialize534110(current.Memory, checked((int)(io.Address - NativeBattleEffectNodeMemory.ArenaAddress))) };
                case NativeBattleEffect314QueueIo5341D0 file:
                    return current with { Memory = NativeBattleEffectIo.Queue5341D0(current.Memory, file.FileId, file.DestinationAddress, file.Type) };
                case NativeBattleEffect314AdvanceIo534210:
                    NativeBattleEffectIoAdvance advanced = NativeBattleEffectIo.Advance534210(current.Memory);
                    current = current with { Memory = advanced.Memory };
                    return advanced.Request is { } request ? QueueFile(current, request) : current;
                default:
                    throw new NotSupportedException($"Effect314 emitted unsupported service {operation.GetType().Name}.");
            }
        }
    }

    private static int NativeEffect314Actor(NativeBattleGraphicsState graphics, uint address)
    {
        long offset = (long)address - 0x1D972C0;
        if (offset < 0 || offset % 156 != 0 || offset / 156 >= graphics.Models.Length)
        {
            throw new NotSupportedException("Effect314 requires an actual current physical model address.");
        }
        return checked((int)(offset / 156));
    }

    private void PublishNativeEffect314Frame(NativeBattleEffect314Frame frame)
    {
        NativeEffectMemory = frame.Memory with { Effect314 = frame.State };
        NativeFileStaging = frame.Staging ?? throw new NotSupportedException("Effect314 lost its current addressed memory.");
        nativeCrtRandom = frame.Random;
    }

    private NativeBattleEffect314Frame ImportNativeEffect314Frame(NativeBattleEffect314Frame frame)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect314 lost its process memory.");
        return frame with { Memory = memory, State = memory.Effect314 ?? throw new NotSupportedException("Effect314 lost its retained state."), Staging = NativeFileStaging, Random = nativeCrtRandom ?? throw new NotSupportedException("Effect314 lost its CRT stream.") };
    }
}
