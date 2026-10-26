using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeEffect30(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Effect30 requires its selected original effect.");
        NativeBattleEffect30Resources resources = effect.Binding.Effect30Resources ?? throw new NotSupportedException("Effect30 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect30 requires its actual process memory.");
        NativeBattleEffect30Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect30 requires its CRT stream."), memory.Effect30 ?? throw new NotSupportedException("Effect30 requires its retained native globals; migrate the selected older snapshot explicitly.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Effect30 requires its addressed memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Effect30 requires its original interpolation constants.");
        NativeBattleEffect30Inputs inputs = new(nativeActionPackets, SelectActors, _ => Camera().CapturePackedView1D97778(), Execute)
        {
            RefreshRoot = RefreshRoot,
            PrepareTarget = PrepareTarget,
            AdvanceNodes = AdvanceNodes,
            PrepareBeam144 = (current, address) => NativeBattleEffect30BeamLower.Prepare(current, address, resources, LowerInputs()),
            DrawBeam144 = (current, address) => NativeBattleEffect30BeamLower.Draw(current, address, resources, LowerInputs()),
            EmitBeamParticles = (current, address) => NativeBattleEffect30BeamLower.Emit(current, address, resources, LowerInputs()),
            AdvanceParticles96 = AdvanceParticles
        };
        frame = initialize ? NativeBattleEffect30Controller.Initialize(frame, resources, inputs) : NativeBattleEffect30Controller.Advance(frame, resources, inputs);
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

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Effect30 requires its current camera.");
        NativeBattleCameraProjection Projection() => Camera().Projection ?? throw new NotSupportedException("Effect30 requires its current projection.");
        NativeBattlePoseSource PoseSource(int actor) => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect30 requires its current actor skeleton.");
        NativeBattleEffectTaskActors SelectActors(uint parameters, sbyte header, sbyte target) => NativeBattleCureController.SelectActors(effect.Parameters!, nativeActionPackets, parameters, header, target);
        NativeBattleEffect30LowerInputs LowerInputs() => new(Bone, (_, actor) => NativeBattlePackedMatrix32.FromMatrix(graphics.Models[actor].GeometryWorldMatrix ?? throw new NotSupportedException("Effect30 requires its retained actor world matrix."), null), Look, _ => Camera().CapturePackedView1D97778(), SelectActors, Deliver, Projection(), colors);

        void Publish(NativeBattleEffect30Frame current)
        {
            NativeEffectMemory = current.Memory with { Effect30 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Effect30 lost its addressed memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleEffect30Frame Import(NativeBattleEffect30Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Effect30 external service lost its process memory.");
            return current with { Memory = latest, State = latest.Effect30 ?? throw new InvalidDataException("Effect30 external service lost its module owner."), Staging = NativeFileStaging, Random = nativeCrtRandom ?? throw new InvalidDataException("Effect30 external service lost its CRT stream.") };
        }

        NativeBattleEffect30Frame ObserveAnchor(NativeBattleEffect30Frame current, NativeBattleBoneAnchorProducer producer)
        {
            NativeFileStaging = current.Staging;
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            return current with { Memory = observed, Staging = NativeFileStaging };
        }

        NativeBattleEffect30PointStep Bone(NativeBattleEffect30Frame current, byte actor, byte selector, short distance)
        {
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[actor], PoseSource(actor), selector, distance);
            current = ObserveAnchor(current, producer);
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)current.Memory.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)current.Memory.ScriptedColorRegisters!.Mac[1]!.Value), unchecked((short)current.Memory.ScriptedColorRegisters!.Mac[2]!.Value)) : producer.DisabledPosition;
            return new(current, point);
        }

        NativeBattleEffect30MatrixStep Look(NativeBattleEffect30Frame current, NativeBattlePackedMatrix32 destination, NativeBattleVector direction, NativeBattleVector up)
        {
            NativeBattleLookBasisStep looked = NativeBattleLookBasis.Execute8DDA50(current.Memory, current.Staging ?? throw new NotSupportedException("Effect30 basis requires its addressed memory."), destination, direction, up);
            return new(current with { Memory = looked.Memory, Staging = looked.Staging }, looked.Matrix);
        }

        NativeBattleEffect30Frame RefreshRoot(NativeBattleEffect30Frame current, uint address)
        {
            NativeBattleModulePoolTask task = NativeBattleEffectModelHelpers.UpdateCaster8DC740(NativeBattleEffect30Controller.Read(current, address), current.Effect.Parameters!, graphics.Models, PoseSource, producer => current = ObserveAnchor(current, producer));
            return NativeBattleEffect30Controller.Write(current, address, task);
        }

        NativeBattleEffect30Frame PrepareTarget(NativeBattleEffect30Frame current, uint address)
        {
            NativeBattleModulePoolTask task = NativeBattleEffectModelHelpers.UpdateTarget8DC610(NativeBattleEffect30Controller.Read(current, address), graphics.Models, PoseSource, producer => current = ObserveAnchor(current, producer));
            int actor = task.ReadByte(0x2D);
            NativeBattlePoseSource source = PoseSource(actor);
            task = NativeBattleEffectModelHelpers.UpdateBounds8DC870(task, source, NativeBattlePoseMatrices.ReadDecodedPose(graphics.Models[actor].SequenceState ?? throw new NotSupportedException("Effect30 requires its target pose."), source));
            return NativeBattleEffect30Controller.Write(current, address, task);
        }

        NativeBattleEffect30TaskStep AdvanceNodes(NativeBattleEffect30Frame current, uint address)
        {
            NativeBattleEffect30NodeInputs nodeInputs = new(nativeActionPackets, actor => graphics.Models[actor], PoseSource, Projection(), Camera().Roll, colors, Look, Deliver)
            {
                ObserveBone = (value, actor, selector) => ObserveAnchor(value, NativeBattlePoseMatrices.ResolveBoneAnchor502170(graphics.Models[actor], PoseSource(actor), selector, 0))
            };
            return NativeBattleEffect30Nodes.Advance(current, address, resources, nodeInputs);
        }

        NativeBattleEffect30TaskStep AdvanceParticles(NativeBattleEffect30Frame current, uint address)
        {
            return NativeBattleEffect30Particles.Advance(current, address, resources, new(Camera().CapturePackedView1D97778(), Projection(), Camera().Roll, Deliver));
        }

        NativeBattleEffect30Frame Deliver(NativeBattleEffect30Frame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect30 requires its current ordering table.");
            uint cursor = current.State.ReadDword(NativeBattleEffect30Profile.CursorGlobal);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, checked((int)(cursor - NativeBattleEffectNodeMemory.ArenaAddress)), primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, State = current.State.WriteDword(NativeBattleEffect30Profile.CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)), Effect = current.Effect with { DrawArenaOffset = written.Cursor } };
        }

        NativeBattleEffect30Frame Execute(NativeBattleEffect30Frame current, NativeBattleEffect30Operation operation)
        {
            switch (operation)
            {
                case NativeBattleEffect30RegisterCamera camera:
                    return current with { Memory = current.Memory with { Textures = (current.Memory.Textures ?? throw new NotSupportedException("Effect30 requires its texture scheduler.")).Register5099A0(camera.Camera) } };
                case NativeBattleEffect30EnqueueTim tim:
                    NativeBattleEffectTextureState textures = (current.Memory.Textures ?? throw new NotSupportedException("Effect30 requires its texture scheduler.")).EnqueueArenaTim505E30(current.Memory.Arena ?? throw new NotSupportedException("Effect30 requires its working arena."), tim.ArenaOffset).State;
                    return current with { Memory = current.Memory with { Textures = textures } };
                case NativeBattleEffect30Stage depth:
                    NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Effect30 requires its stage groups.");
                    NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = depth.Depth, ColorFlags = depth.ClearRgb ? group.ColorFlags & 0xFF000000u : group.ColorFlags }).ToImmutableArray() };
                    return current;
                case NativeBattleEffect30Sound sound:
                    Publish(current);
                    PublishNativeModuleSound(new(sound.Value, sound.Flags, null) { Pan = sound.Pan });
                    return Import(current);
                case NativeBattleEffect30Contact contact:
                    NativeBattleModulePoolTask task = NativeBattleEffect30Controller.Read(current, contact.TaskAddress);
                    int header = checked(current.Effect.Parameters!.HeaderIndex7C + unchecked((sbyte)task.ReadByte(0x2A)));
                    int index = checked((nativeActionPackets.Headers[header].FirstContactIndex ?? throw new NotSupportedException("Effect30 requires its actual contact pointer.")) + unchecked((sbyte)task.ReadByte(0x2B)));
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(index)] });
                    graphics = graphics with { PendingContacts = pending };
                    return Import(current);
                default:
                    throw new NotSupportedException("Effect30 operation has no proved implementation.");
            }
        }
    }
}
