using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect218Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect218Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeEffect218(NativeBattleGraphicsState graphics) => RunNativeEffect218(graphics, true);
    private NativeBattleGraphicsState AdvanceNativeEffect218(NativeBattleGraphicsState graphics) => RunNativeEffect218(graphics, false);

    private NativeBattleGraphicsState RunNativeEffect218(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Effect218 requires its selected original effect.");
        NativeBattleEffect218Resources resources = effect.Binding.Effect218Resources ?? throw new NotSupportedException("Effect218 requires its selected original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect218 requires its current process memory.");
        NativeBattleEffect218Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect218 requires its current CRT stream."), memory.Effect218 ?? throw new NotSupportedException("Effect218 requires its retained storage; explicitly migrate a needed older snapshot.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Effect218 requires its current addressed memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Effect218 requires its actual arithmetic tables.");
        bool hardware = definition.NativeGraphics?.PrimitiveCapabilities?.HardwareEnabled ?? throw new NotSupportedException("Effect218 requires its current hardware capability.");
        NativeBattleEffect218Inputs main = new(_ => Camera().CapturePackedView1D97778(), current => new(current, (NativeEffectIoResult ?? throw new NotSupportedException("Effect218 requires its signed current I/O result.")) < 0), StartVibration, (_, handle) => (NativeVibrationProcess ?? throw new NotSupportedException("Effect218 requires its retained vibration process.")).IsPlaying(handle), Execute);
        NativeBattleEffect218RuntimeInputs inputs = new(main, colors, hardware, _ => graphics.Flags, _ => Projection(), _ => CameraEye(), _ => Camera().Roll, _ => Camera().FocalLength ?? throw new NotSupportedException("Effect218 requires its current normal-depth WORD."), current => NativeBattleTemporaryMemory.ReadView(current.Staging ?? throw new NotSupportedException("Effect218 requires its current primitive flag bytes."), 0x1D969A8, 4), current => NativeEffect218CommonCursor(current.Memory), ReadBone, CaptureServices, (current, batch) => Deliver(current, batch, 0), DeliverModule, RestoreCommon, _ => (Camera().CurrentPose ?? throw new NotSupportedException("Effect218 requires its current camera pose.")) with { FocalLength = Camera().FocalLength, Roll = Camera().Roll }, PublishGuardian);
        frame = initialize ? Initialize6326D0(frame, resources, new(nativeActionPackets, actor => graphics.Models[actor], Center)) : Advance(frame, resources, inputs);
        Publish(frame);
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Effect218 requires its current camera.");
        NativeBattleCameraProjection Projection() => Camera().Projection ?? throw new NotSupportedException("Effect218 requires its current projection.");

        NativeBattleVector CameraEye()
        {
            NativeBattleCameraPoint point = Camera().CurrentPose?.Eye ?? throw new NotSupportedException("Effect218 requires its current camera eye.");
            return new(point.X, point.Y, point.Z);
        }

        int Actor(uint pointer)
        {
            uint delta = unchecked(pointer - 0x1D972C0);
            if (delta % 156 != 0 || delta / 156 >= (uint)graphics.Models.Length)
            {
                throw new InvalidDataException("Effect218 requires an actual physical graphics-model row.");
            }
            return checked((int)(delta / 156));
        }

        NativeBattleVector Center(ushort mask) => NativeBattleActionGeometryRules.TargetCenter(Enumerable.Range(0, 7).Where(actor => (mask & (1 << actor)) != 0).Select(actor => graphics.Models[actor].Transform?.Position28 ?? throw new NotSupportedException("Effect218 requires its selected target position WORDs.")).ToArray());

        void Publish(NativeBattleEffect218Frame current)
        {
            NativeEffectMemory = current.Memory with { Effect218 = current.State };
            nativeCrtRandom = current.Random;
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Effect218 lost its actual addressed memory.");
        }

        NativeBattleEffect218Frame Import(NativeBattleEffect218Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Effect218 service lost its process memory.");
            return current with { Memory = latest, State = latest.Effect218 ?? throw new InvalidDataException("Effect218 service lost its retained storage."), Random = nativeCrtRandom ?? throw new InvalidDataException("Effect218 service lost its CRT owner."), Staging = NativeFileStaging ?? throw new InvalidDataException("Effect218 service lost its addressed memory.") };
        }

        NativeBattleEffect218VibrationStep StartVibration(NativeBattleEffect218Frame current)
        {
            NativeVibrationEnqueue queued = (NativeVibrationProcess ?? throw new NotSupportedException("Effect218 requires its actual vibration process.")).Enqueue(resources.Vibration, 1, 128);
            NativeVibrationProcess = queued.State;
            return new(current, queued.Handle);
        }

        NativeBattleEffect218Frame Deliver(NativeBattleEffect218Frame current, NativeBattleGfGeometryBatch batch, ushort flags)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect218 requires its current ordering table.");
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketDelivery.Deliver(current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Effect218 requires its actual COMMON packet process."), primitive.Pending, batch, flags);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = current.Memory with { PrimitiveProcess = written.Process } };
        }

        NativeBattleEffect218Frame DeliverModule(NativeBattleEffect218Frame current, NativeBattleModulePacketBatch batch)
        {
            if (batch.Primitives.Any(primitive => primitive.Flags20 != 1))
            {
                throw new InvalidDataException("Effect218 screen packet requires its actual module insertion flag.");
            }
            NativeBattleGfGeometryBatch captured = new(batch.Primitives.Select(allocated =>
            {
                NativeBattleGeometryPrimitive primitive = allocated.Primitive;
                byte[] bytes = new byte[primitive.Words.Length * 4];
                for (int word = 0; word < primitive.Words.Length; word++)
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(word * 4), primitive.Words[word]);
                }
                return new NativeBattleGfGeometryAttempt(allocated.ByteOffset, bytes.ToImmutableArray(), primitive.PhysicalWriteMask ?? throw new NotSupportedException("Effect218 screen packet requires its actual physical write mask."), primitive);
            }).ToImmutableArray(), batch.AllocationBytes);
            return Deliver(current, captured, 1);
        }

        NativeBattleEffect218Frame RestoreCommon(NativeBattleEffect218Frame current, NativeBattleEffect218RestoreCommon508630 load)
        {
            (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(load.SourceAddress, load.CompletionAddress, (address, value) => current = WriteBytes(current, address, NativeBattleEffectArena.Clear().Write(0, [value]), 1));
            graphics = graphics with { Auxiliary = auxiliary };
            return current;
        }

        NativeBattleEffect218Frame PublishGuardian(NativeBattleEffect218Frame current, NativeBattleGuardianCameraStep step)
        {
            NativeBattleEffectArena temporary = step.Temporary432 ?? throw new InvalidDataException("Effect218 guardian camera lost its actual publication temporary.");
            short? Padding(int offset)
            {
                (byte[] bytes, bool[] known) = temporary.ReadSnapshot(offset, 2);
                return known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null;
            }
            NativeCamera = Camera().WriteEffectGlobals(step.Pose) with { EyePaddingF6 = Padding(6), TargetPaddingFE = Padding(14) };
            return current;
        }

        NativeBattleEffect218PointStep ReadBone(NativeBattleEffect218Frame current, uint address, byte selector, short fraction)
        {
            NativeBattleBoneAnchorProducer producer;
            if (address is StreamedModelAddress or EmbeddedModelAddress)
            {
                NativeBattleEffectOwnedModelResource resource = address == StreamedModelAddress ? resources.StreamedModel : resources.EmbeddedModel;
                producer = NativeEffect218OwnedBone(current, address, resource, selector, fraction);
            }
            else
            {
                int actor = Actor(address);
                NativeBattleGraphicsModel model = graphics.Models[actor];
                NativeBattlePoseSource? source = (model.Flags0 & 2) == 0 ? null : NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect218 requires its actual recipient skeleton.");
                producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, source!, selector, fraction);
            }
            NativeFileStaging = current.Staging;
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)observed.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)observed.ScriptedColorRegisters.Mac[1]!.Value), unchecked((short)observed.ScriptedColorRegisters.Mac[2]!.Value)) : producer.DisabledPosition;
            return new(current with { Memory = observed, Staging = NativeFileStaging }, point);
        }

        NativeBattleEffect218CaptureServices CaptureServices(NativeBattleEffect218Frame current)
        {
            NativeBattlePrimitiveProcess process = current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Effect218 capture requires its current primitive process.");
            NativeSoftwareDrawingEnvironmentBuffers environments = process.Software?.EnvironmentBuffers ?? throw new NotSupportedException("Effect218 capture requires its actual drawing environments.");
            byte buffer = process.Buffer80 ?? throw new NotSupportedException("Effect218 capture requires its current buffer selector.");
            return new(Projection(), Camera().FocalLength ?? throw new NotSupportedException("Effect218 capture requires its actual focal WORD."), buffer == 0 ? environments.Buffer1 : environments.Buffer0, SetOffsets, DrawStage, Static);
        }

        NativeBattleEffect218Frame SetOffsets(NativeBattleEffect218Frame current, short x, short y)
        {
            NativeCamera = Camera() with { Projection = Projection() with { OffsetX = x, OffsetY = y } };
            return current;
        }

        NativeBattleEffect218Frame DrawStage(NativeBattleEffect218Frame current, NativeBattlePackedMatrix32 matrix, NativeBattleCameraProjection projection)
        {
            (graphics, current) = DrawNativeEffect218CaptureStage(graphics, current, matrix, projection, colors, hardware);
            return current;
        }

        NativeBattleEffect218Frame Static(NativeBattleEffect218Frame current, uint address, NativeBattleGfGeometryBatch batch, ushort flags)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect218 capture requires its actual ordering table.");
            NativeBattleEffect218StaticPacketStep written = NativeBattleEffect218StaticPackets.Store(current.State, primitive.Pending, address, batch, flags);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { State = written.State };
        }

        NativeBattleEffect218Frame Execute(NativeBattleEffect218Frame current, NativeBattleEffect218Operation operation)
        {
            switch (operation)
            {
                case NativeBattleEffect218CameraWord1D97712 translation:
                    NativeBattleCameraState camera = Camera();
                    NativeCamera = camera with { ViewTranslationScratch = (camera.ViewTranslationScratch ?? throw new NotSupportedException("Effect218 requires its retained camera translation scratch.")) with { Y = translation.Value } };
                    return current;
                case NativeBattleEffect218Camera63E960:
                    return BeginGuardianCamera63E960(current, resources);
                case NativeBattleEffect218QueueFile508480 queue:
                    NativeBattleEffectFileAsset file = resources.Files.Single(candidate => candidate.FileId == queue.FileId);
                    if (queue.DestinationAddress == 0x209FAB8)
                    {
                        current = current with { Memory = current.Memory with { GuardianWorkBuffer = (current.Memory.GuardianWorkBuffer ?? throw new NotSupportedException("Effect218 requires its retained work-buffer destination.")).Write(0, file.Data.AsSpan()) } };
                    }
                    else
                    {
                        current = WriteBytes(current, queue.DestinationAddress, NativeBattleEffectArena.Clear().Write(0, file.Data.AsSpan()), file.Data.Length);
                    }
                    NativeEffectIoResult = -1;
                    QueueNativeFileIo482610(file.Data.Length, 0x508470);
                    return current;
                case NativeBattleEffect218UploadTim505E30 upload:
                    NativeBattleEffectTextureState textures = current.Memory.Textures ?? throw new NotSupportedException("Effect218 requires its actual texture queue.");
                    textures = textures.EnqueueArenaTim505E30(current.Memory.Arena ?? throw new NotSupportedException("Effect218 requires its loaded TIM bytes."), NativeBattleWaterExecutionFields.ArenaOffset(upload.SourceAddress, 1)).State;
                    return current with { Memory = current.Memory with { Textures = textures } };
                case NativeBattleEffect218LoadStream501860 stream:
                    int number = stream.SourceAddress == 0xF1DD50 ? unchecked((int)resources.InitialStream) : stream.SourceAddress == 0x209FAB8 ? BinaryPrimitives.ReadInt32LittleEndian((current.Memory.GuardianWorkBuffer ?? throw new NotSupportedException("Effect218 requires its loaded stream descriptor.")).Read(0, 4)) : throw new NotSupportedException("Effect218 selected an unsupported stream owner.");
                    current = current with { Memory = current.Memory with { SummonStream = (current.Memory.SummonStream ?? throw new NotSupportedException("Effect218 requires its retained stream owner.")).Publish501860(stream.SourceAddress, number) } };
                    return WriteBytes(current, stream.CompletionAddress, NativeBattleEffectArena.Clear().Write(0, [1]), 1);
                case NativeBattleEffect218PlayStream5018C0:
                    Publish(current);
                    PublishNativeModuleSound((current.Memory.SummonStream ?? throw new NotSupportedException("Effect218 requires its retained stream number.")).Play5018C0(128, 96));
                    return Import(current);
                case NativeBattleEffect218Sound501330 sound:
                    int index = checked((int)(sound.SourceAddress - 0xE257D0) / 4);
                    if (sound.SourceAddress < 0xE257D0 || sound.SourceAddress != 0xE257D0 + index * 4 || (uint)index >= (uint)resources.Sounds.Length)
                    {
                        throw new NotSupportedException("Effect218 selected an unsupported sound descriptor.");
                    }
                    Publish(current);
                    PublishNativeModuleSound(new(resources.Sounds[index], 0, null) { Pan = 128 });
                    return Import(current);
                case NativeBattleEffect218StageFlags flags:
                    NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Effect218 requires its current stage groups.");
                    NativeStage = stage with { Groups = stage.Groups.Select(group => group with { Flags = (byte)((group.Flags & flags.AndMask) | flags.OrMask) }).ToImmutableArray() };
                    return current;
                case NativeBattleEffect218Text4876F0 text:
                    BattleCombatant actor = GraphicsActor(graphics, text.Actor);
                    string message = (FindNativeOriginalGraphicsBinding(actor.Id) ?? throw new NotSupportedException("Effect218 requires its original recipient text resource.")).Resource.ReadText91(text.Text);
                    StopTurnAdvance();
                    graphics = QueueNativeDescriptorMessage(graphics, new(Frame, "NativeText", actor.Id, null, text.Duration, message) { NativeMessageMode = 3 }, 0, 240);
                    return current;
                case NativeBattleEffect218SelectSequence505C00 sequence:
                    graphics = ScheduleNativeRingSequence505C00(graphics, checked((byte)Actor(sequence.ModelAddress)), sequence.Sequence);
                    return current;
                case NativeBattleEffect218ModelFlags flags:
                    int modelIndex = Actor(flags.ModelAddress);
                    NativeBattleGraphicsModel model = graphics.Models[modelIndex];
                    graphics = graphics with { Models = graphics.Models.SetItem(modelIndex, model with { Flags0 = (ushort)((model.Flags0 & (0xFF00 | flags.AndMask)) | flags.OrMask) }) };
                    return current;
                case NativeBattleEffect218CapturePoint571400 point:
                    NativeBattleEffect218PointStep first = ReadBone(current, point.ModelAddress, 240, 0);
                    NativeBattleEffect218PointStep second = ReadBone(first.Frame, point.ModelAddress, 241, 0);
                    NativeBattleVector center = new(unchecked((short)((first.Point.X + second.Point.X) / 2)), unchecked((short)((first.Point.Y + second.Point.Y) / 2)), unchecked((short)((first.Point.Z + second.Point.Z) / 2)));
                    short angle = (graphics.Models[Actor(point.ModelAddress)].Transform ?? throw new NotSupportedException("Effect218 requires its current recipient angle WORD36.")).Angle36;
                    return WriteBytes(second.Frame, point.DestinationAddress, PointView(center, angle), 8);
                case NativeBattleEffect218Contact506690:
                    Publish(current);
                    int contact = nativeActionPackets.Headers[current.Effect.Parameters!.HeaderIndex7C].FirstContactIndex ?? throw new NotSupportedException("Effect218 requires its actual first contact.");
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact)] });
                    graphics = graphics with { PendingContacts = pending };
                    return Import(current);
                case NativeBattleEffect218StopVibration4A2940 stop:
                    NativeVibrationProcess = (NativeVibrationProcess ?? throw new NotSupportedException("Effect218 requires its retained vibration process.")).Stop4A2940(stop.Handle);
                    return current;
                default:
                    throw new NotSupportedException($"Effect218 emitted unsupported service {operation.GetType().Name}.");
            }
        }
    }

    private static uint NativeEffect218CommonCursor(NativeBattleEffectMemory memory)
    {
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Effect218 requires its actual COMMON cursor.");
        uint bank = process.PacketBank switch
        {
            "off_B6D070" => 0x1901018,
            "off_B6D094" => 0x199B018,
            _ => throw new NotSupportedException("Effect218 requires its actual COMMON packet bank.")
        };
        return checked(bank + (uint)process.PacketBuffer * (uint)process.CommonPacketNominalCapacity + (uint)(process.ByteCursor ?? throw new NotSupportedException("Effect218 requires its actual COMMON byte cursor.")));
    }

    private static NativeBattleBoneAnchorProducer NativeEffect218OwnedBone(NativeBattleEffect218Frame frame, uint address, NativeBattleEffectOwnedModelResource resource, byte selector, short fraction)
    {
        NativeBattleEffectArena model = frame.State.ReadView(address, 156);
        if ((model.Read(0, 1)[0] & 2) == 0)
        {
            return new(null, 0, new(BinaryPrimitives.ReadInt16LittleEndian(model.Read(0x1C, 2)), BinaryPrimitives.ReadInt16LittleEndian(model.Read(0x1E, 2)), BinaryPrimitives.ReadInt16LittleEndian(model.Read(0x20, 2))));
        }
        uint descriptor = BinaryPrimitives.ReadUInt32LittleEndian(model.Read(0x64, 4));
        uint skeletonAddress = BinaryPrimitives.ReadUInt32LittleEndian(ReadBytes(frame, descriptor, 4).Read(0, 4));
        NativeBattleEffectArena skeleton = ReadBytes(frame, skeletonAddress, resource.PoseSource.Skeleton.Length);
        int bone = selector;
        if (selector >= 240)
        {
            bone = skeleton.Read(selector - 236, 1)[0];
            if ((bone & 0x80) != 0)
            {
                fraction = 4096;
                bone &= 0x7F;
            }
        }
        if ((uint)bone >= (uint)resource.PoseSource.BoneCount)
        {
            throw new InvalidDataException("Effect218 bone alias leaves its current original skeleton.");
        }
        short parent = BinaryPrimitives.ReadInt16LittleEndian(skeleton.Read(16 + bone * 48, 2));
        int distance = parent < 0 ? 0 : fraction * BinaryPrimitives.ReadInt16LittleEndian(skeleton.Read(18 + bone * 48, 2)) >> 12;
        NativeBattleActionMatrix world = NativeBattleArenaPose.ReadMatrix(model, 0x40);
        NativeBattleActionMatrix local = NativeBattleArenaPose.ReadMatrix(skeleton, 32 + bone * 48);
        return new(NativeBattleGfMatrices.Compose56C2F0(world, local), unchecked((short)distance), default);
    }
}
