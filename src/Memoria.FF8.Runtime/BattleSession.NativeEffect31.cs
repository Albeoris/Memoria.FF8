using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect31Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeEffect31(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Effect31 requires its current selected effect.");
        NativeBattleEffect31Resources resources = effect.Binding.Effect31Resources ?? throw new NotSupportedException("Effect31 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect31 requires its current process memory.");
        NativeBattleEffect31Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect31 requires its current CRT stream."), memory.Effect31 ?? throw new NotSupportedException("Effect31 requires its retained state.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Effect31 requires its current addressed memory.") };
        NativeBattleEffect31Inputs inputs = new(definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Effect31 requires its arithmetic constants."), _ => nativeActionPackets, (_, actor) => graphics.Models[actor], (current, actor) => ReadNativeEffect31Pose(graphics, current, actor), _ => Camera().CapturePackedView1D97778(), _ => Camera().Projection ?? throw new NotSupportedException("Effect31 requires its current projection."), _ => Camera().Roll, (current, address, length) => ReadNativeEffect31Physical(graphics, current, address, length), (current, address, selector, fraction, retained) => ReadNativeEffect31Point(graphics, current, address, selector, fraction, retained), Execute)
        {
            ReadActorStack = (current, _, stateBody) => new(current, NativeBattleEffect31Stack.ReadMatrix(current.State.ReadCallbackStack(), stateBody)),
            ObserveActorStack = (current, task, stateBody, instruction, matrix) => current with { State = current.State with { CallbackStack = NativeBattleEffect31Stack.ObserveActor(current.State.ReadCallbackStack(), task, NativeBattleEffect31Controller.Read(current, task), stateBody, instruction, matrix) } }
        };
        frame = initialize ? NativeBattleEffect31Controller.Initialize(frame, resources, inputs) : NativeBattleEffect31Controller.Advance(frame, resources, inputs);
        PublishNativeEffect31Frame(frame);
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Effect31 requires its current camera.");

        NativeBattleEffect31Frame Execute(NativeBattleEffect31Frame current, NativeBattleEffect31Operation operation)
        {
            switch (operation)
            {
                case NativeBattleEffect31UpdateCaster8DC740 caster:
                    NativeBattleModulePoolTask casterTask = NativeBattleEffectModelHelpers.UpdateCaster8DC740(NativeBattleEffect31Controller.Read(current, caster.TaskAddress), current.Effect.Parameters ?? throw new NotSupportedException("Effect31 requires its current parameters."), graphics.Models, actor => ReadNativeEffect31Pose(graphics, current, actor), producer => current = ObserveNativeEffect31Point(current, producer));
                    return NativeBattleEffect31Controller.Write(current, caster.TaskAddress, casterTask);
                case NativeBattleEffect31UpdateTarget8DC610 target:
                    NativeBattleModulePoolTask targetTask = NativeBattleEffect31Controller.Read(current, target.TaskAddress);
                    int targetActor = targetTask.ReadByte(0x2D);
                    int anchors = 0;
                    targetTask = NativeBattleEffectModelHelpers.UpdateTarget8DC610(targetTask, graphics.Models, actor => ReadNativeEffect31Pose(graphics, current, actor), producer =>
                    {
                        anchors++;
                        uint temporary = BinaryPrimitives.ReadUInt32LittleEndian((current.Staging ?? throw new NotSupportedException("Effect31 requires its current target temporary cursor.")).Read(NativeBattleTemporaryMemory.Cursor, 4));
                        uint boneAddress = 0;
                        if (anchors == 2)
                        {
                            NativeBattleGraphicsModel model = graphics.Models[targetActor];
                            NativeBattlePoseSource source = ReadNativeEffect31Pose(graphics, current, targetActor);
                            int bone = (model.SequenceState?.Geometry5 ?? source.Skeleton[5]) & 0x7F;
                            if (bone >= source.BoneCount)
                            {
                                throw new InvalidDataException("Effect31 target F1 alias leaves its current skeleton.");
                            }
                            boneAddress = ReadNativeEffect31BoneAddress(graphics, current, targetActor, bone);
                        }
                        current = ObserveNativeEffect31Point(current, producer);
                        if (anchors == 2)
                        {
                            current = current with { State = current.State with { CallbackStack = NativeBattleEffect31Stack.ObserveTarget(current.State.ReadCallbackStack(), temporary, checked(0x1D972C0u + (uint)targetActor * 0x9C), boneAddress) } };
                        }
                    });
                    return NativeBattleEffect31Controller.Write(current, target.TaskAddress, targetTask);
                case NativeBattleEffect31UpdateBounds8DC870 bounds:
                    NativeBattleModulePoolTask boundsTask = NativeBattleEffect31Controller.Read(current, bounds.TaskAddress);
                    int boundsActor = boundsTask.ReadByte(0x2D);
                    NativeBattlePoseSource boundsSource = ReadNativeEffect31Pose(graphics, current, boundsActor);
                    ImmutableArray<NativeBattlePackedMatrix32> bones = ReadNativeEffect31BodyBones(graphics, current, boundsActor, boundsSource);
                    NativeBattleEffect31BoundsStep bounded = NativeBattleEffect31Bounds.Advance(current.Memory, current.Staging ?? throw new NotSupportedException("Effect31 requires its bounds temporary owner."), boundsTask, boundsSource, bones);
                    current = current with { Memory = bounded.Memory, Staging = bounded.Staging, State = current.State with { CallbackStack = NativeBattleEffect31Stack.ObserveBounds(current.State.ReadCallbackStack(), bounded.TemporaryAddress, bones.Length) } };
                    return NativeBattleEffect31Controller.Write(current, bounds.TaskAddress, bounded.Task);
                case NativeBattleEffect31UploadTim505E30 upload:
                    if (upload.Source != current.State.ReadDword(InitializerGlobal))
                    {
                        throw new NotSupportedException("Effect31 TIM source has no current initializer owner.");
                    }
                    NativeBattleEffectHeapAllocation tim = new(resources.MainTim.Identity, NativeBattleEffect31Controller.ReadBytes(current, upload.Source, resources.MainTim.Data.Length).Read(0, resources.MainTim.Data.Length).ToImmutableArray());
                    return current with { Memory = current.Memory with { Textures = (current.Memory.Textures ?? throw new NotSupportedException("Effect31 requires its current texture scheduler.")).EnqueueTim505E30(tim).State } };
                case NativeBattleEffect31RegisterCamera5099A0 registration when registration.Source == CameraAddress:
                    NativeBattleEffectTextureState textures = (current.Memory.Textures ?? throw new NotSupportedException("Effect31 requires its current texture scheduler.")).Register5099A0(resources.Camera);
                    NativeCamera = Camera().RegisterControlResource(textures.Animation);
                    return current with { Memory = current.Memory with { Textures = textures } };
                case NativeBattleEffect31StageTransition transition:
                    NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Effect31 requires its current stage groups.");
                    if (stage.Groups.IsDefault || stage.Groups.Length != 4 || (uint)transition.Group >= 4)
                    {
                        throw new InvalidDataException("Effect31 requires its actual four physical stage groups.");
                    }
                    NativeBattleStageGroup group = stage.Groups[transition.Group];
                    group = group with { ColorDepth = unchecked((short)transition.Intensity), ColorFlags = transition.ClearRgb ? group.ColorFlags & 0xFF000000u : group.ColorFlags };
                    NativeStage = stage with { Groups = stage.Groups.SetItem(transition.Group, group) };
                    return current;
                case NativeBattleEffect31Sound501330 sound:
                    if (sound.Source != SoundAddress)
                    {
                        throw new NotSupportedException("Effect31 selected an unsupported sound descriptor.");
                    }
                    PublishNativeEffect31Frame(current);
                    PublishNativeModuleSound(new(BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(sound.Source, 4)), sound.Flags, null) { Pan = sound.Pan });
                    return ImportNativeEffect31Frame(current);
                case NativeBattleEffect31Contact506690 contact:
                    PublishNativeEffect31Frame(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.AbsoluteIndex)] });
                    graphics = graphics with { PendingContacts = pending };
                    return ImportNativeEffect31Frame(current);
                case NativeBattleEffect31DrawBatch draw:
                    if (draw.CursorGlobal != CursorGlobal)
                    {
                        throw new NotSupportedException("Effect31 selected an unsupported private packet cursor.");
                    }
                    NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect31 requires its current ordering table.");
                    int cursor = NativeBattleEffect31PacketBank.Require(current, draw.Batch);
                    NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Effect31 requires its current packet arena.");
                    NativeBattleGfGeometryBatch resolved = NativeBattlePacketBatchWords.ResolveWithKnownness(draw.Batch, cursor, NativeBattleEffectArena.Length, offset =>
                    {
                        (byte[] bytes, bool[] known) = arena.ReadSnapshot(offset, 1);
                        return (bytes[0], known[0]);
                    });
                    NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, resolved, draw.Flags);
                    graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                    return current with { Memory = written.Memory, State = current.State.WriteDword(draw.CursorGlobal, checked(NativeBattleEffect31Controller.CommonAddress + (uint)written.Cursor)) };
                case NativeBattleEffect31DrawOverlay overlay:
                    NativeBattlePrimitiveFrame overlayFrame = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect31 overlay requires its current ordering table.");
                    int overlayCursor = NativeBattleEffect31PacketBank.Require(current, 36);
                    NativeBattleGeometryPrimitive quad = new(7, [0x05000000, (overlay.Color & 0xFFFFFF) | 0x2A000000, 0, 320, 216u << 16, (216u << 16) | 320], [0, 0, 0, 0]);
                    NativeBattleGeometryPrimitive mode = new(7, [0x02000000, 0xE100002A, 0], [0, 0, 0, 0]);
                    NativeBattleModulePacketBatch batch = new([new(quad, 0, 1), new(mode, 24, 0)], 36);
                    NativeBattleEffectPacketWrite overlayWritten = NativeBattleEffectPacketArena.Append(current.Memory, overlayCursor, overlayFrame.Pending, batch, NativeBattleEffectGouraudWrites.FullDword);
                    graphics = graphics with { PrimitiveFrame = overlayFrame with { Pending = overlayWritten.Ordering } };
                    return current with { Memory = overlayWritten.Memory, State = current.State.WriteDword(CursorGlobal, checked(NativeBattleEffect31Controller.CommonAddress + (uint)overlayWritten.Cursor)) };
                default:
                    throw new NotSupportedException($"Effect31 emitted unsupported service {operation.GetType().Name}.");
            }
        }
    }

    private void PublishNativeEffect31Frame(NativeBattleEffect31Frame frame)
    {
        NativeEffectMemory = frame.Memory with { Effect31 = frame.State };
        NativeFileStaging = frame.Staging ?? throw new NotSupportedException("Effect31 lost its current addressed memory.");
        nativeCrtRandom = frame.Random;
    }

    private NativeBattleEffect31Frame ImportNativeEffect31Frame(NativeBattleEffect31Frame frame)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect31 service lost its current process memory.");
        return frame with { Memory = memory, State = memory.Effect31 ?? throw new NotSupportedException("Effect31 service lost its retained state."), Staging = NativeFileStaging ?? throw new NotSupportedException("Effect31 service lost its addressed memory."), Random = nativeCrtRandom ?? throw new NotSupportedException("Effect31 service lost its CRT stream.") };
    }
}
