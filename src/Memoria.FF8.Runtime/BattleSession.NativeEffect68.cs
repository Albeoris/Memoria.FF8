using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect68Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeEffect68(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Effect68 requires its selected original effect.");
        NativeBattleEffect68Resources resources = effect.Binding.Effect68Resources ?? throw new NotSupportedException("Effect68 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect68 requires its current process memory.");
        NativeBattleEffect68Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect68 requires its current CRT stream."), memory.Effect68 ?? throw new NotSupportedException("Effect68 requires its retained storage.")) { Staging = NativeFileStaging };
        NativeBattleEffect68Inputs inputs = new(definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Effect68 requires its arithmetic constants."), _ => nativeActionPackets, _ => graphics.Flags, _ => NativeBattleScanPhysicalModels.Capture(graphics.Models).Select(Arena).ToImmutableArray(), (_, actor) => Arena(ReadNativeScanDescriptor208(actor)), (_, actor) => graphics.Models[actor].Sequence ?? throw new NotSupportedException("Effect68 requires its current resource sequence."), ReadCamera, _ => Camera().CapturePackedView1D97778(), _ => Camera().Projection ?? throw new NotSupportedException("Effect68 requires its current projection."), ReadBone, current => NativeInputConfiguration.Project((current.Memory.Scan ?? throw new NotSupportedException("Effect68 requires its current input owner.")).Input.Input.Read49ED30(0, 0), definition.NativeSavedOptions ?? throw new NotSupportedException("Effect68 requires its saved input options."), definition.NativeInputButtonMap), BuildResults, NativeBattleEffect68Results.PageB68370, Execute);
        frame = initialize ? NativeBattleEffect68Controller.Initialize(frame, resources, inputs) : NativeBattleEffect68Controller.Advance(frame, resources, inputs);
        Publish(frame);
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Effect68 requires its current camera.");
        NativeBattlePoseSource Pose(int actor) => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect68 requires its current physical skeleton.");
        void Publish(NativeBattleEffect68Frame current)
        {
            NativeEffectMemory = current.Memory with { Effect68 = current.State };
            NativeFileStaging = current.Staging;
            nativeCrtRandom = current.Random;
        }
        NativeBattleEffect68Frame Import(NativeBattleEffect68Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Effect68 service lost its current memory.");
            return current with { Memory = latest, State = latest.Effect68 ?? throw new InvalidDataException("Effect68 service lost its storage."), Staging = NativeFileStaging, Random = nativeCrtRandom ?? throw new InvalidDataException("Effect68 service lost its CRT stream.") };
        }
        NativeBattleEffect68CameraSnapshot ReadCamera(NativeBattleEffect68Frame current)
        {
            NativeBattleCameraState camera = Camera();
            NativeBattleCameraPose pose = camera.CurrentPose ?? throw new NotSupportedException("Effect68 requires its current camera pose.");
            NativeBattleCameraPose saved = camera.CapturedPose ?? throw new NotSupportedException("Effect68 requires its captured camera pose.");
            NativeBattleActionMatrix view = camera.Projection?.View ?? throw new NotSupportedException("Effect68 requires its current view.");
            return new(Arena(CameraVector(pose.Eye, camera.EyePaddingF6)), Arena(CameraVector(pose.Target, camera.TargetPaddingFE)), unchecked((ushort)(camera.FocalLength ?? throw new NotSupportedException("Effect68 requires its current zoom."))), view.Rotation[2], view.Rotation[8], Arena(CameraVector(saved.Eye, camera.CapturedEyePadding806)), Arena(CameraVector(saved.Target, camera.CapturedTargetPadding80E)), unchecked((ushort)(saved.FocalLength ?? throw new NotSupportedException("Effect68 requires its captured zoom."))));
        }
        NativeBattleEffect68PointStep ReadBone(NativeBattleEffect68Frame current, int actor, byte selector, short distance, NativeBattleEffectArena retained)
        {
            NativeFileStaging = current.Staging;
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[actor], Pose(actor), selector, distance);
            current = current with { Memory = ObserveNativeBoneAnchor(current.Memory, producer), Staging = NativeFileStaging };
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)current.Memory.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)current.Memory.ScriptedColorRegisters.Mac[1]!.Value), unchecked((short)current.Memory.ScriptedColorRegisters.Mac[2]!.Value)) : producer.DisabledPosition;
            return new(current, retained.Write(0, BitConverter.GetBytes(unchecked((short)point.X))).Write(2, BitConverter.GetBytes(unchecked((short)point.Y))).Write(4, BitConverter.GetBytes(unchecked((short)point.Z))));
        }
        NativeBattleEffect68Frame Observe(NativeBattleEffect68Frame current, NativeBattleBoneAnchorProducer producer)
        {
            NativeFileStaging = current.Staging;
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            return current with { Memory = observed, Staging = NativeFileStaging };
        }
        NativeBattleEffect68ResultStep BuildResults(NativeBattleEffect68Frame current, int actor)
        {
            NativeBattleScanTextResources text = current.Effect.Binding.ScanTextResources ?? throw new NotSupportedException("Effect68 requires its original text templates.");
            return NativeBattleEffect68Results.BuildB67EF0(current, actor, new(ReadNativeScanDescriptor208, id => text.ReadEncodedMiscText(id) ?? throw new NotSupportedException("Effect68 requires its reached text template."), index => ReadNativeScanEnemyInformation(index)[0xF7], ReadNativeScanExtra71));
        }
        NativeBattleEffect68Frame Execute(NativeBattleEffect68Frame current, NativeBattleEffect68Operation operation)
        {
            switch (operation)
            {
                case NativeBattleEffect68ForceCamera50A730:
                    NativeCamera = Camera().Apply50A730();
                    return current;
                case NativeBattleEffect68SetMode4A8480 mode:
                    NativeBattleUiServices ui = graphics.UiServices ?? throw new NotSupportedException("Effect68 requires its current UI flags.");
                    graphics = graphics with { UiServices = mode.Mode == 0 ? ui.SetFlags(0x200, 0x100) : ui.SetFlags(0x100, 0x200) };
                    return current;
                case NativeBattleEffect68Sound501330 sound:
                    if (sound.Source != SoundAddress)
                    {
                        throw new NotSupportedException("Effect68 selected an unsupported sound descriptor.");
                    }
                    Publish(current);
                    PublishNativeModuleSound(new(resources.Sound, sound.Flags, null) { Pan = sound.Pan, RawPan = sound.Pan });
                    return Import(current);
                case NativeBattleEffect68Contact506690 contact:
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.Index)] });
                    graphics = graphics with { PendingContacts = pending };
                    return Import(current);
                case NativeBattleEffect68UploadTim505E30 upload:
                    NativeBattleEffectHeapAllocation tim = new("current-effect-texture", NativeBattleEffect68Controller.ReadBytes(current, upload.Source, TextureLength).Read(0, TextureLength).ToImmutableArray());
                    return current with { Memory = current.Memory with { Textures = (current.Memory.Textures ?? throw new NotSupportedException("Effect68 requires its current texture queue.")).EnqueueTim505E30(tim).State } };
                case NativeBattleEffect68WriteModelBytes model:
                    ImmutableArray<NativeBattleScanByteOwner> models = NativeBattleScanPhysicalModels.Capture(graphics.Models);
                    NativeBattleEffectArena changed = NativeBattleEffectNodeContextMemory.Copy(model.Data, 0, Arena(models[model.Actor]), model.Offset, model.Length);
                    (byte[] bytes, bool[] known) = changed.ReadSnapshot(0, 156);
                    graphics = graphics with { Models = NativeBattleScanPhysicalModels.Apply(graphics.Models, models.SetItem(model.Actor, new(bytes.ToImmutableArray(), known.ToImmutableArray()))) };
                    return current;
                case NativeBattleEffect68PublishCamera camera:
                    NativeCamera = Camera().WriteEffectGlobals(new(new(unchecked((short)Word(camera.Eye8, 0)), unchecked((short)Word(camera.Eye8, 2)), unchecked((short)Word(camera.Eye8, 4))), new(unchecked((short)Word(camera.Target8, 0)), unchecked((short)Word(camera.Target8, 2)), unchecked((short)Word(camera.Target8, 4))), unchecked((short)camera.Zoom), Camera().Roll)) with { EyePaddingF6 = unchecked((short)Word(camera.Eye8, 6)), TargetPaddingFE = unchecked((short)Word(camera.Target8, 6)) };
                    return current;
                case NativeBattleEffect68UpdateCaster8DC740 caster:
                    NativeBattleModulePoolTask casterTask = NativeBattleEffectModelHelpers.UpdateCaster8DC740(NativeBattleEffect68Controller.Read(current, caster.TaskAddress), current.Effect.Parameters!, graphics.Models, Pose, producer => current = Observe(current, producer));
                    return NativeBattleEffect68Controller.Write(current, caster.TaskAddress, casterTask);
                case NativeBattleEffect68UpdateTarget8DC610 target:
                    NativeBattleModulePoolTask targetTask = NativeBattleEffectModelHelpers.UpdateTarget8DC610(NativeBattleEffect68Controller.Read(current, target.TaskAddress), graphics.Models, Pose, producer => current = Observe(current, producer));
                    return NativeBattleEffect68Controller.Write(current, target.TaskAddress, targetTask);
                case NativeBattleEffect68UpdateBounds8DC870 bounds:
                    NativeBattleModulePoolTask boundsTask = NativeBattleEffect68Controller.Read(current, bounds.TaskAddress);
                    int actor = boundsTask.ReadByte(0x2D);
                    NativeBattlePoseSource source = Pose(actor);
                    return NativeBattleEffect68Controller.Write(current, bounds.TaskAddress, NativeBattleEffectModelHelpers.UpdateBounds8DC870(boundsTask, source, NativeBattlePoseMatrices.ReadDecodedPose(graphics.Models[actor].SequenceState ?? throw new NotSupportedException("Effect68 requires its current target pose."), source)));
                case NativeBattleEffect68DrawBatch draw:
                    NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect68 requires its current ordering table.");
                    if (draw.CursorGlobal == 0x1D8E054)
                    {
                        NativeBattleCommonPacketWrite ordinary = NativeBattleCommonPacketDelivery.Deliver(current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Effect68 requires its ordinary packet process."), primitive.Pending, draw.Batch, draw.Flags);
                        graphics = graphics with { PrimitiveFrame = primitive with { Pending = ordinary.Ordering } };
                        return current with { Memory = current.Memory with { PrimitiveProcess = ordinary.Process } };
                    }
                    if (draw.CursorGlobal != CursorGlobal)
                    {
                        throw new NotSupportedException("Effect68 selected an unsupported packet cursor.");
                    }
                    int cursor = NativeBattleWaterExecutionFields.ArenaOffset(current.State.ReadDword(CursorGlobal), 0);
                    NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Effect68 requires its current COMMON arena.");
                    NativeBattleGfGeometryBatch resolved = NativeBattlePacketBatchWords.ResolveWithKnownness(draw.Batch, cursor, NativeBattleEffectArena.Length, offset =>
                    {
                        (byte[] retained, bool[] retainedKnown) = arena.ReadSnapshot(offset, 1);
                        return (retained[0], retainedKnown[0]);
                    });
                    NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, resolved, draw.Flags);
                    graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                    return current with { Memory = written.Memory, State = current.State.WriteDword(CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)) };
                default:
                    NativeBattleScanProcess scan = current.Memory.Scan ?? throw new NotSupportedException("Effect68 requires its current shared text process.");
                    NativeBattleScanTextWindows windows = operation switch
                    {
                        NativeBattleEffect68WindowText text => scan.Text.Windows.SetText4A0410(text.Index, text.Pointer),
                        NativeBattleEffect68WindowFlags flags => scan.Text.Windows.SetFlags4A0700(flags.Index, flags.Flags),
                        NativeBattleEffect68WindowRect rectangle => scan.Text.Windows.SetRect4A07A0(rectangle.Index, rectangle.Rect),
                        NativeBattleEffect68WindowValue value => scan.Text.Windows.SetValue49FBC0(value.Index, value.Value),
                        NativeBattleEffect68OpenWindow open => scan.Text.Windows.Open4A0640(open.Index),
                        NativeBattleEffect68CloseWindow close => scan.Text.Windows.Close4A0680(close.Index),
                        _ => throw new NotSupportedException($"Effect68 emitted unsupported service {operation.GetType().Name}.")
                    };
                    return current with { Memory = current.Memory with { Scan = scan with { Text = scan.Text with { Windows = windows } } } };
            }
        }
        static ushort Word(NativeBattleEffectArena value, int offset) => System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(value.Read(offset, 2));
        static NativeBattleEffectArena Arena(NativeBattleScanByteOwner owner)
        {
            NativeBattleEffectArena result = NativeBattleEffectArena.Clear().MarkUnknown(0, owner.Bytes.Length);
            for (int index = 0; index < owner.Bytes.Length; index++)
            {
                if (owner.Known[index])
                {
                    result = result.Write(index, [owner.Bytes[index]]);
                }
            }
            return result;
        }
    }
}
