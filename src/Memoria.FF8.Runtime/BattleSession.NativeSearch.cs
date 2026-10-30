using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeSearch755130(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleSearchResources resources = effect.Binding.SearchResources ?? throw new NotSupportedException("Native Search requires its original resources.");
        NativeBattleSearchFrame frame = NativeBattleSearchController.Initialize755130(NativeEffectMemory ?? throw new NotSupportedException("Native Search requires retained effect memory."), effect, nativeActionPackets, resources, nativeCrtRandom ?? throw new NotSupportedException("Native Search requires its CRT random owner."));
        NativeEffectMemory = frame.Memory;
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };
    }

    private NativeBattleGraphicsState AdvanceNativeSearch7552B0(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleSearchResources resources = effect.Binding.SearchResources ?? throw new NotSupportedException("Native Search requires its original resources.");
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Search requires its published projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Search requires its retained view matrix.");
        NativeBattleGraphicsDefinition definition = this.definition.NativeGraphics ?? throw new NotSupportedException("Native Search requires original graphics rules.");
        NativeBattleSummonColors colors = definition.Rules.SummonColors ?? throw new NotSupportedException("Native Search requires its depth-cue constants.");
        NativeBattleSearchFrame frame = new(NativeEffectMemory ?? throw new NotSupportedException("Native Search requires retained effect memory."), effect, nativeCrtRandom ?? throw new NotSupportedException("Native Search requires retained CRT random state."));
        frame = NativeBattleSearchController.Advance7552B0(frame, new(graphics.Flags, nativeActionPackets, view, actor => graphics.Models[actor], actor => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Native Search requires the selected actor pose source."), ReadWorld, ReadBone, DrawModel, AdvanceProgram, Execute), resources);
        NativeEffectMemory = frame.Memory;
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };

        ImmutableArray<byte> ReadWorld(int actor)
        {
            NativeBattleModelTransform transform = graphics.Models[actor].Transform ?? throw new NotSupportedException("Native Search requires the actual target transform.");
            byte[] bytes = new byte[8];
            BinaryPrimitives.WriteInt16LittleEndian(bytes, transform.Position28.X);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2), transform.Position28.Y);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), transform.Position28.Z);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(6), transform.Vertical34);
            return bytes.ToImmutableArray();
        }

        NativeBattleVector ReadBone(NativeBattleSearchFrame current, uint pointer, byte selector)
        {
            NativeBattleSearchState shared = current.Memory.Search!;
            NativeBattleEffectOwnedModelState model = shared.Models.Single(candidate => candidate.TaskAddress + 0x30 == pointer);
            NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(current.Effect.Pools).Read(model.TaskAddress);
            ImmutableArray<short> rotation = Enumerable.Range(0, 9).Select(index => unchecked((short)task.ReadWord(0x70 + index * 2))).ToImmutableArray();
            NativeBattleActionMatrix world = new(rotation, unchecked((int)task.ReadDword(0x84)), unchecked((int)task.ReadDword(0x88)), unchecked((int)task.ReadDword(0x8C)), 4096);
            return NativeBattlePoseMatrices.ReadEffectOwnedBone(resources.ModelProfile.Model.PoseSource, model.DecodedPose ?? throw new NotSupportedException("Native Search requires its current independent model pose."), world, selector);
        }

        NativeBattleSearchModelDrawStep DrawModel(NativeBattleSearchFrame current, NativeBattleModulePoolTask task, NativeBattleEffectOwnedModelState model)
        {
            NativeBattleSearchState shared = current.Memory.Search!;
            int size = checked(resources.ModelProfile.Model.Geometry[0].Vertices.Length * 8);
            if (shared.ProjectedScratch is null && (task.ReadDword(0xAC) & 1) == 0)
            {
                throw new NotSupportedException("Native Search cannot read an uninitialized projected scratch owner.");
            }
            NativeBattleEffectOwnedModelDraw drawn = NativeBattleEffectOwnedModelDrawing.Draw7558A0(task, model, resources.ModelProfile.Model, view, projection, definition.Rules.ShadowSource ?? throw new NotSupportedException("Native Search requires its original shadow source."), definition.PrimitiveCapabilities ?? throw new NotSupportedException("Native Search requires its selected device capabilities."), colors, shared.ProjectedScratch ?? new byte[size].ToImmutableArray(), pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(92, task) with { ResourceGeneration = model.ResourceAddress });
            if (shared.ProjectedScratch is null && drawn.ProjectedBytesWritten != size)
            {
                throw new InvalidDataException("Native Search's first body draw did not establish every projected record.");
            }
            current = current with { Memory = current.Memory with { Search = shared with { ProjectedScratch = drawn.ProjectedScratch } } };
            return new(Append(current, drawn.Packets, false), drawn.Task);
        }

        NativeBattleSearchProgramStep AdvanceProgram(NativeBattleSearchFrame current, NativeBattleModulePoolTask task)
        {
            NativeBattleSearchProgramDraw drawn = NativeBattleSearchProgramDrawing.Advance758D70(task, resources.ProgramSource, view, projection, colors, capturePresentation: true);
            return new(Append(current, drawn.Packets, false), drawn.Task, drawn.Remaining);
        }

        NativeBattleSearchFrame Execute(NativeBattleSearchFrame current, NativeBattleSearchOperation operation)
        {
            NativeEffectMemory = current.Memory;
            switch (operation)
            {
                case NativeBattleSearchContact contact:
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.Contact)] });
                    graphics = graphics with { PendingContacts = pending };
                    return current with { Memory = NativeEffectMemory! };
                case NativeBattleSearchSound sound:
                    PublishNativeModuleSound(new(sound.Descriptor, unchecked((uint)sound.First), null) { Pan = checked((byte)sound.Second) });
                    return current;
                case NativeBattleSearchVoice voice:
                    if (voice.Second != 1)
                    {
                        throw new NotSupportedException("Native Search requires its reached voice logging operand.");
                    }
                    PublishNativeModuleSound(new(voice.Descriptor, 0, null) { RawPan = checked((uint)voice.First), Volume = checked((uint)voice.Third) });
                    return current;
                case NativeBattleSearchUploadModelTim:
                    NativeBattleEffectTextureState textures = current.Memory.Textures ?? throw new NotSupportedException("Native Search requires its texture owner.");
                    (textures, _) = textures.EnqueueTim505E30(resources.ModelProfile.ModelTim);
                    return current with { Memory = current.Memory with { Textures = textures } };
                case NativeBattleSearchBindTargetResource bind:
                    NativeBattleGraphicsModel model = RetainNativeDecodedPoseBeforeResourceChange(graphics.Models[bind.Actor], NativeGraphicsPoseResource(graphics, bind.Actor));
                    NativeBattleFileStaging staging = (NativeFileStaging ?? NativeBattleFileStaging.Unknown()).Write(0x16C96E8, resources.TargetOverride.Data.AsSpan());
                    NativeBattleVictoryResourceBinding binding = NativeBattleVictoryResources.Bind5022C0(staging, NativeModelCpuResources ?? throw new NotSupportedException("Native Search requires current CPU model resources."), model, 0x2598EF8, 0x16C96E8);
                    NativeFileStaging = binding.Staging;
                    graphics = graphics with { Models = graphics.Models.SetItem(bind.Actor, binding.Model) };
                    return current;
                case NativeBattleSearchSelectActorSequence sequence:
                    graphics = SelectNativeHeaderSequence(graphics, checked((byte)sequence.Actor), sequence.Sequence);
                    return current;
                case NativeBattleSearchSprite sprite:
                    return Append(current, NativeBattleSearchSprites.Draw7582A0(sprite.Task, resources.Sprite8.Bytes, view, projection, NativeCamera!.Roll, pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(92, sprite.Task) with { ResourceGeneration = resources.Sprite8.Address }), false);
                case NativeBattleSearchMesh mesh:
                    return Append(current, NativeBattleSearchMeshDrawing.Draw755FC0(mesh.Task, resources.ParticleMesh, view, projection, colors, NativeBattlePilotVisualLifetimes.Identity(92, mesh.Task) with { ResourceGeneration = resources.ParticleMesh.Address }), true);
                case NativeBattleSearchCommonTextureLoad load:
                    (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)load.DestinationArenaOffset), load.CompletionAddress, (address, value) => current = current with { Effect = NativeBattleSearchController.WriteCompletionByte(current.Effect, address, value) });
                    graphics = graphics with { Auxiliary = auxiliary };
                    return current;
                default:
                    throw new NotSupportedException("Native Search operation requires its original callback.");
            }
        }

        NativeBattleSearchFrame Append(NativeBattleSearchFrame current, NativeBattleGfGeometryBatch batch, bool common)
        {
            batch = NativeBattleRetainedPolygonDepths.Capture(batch);
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Search requires its initialized ordering table.");
            if (common)
            {
                NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Search requires its common packet owner."), primitive.Pending, batch);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                return current with { Memory = current.Memory with { PrimitiveProcess = written.Process } };
            }
            NativeBattleEffectPacketWrite privateWrite = NativeBattleEffectPacketArena.Append(current.Memory, current.Effect.DrawArenaOffset!.Value, primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = privateWrite.Ordering } };
            return current with { Memory = privateWrite.Memory, Effect = current.Effect with { DrawArenaOffset = privateWrite.Cursor } };
        }
    }

    private bool TryWriteNativeSearchCompletion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState updated)
    {
        updated = graphics;
        uint delta = unchecked(address - NativeBattleSearchController.MainBank);
        if (delta >= 0xC8 || delta % 0x64 != 0x62)
        {
            return false;
        }
        if (NativeEffectMemory?.DetachedReturnedTask is { Binding.Entry: 0x755130 } detached)
        {
            NativeEffectMemory = NativeEffectMemory with { DetachedReturnedTask = NativeBattleSearchController.WriteCompletionByte(detached, address, value) };
        }
        else
        {
            NativeBattleOriginalEffectState effect = graphics.OriginalEffect is { Binding.Entry: 0x755130 } current ? current : throw new NotSupportedException("Native Search completion lost its physical task owner.");
            updated = graphics with { OriginalEffect = NativeBattleSearchController.WriteCompletionByte(effect, address, value) };
        }
        return true;
    }
}
