using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeLaserEye71(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Laser Eye requires its selected original effect.");
        NativeBattleLaserEye71Resources resources = effect.Binding.LaserEye71Resources ?? throw new NotSupportedException("Laser Eye requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Laser Eye requires its actual process memory.");
        NativeBattleLaserEye71Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Laser Eye requires its CRT stream."), memory.LaserEye71 ?? throw new NotSupportedException("Laser Eye requires its retained native globals; migrate the selected older snapshot explicitly.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Laser Eye requires its addressed memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Laser Eye requires its original interpolation constants.");
        NativeBattleLaserEye71Inputs inputs = new(nativeActionPackets, SelectActors, _ => Camera().CapturePackedView1D97778(), Execute)
        {
            RefreshRoot = RefreshRoot,
            PrepareTarget = PrepareTarget,
            AdvanceNodes = AdvanceNodes,
            PrepareBeam148 = (current, address) => NativeBattleLaserEye71BeamLower.Prepare(current, address, resources, LowerInputs()),
            DrawBeam148 = (current, address) => NativeBattleLaserEye71BeamLower.Draw(current, address, resources, LowerInputs()),
            EmitBeamParticles = (current, address) => NativeBattleLaserEye71BeamLower.Emit(current, address, resources, LowerInputs()),
            AdvanceParticles96 = AdvanceParticles
        };
        frame = initialize ? NativeBattleLaserEye71Controller.Initialize(frame, resources, inputs) : NativeBattleLaserEye71Controller.Advance(frame, resources, inputs);
        if (!initialize && frame.Effect.ReturnedDescriptor == 0)
        {
            NativeBattleEffectTaskTransfer transferred = NativeBattleEffectTaskMemory.TransferInactiveArenaBanks(frame.Memory, frame.Effect);
            frame = frame with { Memory = transferred.Memory, Effect = transferred.Effect };
        }
        Publish(frame);
        if (initialize && (effect.Parameters!.Flags79 & 1) == 0)
        {
            NativeCamera = Camera().RegisterControlResource(frame.Memory.Textures!.Animation);
        }
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Laser Eye requires its current camera.");
        NativeBattleCameraProjection Projection() => Camera().Projection ?? throw new NotSupportedException("Laser Eye requires its current projection.");
        NativeBattlePoseSource PoseSource(int actor) => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Laser Eye requires its current actor skeleton.");
        NativeBattleEffectTaskActors SelectActors(uint parameters, sbyte header, sbyte target) => NativeBattleCureController.SelectActors(effect.Parameters!, nativeActionPackets, parameters, header, target);
        NativeBattleLaserEye71LowerInputs LowerInputs() => new(Bone, (_, actor) => NativeBattlePackedMatrix32.FromMatrix(graphics.Models[actor].GeometryWorldMatrix ?? throw new NotSupportedException("Laser Eye requires its retained actor world matrix."), null), Look, _ => Camera().CapturePackedView1D97778(), SelectActors, Deliver, Projection(), colors);

        void Publish(NativeBattleLaserEye71Frame current)
        {
            NativeEffectMemory = current.Memory with { LaserEye71 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Laser Eye lost its addressed memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleLaserEye71Frame Import(NativeBattleLaserEye71Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Laser Eye external service lost its process memory.");
            return current with { Memory = latest, State = latest.LaserEye71 ?? throw new InvalidDataException("Laser Eye external service lost its module owner."), Staging = NativeFileStaging, Random = nativeCrtRandom ?? throw new InvalidDataException("Laser Eye external service lost its CRT stream.") };
        }

        NativeBattleLaserEye71Frame ObserveAnchor(NativeBattleLaserEye71Frame current, NativeBattleBoneAnchorProducer producer)
        {
            NativeFileStaging = current.Staging;
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            return current with { Memory = observed, Staging = NativeFileStaging };
        }

        NativeBattleLaserEye71PointStep Bone(NativeBattleLaserEye71Frame current, byte actor, byte selector, short distance)
        {
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[actor], PoseSource(actor), selector, distance);
            current = ObserveAnchor(current, producer);
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)current.Memory.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)current.Memory.ScriptedColorRegisters!.Mac[1]!.Value), unchecked((short)current.Memory.ScriptedColorRegisters!.Mac[2]!.Value)) : producer.DisabledPosition;
            return new(current, point);
        }

        NativeBattleLaserEye71MatrixStep Look(NativeBattleLaserEye71Frame current, NativeBattlePackedMatrix32 destination, NativeBattleVector direction, NativeBattleVector up)
        {
            NativeBattleLookBasisStep looked = NativeBattleLookBasis.Execute8DDA50(current.Memory, current.Staging ?? throw new NotSupportedException("Laser Eye basis requires its addressed memory."), destination, direction, up);
            return new(current with { Memory = looked.Memory, Staging = looked.Staging }, looked.Matrix);
        }

        NativeBattleLaserEye71Frame RefreshRoot(NativeBattleLaserEye71Frame current, uint address)
        {
            NativeBattleModulePoolTask task = NativeBattleEffectModelHelpers.UpdateCaster8DC740(NativeBattleLaserEye71Controller.Read(current, address), current.Effect.Parameters!, graphics.Models, PoseSource, producer => current = ObserveAnchor(current, producer));
            return NativeBattleLaserEye71Controller.Write(current, address, task);
        }

        NativeBattleLaserEye71Frame PrepareTarget(NativeBattleLaserEye71Frame current, uint address)
        {
            NativeBattleModulePoolTask task = NativeBattleEffectModelHelpers.UpdateTarget8DC610(NativeBattleLaserEye71Controller.Read(current, address), graphics.Models, PoseSource, producer => current = ObserveAnchor(current, producer));
            int actor = task.ReadByte(0x2D);
            NativeBattlePoseSource source = PoseSource(actor);
            task = NativeBattleEffectModelHelpers.UpdateBounds8DC870(task, source, NativeBattlePoseMatrices.ReadDecodedPose(graphics.Models[actor].SequenceState ?? throw new NotSupportedException("Laser Eye requires its target pose."), source));
            return NativeBattleLaserEye71Controller.Write(current, address, task);
        }

        NativeBattleLaserEye71TaskStep AdvanceNodes(NativeBattleLaserEye71Frame current, uint address)
        {
            NativeBattleLaserEye71NodeInputs nodeInputs = new(nativeActionPackets, actor => graphics.Models[actor], PoseSource, Projection(), Camera().Roll, colors, Look, Deliver)
            {
                ObserveBone = (value, actor, selector) => ObserveAnchor(value, NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[actor], PoseSource(actor), selector, 0))
            };
            return NativeBattleLaserEye71Nodes.Advance(current, address, resources, nodeInputs);
        }

        NativeBattleLaserEye71TaskStep AdvanceParticles(NativeBattleLaserEye71Frame current, uint address)
        {
            return NativeBattleLaserEye71Particles.Advance(current, address, resources, new(Camera().CapturePackedView1D97778(), Projection(), Camera().Roll, Deliver));
        }

        NativeBattleLaserEye71Frame Deliver(NativeBattleLaserEye71Frame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Laser Eye requires its current ordering table.");
            uint cursor = current.State.ReadDword(NativeBattleLaserEye71Profile.CursorGlobal);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, checked((int)(cursor - NativeBattleEffectNodeMemory.ArenaAddress)), primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, State = current.State.WriteDword(NativeBattleLaserEye71Profile.CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)) };
        }

        NativeBattleLaserEye71Frame Execute(NativeBattleLaserEye71Frame current, NativeBattleLaserEye71Operation operation)
        {
            switch (operation)
            {
                case NativeBattleLaserEye71RegisterCamera camera:
                    return current with { Memory = current.Memory with { Textures = (current.Memory.Textures ?? throw new NotSupportedException("Laser Eye requires its texture scheduler.")).Register5099A0(camera.Camera) } };
                case NativeBattleLaserEye71EnqueueTim tim:
                    NativeBattleEffectTextureState textures = (current.Memory.Textures ?? throw new NotSupportedException("Laser Eye requires its texture scheduler.")).EnqueueArenaTim505E30(current.Memory.Arena ?? throw new NotSupportedException("Laser Eye requires its working arena."), tim.ArenaOffset).State;
                    return current with { Memory = current.Memory with { Textures = textures } };
                case NativeBattleLaserEye71Stage depth:
                    NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Laser Eye requires its stage groups.");
                    NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = depth.Depth, ColorFlags = depth.ClearRgb ? group.ColorFlags & 0xFF000000u : group.ColorFlags }).ToImmutableArray() };
                    return current;
                case NativeBattleLaserEye71BindCaster bind:
                    NativeBattleGraphicsModel actor = RetainNativeDecodedPoseBeforeResourceChange(graphics.Models[bind.Actor], NativeGraphicsPoseResource(graphics, bind.Actor));
                    NativeBattleFileStaging staging = current.Staging!.Write(0x1590024, resources.CasterDat.Data.AsSpan());
                    var bound = NativeBattleVictoryResources.Bind5022C0(resources.CasterDat.Data, NativeModelCpuResources ?? throw new NotSupportedException("Laser Eye requires its CPU resource rows."), actor, bind.SaveAddress, 0x1590024, ReadPointer, WritePointer);
                    graphics = graphics with { Models = graphics.Models.SetItem(bind.Actor, bound.Model) };
                    return current with { Staging = staging };

                    NativeBattleResourcePointer? ReadPointer(uint address) => address >= NativeBattleLaserEye71Profile.SavedBindingAddress && address < NativeBattleLaserEye71Profile.SavedBindingAddress + 32 ? current.State.ReadResourcePointer(address) : staging.ReadResourcePointer(address);

                    void WritePointer(uint address, NativeBattleResourcePointer? pointer)
                    {
                        if (address < NativeBattleLaserEye71Profile.SavedBindingAddress || address >= NativeBattleLaserEye71Profile.SavedBindingAddress + 32)
                        {
                            throw new NotSupportedException("Laser Eye binding leaves its original saved rows.");
                        }
                        current = current with { State = current.State.WriteResourcePointer(address, pointer) };
                    }
                case NativeBattleLaserEye71Ring ring:
                    Publish(current);
                    graphics = ScheduleNativeRingSequence505C00(graphics, ring.Actor, ring.Sequence);
                    return Import(current);
                case NativeBattleLaserEye71Sound sound:
                    Publish(current);
                    PublishNativeModuleSound(new(sound.Value, sound.Flags, null) { Pan = sound.Pan });
                    return Import(current);
                case NativeBattleLaserEye71Contact contact:
                    NativeBattleModulePoolTask task = NativeBattleLaserEye71Controller.Read(current, contact.TaskAddress);
                    int header = checked(current.Effect.Parameters!.HeaderIndex7C + unchecked((sbyte)task.ReadByte(0x2A)));
                    int index = checked((nativeActionPackets.Headers[header].FirstContactIndex ?? throw new NotSupportedException("Laser Eye requires its actual contact pointer.")) + unchecked((sbyte)task.ReadByte(0x2B)));
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(index)] });
                    graphics = graphics with { PendingContacts = pending };
                    return Import(current);
                default:
                    throw new NotSupportedException("Laser Eye operation has no proved implementation.");
            }
        }
    }
}
