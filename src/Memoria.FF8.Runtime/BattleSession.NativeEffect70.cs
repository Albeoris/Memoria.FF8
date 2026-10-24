using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect70Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeEffect70(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Effect70 requires its selected original effect.");
        NativeBattleEffect70Resources resources = effect.Binding.Effect70Resources ?? throw new NotSupportedException("Effect70 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect70 requires its current process memory.");
        NativeBattleEffect70Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect70 requires its current CRT stream."), memory.Effect70 ?? throw new NotSupportedException("Effect70 requires its retained storage.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Effect70 requires its current addressed memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Effect70 requires its arithmetic constants.");
        NativeBattleEffect70Inputs inputs = new(colors, _ => nativeActionPackets, _ => Camera().CapturePackedView1D97778(), _ => Camera().Projection ?? throw new NotSupportedException("Effect70 requires its current projection."), (_, actor) => graphics.Models[actor], (_, actor) => Pose(actor), _ => Camera().Roll, (_, address) => address switch
        {
            0xB8B7D8 => NativeStage?.ModelColors?.Color40 ?? throw new NotSupportedException("Effect70 requires its current model color."),
            0xB8B9A8 => NativeStage?.ModelColors?.Color44 ?? throw new NotSupportedException("Effect70 requires its current model color."),
            _ => throw new NotSupportedException("Effect70 selected an unsupported current color address.")
        }, Execute)
        {
            ReadPosition8 = (_, actor) => NativeBattleEffectNodeContextMemory.Copy(NativeBattleRaldo19ModelMemory.Capture(graphics.Models[actor]), 0x1C, NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 0, 8),
            ReadAnchor571400 = Anchor,
            SelectIo5342C0 = PollIo,
            ReadSavedFocal = _ => unchecked((ushort)(Camera().CapturedPose?.FocalLength ?? throw new NotSupportedException("Effect70 requires its actual captured focal WORD.")))
        };
        frame = initialize ? NativeBattleEffect70Controller.Initialize(frame, resources, inputs) : NativeBattleEffect70Controller.Advance(frame, resources, inputs);
        Publish(frame);
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Effect70 requires its current camera.");
        NativeBattlePoseSource Pose(int actor) => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect70 requires its current skeleton.");

        void Publish(NativeBattleEffect70Frame current)
        {
            NativeEffectMemory = current.Memory with { Effect70 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Effect70 lost its current addressed memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleEffect70Frame Import(NativeBattleEffect70Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Effect70 service lost its memory.");
            return current with { Memory = latest, State = latest.Effect70 ?? throw new InvalidDataException("Effect70 service lost its storage."), Random = nativeCrtRandom ?? throw new InvalidDataException("Effect70 service lost its CRT stream."), Staging = NativeFileStaging ?? throw new InvalidDataException("Effect70 service lost its addressed memory.") };
        }

        NativeBattleEffect70PointStep Anchor(NativeBattleEffect70Frame current, int actor)
        {
            NativeBattleGraphicsModel model = graphics.Models[actor];
            if ((model.Flags0 & 2) != 0)
            {
                foreach (byte selector in new byte[] { 0xF0, 0xF1 })
                {
                    NativeFileStaging = current.Staging;
                    NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, Pose(actor), selector, 0));
                    current = current with { Memory = observed, Staging = NativeFileStaging };
                }
            }
            NativeBattleModuleAnchor anchor = ReadNativeModelAnchor(graphics, actor);
            NativeBattleEffectArena point = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(anchor.X)).Write(2, BitConverter.GetBytes(anchor.Y)).Write(4, BitConverter.GetBytes(anchor.Z)).Write(6, BitConverter.GetBytes(anchor.W));
            return new(current, point);
        }

        NativeBattleEffect70Frame Deliver(NativeBattleEffect70Frame current, NativeBattleGfGeometryBatch batch, uint? cursorGlobal, ushort flags = 0)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect70 requires its current ordering table.");
            if (cursorGlobal is null)
            {
                NativeBattleCommonPacketWrite ordinary = NativeBattleCommonPacketDelivery.Deliver(current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Effect70 requires its current COMMON packet process."), primitive.Pending, batch);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = ordinary.Ordering } };
                return current with { Memory = current.Memory with { PrimitiveProcess = ordinary.Process } };
            }
            uint cursor = current.State.ReadDword(cursorGlobal.Value);
            uint end = current.State.ReadDword(SecondaryCursorGlobal);
            uint start = checked(end - 0x10000);
            uint arenaAddress = current.State.ReadDword(ArenaGlobal);
            if (start != arenaAddress + FirstPacketOffset && start != arenaAddress + SecondPacketOffset || cursor < start || cursor > end || batch.AllocationBytes < 0 || batch.AllocationBytes > end - cursor)
            {
                throw new InvalidDataException("Effect70 drawing leaves its actual current private packet bank.");
            }
            int offset = NativeBattleWaterExecutionFields.ArenaOffset(cursor, 0);
            NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Effect70 requires its current arena bytes.");
            NativeBattleGfGeometryBatch resolved = NativeBattlePacketBatchWords.Resolve(batch, offset, NativeBattleEffectArena.Length, address => arena.Read(address, 1)[0]);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, offset, primitive.Pending, resolved, flags);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, State = current.State.WriteDword(cursorGlobal.Value, checked(ArenaAddress + (uint)written.Cursor)) };
        }

        NativeBattleEffect70Frame QueueFile(NativeBattleEffect70Frame current, NativeBattleEffectFileRequest request)
        {
            NativeBattleEffectFileAsset file = resources.ReadFile(request.FileId);
            int offset = NativeBattleWaterExecutionFields.ArenaOffset(request.DestinationAddress, file.Data.Length);
            current = current with { Memory = current.Memory with { Arena = (current.Memory.Arena ?? throw new NotSupportedException("Effect70 file load requires its current destination arena.")).Write(offset, file.Data.AsSpan()) } };
            QueueNativeFileIo482610(file.Data.Length, request.CompletionCallback);
            return current;
        }

        NativeBattleEffect70Step PollIo(NativeBattleEffect70Frame current, int index)
        {
            NativeBattleEffectIoPoll poll = NativeBattleEffectIo.Select5342C0(current.Memory, index);
            current = current with { Memory = poll.Memory };
            return new(poll.Request is { } request ? QueueFile(current, request) : current, poll.Busy ? 0 : 1);
        }

        NativeBattleEffect70Frame Execute(NativeBattleEffect70Frame current, NativeBattleEffect70Operation operation)
        {
            switch (operation)
            {
                case NativeBattleEffect70UploadTim upload:
                    NativeBattleEffectHeapAllocation tim = new("current-effect-texture", NativeBattleEffect70Controller.ReadBytes(current, upload.Source, resources.MainTim.Data.Length).Read(0, resources.MainTim.Data.Length).ToImmutableArray());
                    return current with { Memory = current.Memory with { Textures = (current.Memory.Textures ?? throw new NotSupportedException("Effect70 requires its current texture queue.")).EnqueueTim505E30(tim).State } };
                case NativeBattleEffect70ModelFlags flags:
                    NativeBattleGraphicsModel model = graphics.Models[flags.Actor];
                    ushort mask = flags.WordWidth ? flags.AndMask : (ushort)(0xFF00 | (byte)flags.AndMask);
                    graphics = graphics with { Models = graphics.Models.SetItem(flags.Actor, model with { Flags0 = (ushort)((model.Flags0 & mask) | flags.OrMask) }) };
                    return current;
                case NativeBattleEffect70ModelPositionWord position:
                    NativeBattleGraphicsModel changed = NativeBattleRaldo19ModelMemory.Apply(graphics.Models[position.Actor], position.Offset, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(position.Value)), 0, 2);
                    graphics = graphics with { Models = graphics.Models.SetItem(position.Actor, changed) };
                    return current;
                case NativeBattleEffect70ModelPositionPacked position:
                    NativeBattleGraphicsModel restored = NativeBattleRaldo19ModelMemory.Apply(graphics.Models[position.Actor], 0x1C, position.Position8, 0, 4);
                    restored = NativeBattleRaldo19ModelMemory.Apply(restored, 0x20, position.Position8, 4, 4);
                    graphics = graphics with { Models = graphics.Models.SetItem(position.Actor, restored) };
                    return current;
                case NativeBattleEffect70CaptureBounds6DFD90 bounds:
                    (NativeBattleGraphicsState boundsGraphics, NativeBattleEffect70Frame boundsFrame) = CaptureNativeEffect70Bounds(graphics, current, bounds);
                    graphics = graphics with { Models = boundsGraphics.Models };
                    return boundsFrame;
                case NativeBattleEffect70DrawActor693170 actor:
                    (NativeBattleGraphicsState bodyGraphics, NativeBattleEffect70Frame bodyFrame) = DrawNativeEffect70Actor(graphics, current, actor, colors, (body, batch) => Deliver(body, batch, null));
                    graphics = graphics with { Models = bodyGraphics.Models };
                    return bodyFrame;
                case NativeBattleEffect70QueryBonePoint bone:
                    NativeBattleEffect70RingAnchorStep point = NativeBattleEffect70RingAnchor.Resolve6DFC70(graphics.Models[bone.Actor], Pose(bone.Actor), bone.Selector, NativeBattleEffect70Controller.ReadBytes(current, bone.Destination, 12));
                    return NativeBattleEffect70Controller.WriteBytes(current, bone.Destination, point.Output12, 12);
                case NativeBattleEffect70StageIntensity5713E0 stageWrite:
                    NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Effect70 requires its current stage rows.");
                    NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = unchecked((short)stageWrite.Depth), ColorFlags = unchecked((uint)stageWrite.Flags) }).ToImmutableArray() };
                    return current;
                case NativeBattleEffect70StageFlags stageFlags:
                    NativeBattleStageState stageState = NativeStage ?? throw new NotSupportedException("Effect70 requires its current stage flags.");
                    NativeBattleStageGroup group = stageState.Groups[stageFlags.Group];
                    NativeStage = stageState with { Groups = stageState.Groups.SetItem(stageFlags.Group, group with { Flags = (byte)((group.Flags & stageFlags.AndMask) | stageFlags.OrMask) }) };
                    return current;
                case NativeBattleEffect70CameraInitialize50A730:
                    NativeCamera = Camera().Apply50A730();
                    return current;
                case NativeBattleEffect70CameraPose pose:
                    NativeCamera = Camera().WriteEffectGlobals(pose.Pose with { FocalLength = Camera().FocalLength, Roll = Camera().Roll });
                    return current;
                case NativeBattleEffect70CameraFocal focal:
                    NativeCamera = Camera() with { FocalLength = unchecked((short)focal.Value), CurrentPose = Camera().CurrentPose is { } currentPose ? currentPose with { FocalLength = unchecked((short)focal.Value) } : null };
                    return current;
                case NativeBattleEffect70CameraRoll roll:
                    NativeCamera = Camera() with { Roll = roll.Value, CurrentPose = Camera().CurrentPose is { } rolledPose ? rolledPose with { Roll = roll.Value } : null };
                    return current;
                case NativeBattleEffect70Sound501330 sound:
                    Publish(current);
                    PublishNativeModuleSound(new(resources.Sounds[sound.Descriptor], sound.Flags, null) { Pan = sound.Pan });
                    return Import(current);
                case NativeBattleEffect70StartVibration vibration:
                    NativeVibrationEnqueue queued = (NativeVibrationProcess ?? throw new NotSupportedException("Effect70 requires its vibration process.")).Enqueue(vibration.Bank, 1, 128);
                    NativeVibrationProcess = queued.State;
                    return NativeBattleEffect70Controller.Write(current, vibration.TaskAddress, NativeBattleEffect70Controller.Read(current, vibration.TaskAddress).WriteDword(vibration.OutputOffset, unchecked((uint)queued.Handle)));
                case NativeBattleEffect70StopVibration stop:
                    NativeVibrationProcess = (NativeVibrationProcess ?? throw new NotSupportedException("Effect70 requires its vibration process.")).Stop4A2940(stop.Handle);
                    return current;
                case NativeBattleEffect70IoInitialize534110 io:
                    return current with { Memory = NativeBattleEffectIo.Initialize534110(current.Memory, NativeBattleWaterExecutionFields.ArenaOffset(io.ControlAddress, IoLength)) };
                case NativeBattleEffect70IoQueue5341D0 file:
                    return current with { Memory = NativeBattleEffectIo.Queue5341D0(current.Memory, file.FileId, file.Destination, file.Type) };
                case NativeBattleEffect70IoAdvance534210:
                    NativeBattleEffectIoAdvance advance = NativeBattleEffectIo.Advance534210(current.Memory);
                    current = current with { Memory = advance.Memory };
                    return advance.Request is { } request ? QueueFile(current, request) : current;
                case NativeBattleEffect70ConsumeContacts506BA0:
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = nativeActionPackets.ReadHeaderContacts506BA0(current.Effect.Parameters!.HeaderIndex7C) });
                    graphics = graphics with { PendingContacts = pending };
                    return Import(current);
                case NativeBattleEffect70DeliverPrivate packets:
                    return Deliver(current, packets.Batch, packets.CursorGlobal, packets.Flags);
                case NativeBattleEffect70DeliverCommon packets:
                    return Deliver(current, packets.Batch, null);
                default:
                    throw new NotSupportedException("Effect70 emitted an unsupported native operation.");
            }
        }
    }
}
