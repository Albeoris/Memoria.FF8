using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using static Memoria.FF8.Runtime.NativeBattleEffect316Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeEffect316(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Effect316 requires its current effect.");
        NativeBattleEffect316Resources resources = effect.Binding.Effect316Resources ?? throw new NotSupportedException("Effect316 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect316 requires its current process memory.");
        NativeBattleEffect316Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect316 requires its current CRT stream."), memory.Effect316 ?? throw new NotSupportedException("Effect316 requires its retained state.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Effect316 requires its current addressed memory.") };
        NativeBattleEffect316Inputs inputs = null!;
        inputs = new(definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Effect316 requires its arithmetic constants."), _ => nativeActionPackets, (_, address) => graphics.Models[NativeEffect316Actor(graphics, address)], _ => Camera().CapturePackedView1D97778(), _ => Camera().Projection ?? throw new NotSupportedException("Effect316 requires its current projection."), _ => Camera().Roll, ReadArena, GeometryAddress, BoneAddress, Allocate, Release, Execute);
        frame = initialize ? NativeBattleEffect316Controller.Initialize(frame, resources, inputs) : NativeBattleEffect316Controller.Advance(frame, resources, inputs);
        PublishNativeEffect316Frame(frame);
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Effect316 requires its current camera.");
        NativeBattlePoseSource Pose(int actor) => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect316 requires its current physical skeleton.");
        NativeBattleModelCpuResources Cpu() => NativeModelCpuResources ?? throw new NotSupportedException("Effect316 requires its current CPU resource owner.");
        NativeBattleModelCpuResourceRow Row(int actor) => Cpu().Rows[graphics.Models[actor].ActiveResourceRow100 ?? throw new NotSupportedException("Effect316 requires its current resource row.")];
        NativeBattleEffect316AddressStep Allocate(NativeBattleEffect316Frame current, int length)
        {
            NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(current.Staging!, length);
            return new(current with { Staging = allocation.Staging }, allocation.Address);
        }
        NativeBattleEffect316Frame Release(NativeBattleEffect316Frame current, int length) => current with { Staging = NativeBattleTemporaryMemory.Release5082D0(current.Staging!, length) };
        uint GeometryAddress(NativeBattleEffect316Frame current, uint address)
        {
            int actor = NativeEffect316Actor(graphics, address);
            NativeDeformedBodyBridge bridge = new(current.Memory, current.Effect, current.Random, graphics, graphics.Models[actor], current.Staging!, NativeBattleEffectArena.Clear(), 0);
            NativeBattleResourcePointer pointer = NativeDeformedGeometryPointer(bridge, false);
            return pointer.Cpu?.PhysicalAddress ?? pointer.StagingAddress ?? throw new NotSupportedException("Effect316 requires its actual addressed geometry.");
        }
        uint BoneAddress(NativeBattleEffect316Frame current, uint address, int bone)
        {
            int actor = NativeEffect316Actor(graphics, address);
            ImmutableArray<NativeBattleActionMatrix> matrices = NativeBattleBodyBoneMatrices.Require(graphics.Models[actor], Pose(actor));
            if ((uint)bone >= matrices.Length)
            {
                throw new InvalidDataException("Effect316 bone index leaves its current skeleton.");
            }
            return checked(Row(actor).Require(12).PhysicalAddress + 32 + (uint)bone * 48);
        }
        NativeBattleEffectArena ReadArena(NativeBattleEffect316Frame current, uint address, int length) => ReadNativeEffect316Arena(graphics, current, address, length);
        NativeBattleEffect316Frame Execute(NativeBattleEffect316Frame current, NativeBattleEffect316Operation operation)
        {
            switch (operation)
            {
                case NativeBattleEffect316UploadTim505E30 upload:
                    NativeBattleEffectHeapAllocation tim = upload.SourceAddress == EmbeddedTimAddress ? new("texture", resources.EmbeddedTim) : new(resources.MainTim.Name, NativeBattleEffect316Controller.ReadBytes(current, upload.SourceAddress, resources.MainTim.Data.Length).Read(0, resources.MainTim.Data.Length).ToImmutableArray());
                    return current with { Memory = current.Memory with { Textures = (current.Memory.Textures ?? throw new NotSupportedException("Effect316 requires its texture scheduler.")).EnqueueTim505E30(tim).State } };
                case NativeBattleEffect316RegisterCamera5099A0 registration when registration.SourceAddress == CameraAddress:
                    NativeBattleEffectTextureState textures = (current.Memory.Textures ?? throw new NotSupportedException("Effect316 requires its texture scheduler.")).Register5099A0(resources.Camera);
                    NativeCamera = Camera().RegisterControlResource(textures.Animation);
                    return current with { Memory = current.Memory with { Textures = textures } };
                case NativeBattleEffect316WriteModel write:
                    int actor = NativeEffect316Actor(graphics, write.ModelAddress);
                    NativeBattleGraphicsModel model = graphics.Models[actor];
                    model = write.Offset == 0x2C && write.Length == 4 ? model with { Color44 = BinaryPrimitives.ReadUInt32LittleEndian(write.Data.Read(0, 4)) } : ApplyNativeEffect301ModelBytes(model, write.Offset, write.Data, write.Length);
                    graphics = graphics with { Models = graphics.Models.SetItem(actor, model) };
                    return current;
                case NativeBattleEffect316ModelFlags flags:
                    int physical = NativeEffect316Actor(graphics, flags.ModelAddress);
                    NativeBattleGraphicsModel selected = graphics.Models[physical];
                    ushort updated = flags.Width switch
                    {
                        1 => unchecked((ushort)((selected.Flags0 & 0xFF00) | (byte)(((byte)selected.Flags0 & (byte)flags.AndMask) | (byte)flags.OrMask))),
                        2 => unchecked((ushort)((selected.Flags0 & flags.AndMask) | flags.OrMask)),
                        _ => throw new InvalidDataException("Effect316 requires its actual model flags store width.")
                    };
                    graphics = graphics with { Models = graphics.Models.SetItem(physical, selected with { Flags0 = updated }) };
                    return current;
                case NativeBattleEffect316WriteArena write:
                    return WriteNativeEffect316Arena(current, write.Address, write.Data, write.Length);
                case NativeBattleEffect316PublishCursors cursors:
                    NativeBattlePrimitiveProcess process = current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Effect316 requires its current packet process.");
                    int offset = checked((int)(cursors.CommonCursor - NativeEffect316CommonBase(process)));
                    if (offset < 0 || offset > process.CommonPacketWriteCapacity)
                    {
                        throw new InvalidDataException("Effect316 COMMON cursor leaves its current physical bank.");
                    }
                    return current with { Memory = current.Memory with { PrimitiveProcess = process with { ByteCursor = offset } }, State = current.State.WriteDword(CursorGlobal, cursors.PrivateCursor) };
                case NativeBattleEffect316Prepare5095B0 prepare:
                    int preparedActor = NativeEffect316Actor(graphics, prepare.ModelAddress);
                    NativeBattleGraphicsModel preparedModel = graphics.Models[preparedActor];
                    graphics = graphics with { Models = graphics.Models.SetItem(preparedActor, NativeBattleBodyBoneMatrices.Compose5095B0(preparedModel, preparedModel.GeometryWorldMatrix ?? throw new NotSupportedException("Effect316 requires its current model world matrix."))) };
                    return current;
                case NativeBattleEffect316Restore508C90 restore:
                    int restoredActor = NativeEffect316Actor(graphics, restore.ModelAddress);
                    NativeBattleEffect316AddressStep allocation = Allocate(current, 88);
                    NativeBattleBodyBonePreparation restored = NativeBattleBodyBoneTemporary.Prepare508C90(graphics.Models[restoredActor], Pose(restoredActor), ReadArena(allocation.Frame, allocation.Address, 88));
                    graphics = graphics with { Models = graphics.Models.SetItem(restoredActor, restored.Model) };
                    return Release(WriteNativeEffect316Arena(allocation.Frame, allocation.Address, restored.Temporary58, 88), 88);
                case NativeBattleEffect316ClearGeometryMarkers6612C0 clear:
                    NativeBattleEffect316AddressStep markerAllocation = Allocate(current, 8);
                    current = markerAllocation.Frame;
                    uint geometry = GeometryAddress(current, clear.ModelAddress);
                    NativeBattleEffectArena temporary = NativeBattleEffect64GeometryCleanup.Clear(geometry, (address, length) => ReadArena(current, address, length), (address, value) => current = WriteNativeEffect316Arena(current, address, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(value)), 2), ReadArena(current, markerAllocation.Address, 8));
                    return Release(WriteNativeEffect316Arena(current, markerAllocation.Address, temporary, 8), 8);
                case NativeBattleEffect316Shadow5088A0 shadow:
                    int shadowActor = NativeEffect316Actor(graphics, shadow.ModelAddress);
                    NativeBattleShadowDrawStep drawn = NativeBattleShadowDrawing.Prepare5088A0(current.Staging!, new(current.Memory.GeometryRegisters ?? throw new NotSupportedException("Effect316 shadow requires its current geometry registers."), current.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect316 shadow requires its current arithmetic registers.")), graphics.Models[shadowActor], inputs.ReadProjection(current), current.Memory.ScriptedGraphics ?? throw new NotSupportedException("Effect316 shadow requires its current depth controls."), definition.NativeGraphics?.Rules.ShadowSource ?? throw new NotSupportedException("Effect316 requires its original shadow template."), definition.NativeGraphics?.PrimitiveCapabilities?.GouraudAlpha ?? throw new NotSupportedException("Effect316 requires its primitive capabilities."), shadow.OrderingBucket, checked((byte)shadow.Count));
                    current = current with { Memory = current.Memory with { GeometryRegisters = drawn.Registers.Geometry, ScriptedColorRegisters = drawn.Registers.Colors }, Staging = NativeBattleTemporaryMemory.PublishView(drawn.Staging, drawn.TemporaryAddress, drawn.Temporary78, 0x78) };
                    current = DeliverNativeEffect316Batch(ref graphics, current, drawn.Batch, 0x1D8E054);
                    return Release(current, 0x78);
                case NativeBattleEffect316Blend56CC00 blend:
                    byte[] first = ReadArena(current, blend.Source1Address, 3).Read(0, 3);
                    byte[] second = ReadArena(current, blend.Source2Address, 3).Read(0, 3);
                    uint firstRgb = (uint)(first[0] | first[1] << 8 | first[2] << 16);
                    uint secondRgb = (uint)(second[0] | second[1] << 8 | second[2] << 16);
                    NativeBattleWeightedColorStep color = NativeBattleWeightedColor56CC00.Blend(current.Memory, firstRgb, secondRgb, unchecked((short)blend.Weight1), unchecked((short)blend.Weight2), 0, inputs.Colors);
                    current = current with { Memory = color.Memory };
                    return Execute(current, new NativeBattleEffect316WriteModel(blend.DestinationAddress - 0x2C, 0x2C, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(color.Color).AsSpan(0, 3)), 3));
                case NativeBattleEffect316Music47E3C0 music:
                    if ((ResolvedNativeFlags & 2) == 0)
                    {
                        ushort channel = NativeMusicProcess?.ReturnChannel ?? throw new NotSupportedException("Effect316 requires its music return channel.");
                        NativeGlobalSoundProcess sound = NativeGlobalSoundProcess ?? throw new NotSupportedException("Effect316 requires its sound process.");
                        NativeGlobalSoundProcess = sound with { MusicPlayback = (sound.MusicPlayback ?? throw new NotSupportedException("Effect316 requires its music playback.")).FadeGain46BBC0(unchecked((uint)(short)channel), unchecked((byte)music.Duration), 0) };
                    }
                    return current;
                case NativeBattleEffect316Vibration4A29A0 vibration when vibration.SourceAddress == VibrationAddress:
                    NativeVibrationBank bank = new(Convert.ToHexString(SHA256.HashData(resources.Vibration.Data.AsSpan())), [checked((int)BinaryPrimitives.ReadUInt32LittleEndian(resources.Vibration.Data.AsSpan())), checked((int)BinaryPrimitives.ReadUInt32LittleEndian(resources.Vibration.Data.AsSpan().Slice(4)))], resources.Vibration.Data);
                    NativeVibrationEnqueue queued = (NativeVibrationProcess ?? throw new NotSupportedException("Effect316 requires its vibration process.")).Enqueue(bank, vibration.Mode, vibration.Magnitude);
                    NativeVibrationProcess = queued.State;
                    return NativeBattleEffect316Controller.WriteBytes(current, vibration.OutputAddress, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(queued.Handle)), 4);
                case NativeBattleEffect316StopVibration4A2940 vibration:
                    NativeVibrationProcess = (NativeVibrationProcess ?? throw new NotSupportedException("Effect316 requires its vibration process.")).Stop4A2940(vibration.Handle);
                    return current;
                case NativeBattleEffect316Stage5713E0 stage:
                    NativeBattleStageState stageState = NativeStage ?? throw new NotSupportedException("Effect316 requires its current stage.");
                    NativeStage = stageState with { Groups = stageState.Groups.Select(group => group with { ColorDepth = unchecked((short)stage.Amount), ColorFlags = stage.Flags }).ToImmutableArray() };
                    return current;
                case NativeBattleEffect316Sound501330 sound:
                    PublishNativeEffect316Frame(current);
                    PublishNativeModuleSound(new(resources.Sound, sound.Flags, null) { Pan = sound.Pan });
                    return ImportNativeEffect316Frame(current);
                case NativeBattleEffect316DrawBatch draw:
                    return DeliverNativeEffect316Batch(ref graphics, current, draw.Batch, draw.CursorGlobal, draw.Flags);
                case NativeBattleEffect316Draw661550 draw:
                    return NativeBattleEffect316Body.Draw661550(current, resources, inputs, draw.ModelAddress, draw.X, draw.Y, draw.Z);
                case NativeBattleEffect316Draw661CC0 draw:
                    return NativeBattleEffect316Body.Draw661CC0(current, resources, inputs, draw.MainAddress, draw.ModelAddress, draw.ProjectedRowsAddress, draw.X, draw.Y, draw.Z);
                case NativeBattleEffect316AdvanceRecords662720 records:
                    return NativeBattleEffect316Body.AdvanceRecords662720(current, resources, inputs, records.MainAddress);
                default:
                    throw new NotSupportedException($"Effect316 emitted unsupported service {operation.GetType().Name}.");
            }
        }
    }

    private static int NativeEffect316Actor(NativeBattleGraphicsState graphics, uint address)
    {
        long offset = (long)address - 0x1D972C0;
        if (offset < 0 || offset % 156 != 0 || offset / 156 >= graphics.Models.Length)
        {
            throw new NotSupportedException("Effect316 requires an actual current physical model address.");
        }
        return checked((int)(offset / 156));
    }

    private void PublishNativeEffect316Frame(NativeBattleEffect316Frame frame)
    {
        NativeEffectMemory = frame.Memory with { Effect316 = frame.State };
        NativeFileStaging = frame.Staging ?? throw new NotSupportedException("Effect316 lost its current addressed memory.");
        nativeCrtRandom = frame.Random;
    }

    private NativeBattleEffect316Frame ImportNativeEffect316Frame(NativeBattleEffect316Frame frame)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect316 lost its process memory.");
        return frame with { Memory = memory, State = memory.Effect316 ?? throw new NotSupportedException("Effect316 lost its retained state."), Staging = NativeFileStaging, Random = nativeCrtRandom ?? throw new NotSupportedException("Effect316 lost its CRT stream.") };
    }
}
