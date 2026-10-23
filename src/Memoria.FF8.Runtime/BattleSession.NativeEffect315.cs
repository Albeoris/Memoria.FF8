using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeEffect315(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Effect315 requires its selected original effect.");
        NativeBattleEffect315Resources resources = effect.Binding.Effect315Resources ?? throw new NotSupportedException("Effect315 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect315 requires its actual process memory.");
        NativeBattleEffect315Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect315 requires its current CRT stream."), memory.Effect315 ?? throw new NotSupportedException("Effect315 requires its retained executable storage.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Effect315 requires its temporary memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Effect315 requires its arithmetic constants.");
        NativeBattleEffect315Inputs inputs = new(colors, _ => graphics.Flags, _ => Camera().CapturePackedView1D97778(), _ => Projection(), (_, pointer) => graphics.Models[Actor(pointer)], _ => nativeActionPackets, Anchor, _ => (NativeStage?.ModelColors ?? throw new NotSupportedException("Effect315 requires its current stage colors.")).Color40, Execute, StartVibration, (_, handle) => (NativeVibrationProcess ?? throw new NotSupportedException("Effect315 requires its vibration process.")).IsPlaying(handle));
        frame = initialize ? NativeBattleEffect315Controller.Initialize(frame, resources, inputs) : NativeBattleEffect315Controller.Advance(frame, resources, inputs);
        Publish(frame);
        if (initialize)
        {
            NativeCamera = Camera().RegisterControlResource(frame.Memory.Textures!.Animation);
        }
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Effect315 requires its current camera.");
        NativeBattleCameraProjection Projection() => Camera().Projection ?? throw new NotSupportedException("Effect315 requires its current projection.");
        int Actor(uint pointer)
        {
            uint delta = unchecked(pointer - 0x1D972C0);
            if (delta % 156 != 0 || delta / 156 >= (uint)graphics.Models.Length)
            {
                throw new InvalidDataException("Effect315 requires an actual physical graphics-model row.");
            }
            return checked((int)(delta / 156));
        }
        void Publish(NativeBattleEffect315Frame current)
        {
            NativeEffectMemory = current.Memory with { Effect315 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Effect315 lost its temporary memory.");
            nativeCrtRandom = current.Random;
        }
        NativeBattleEffect315Frame Import(NativeBattleEffect315Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Effect315 service lost its process memory.");
            return current with { Memory = latest, State = latest.Effect315 ?? throw new InvalidDataException("Effect315 service lost its executable storage."), Random = nativeCrtRandom ?? throw new InvalidDataException("Effect315 service lost its CRT stream."), Staging = NativeFileStaging ?? throw new InvalidDataException("Effect315 service lost its temporary memory.") };
        }
        NativeBattleEffect315PointStep Anchor(NativeBattleEffect315Frame current, uint pointer, byte selector, short fraction, NativeBattleEffectArena retained)
        {
            int actor = Actor(pointer);
            NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect315 requires its current actor skeleton.");
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[actor], source, selector, fraction);
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Effect315 bone query requires its temporary memory.");
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)observed.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[1]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[2]!.Value)) : producer.DisabledPosition;
            return new(current with { Memory = observed, Staging = NativeFileStaging }, retained.Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z)));
        }
        NativeBattleEffect315VibrationStep StartVibration(NativeBattleEffect315Frame current)
        {
            NativeVibrationEnqueue queued = (NativeVibrationProcess ?? throw new NotSupportedException("Effect315 requires its vibration process.")).Enqueue(resources.Vibration, 1, 128);
            NativeVibrationProcess = queued.State;
            return new(current, queued.Handle);
        }
        NativeBattleEffect315Frame Deliver(NativeBattleEffect315Frame current, NativeBattleGfGeometryBatch batch, uint cursorGlobal = NativeBattleEffect315Profile.CursorGlobal, ushort flags = 0)
        {
            if (cursorGlobal != NativeBattleEffect315Profile.CursorGlobal)
            {
                throw new InvalidDataException("Effect315 selected an unsupported packet cursor.");
            }
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect315 requires its ordering table.");
            int cursor = NativeBattleWaterExecutionFields.ArenaOffset(current.State.ReadDword(cursorGlobal), 0);
            NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Effect315 requires its effect packet arena.");
            NativeBattleGfGeometryBatch resolved = NativeBattlePacketBatchWords.Resolve(batch, cursor, NativeBattleEffectArena.Length, offset => arena.Read(offset, 1)[0]);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, resolved, flags);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, State = current.State.WriteDword(cursorGlobal, checked(NativeBattleEffect315Profile.ArenaAddress + (uint)written.Cursor)) };
        }
        NativeBattleEffect315Frame Draw(NativeBattleEffect315Frame current, uint address, int size, int table, byte shift)
        {
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Effect315 drawing requires its temporary owner.");
            NativeBattleEffectArena context = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, address, size), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, size), 0, size);
            uint source = BinaryPrimitives.ReadUInt32LittleEndian(context.Read(0, 4));
            NativeBattleGfGeometryBatch batch;
            if (size == 180)
            {
                NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(current.Memory, context, resources.ReadSprite, Projection(), Camera().Roll, table, shift, true);
                current = current with { Memory = drawn.Memory };
                context = drawn.Temporary180;
                batch = drawn.Batch;
            }
            else if (size == 88)
            {
                NativeBattleStaticMeshContext88Step drawn = NativeBattleStaticMeshContext88.DrawLoaded(current.Memory, resources.ReadMesh(source), Projection(), context, colors, table, shift);
                current = current with { Memory = drawn.Memory };
                context = drawn.Context;
                batch = drawn.Batch;
            }
            else if (size == 124)
            {
                NativeBattleEffect315Scrolling124Step drawn = NativeBattleEffect315Scrolling124.DrawLoaded(current.Memory, resources.ReadMesh(source), Projection(), context, colors, table, shift);
                current = current with { Memory = drawn.Memory };
                context = drawn.Context124;
                batch = drawn.Batch;
            }
            else if (size == 96)
            {
                NativeBattleEffect315ModeMesh96Step drawn = NativeBattleEffect315ModeMesh96.DrawLoaded(current.Memory, resources.ReadMesh(source), Projection(), context, colors, table, shift);
                current = current with { Memory = drawn.Memory };
                context = drawn.Context96;
                batch = drawn.Batch;
            }
            else
            {
                throw new InvalidDataException("Effect315 selected an unsupported temporary extent.");
            }
            current = current with { Staging = NativeBattleTemporaryMemory.PublishView(staging, address, context, size) };
            return Deliver(current, batch);
        }
        NativeBattleEffect315Frame Execute(NativeBattleEffect315Frame current, NativeBattleEffect315Operation operation)
        {
            switch (operation)
            {
                case NativeBattleEffect315ModelFlags flags:
                    int actor = Actor(flags.ModelAddress);
                    NativeBattleGraphicsModel model = graphics.Models[actor];
                    graphics = graphics with { Models = graphics.Models.SetItem(actor, model with { Flags0 = unchecked((ushort)((model.Flags0 & ~flags.ClearFlags0) | flags.SetFlags0)) }) };
                    return current;
                case NativeBattleEffect315ModelFlags8 flags:
                    int statusActor = Actor(flags.ModelAddress);
                    NativeBattleGraphicsModel statusModel = graphics.Models[statusActor];
                    uint status = statusModel.Flags8 ?? throw new NotSupportedException("Effect315 requires its current status DWORD.");
                    graphics = graphics with { Models = graphics.Models.SetItem(statusActor, statusModel with { Flags8 = (status & ~flags.ClearFlags8) | flags.SetFlags8 }) };
                    return current;
                case NativeBattleEffect315ModelInitial20XZ position:
                    int initialActor = Actor(position.ModelAddress);
                    NativeBattleGraphicsModel initialModel = graphics.Models[initialActor];
                    NativeBattleModelTransform initial = initialModel.Transform ?? throw new NotSupportedException("Effect315 requires its initial position owner.");
                    graphics = graphics with { Models = graphics.Models.SetItem(initialActor, initialModel with { Transform = initial with { Initial20 = initial.Initial20 with { X = position.X, Z = position.Z } } }) };
                    return current;
                case NativeBattleEffect315ModelPosition28XZ position:
                    int positionActor = Actor(position.ModelAddress);
                    NativeBattleGraphicsModel positionModel = graphics.Models[positionActor];
                    NativeBattleModelTransform transform = positionModel.Transform ?? throw new NotSupportedException("Effect315 requires its current position owner.");
                    graphics = graphics with { Models = graphics.Models.SetItem(positionActor, positionModel with { Transform = transform with { Position28 = transform.Position28 with { X = position.X, Z = position.Z } } }) };
                    return current;
                case NativeBattleEffect315ModelDefaultSequence2 sequence:
                    int sequenceActor = Actor(sequence.ModelAddress);
                    NativeBattleGraphicsModel sequenceModel = graphics.Models[sequenceActor];
                    NativeBattleSequenceState sequenceState = sequenceModel.SequenceState ?? throw new NotSupportedException("Effect315 requires its current sequence control.");
                    graphics = graphics with { Models = graphics.Models.SetItem(sequenceActor, sequenceModel with { SequenceState = sequenceState with { Default2 = sequence.Value } }) };
                    return current;
                case NativeBattleEffect315ModelColor40 color:
                    int colorActor = Actor(color.ModelAddress);
                    graphics = graphics with { Models = graphics.Models.SetItem(colorActor, graphics.Models[colorActor] with { Color40 = color.Value }) };
                    return current;
                case NativeBattleEffect315Activate487670 activation:
                    graphics = QueueNativeDescriptor(graphics, 113, 0x487670, checked((ushort)activation.ActorIndex));
                    return current;
                case NativeBattleEffect315Contact506690:
                    Publish(current);
                    var pending = graphics.PendingContacts;
                    int header = current.Effect.Parameters?.HeaderIndex7C ?? throw new NotSupportedException("Effect315 requires its current action header.");
                    int contact = nativeActionPackets.Headers[header].FirstContactIndex ?? throw new NotSupportedException("Effect315 requires its current first contact.");
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact)] });
                    graphics = graphics with { PendingContacts = pending };
                    return Import(current);
                case NativeBattleEffect315Sound5013A0 sound:
                    Publish(current);
                    byte[] point = sound.Point8.Read(0, 6);
                    PublishNativeModuleSound(sound.Packed, sound.Flags, new(BinaryPrimitives.ReadInt16LittleEndian(point), BinaryPrimitives.ReadInt16LittleEndian(point.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(point.AsSpan(4)), 0));
                    return Import(current);
                case NativeBattleEffect315StopVibration4A2940 vibration:
                    NativeVibrationProcess = (NativeVibrationProcess ?? throw new NotSupportedException("Effect315 requires its vibration process.")).Stop4A2940(vibration.Handle);
                    return current;
                case NativeBattleEffect315ReleaseCommon508630 release:
                    (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(release.SourceAddress, release.CompletionAddress, (address, value) => current = NativeBattleEffect315Controller.WriteCompletionByte(current, address, value));
                    graphics = graphics with { Auxiliary = auxiliary };
                    return current;
                case NativeBattleEffect315DrawBody156 body:
                    (graphics, current) = DrawNativeEffect315Body(graphics, current, body.TemporaryAddress, body.ModelAddress);
                    return current;
                case NativeBattleEffect315DrawSprite180 sprite:
                    return Draw(current, sprite.TemporaryAddress, 180, sprite.TableBaseBucket, checked((byte)sprite.DepthShift));
                case NativeBattleEffect315DrawMesh88 mesh:
                    return Draw(current, mesh.TemporaryAddress, 88, mesh.TableBaseBucket, checked((byte)mesh.DepthShift));
                case NativeBattleEffect315DrawScrolling124 mesh:
                    return Draw(current, mesh.TemporaryAddress, 124, mesh.TableBaseBucket, checked((byte)mesh.DepthShift));
                case NativeBattleEffect315DrawMesh96 mesh:
                    return Draw(current, mesh.TemporaryAddress, 96, mesh.TableBaseBucket, checked((byte)mesh.DepthShift));
                case NativeBattleEffect315DeliverBatch packets:
                    return Deliver(current, packets.Batch, packets.CursorGlobal, packets.Flags);
                default:
                    throw new NotSupportedException("Effect315 emitted an unsupported native operation.");
            }
        }
    }
}
