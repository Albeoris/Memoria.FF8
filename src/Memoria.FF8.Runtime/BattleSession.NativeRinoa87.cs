using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeRinoa87(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new InvalidDataException("Native Rinoa87 requires its actual selected module.");
        NativeBattleRinoa87Resources resources = effect.Binding.Rinoa87Resources ?? throw new NotSupportedException("Native Rinoa87 requires its original resources.");
        NativeBattleRinoa87Frame frame = NativeBattleRinoa87Controller.Initialize(NativeEffectMemory ?? throw new NotSupportedException("Native Rinoa87 requires its retained process."), effect, nativeActionPackets);
        NativeEffectMemory = frame.Memory with { Rinoa87 = frame.State };
        return graphics with { OriginalEffect = frame.Effect };
    }

    private NativeBattleGraphicsState AdvanceNativeRinoa87(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new InvalidDataException("Native Rinoa87 requires its actual selected module.");
        NativeBattleRinoa87Resources resources = effect.Binding.Rinoa87Resources ?? throw new NotSupportedException("Native Rinoa87 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native Rinoa87 requires its retained process.");
        NativeBattleRinoa87Frame frame = new(memory, effect, memory.Rinoa87 ?? throw new NotSupportedException("Native Rinoa87 requires its entered process storage."));
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Rinoa87 requires its published projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Rinoa87 requires its published view matrix.");
        NativeBattleGraphicsDefinition rules = definition.NativeGraphics ?? throw new NotSupportedException("Native Rinoa87 requires its original graphics rules.");
        NativeBattleSummonColors colors = rules.Rules.SummonColors ?? throw new NotSupportedException("Native Rinoa87 requires its actual color constants.");
        NativeCrtRandomState random = nativeCrtRandom ?? throw new NotSupportedException("Native Rinoa87 requires its retained CRT generator.");
        NativeBattleRinoa87Inputs controller = null!;
        controller = new(nativeActionPackets, view, index => graphics.Models[index], index => NativeGraphicsPoseResource(graphics, index).PoseSource ?? throw new NotSupportedException("Native Rinoa87 requires its actual actor skeleton."), AdvanceLeaf, ReloadCommon);
        frame = NativeBattleRinoa87Controller.Advance(frame, controller);
        NativeEffectMemory = frame.Memory with { Rinoa87 = frame.State };
        nativeCrtRandom = random;
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleLogicalOrdering Ordering() => (graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Rinoa87 requires its initialized ordering table.")).Pending;
        void StoreOrdering(NativeBattleLogicalOrdering ordering) => graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame! with { Pending = ordering } };
        NativeBattleRinoa87Resources CurrentResources() => resources with { DefaultColor44 = (NativeStage?.ModelColors ?? throw new NotSupportedException("Native8DD0F0 requires its current shared model colors.")).Color44 };
        NativeBattleEffectTaskActors SelectActors(uint pointer, sbyte group, sbyte target) => NativeBattleCureController.SelectActors(effect.Parameters!, nativeActionPackets, pointer, group, target);
        NativeBattleVector ReadBone(NativeBattleRinoa87Frame current, uint body, byte selector) => NativeBattleRinoa87BodyDrawing.ReadBone502170(current, body, resources.Model, selector);

        NativeBattleRinoa87LeafStep AdvanceLeaf(NativeBattleRinoa87Frame current, uint address)
        {
            NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(current.Effect.Pools).Read(address);
            switch (task.Callback)
            {
                case 0x773C70:
                    return NativeBattleRinoa87Controller.AdvanceTarget(current, address, new(controller, (next, target) => NativeBattleRinoa87Scene.InitializeTarget(next, target, controller, resources.Io, Minimum), next => NativeBattleRinoa87Scene.Read(next).Word(72), next => NativeBattleRinoa87Scene.Read(next).Word(76), Contact));
                case 0x774240:
                    return NativeBattleRinoa87Scene.Advance(current, address, new(controller, Execute));
                case 0x773E30:
                    NativeBattleRinoa87IoAdvance io = NativeBattleRinoa87Io.Advance773E30(task, current.State.Io ?? throw new NotSupportedException("Native773E30 requires its initialized IO workspace."));
                    current = current with { State = current.State with { Io = io.State }, Effect = current.Effect with { Pools = new NativeBattleEffectHierarchy(current.Effect.Pools).Write(address, io.Task).Pools } };
                    if (io.Request is { } request)
                    {
                        NativeBattleRinoa87IoQueued queued = NativeBattleRinoa87Io.CopyAndQueueFile(io.State, current.Memory, NativeFileIo ?? throw new NotSupportedException("Native773E30 requires its retained asynchronous file queue."), resources.Io.Files.Single(file => file.FileId == request.FileId), (destination, bytes) => NativeFileStaging = (NativeFileStaging ?? NativeBattleFileStaging.Unknown()).Write(destination, bytes.AsSpan()));
                        NativeFileIo = queued.FileIo;
                        current = current with { Memory = queued.Memory };
                    }
                    if (io.ReadyToComplete)
                    {
                        current = current with { Effect = current.Effect with { Pools = new NativeBattleEffectHierarchy(current.Effect.Pools).Complete8DC530(address).Pools } };
                    }
                    return new(current, io.ReadyToComplete ? 2 : 0);
                case 0x7751A0:
                    return NativeBattleRinoa87Body.Advance(current, address, new(controller, resources.Model, resources.TargetCurve, (_, actor) => graphics.Models[actor], (next, actor, model) =>
                    {
                        graphics = graphics with { Models = graphics.Models.SetItem(actor, model) };
                        return next;
                    }, (_, actor) => Minimum(actor), (_, actor, selector) => ReadNativeGraphicsBone(graphics, actor, selector, 0), Execute, DrawBody));
                case 0x7743A0:
                    return NativeBattleRinoa87Camera.Advance(current, address, new(resources.Camera, actor => graphics.Models[actor].Rotation14 ?? throw new NotSupportedException("Native774450 requires its caster yaw."), next =>
                    {
                        NativeBattleRinoa87SceneMemory scene = NativeBattleRinoa87Scene.Read(next);
                        uint body = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(scene.Settings.Read(24, 4));
                        return new NativeBattleEffectHierarchy(next.Effect.Pools).Read(body).ReadBytes(328, 8).ToImmutableArray();
                    }, next => (next, Roll()), (next, camera) =>
                    {
                        NativeCamera = NativeBattleMoogleCamera.Publish733150(camera, NativeCamera ?? throw new NotSupportedException("Native774850 requires its retained camera globals."));
                        return next;
                    }));
                case 0x775720:
                case 0x77DFC0:
                case 0x77EAB0:
                case 0x77EC00:
                    return NativeBattleRinoa87Particles.Advance(current, address, new((model, selector) => ReadBone(current, checked(model - 48), selector), Roll, (next, particle, callback) =>
                    {
                        NativeBattleRinoa87ParticleDraw draw = NativeBattleRinoa87ParticleDrawing.Draw(new NativeBattleEffectHierarchy(next.Effect.Pools).Read(particle), callback, resources.Sprite, resources.Meshes, view, projection, colors, NativeCamera!.Roll);
                        return Append(next, draw.Packets, draw.CommonCursor);
                    }));
                case 0x77E800:
                    NativeBattleRinoa87ParticleEmission impact = NativeBattleRinoa87Emitters.Advance77E800(new(current.Effect.Pools), address, random, resources.FirstImpactCounts, resources.SecondImpactCounts, SelectActors);
                    random = impact.Random;
                    NativeBattleRinoa87Frame impacted = current with { Effect = current.Effect with { Pools = impact.Hierarchy.Pools } };
                    return new(impacted, impact.Completed ? 2 : 0);
                case 0x7764B0:
                case 0x77E6C0:
                    return NativeBattleRinoa87Program.Advance(current, address, new(pointer => pointer == resources.Program.Address ? resources.Program : pointer == 0x19CF018 ? new(pointer, (NativeFileStaging ?? throw new NotSupportedException("Native77E600 requires its completed external file407 owner.")).Read(pointer, resources.Io.Files[0].Data.Length).ToImmutableArray()) : throw new NotSupportedException("Native776580 programme source has no original addressed owner."), (next, model, selector) => ReadBone(next, checked(model - 48), selector), (_, actor) => graphics.Models[Minimum(actor)].Transform?.Scale38 ?? throw new NotSupportedException("Native77E780 requires its target scaleWORD38."), (next, source, node) =>
                    {
                        NativeBattleRinoa87NodeDraw draw = NativeBattleRinoa87ProgramDrawing.Draw7766B0(source, node, next.State.MorphScratch, projection, colors, NativeBattlePilotVisualLifetimes.Identity(87, task, unchecked((int)source.Address * 397 + node.Node.ResourceOffset)));
                        return Append(next with { State = next.State with { MorphScratch = draw.MorphScratch } }, draw.Packets, false);
                    }, view));
                case 0x779050:
                    NativeBattleRinoa87NodeStep nodeStep = NativeBattleRinoa87Nodes.Advance(current, address, resources.Nodes, controller, random, projection, NativeCamera!.Roll, Ordering(), colors, (model, selector) => ReadBone(current, checked(model - 48), selector));
                    random = nodeStep.Random;
                    StoreOrdering(nodeStep.Ordering);
                    return new(nodeStep.Frame, nodeStep.Result);
                default:
                    throw new NotSupportedException($"Native Rinoa87 callback {task.Callback:X8} has no current implementation.");
            }
        }

        NativeBattleRinoa87Frame Execute(NativeBattleRinoa87Frame current, NativeBattleRinoa87Operation operation)
        {
            switch (operation)
            {
                case NativeBattleRinoa87StaticTexture texture when texture.Address == 0x16FDF78:
                    NativeBattleEffectTextureState textures = current.Memory.Textures ?? throw new NotSupportedException("Native Rinoa87 requires its retained TIM queue.");
                    (textures, _) = textures.EnqueueTim505E30(resources.SceneTim);
                    return current with { Memory = current.Memory with { Textures = textures } };
                case NativeBattleRinoa87BindActor bind when bind.Resource == 0x1560550 && bind.SavedModel == 0x25B4B28:
                    NativeBattleGraphicsModel actor = RetainNativeDecodedPoseBeforeResourceChange(graphics.Models[bind.Actor], NativeGraphicsPoseResource(graphics, bind.Actor));
                    NativeBattleFileStaging staging = (NativeFileStaging ?? NativeBattleFileStaging.Unknown()).Write(bind.Resource, resources.ActorOverride.Data.AsSpan());
                    NativeBattleVictoryResourceBinding binding = NativeBattleVictoryResources.Bind5022C0(staging, NativeModelCpuResources ?? throw new NotSupportedException("Native Rinoa87 requires its actual CPU resource rows."), actor, bind.SavedModel, bind.Resource);
                    NativeFileStaging = binding.Staging;
                    graphics = graphics with { Models = graphics.Models.SetItem(bind.Actor, binding.Model) };
                    return current;
                case NativeBattleRinoa87ActorSequence sequence:
                    graphics = SelectNativeHeaderSequence(graphics, sequence.Actor, checked((ushort)sequence.Sequence));
                    return current;
                case NativeBattleRinoa87BirthBody birth:
                    NativeBattleEffectHierarchy hierarchy = new(current.Effect.Pools);
                    NativeBattleEffectOwnedModelStep initialized = NativeBattleEffectOwnedModel.Initialize8DD0F0(hierarchy.Read(birth.Address), birth.Address, resources.Model, 3, CurrentResources().DefaultColor44);
                    return current with { Effect = current.Effect with { Pools = hierarchy.Write(birth.Address, initialized.Task).Pools }, State = current.State with { BodyModel = initialized.State, BodyBones = current.State.BodyBones ?? NativeBattleBodyBoneMatrices.ReadLoadedSkeleton(resources.Model.PoseSource).Matrices } };
                case NativeBattleRinoa87CameraControl:
                    NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native50A730 requires its actual camera owner.")).Apply50A730();
                    return current;
                case NativeBattleRinoa87Sound sound:
                    PublishNativeModuleSound(new(resources.Sounds[sound.Descriptor], unchecked((uint)sound.Argument), null) { RawPan = unchecked((uint)sound.Volume) });
                    return current;
                case NativeBattleRinoa87Emit emit:
                    NativeBattleRinoa87ParticleEmission emitted = NativeBattleRinoa87Emitters.Emit775510(new(current.Effect.Pools), emit.Body, emit.Mode, random, selector => ReadBone(current, emit.Body, selector), SelectActors);
                    random = emitted.Random;
                    return current with { Effect = current.Effect with { Pools = emitted.Hierarchy.Pools } };
                case NativeBattleRinoa87GroundParticle ground:
                    NativeBattleRinoa87ParticleEmission particle = NativeBattleRinoa87Emitters.Emit775F40(new(current.Effect.Pools), ground.Body, random, selector => ReadBone(current, ground.Body, selector), SelectActors);
                    random = particle.Random;
                    return current with { Effect = current.Effect with { Pools = particle.Hierarchy.Pools } };
                case NativeBattleRinoa87CaptureGround capture:
                    NativeBattleModulePoolTask body = new NativeBattleEffectHierarchy(current.Effect.Pools).Read(capture.Body);
                    return CaptureGround(current, new(unchecked((short)body.ReadWord(76)), 0, unchecked((short)body.ReadWord(80))));
                case NativeBattleRinoa87PublishWorld matrix:
                    return current with { Memory = current.Memory with { SharedCustomMatrix = new(matrix.Matrix, Enumerable.Repeat(true, 32).ToImmutableArray()) } };
                default:
                    throw new NotSupportedException("Native Rinoa87 operation has no selected original owner.");
            }
        }

        NativeBattleRinoa87Frame DrawBody(NativeBattleRinoa87Frame current, uint address)
        {
            NativeBattleRinoa87BodyDraw drawn = NativeBattleRinoa87BodyDrawing.Draw775330(current, address, resources.Model, view, projection, rules.Rules.ShadowSource ?? throw new NotSupportedException("Native Rinoa87 requires its actual shadow source."), rules.PrimitiveCapabilities ?? throw new NotSupportedException("Native Rinoa87 requires its actual primitive capabilities."), colors, Ordering());
            StoreOrdering(drawn.Ordering);
            return drawn.Frame;
        }

        NativeBattleRinoa87Frame CaptureGround(NativeBattleRinoa87Frame current, NativeBattleVector point)
        {
            ImmutableArray<byte> mesh = resources.Meshes[0x155AA5C];
            int count = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(mesh.AsSpan().Slice(4));
            short[] minimum = [32767, 32767, 32767];
            short[] maximum = [-32767, -32767, -32767];
            if (count < 1 || count > (mesh.Length - 8) / 8)
            {
                throw new InvalidDataException("Native778A20 capture bounds leave its original vertex owner.");
            }
            for (int vertex = 0; vertex < count; vertex++)
            {
                for (int axis = 0; axis < 3; axis++)
                {
                    short coordinate = System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(mesh.AsSpan().Slice(8 + vertex * 8 + axis * 2));
                    minimum[axis] = Math.Min(minimum[axis], coordinate);
                    maximum[axis] = Math.Max(maximum[axis], coordinate);
                }
            }
            NativeBattleThundaga105Bounds bounds = new(new(minimum[0], minimum[1], minimum[2]), new(maximum[0], maximum[1], maximum[2]));
            NativeBattleThundaga105CaptureTransform capture = NativeBattleRinoa87GroundCapture.Transform778AB0(point, bounds, projection, unchecked((short)(NativeCamera?.FocalLength ?? throw new NotSupportedException("Native778AB0 requires its actual focal WORD."))));
            NativeBattleGroundCaptureWrite captured = NativeBattleRinoa87GroundCapture.Append778AB0(current.Memory, Ordering(), DrawStage);
            StoreOrdering(captured.Ordering);
            return current with { Memory = captured.Memory };

            NativeBattleGroundCaptureWrite DrawStage(NativeBattleGroundCaptureWrite next)
            {
                NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native778AB0 requires its actual stage group1 owner.");
                NativeBattleStageGroup group = stage.Groups[1];
                if ((group.Flags & 1) == 0)
                {
                    return next;
                }
                NativeBattlePoseSource source = group.Source ?? throw new NotSupportedException("Native778AB0 requires current stage group1 pose source.");
                NativeBattleDecodedPose pose = source.Sample(group.Animation, group.Counter, group.PoseFlags);
                group = group with { Matrices = NativeBattlePoseMatrices.ComposeStageCamera(NativeBattlePoseMatrices.StageBoneMatrices(source, pose), capture.Matrix, 0) };
                NativeStage = stage with { Groups = stage.Groups.SetItem(1, group) };
                NativeBattlePrimitiveProcess process = next.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native5099D0 requires its actual primitive process.");
                bool hardware = rules.PrimitiveCapabilities?.HardwareEnabled ?? throw new NotSupportedException("Native5099D0 requires its actual renderer capability.");
                var gate = process.Enter5099D0(hardware);
                next = next with { Memory = next.Memory with { PrimitiveProcess = gate.Process } };
                if (!gate.Draw)
                {
                    return next;
                }
                ImmutableArray<NativeBattleRawMesh> meshes = RequireNativeStageGeometry().Where(mesh => mesh.StageGroup == 1).ToImmutableArray();
                int length = meshes.IsEmpty ? 0 : meshes.Max(mesh => checked(mesh.Vertices.Length * 8));
                NativeBattleRawModelBodyDraw drawn = NativeBattleRawModelBodyDrawing.Draw5099D0(meshes, group.Matrices, capture.Projection, group.ColorFlags, uint.MaxValue, new byte[length].ToImmutableArray(), colors, 16, new(0, 0, 320, 216), 17, group.ColorDepth, new(0, 0x500FD0, 1, 0, 0), pilotScreenSpace: true, pilotContinuous: false);
                if (drawn.ProjectedBytesWritten != length)
                {
                    throw new NotSupportedException("Native778AB0 requires complete projected records before packet consumers.");
                }
                NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(gate.Process.PublishModelColor(group.ColorFlags), next.Ordering, drawn.Packets);
                NativeBattlePrimitiveProcess published = written.Process.Publish50FDF0(unchecked((uint)(written.Process.ByteCursor ?? throw new NotSupportedException("Native778AB0 lost its COMMON packet cursor."))), null);
                return new(next.Memory with { PrimitiveProcess = published }, written.Ordering);
            }
        }

        NativeBattleRinoa87Frame Contact(NativeBattleRinoa87Frame current, int index)
        {
            NativeEffectMemory = current.Memory;
            ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
            graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(index)] });
            graphics = graphics with { PendingContacts = pending };
            return current with { Memory = NativeEffectMemory! };
        }

        NativeBattleRinoa87Frame ReloadCommon(NativeBattleRinoa87Frame current, uint root)
        {
            (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(checked(0x20DFAB8u + (uint)current.State.SourceArenaOffset), checked(root + 98), (address, value) => current = WriteRinoa87Completion(current, address, value));
            graphics = graphics with { Auxiliary = auxiliary };
            return current;
        }

        NativeBattleRinoa87Frame Append(NativeBattleRinoa87Frame current, NativeBattleGfGeometryBatch batch, bool common)
        {
            if (common)
            {
                NativeEffectMemory = current.Memory;
                graphics = AppendNativeModulePacketBatch(graphics, new([], batch.AllocationBytes) { CapturedWrites = batch }, null, null);
                return current with { Memory = NativeEffectMemory! };
            }
            var appended = NativeBattleEffectPacketArena.Append(current.Memory, current.State.PrimaryDrawCursor ?? throw new NotSupportedException("Native Rinoa87 requires its current private packet cursor."), Ordering(), batch);
            StoreOrdering(appended.Ordering);
            return current with { Memory = appended.Memory, State = current.State with { PrimaryDrawCursor = appended.Cursor } };
        }

        int Minimum(int source)
        {
            int[] ring = NativeBattleModelRings.Walk(graphics.Models, source).ToArray();
            return graphics.Models[ring[^1]].ReadLinkedModelIndex140() is null ? source : ring.Min();
        }

        int Roll()
        {
            random = random.Draw(out ushort value);
            return value;
        }
    }

    private static NativeBattleRinoa87Frame WriteRinoa87Completion(NativeBattleRinoa87Frame frame, uint address, byte value)
    {
        NativeBattleEffectTaskPool pool = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == NativeBattleRinoa87Controller.Descriptor);
        uint delta = unchecked(address - pool.BankAddress);
        if (delta >= (uint)(pool.Stride * pool.Slots.Length) || delta % pool.Stride != 98)
        {
            throw new NotSupportedException("Native Rinoa87 common texture completion requires its exact root BYTE98.");
        }
        int slot = checked((int)(delta / pool.Stride));
        return frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(frame.Effect.Pools.IndexOf(pool), pool.WriteSlot(slot, pool.Slots[slot].WriteByte(98, value))) } };
    }

    private void CompleteNativeRinoa87Io(uint callback)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native774030 requires its retained effect process.");
        NativeBattleRinoa87State state = memory.Rinoa87 ?? throw new NotSupportedException("Native774030 requires its entered module storage.");
        NativeBattleRinoa87IoCompletion completed = NativeBattleRinoa87Io.Complete774030(state.Io ?? throw new NotSupportedException("Native774030 requires its current TIM request."), memory, callback);
        NativeEffectMemory = completed.Memory with { Rinoa87 = state with { Io = completed.State } };
    }
}
