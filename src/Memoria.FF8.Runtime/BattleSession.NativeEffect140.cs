using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeEffect140(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Effect140 requires its selected original effect.");
        NativeBattleEffect140Resources resources = effect.Binding.Effect140Resources ?? throw new NotSupportedException("Effect140 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect140 requires its current process memory.");
        NativeBattleEffect140Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect140 requires its current CRT stream."), memory.Effect140 ?? throw new NotSupportedException("Effect140 requires its retained storage.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Effect140 requires its current addressed memory.") };
        NativeBattleEffect140Inputs inputs = null!;
        inputs = new(definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Effect140 requires its arithmetic constants."), _ => graphics.Flags, _ => nativeActionPackets, (_, actor) => graphics.Models[actor], (_, actor) => NativeBattleEffectNodeContextMemory.Copy(NativeBattleRaldo19ModelMemory.Capture(graphics.Models[actor]), 0x1C, NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 0, 8), _ => Camera().CapturePackedView1D97778(), _ => Camera().Projection ?? throw new NotSupportedException("Effect140 requires its current projection."), _ => (Camera().CurrentPose ?? throw new NotSupportedException("Effect140 requires its current camera pose.")) with { FocalLength = Camera().FocalLength, Roll = Camera().Roll }, _ => Camera().EyePaddingF6, _ => Camera().TargetPaddingFE, Execute)
        {
            ReadCommonSize = _ => NativeBattleEffectArena.Length,
            CapturePresentation = true,
            Poll534270 = PollIo
        };
        frame = initialize ? NativeBattleEffect140Controller.Initialize(frame, resources, inputs) : NativeBattleEffect140Controller.Advance(frame, resources, inputs);
        Publish(frame);
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Effect140 requires its current camera.");

        void Publish(NativeBattleEffect140Frame current)
        {
            NativeEffectMemory = current.Memory with { Effect140 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Effect140 lost its current addressed memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleEffect140Frame Import(NativeBattleEffect140Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Effect140 service lost its memory.");
            return current with { Memory = latest, State = latest.Effect140 ?? throw new InvalidDataException("Effect140 service lost its storage."), Random = nativeCrtRandom ?? throw new InvalidDataException("Effect140 service lost its CRT stream."), Staging = NativeFileStaging ?? throw new InvalidDataException("Effect140 service lost its addressed memory.") };
        }

        NativeBattleEffect140Frame QueueFile(NativeBattleEffect140Frame current, NativeBattleEffectFileRequest request)
        {
            NativeBattleEffectFileAsset file = resources.ReadFile(request.FileId);
            int offset = NativeBattleWaterExecutionFields.ArenaOffset(request.DestinationAddress, file.Data.Length);
            current = current with { Memory = current.Memory with { Arena = (current.Memory.Arena ?? throw new NotSupportedException("Effect140 file load requires its current destination arena.")).Write(offset, file.Data.AsSpan()) } };
            QueueNativeFileIo482610(file.Data.Length, request.CompletionCallback);
            return current;
        }

        NativeBattleEffect140IoStep PollIo(NativeBattleEffect140Frame current)
        {
            NativeBattleEffectIoPoll poll = NativeBattleEffectIo.Poll534270(current.Memory);
            current = current with { Memory = poll.Memory };
            return new(poll.Request is { } request ? QueueFile(current, request) : current, poll.Busy);
        }

        NativeBattleEffect140Frame Execute(NativeBattleEffect140Frame current, NativeBattleEffect140Operation operation)
        {
            switch (operation)
            {
                case NativeBattleEffect140UploadTim505E30 upload:
                    NativeBattleEffectHeapAllocation tim = new("current-effect-texture", NativeBattleEffect140Controller.ReadBytes(current, upload.SourceAddress, resources.MainTim.Data.Length).Read(0, resources.MainTim.Data.Length).ToImmutableArray());
                    return current with { Memory = current.Memory with { Textures = (current.Memory.Textures ?? throw new NotSupportedException("Effect140 requires its current texture queue.")).EnqueueTim505E30(tim).State } };
                case NativeBattleEffect140InitializeIo534110 io:
                    return current with { Memory = NativeBattleEffectIo.Initialize534110(current.Memory, NativeBattleWaterExecutionFields.ArenaOffset(io.ControllerAddress, 5)) };
                case NativeBattleEffect140QueueIo5341D0 queue:
                    return current with { Memory = NativeBattleEffectIo.Queue5341D0(current.Memory, queue.FileId, queue.DestinationAddress, queue.Type) };
                case NativeBattleEffect140AdvanceIo534210:
                    NativeBattleEffectIoAdvance advance = NativeBattleEffectIo.Advance534210(current.Memory);
                    current = current with { Memory = advance.Memory };
                    return advance.Request is { } request ? QueueFile(current, request) : current;
                case NativeBattleEffect140Sound501330 sound:
                    Publish(current);
                    int soundIndex = NativeBattleEffect140Profile.SoundAddresses.IndexOf(sound.SourceAddress);
                    if (soundIndex < 0)
                    {
                        throw new NotSupportedException("Effect140 selected an unsupported sound descriptor.");
                    }
                    PublishNativeModuleSound(new(resources.Sounds[soundIndex], sound.Flags, null) { Pan = sound.Pan });
                    return Import(current);
                case NativeBattleEffect140Stream5018C0 stream:
                    Publish(current);
                    PublishNativeModuleSound((current.Memory.SummonStream ?? throw new NotSupportedException("Effect140 requires its current stream number.")).Play5018C0(stream.Pan, stream.Volume));
                    return Import(current);
                case NativeBattleEffect140Vibration4A29A0 vibration:
                    if (vibration.SourceAddress != 0x11D0048)
                    {
                        throw new NotSupportedException("Effect140 selected an unsupported vibration descriptor.");
                    }
                    ImmutableArray<byte> vibrationBytes = resources.Vibration.Data;
                    NativeVibrationBank bank = new(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(vibrationBytes.AsSpan())), [BinaryPrimitives.ReadInt32LittleEndian(vibrationBytes.AsSpan()), BinaryPrimitives.ReadInt32LittleEndian(vibrationBytes.AsSpan().Slice(4))], vibrationBytes);
                    NativeVibrationEnqueue queued = (NativeVibrationProcess ?? throw new NotSupportedException("Effect140 requires its current vibration process.")).Enqueue(bank, vibration.Mode, vibration.Magnitude);
                    NativeVibrationProcess = queued.State;
                    return NativeBattleEffect140Controller.WriteBytes(current, vibration.OutputAddress, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(queued.Handle)), 4);
                case NativeBattleEffect140StopVibration4A2940 stop:
                    NativeVibrationProcess = (NativeVibrationProcess ?? throw new NotSupportedException("Effect140 requires its current vibration process.")).Stop4A2940(stop.Handle);
                    return current;
                case NativeBattleEffect140Stage5713E0 intensity:
                    NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Effect140 requires its current stage.");
                    NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = unchecked((short)intensity.Intensity), ColorFlags = unchecked((uint)intensity.Color) }).ToImmutableArray() };
                    return current;
                case NativeBattleEffect140ContactAll506BA0:
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = nativeActionPackets.ReadHeaderContacts506BA0(current.Effect.Parameters!.HeaderIndex7C) });
                    graphics = graphics with { PendingContacts = pending };
                    return Import(current);
                case NativeBattleEffect140ModelColor40 color:
                    graphics = graphics with { Models = graphics.Models.SetItem(color.Actor, graphics.Models[color.Actor] with { Color40 = color.Color }) };
                    return current;
                case NativeBattleEffect140ModelFlags flags:
                    NativeBattleGraphicsModel model = graphics.Models[flags.Actor];
                    graphics = graphics with { Models = graphics.Models.SetItem(flags.Actor, model with { Flags0 = (ushort)((model.Flags0 & flags.AndMask) | flags.OrMask) }) };
                    return current;
                case NativeBattleEffect140PublishCamera camera:
                    NativeCamera = Camera().WriteEffectGlobals(camera.Pose) with { EyePaddingF6 = camera.EyePadding, TargetPaddingFE = camera.TargetPadding };
                    return current;
                case NativeBattleEffect140CommonTextureLoad508630 load:
                    (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(load.SourceAddress, load.CompletionAddress, (address, value) => current = NativeBattleEffect140Controller.WriteCompletionByte(current, address, value));
                    graphics = graphics with { Auxiliary = auxiliary };
                    return current;
                case NativeBattleEffect140DrawOwnedModel draw:
                    return DrawNativeEffect140OwnedModel(current, draw, resources, inputs);
                case NativeBattleEffect140DrawBatch draw:
                    NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect140 requires its current ordering table.");
                    if (draw.CursorGlobal == 0x1D8E054)
                    {
                        NativeBattleCommonPacketWrite ordinary = NativeBattleCommonPacketDelivery.Deliver(current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Effect140 requires its ordinary packet process."), primitive.Pending, draw.Batch, draw.Flags);
                        graphics = graphics with { PrimitiveFrame = primitive with { Pending = ordinary.Ordering } };
                        return current with { Memory = current.Memory with { PrimitiveProcess = ordinary.Process } };
                    }
                    if (draw.CursorGlobal != 0x2517B0C)
                    {
                        throw new NotSupportedException("Effect140 selected an unsupported packet cursor.");
                    }
                    int cursor = NativeBattleWaterExecutionFields.ArenaOffset(current.State.ReadDword(draw.CursorGlobal), 0);
                    NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Effect140 requires its current COMMON arena.");
                    NativeBattleGfGeometryBatch resolved = NativeBattlePacketBatchWords.ResolveWithKnownness(draw.Batch, cursor, NativeBattleEffectArena.Length, offset =>
                    {
                        (byte[] bytes, bool[] known) = arena.ReadSnapshot(offset, 1);
                        return (bytes[0], known[0]);
                    });
                    NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, resolved, draw.Flags);
                    graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                    return current with { Memory = written.Memory, State = current.State.WriteDword(draw.CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)) };
                default:
                    throw new NotSupportedException($"Effect140 emitted unsupported service {operation.GetType().Name}.");
            }
        }
    }

    private void CompleteNativeEffect140File()
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect140 completion requires its current process memory.");
        NativeBattleEffect140State state = memory.Effect140 ?? throw new NotSupportedException("Effect140 completion requires its current control owner.");
        NativeEffectMemory = NativeBattleEffectIo.Complete6A6360(memory, state.ReadDword(0x2517B28), ReadSource).Memory;

        NativeBattleEffectHeapAllocation ReadSource(uint address, int? length)
        {
            int count = length ?? throw new NotSupportedException("Effect140 completion requires its bounded current COMMON source.");
            int offset = NativeBattleWaterExecutionFields.ArenaOffset(address, count);
            return new("current-effect-source", (memory.Arena ?? throw new NotSupportedException("Effect140 completion requires its current COMMON owner.")).Read(offset, count).ToImmutableArray());
        }
    }
}
