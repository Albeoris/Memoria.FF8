using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleShockwavePulsar260Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeShockwavePulsar260(NativeBattleGraphicsState graphics) => RunNativeShockwavePulsar260(graphics, true);
    private NativeBattleGraphicsState AdvanceNativeShockwavePulsar260(NativeBattleGraphicsState graphics) => RunNativeShockwavePulsar260(graphics, false);

    private NativeBattleGraphicsState RunNativeShockwavePulsar260(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Shockwave Pulsar requires its selected original effect.");
        NativeBattleShockwavePulsar260Resources resources = effect.Binding.ShockwavePulsar260Resources ?? throw new NotSupportedException("Shockwave Pulsar requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Shockwave Pulsar requires its current process memory.");
        NativeBattleShockwavePulsar260Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Shockwave Pulsar requires its CRT stream."), memory.ShockwavePulsar260 ?? throw new NotSupportedException("Shockwave Pulsar requires its retained native state.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Shockwave Pulsar requires its addressed memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Shockwave Pulsar requires its original color constants.");
        NativeBattleShockwavePulsar260Inputs inputs = new(nativeActionPackets, checked((ushort)(definition.EncounterId ?? throw new NotSupportedException("Shockwave Pulsar requires its current encounter identity."))), actor => graphics.Models[actor], Position, actor => graphics.Models[actor].ReadLinkedModelIndex140(), _ => Camera().CapturePackedView1D97778(), _ => Camera(), Anchor, PollIo, Execute);
        frame = initialize ? NativeBattleShockwavePulsar260Controller.Initialize(frame, resources, inputs) : NativeBattleShockwavePulsar260Controller.Advance(frame, resources, inputs);
        Publish(frame);
        if (initialize)
        {
            NativeCamera = Camera().RegisterControlResource(frame.Memory.Textures!.Animation);
        }
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Shockwave Pulsar requires its current camera process.");
        NativeBattleEffectArena Position(int actor) => NativeBattleEffectNodeContextMemory.Copy(NativeBattleRaldo19ModelMemory.Capture(graphics.Models[actor]), 0x1C, NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 0, 8);
        int Actor(uint pointer)
        {
            uint offset = unchecked(pointer - 0x1D972C0u);
            if (offset % 156 != 0 || offset / 156 >= graphics.Models.Length)
            {
                throw new InvalidDataException("Shockwave Pulsar model pointer leaves its current physical model directory.");
            }
            return checked((int)(offset / 156));
        }
        void Publish(NativeBattleShockwavePulsar260Frame current)
        {
            NativeEffectMemory = current.Memory with { ShockwavePulsar260 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Shockwave Pulsar lost its addressed memory.");
            nativeCrtRandom = current.Random;
            graphics = graphics with { OriginalEffect = current.Effect };
        }
        NativeBattleShockwavePulsar260Frame Import(NativeBattleShockwavePulsar260Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Shockwave Pulsar external service lost its process memory.");
            return current with { Memory = latest, State = latest.ShockwavePulsar260 ?? throw new InvalidDataException("Shockwave Pulsar lost its retained native state."), Effect = graphics.OriginalEffect ?? throw new InvalidDataException("Shockwave Pulsar lost its effect tasks."), Staging = NativeFileStaging, Random = nativeCrtRandom ?? throw new InvalidDataException("Shockwave Pulsar lost its CRT stream.") };
        }
        NativeBattleShockwavePulsar260AnchorStep Anchor(NativeBattleShockwavePulsar260Frame current, int actor)
        {
            Publish(current);
            NativeBattleGraphicsModel model = graphics.Models[actor];
            if ((model.Flags0 & 2) != 0)
            {
                NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Shockwave Pulsar requires its current skeleton.");
                foreach (byte selector in new byte[] { 0xF0, 0xF1 })
                {
                    NativeFileStaging = current.Staging;
                    current = current with { Memory = ObserveNativeBoneAnchor(current.Memory, NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, source, selector, 0)), Staging = NativeFileStaging };
                }
            }
            NativeBattleModuleAnchor anchor = ReadNativeModelAnchor(graphics, actor);
            return new(current, anchor with { W = (model.Transform ?? throw new NotSupportedException("Shockwave Pulsar requires its current model heading.")).Angle36 });
        }
        NativeBattleShockwavePulsar260Frame Deliver(NativeBattleShockwavePulsar260Frame current, NativeBattleGfGeometryBatch batch)
        {
            uint cursor = current.State.ReadDword(PrimaryCursorGlobal);
            uint end = current.State.ReadDword(SecondaryCursorGlobal);
            uint start = checked(end - PacketBankBytes);
            uint working = current.State.ReadDword(WorkingGlobal);
            if (start != working + FirstPacketOffset && start != working + SecondPacketOffset || cursor < start || cursor > end || batch.AllocationBytes < 0 || batch.AllocationBytes > end - cursor)
            {
                throw new InvalidDataException("Shockwave Pulsar drawing leaves its current physical packet bank.");
            }
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Shockwave Pulsar requires its current PRIVATE ordering table.");
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, checked((int)(cursor - ArenaAddress)), primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, State = current.State.WriteDword(PrimaryCursorGlobal, checked(ArenaAddress + (uint)written.Cursor)) };
        }
        NativeBattleShockwavePulsar260Frame QueueFile(NativeBattleShockwavePulsar260Frame current, NativeBattleEffectFileRequest request)
        {
            NativeBattleEffectFileAsset file = resources.Files.Single(file => file.FileId == request.FileId);
            NativeBattleEffectArena arena = (current.Memory.Arena ?? throw new NotSupportedException("Shockwave Pulsar reload requires its physical arena.")).Write(NativeBattleWaterExecutionFields.ArenaOffset(request.DestinationAddress, file.Data.Length), file.Data.AsSpan());
            NativeFileIo = (NativeFileIo ?? throw new NotSupportedException("Shockwave Pulsar reload requires its IO scheduler.")).Queue482610(file.Data.Length, request.CompletionCallback, null);
            return current with { Memory = current.Memory with { Arena = arena } };
        }
        NativeBattleShockwavePulsar260IoStep PollIo(NativeBattleShockwavePulsar260Frame current, int index)
        {
            NativeBattleEffectIoPoll poll = NativeBattleEffectIo.Select5342C0(current.Memory, index);
            current = current with { Memory = poll.Memory };
            return new(poll.Request is { } request ? QueueFile(current, request) : current, poll.Busy);
        }
        NativeBattleShockwavePulsar260Frame Execute(NativeBattleShockwavePulsar260Frame current, NativeBattleShockwavePulsar260Operation operation)
        {
            Publish(current);
            switch (operation)
            {
                case NativeBattleShockwavePulsar260BindCaster bind:
                    if (bind.ResourceAddress != 0x111B3CC || bind.SaveAddress != current.Effect.Pools.Single(pool => pool.DescriptorAddress == MainDescriptor).BankAddress + 0x178)
                    {
                        throw new InvalidDataException("Shockwave Pulsar binding leaves its original resource and current saved row.");
                    }
                    NativeBattleGraphicsModel boundActor = RetainNativeDecodedPoseBeforeResourceChange(graphics.Models[bind.Actor], NativeGraphicsPoseResource(graphics, bind.Actor));
                    NativeBattleFileStaging bindingStaging = current.Staging!.Write(bind.ResourceAddress, resources.CasterDat.Data.AsSpan());
                    var bound = NativeBattleVictoryResources.Bind5022C0(resources.CasterDat.Data, NativeModelCpuResources ?? throw new NotSupportedException("Shockwave Pulsar requires its current CPU resource rows."), boundActor, bind.SaveAddress, bind.ResourceAddress, ReadPointer, WritePointer);
                    graphics = graphics with { Models = graphics.Models.SetItem(bind.Actor, bound.Model) };
                    return current with { Staging = bindingStaging };

                    NativeBattleResourcePointer? ReadPointer(uint address)
                    {
                        if (NativeBattleEffectTaskMemory.TryReadPointer(current.Effect.Pools, address, out NativeBattleResourcePointer? pointer))
                        {
                            return pointer;
                        }
                        return IsNativeEffectArenaAddress(address) ? current.Memory.Arena!.ReadResourcePointer(NativeBattleWaterExecutionFields.ArenaOffset(address, 4)) : bindingStaging.ReadResourcePointer(address);
                    }
                    void WritePointer(uint address, NativeBattleResourcePointer? pointer)
                    {
                        current = current with { Effect = current.Effect with { Pools = NativeBattleEffectTaskMemory.WritePointer(current.Effect.Pools, address, pointer) } };
                    }
                case NativeBattleShockwavePulsar260RingSequence sequence:
                    graphics = ScheduleNativeRingSequence505C00(graphics, sequence.Actor, sequence.Sequence);
                    return Import(current);
                case NativeBattleShockwavePulsar260ActorFlags flags:
                    graphics = graphics with { Models = graphics.Models.SetItem(flags.Actor, graphics.Models[flags.Actor] with { Flags0 = unchecked((ushort)((graphics.Models[flags.Actor].Flags0 & ~flags.ClearMask) | flags.SetMask)) }) };
                    return current;
                case NativeBattleShockwavePulsar260ActorWrite write:
                    NativeBattleGraphicsModel changed = graphics.Models[write.Actor];
                    if (write.Length == 8)
                    {
                        changed = NativeBattleRaldo19ModelMemory.Apply(NativeBattleRaldo19ModelMemory.Apply(changed, write.Offset, write.Bytes, 0, 4), write.Offset + 4, write.Bytes, 4, 4);
                    }
                    else
                    {
                        changed = NativeBattleRaldo19ModelMemory.Apply(changed, write.Offset, write.Bytes, 0, write.Length);
                    }
                    graphics = graphics with { Models = graphics.Models.SetItem(write.Actor, changed) };
                    return current;
                case NativeBattleShockwavePulsar260StageWrite fade:
                    NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Shockwave Pulsar requires its current stage groups.");
                    NativeStage = stage with { Groups = stage.Groups.Select((group, index) => (fade.GroupMask & (1 << index)) != 0 && (group.Flags & 1) != 0 ? group with { ColorDepth = fade.Depth, ColorFlags = fade.ColorFlags } : group).ToImmutableArray() };
                    return current;
                case NativeBattleShockwavePulsar260StageFlags flags:
                    NativeBattleStageState stageFlags = NativeStage ?? throw new NotSupportedException("Shockwave Pulsar requires its current stage flags.");
                    NativeStage = stageFlags with { Groups = stageFlags.Groups.Select((group, index) => (flags.GroupMask & (1 << index)) != 0 ? group with { Flags = unchecked((byte)((group.Flags & ~flags.ClearMask) | flags.SetMask)) } : group).ToImmutableArray() };
                    return current;
                case NativeBattleShockwavePulsar260Flash flash:
                    return Deliver(current, NativeBattleRinoa89FadeDrawing.Draw7040B0(flash.Red, flash.Green, flash.Blue, flash.Factor));
                case NativeBattleShockwavePulsar260SoundPlay sound:
                    PublishNativeModuleSound(new(sound.Value, sound.Flags, null) { Pan = 128, RawPan = 128, Volume = unchecked((uint)sound.Volume) });
                    return Import(current);
                case NativeBattleShockwavePulsar260ContactAll:
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = nativeActionPackets.ReadHeaderContacts506BA0(current.Effect.Parameters!.HeaderIndex7C) });
                    graphics = graphics with { PendingContacts = pending };
                    return Import(current);
                case NativeBattleShockwavePulsar260CameraPose pose:
                    NativeCamera = Camera().WriteEffectGlobals(pose.Pose);
                    return current;
                case NativeBattleShockwavePulsar260CameraFocal focal:
                    NativeCamera = Camera() with { FocalLength = focal.Value, CurrentPose = Camera().CurrentPose is { } currentPose ? currentPose with { FocalLength = focal.Value } : null };
                    return current;
                case NativeBattleShockwavePulsar260CameraControl:
                    NativeCamera = Camera().Apply50A730();
                    return current;
                case NativeBattleShockwavePulsar260CameraRestore:
                    NativeBattleCameraState camera = Camera();
                    NativeBattleCameraPose saved = camera.CapturedPose ?? throw new NotSupportedException("Shockwave Pulsar requires its actual captured camera pose and focal owner.");
                    NativeCamera = camera with { CurrentPose = saved with { Roll = camera.Roll }, FocalLength = saved.FocalLength, EyePaddingF6 = camera.CapturedEyePadding806, TargetPaddingFE = camera.CapturedTargetPadding80E };
                    return current;
                case NativeBattleShockwavePulsar260AdvanceIo:
                    NativeBattleEffectIoAdvance advanced = NativeBattleEffectIo.Advance534210(current.Memory);
                    current = current with { Memory = advanced.Memory };
                    return advanced.Request is { } request ? QueueFile(current, request) : current;
                case NativeBattleShockwavePulsar260CaptureBounds bounds:
                    (current, graphics) = CaptureNativeShockwavePulsar260Bounds(current, graphics, bounds.Actor, bounds.DestinationAddress);
                    return current;
                case NativeBattleShockwavePulsar260DrawBody body:
                    (current, graphics) = DrawNativeShockwavePulsar260Body(current, graphics, body, colors);
                    return current;
                case NativeBattleShockwavePulsar260ActNode act:
                    if (!NativeBattleShockwavePulsar260ActDrawing.Visible(act.Node))
                    {
                        return current;
                    }
                    uint dispatchAddress = BinaryPrimitives.ReadUInt32LittleEndian(act.Context20.Read(8, 4));
                    ImmutableArray<byte> dispatch = dispatchAddress switch { 0x112D190 => resources.DefaultDispatch, 0x112D1B4 => resources.MainDispatch, _ => throw new NotSupportedException("Shockwave Pulsar requires its original addressed dispatch mask.") };
                    return NativeBattleShockwavePulsar260ActDrawing.EmitNode(current, resources.Act(act.SourceAddress), act.Node, act.Context20, Camera().CapturePackedView1D97778(), dispatch[act.Node.GeometryIndex], Camera().Projection ?? throw new NotSupportedException("Shockwave Pulsar requires its ACT projection."), colors, Deliver);
                case NativeBattleShockwavePulsar260DrawPuffs:
                    return NativeBattleShockwavePulsar260ParticlesDrawing.DrawPuffs(current, resources, Camera().CapturePackedView1D97778(), Camera().Projection ?? throw new NotSupportedException("Shockwave Pulsar requires its puff projection."), Deliver);
                case NativeBattleShockwavePulsar260DrawSecondaryParticles:
                    return NativeBattleShockwavePulsar260ParticlesDrawing.DrawSecondaryParticles(current, resources, Camera().CapturePackedView1D97778(), Camera().Projection ?? throw new NotSupportedException("Shockwave Pulsar requires its secondary projection."), colors, address => NativeBattleShockwavePulsar260Controller.ReadView(current, address, 36), pointer => (graphics.Models[Actor(pointer)].Transform ?? throw new NotSupportedException("Shockwave Pulsar requires its target position.")).Position28, Deliver);
                default:
                    throw new NotSupportedException("Shockwave Pulsar selected an unsupported native service.");
            }
        }
    }
}
