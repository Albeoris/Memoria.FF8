using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeRinoa88(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new InvalidDataException("Native Rinoa88 requires its actual selected module.");
        NativeBattleRinoa88Resources resources = effect.Binding.Rinoa88Resources ?? throw new NotSupportedException("Native Rinoa88 requires its original resources.");
        NativeBattleRinoa88Frame frame = NativeBattleRinoa88Controller.Initialize(NativeEffectMemory ?? throw new NotSupportedException("Native Rinoa88 requires its retained process."), effect, nativeActionPackets);
        NativeEffectMemory = frame.Memory with { Rinoa88 = frame.State };
        return graphics with { OriginalEffect = frame.Effect };
    }

    private NativeBattleGraphicsState AdvanceNativeRinoa88(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new InvalidDataException("Native Rinoa88 requires its actual selected module.");
        NativeBattleRinoa88Resources resources = effect.Binding.Rinoa88Resources ?? throw new NotSupportedException("Native Rinoa88 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native Rinoa88 requires its retained process.");
        NativeBattleRinoa88Frame frame = new(memory, effect, memory.Rinoa88 ?? throw new NotSupportedException("Native Rinoa88 requires its entered process storage."));
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Rinoa88 requires its published projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Rinoa88 requires its published view matrix.");
        NativeBattleGraphicsDefinition rules = definition.NativeGraphics ?? throw new NotSupportedException("Native Rinoa88 requires its original graphics rules.");
        NativeBattleSummonColors colors = rules.Rules.SummonColors ?? throw new NotSupportedException("Native Rinoa88 requires its actual color constants.");
        NativeCrtRandomState random = nativeCrtRandom ?? throw new NotSupportedException("Native Rinoa88 requires its retained CRT generator.");
        NativeBattleRinoa88Inputs controller = null!;
        controller = new(nativeActionPackets, view, index => graphics.Models[index], index => NativeGraphicsPoseResource(graphics, index).PoseSource ?? throw new NotSupportedException("Native Rinoa88 requires its actual actor skeleton."), AdvanceLeaf, ReloadCommon);
        frame = NativeBattleRinoa88Controller.Advance(frame, controller);
        NativeEffectMemory = frame.Memory with { Rinoa88 = frame.State };
        nativeCrtRandom = random;
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleLogicalOrdering Ordering() => (graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Rinoa88 requires its initialized ordering table.")).Pending;
        void StoreOrdering(NativeBattleLogicalOrdering ordering) => graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame! with { Pending = ordering } };
        NativeBattleRinoa88Resources CurrentResources() => resources with { DefaultColor44 = (NativeStage?.ModelColors ?? throw new NotSupportedException("Native8DD0F0 requires its current shared model colors.")).Color44 };
        NativeBattleEffectTaskActors SelectActors(uint pointer, sbyte group, sbyte target) => NativeBattleCureController.SelectActors(effect.Parameters!, nativeActionPackets, pointer, group, target);
        NativeBattleEffectOwnedModelResource Model(NativeBattleRinoa88Frame current, uint body)
        {
            uint source = current.State.OwnedModels.GetValueOrDefault(body)?.ResourceAddress ?? throw new NotSupportedException("Native Rinoa88 requires its actual task-owned model source.");
            return source == resources.PrimaryModel.Address ? resources.PrimaryModel : source == resources.SecondaryModel.Address ? resources.SecondaryModel : throw new NotSupportedException("Native Rinoa88 model task has no selected original source.");
        }
        NativeBattleVector ReadBone(NativeBattleRinoa88Frame current, uint body, byte selector) => NativeBattleRinoa88BodyDrawing.ReadBone502170(current, body, Model(current, body), selector);

        NativeBattleRinoa88LeafStep AdvanceLeaf(NativeBattleRinoa88Frame current, uint address)
        {
            NativeBattleEffectHierarchy hierarchy = new(current.Effect.Pools);
            NativeBattleModulePoolTask task = hierarchy.Read(address);
            switch (task.Callback)
            {
                case 0x7688F0:
                    return NativeBattleRinoa88Controller.AdvanceTarget(current, address, new(controller, (next, target) => NativeBattleRinoa88Scene.InitializeTarget(next, target, controller, resources.Io), next => NativeBattleRinoa88Scene.Read(next).Word(72), next => NativeBattleRinoa88Scene.Read(next).Word(76), Contact));
                case 0x768ED0:
                    return NativeBattleRinoa88Scene.Advance(current, address, new(controller, Execute));
                case 0x768AC0:
                    NativeBattleRinoa88IoAdvance io = NativeBattleRinoa88Io.Advance768AC0(task, current.State.Io ?? throw new NotSupportedException("Native768AC0 requires its initialized IO owner."));
                    current = current with { State = current.State with { Io = io.State }, Effect = current.Effect with { Pools = hierarchy.Write(address, io.Task).Pools } };
                    if (io.Request is { } request)
                    {
                        NativeBattleRinoa88IoQueued queued = NativeBattleRinoa88Io.CopyAndQueueFile(io.State, current.Memory, NativeFileIo ?? throw new NotSupportedException("Native768AC0 requires its actual file queue."), resources.Io.Files.Single(file => file.FileId == request.FileId), (destination, bytes) => NativeFileStaging = (NativeFileStaging ?? NativeBattleFileStaging.Unknown()).Write(destination, bytes.AsSpan()));
                        current = current with { Memory = queued.Memory };
                        NativeFileIo = queued.FileIo;
                    }
                    return Finish(current, address, io.ReadyToComplete);
                case 0x771DB0:
                    return NativeBattleRinoa88PrimaryBody.Advance(current, address, new(resources.PrimaryModel, (_, actor) => graphics.Models[actor], DrawBody, Execute, controller));
                case 0x7718D0:
                    return NativeBattleRinoa88SecondaryBody.Advance(current, address, new(resources.SecondaryModel, (_, actor) => graphics.Models[actor], DrawBody));
                case 0x769060:
                    return NativeBattleRinoa88Camera.Advance(current, address, new(resources.Camera, _ => NativeBattleRinoa88ModelOperations.PartyMidpoint769A90(graphics.Models), next => (next, Roll()), (next, camera) =>
                    {
                        NativeCamera = NativeBattleMoogleCamera.Publish733150(camera, NativeCamera ?? throw new NotSupportedException("Native7694F0 requires its actual camera globals."));
                        return next;
                    }));
                case 0x772060:
                case 0x772990:
                    return NativeBattleRinoa88Particles.Advance(current, address, (next, particle) =>
                    {
                        NativeBattleModulePoolTask draw = new NativeBattleEffectHierarchy(next.Effect.Pools).Read(particle);
                        if ((draw.ReadByte(38) & 4) != 0)
                        {
                            return next;
                        }
                        return Append(next, NativeBattleRinoa88ParticleDrawing.DrawSprite7720C0(draw, resources.Sprites[draw.ReadDword(76)], view, projection, NativeCamera!.Roll));
                    });
                case 0x771660:
                case 0x772D90:
                    return NativeBattleRinoa88MeshTasks.Advance(current, address, (next, body, selector) => ReadBone(next, checked(body - 48), selector), (next, meshAddress) =>
                    {
                        NativeBattleModulePoolTask draw = new NativeBattleEffectHierarchy(next.Effect.Pools).Read(meshAddress);
                        if ((draw.ReadByte(38) & 4) != 0)
                        {
                            return next;
                        }
                        uint source = draw.ReadDword(76);
                        ImmutableArray<byte> mesh = resources.Meshes.TryGetValue(source, out ImmutableArray<byte> staticMesh) ? staticMesh : source == 0x19EF0BC ? Staging(source, resources.Io.Files[1].Data.Length) : source == 0x19F1B5C ? Staging(source, resources.Io.Files[2].Data.Length) : throw new NotSupportedException("Native Rinoa88 mesh has no actual static or completed staged owner.");
                        return Append(next, NativeBattleRinoa88ParticleDrawing.DrawMesh(draw, mesh, view, projection, colors));
                    });
                case 0x772C50:
                case 0x76A180:
                    return NativeBattleRinoa88Program.Advance(current, address, new(pointer => pointer == resources.Program.Address ? resources.Program : pointer == 0x19F3A8C ? new(pointer, Staging(pointer, resources.Io.Files[3].Data.Length)) : throw new NotSupportedException("Native76A250 program has no actual source owner."), (next, model, selector) => ReadBone(next, checked(model - 48), selector), _ =>
                    {
                        ImmutableArray<byte> anchor = NativeBattleRinoa88ModelOperations.PartyMidpoint769A90(graphics.Models);
                        return new(BinaryPrimitives.ReadInt16LittleEndian(anchor.AsSpan()), 0, BinaryPrimitives.ReadInt16LittleEndian(anchor.AsSpan().Slice(4)));
                    }, (next, source, node) =>
                    {
                        NativeBattleRinoa88NodeDraw drawn = NativeBattleRinoa88ProgramDrawing.Draw76A380(source, node, next.State.MorphScratch, projection, colors);
                        return Append(next with { State = next.State with { MorphScratch = drawn.MorphScratch } }, drawn.Packets);
                    }, view));
                case 0x76C700:
                {
                    NativeBattleFileStaging sourceStaging = NativeFileStaging ?? throw new NotSupportedException("Native76C700 requires the actual staged node owner.");
                    using NativeBattleFileStagingTransaction nodeStaging = NativeBattleFileStagingTransaction.CreatePrivateChild(sourceStaging);
                    current = current with { State = current.State with { NodeData = NativeBattleNodeSource.BorrowPrivateView(nodeStaging.View, NativeBattleRinoa88NodeResources.Address, NativeBattleRinoa88NodeResources.Length) } };
                    NativeBattleRinoa88NodeStep nodeStep = NativeBattleRinoa88Nodes.Advance(current, address, resources.Nodes, controller, random, projection, NativeCamera!.Roll, Ordering(), colors);
                    random = nodeStep.Random;
                    StoreOrdering(nodeStep.Ordering);
                    NativeBattleFileStaging publishedStaging = nodeStaging.Freeze(nodeStaging.View);
                    NativeBattleNodeSource nodeSource = nodeStep.Frame.State.NodeData ?? throw new InvalidDataException("Native76C700 lost its mutable source.");
                    NativeBattleRinoa88Frame published = nodeStep.Frame with { State = nodeStep.Frame.State with { NodeData = nodeSource.RebindStaging(publishedStaging, NativeBattleRinoa88NodeResources.Address, NativeBattleRinoa88NodeResources.Length) } };
                    NativeFileStaging = publishedStaging;
                    return new(published, nodeStep.Result);
                }
                case 0x773090:
                    NativeBattleRinoa88OverlayStep overlay = NativeBattleRinoa88Overlay.Advance773090(task, current.State.Counter25ACD34, resources.ScreenColors, ReadProxyDepth);
                    current = current with { Effect = current.Effect with { Pools = hierarchy.Write(address, overlay.Task).Pools } };
                    return Finish(Append(current, overlay.Packets), address, overlay.ReadyToComplete);
                case 0x771510:
                    NativeBattleRinoa88StageStep stage = NativeBattleRinoa88Stage.Advance771510(task, NativeStage ?? throw new NotSupportedException("Native771510 requires its four stage rows."));
                    NativeStage = stage.Stage;
                    current = current with { Effect = current.Effect with { Pools = hierarchy.Write(address, stage.Task).Pools } };
                    return Finish(current, address, stage.ReadyToComplete);
                default:
                    throw new NotSupportedException($"Native Rinoa88 callback {task.Callback:X8} has no current implementation.");
            }
        }

        NativeBattleRinoa88Frame Execute(NativeBattleRinoa88Frame current, NativeBattleRinoa88Operation operation)
        {
            switch (operation)
            {
                case NativeBattleRinoa88StaticTexture texture when resources.SceneTextures.TryGetValue(texture.Address, out NativeBattleEffectHeapAllocation? tim):
                    NativeBattleEffectTextureState textures = current.Memory.Textures ?? throw new NotSupportedException("Native Rinoa88 requires its actual texture queue.");
                    (textures, _) = textures.EnqueueTim505E30(tim);
                    return current with { Memory = current.Memory with { Textures = textures } };
                case NativeBattleRinoa88CameraControl:
                    NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native50A730 requires its camera owner.")).Apply50A730();
                    return current;
                case NativeBattleRinoa88PhaseBirth birth:
                    return Birth(current, birth.SceneAddress, birth.Phase);
                case NativeBattleRinoa88Visibility visibility:
                    if (visibility.Callback is 0x769DB0 or 0x769E60)
                    {
                        NativeStage = NativeBattleRinoa88Stage.Visibility(NativeStage ?? throw new NotSupportedException("Native Rinoa88 requires its stage visibility rows."), visibility.Callback);
                        return current;
                    }
                    NativeBattleRinoa88VisibilityStep visible = NativeBattleRinoa88ModelOperations.ApplyVisibility(NativeBattleRinoa88Scene.Read(current), graphics.Models, visibility.Callback);
                    graphics = graphics with { Models = visible.Models };
                    return current with { State = current.State with { Scene = visible.Scene } };
                case NativeBattleRinoa88BindActor bind when bind.Resource == 0x15560C0 && bind.SavedOwner == 0x25ACD10:
                    NativeBattleGraphicsModel actor = RetainNativeDecodedPoseBeforeResourceChange(graphics.Models[bind.Actor], NativeGraphicsPoseResource(graphics, bind.Actor));
                    NativeBattleFileStaging staging = (NativeFileStaging ?? NativeBattleFileStaging.Unknown()).Write(bind.Resource, resources.ActorOverride.Data.AsSpan());
                    NativeBattleVictoryResourceBinding binding = NativeBattleVictoryResources.Bind5022C0(staging, NativeModelCpuResources ?? throw new NotSupportedException("Native Rinoa88 requires its actual CPU resource rows."), actor, bind.SavedOwner, bind.Resource);
                    NativeFileStaging = binding.Staging;
                    graphics = graphics with { Models = graphics.Models.SetItem(bind.Actor, binding.Model) };
                    return current;
                case NativeBattleRinoa88ActorSequence sequence:
                    graphics = SelectNativeHeaderSequence(graphics, sequence.Actor, checked((ushort)sequence.Sequence));
                    return current;
                case NativeBattleRinoa88Sound sound:
                    PublishNativeModuleSound(new(resources.Sounds[sound.Descriptor], unchecked((uint)sound.Argument), null) { RawPan = unchecked((uint)sound.Volume) });
                    return current;
                case NativeBattleRinoa88BodyEmission emit:
                    NativeBattleRinoa88ParticleEmission emitted = NativeBattleRinoa88Particles.Emit(new(current.Effect.Pools), emit.Body, emit.Callback, random, selector => ReadBone(current, emit.Body, selector), SelectActors);
                    random = emitted.Random;
                    return current with { Effect = current.Effect with { Pools = emitted.Hierarchy.Pools } };
                case NativeBattleRinoa88Pause pause:
                    if (pause.Value != 0 && current.State.Counter25ACD34 == 0 || pause.Value == 0 && current.State.Counter25ACD34 == 1)
                    {
                        current = current with { State = current.State with { Counter25ACD34 = pause.Value == 0 ? 0 : 1, Counter25A6BBC = pause.Value == 0 ? 0 : 1 } };
                    }
                    return current;
                case NativeBattleRinoa88Deform deform:
                    NativeBattleEffectHierarchy hierarchy = new(current.Effect.Pools);
                    NativeBattleModulePoolTask body = hierarchy.Read(deform.Body);
                    return current with { Effect = current.Effect with { Pools = hierarchy.Write(deform.Body, NativeBattleRinoa88ModelOperations.Deform773420(body, colors.Transition, unchecked((short)body.ReadWord(314)), deform.Argument)).Pools } };
                case NativeBattleRinoa88PublishWorld world:
                    return current with { Memory = current.Memory with { SharedCustomMatrix = new(world.Matrix, Enumerable.Repeat(true, 32).ToImmutableArray()) } };
                default:
                    throw new NotSupportedException("Native Rinoa88 operation has no selected original owner.");
            }
        }

        NativeBattleRinoa88Frame Birth(NativeBattleRinoa88Frame current, uint parent, ushort phase)
        {
            NativeBattleEffectHierarchy hierarchy = new(current.Effect.Pools);
            switch (phase)
            {
                case 1:
                    Body(0x771DB0, resources.PrimaryModel);
                    Body(0x7718D0, resources.SecondaryModel);
                    break;
                case 3:
                    for (ushort index = 0; index < 3; index++)
                    {
                        NativeBattleEffectHierarchyAppend mesh = hierarchy.Append8DC540(6, 0x771660, 156, parent, SelectActors);
                        hierarchy = mesh.Hierarchy.Write(mesh.Address, mesh.Hierarchy.Read(mesh.Address).WriteWord(152, index));
                    }
                    break;
                case 4:
                    NativeBattleRinoa88IoState io = current.State.Io ?? throw new NotSupportedException("Native76A000 requires its initialized IO directory.");
                    NativeBattleEffectHierarchyAppend node = hierarchy.Append8DC540(3, 0x76C700, 676, parent, SelectActors);
                    hierarchy = node.Hierarchy.Write(node.Address, node.Hierarchy.Read(node.Address).WriteDword(48, BinaryPrimitives.ReadUInt32LittleEndian(io.Control.AsSpan().Slice(260))).WriteWord(664, 8).WriteWord(668, 1).WriteWord(670, 15));
                    NativeBattleEffectHierarchyAppend program = hierarchy.Append8DC540(4, 0x76A180, 1140, parent, SelectActors);
                    hierarchy = program.Hierarchy.Write(program.Address, program.Hierarchy.Read(program.Address).WriteWord(128, 1).WriteDword(116, BinaryPrimitives.ReadUInt32LittleEndian(io.Control.AsSpan().Slice(296))).WriteDword(120, 964).WriteWord(130, 0));
                    hierarchy = hierarchy.Append8DC540(5, 0x771510, 144, parent, SelectActors).Hierarchy;
                    break;
            }
            return current with { Effect = current.Effect with { Pools = hierarchy.Pools } };

            void Body(uint callback, NativeBattleEffectOwnedModelResource resource)
            {
                NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(8, callback, 352, parent, SelectActors);
                NativeBattleEffectOwnedModelStep initialized = NativeBattleEffectOwnedModel.Initialize8DD0F0(appended.Hierarchy.Read(appended.Address), appended.Address, resource, 3, CurrentResources().DefaultColor44);
                hierarchy = appended.Hierarchy.Write(appended.Address, initialized.Task);
                current = current with { State = current.State with { OwnedModels = current.State.OwnedModels.SetItem(appended.Address, initialized.State) } };
            }
        }

        NativeBattleRinoa88Frame DrawBody(NativeBattleRinoa88Frame current, uint address)
        {
            NativeBattleRinoa88BodyDraw drawn = NativeBattleRinoa88BodyDrawing.Draw7719A0(current, address, Model(current, address), view, projection, rules.Rules.ShadowSource ?? throw new NotSupportedException("Native Rinoa88 requires its actual shadow source."), rules.PrimitiveCapabilities ?? throw new NotSupportedException("Native Rinoa88 requires its actual renderer capabilities."), colors, Ordering());
            StoreOrdering(drawn.Ordering);
            return drawn.Frame;
        }

        ImmutableArray<byte> Staging(uint address, int length) => (NativeFileStaging ?? throw new NotSupportedException("Native Rinoa88 requires its actual completed file-staging owner.")).Read(address, length).ToImmutableArray();
        ImmutableArray<int> ReadProxyDepth(int ordinal)
        {
            NativeBattleLogicalOrdering ordering = Ordering();
            int index = Math.Min(new NativeBattleLogicalOrdering.Builder(ordering).ProxyCount + ordinal, NativeBattlePrimitiveOrdering.ProxyCapacity);
            return index < ordering.Proxies.Length ? ordering.Proxies[index].VertexPayload4 : [];
        }
        NativeBattleRinoa88LeafStep Finish(NativeBattleRinoa88Frame current, uint address, bool complete)
        {
            return new(complete ? current with { Effect = current.Effect with { Pools = new NativeBattleEffectHierarchy(current.Effect.Pools).Complete8DC530(address).Pools } } : current, complete ? 2 : 0);
        }
        NativeBattleRinoa88Frame Contact(NativeBattleRinoa88Frame current, int index)
        {
            NativeEffectMemory = current.Memory;
            ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
            graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(index)] });
            graphics = graphics with { PendingContacts = pending };
            return current with { Memory = NativeEffectMemory! };
        }

        NativeBattleRinoa88Frame ReloadCommon(NativeBattleRinoa88Frame current, uint root)
        {
            (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(checked(0x20DFAB8u + (uint)current.State.SourceArenaOffset), checked(root + 98), (address, value) => current = WriteRinoa88Completion(current, address, value));
            graphics = graphics with { Auxiliary = auxiliary };
            return current;
        }

        NativeBattleRinoa88Frame Append(NativeBattleRinoa88Frame current, NativeBattleGfGeometryBatch batch, bool common = false)
        {
            if (common)
            {
                NativeEffectMemory = current.Memory;
                graphics = AppendNativeModulePacketBatch(graphics, new([], batch.AllocationBytes) { CapturedWrites = batch }, null, null);
                return current with { Memory = NativeEffectMemory! };
            }
            var appended = NativeBattleEffectPacketArena.Append(current.Memory, current.State.PrimaryDrawCursor ?? throw new NotSupportedException("Native Rinoa88 requires its current private packet cursor."), Ordering(), batch);
            StoreOrdering(appended.Ordering);
            return current with { Memory = appended.Memory, State = current.State with { PrimaryDrawCursor = appended.Cursor } };
        }

        int Roll()
        {
            random = random.Draw(out ushort value);
            return value;
        }
    }

    private static NativeBattleRinoa88Frame WriteRinoa88Completion(NativeBattleRinoa88Frame frame, uint address, byte value)
    {
        NativeBattleEffectTaskPool pool = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == NativeBattleRinoa88Controller.Descriptor);
        uint delta = unchecked(address - pool.BankAddress);
        if (delta >= (uint)(pool.Stride * pool.Slots.Length) || delta % pool.Stride != 98)
        {
            throw new NotSupportedException("Native Rinoa88 common texture completion requires its exact root BYTE98.");
        }
        int slot = checked((int)(delta / pool.Stride));
        return frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(frame.Effect.Pools.IndexOf(pool), pool.WriteSlot(slot, pool.Slots[slot].WriteByte(98, value))) } };
    }

    private void CompleteNativeRinoa88Io(uint callback)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native768CC0 requires its retained effect process.");
        NativeBattleRinoa88State state = memory.Rinoa88 ?? throw new NotSupportedException("Native768CC0 requires its entered module storage.");
        NativeBattleRinoa88IoCompletion completed = NativeBattleRinoa88Io.Complete768CC0(state.Io ?? throw new NotSupportedException("Native768CC0 requires its current TIM request."), memory, callback);
        NativeEffectMemory = completed.Memory with { Rinoa88 = state with { Io = completed.State } };
    }
}
