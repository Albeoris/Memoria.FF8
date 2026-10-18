using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeRush759370(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleRush91Resources resources = effect.Binding.Rush91Resources ?? throw new NotSupportedException("Native Rush requires its original per-module resources.");
        NativeBattleRush91Frame frame = NativeBattleRush91Controller.Initialize759370(NativeEffectMemory ?? throw new NotSupportedException("Native Rush requires retained effect memory."), effect, nativeActionPackets, resources, nativeCrtRandom ?? throw new NotSupportedException("Native Rush requires its CRT random owner."));
        NativeEffectMemory = frame.Memory;
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };
    }

    private NativeBattleGraphicsState AdvanceNativeRush759510(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleRush91Resources resources = effect.Binding.Rush91Resources ?? throw new NotSupportedException("Native Rush requires its original per-module resources.");
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Rush requires its published projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Rush requires its retained view matrix.");
        NativeBattleGraphicsDefinition definition = this.definition.NativeGraphics ?? throw new NotSupportedException("Native Rush requires original graphics rules.");
        NativeBattleSummonColors colors = definition.Rules.SummonColors ?? throw new NotSupportedException("Native Rush requires its depth-cue constants.");
        NativeBattleRush91Frame frame = new(NativeEffectMemory ?? throw new NotSupportedException("Native Rush requires retained effect memory."), effect, nativeCrtRandom ?? throw new NotSupportedException("Native Rush requires retained CRT random state."));
        frame = NativeBattleRush91Controller.Advance759510(frame, new(frame.Memory.FullscreenOverlayPause2795914 ?? throw new NotSupportedException("Native Rush requires its retained fullscreen pause DWORD."), nativeActionPackets, view, actor => graphics.Models[actor], actor => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Native Rush requires the selected actor pose source."), ReadBone, DrawModel, AdvanceProgram, AdvanceNodes, AdvanceOverlay, Execute), resources);
        NativeEffectMemory = frame.Memory;
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleVector ReadBone(NativeBattleRush91Frame current, uint pointer, byte selector)
        {
            NativeBattleRush91State shared = current.Memory.Rush91!;
            NativeBattleEffectOwnedModelState model = shared.Models.Single(candidate => candidate.TaskAddress + 0x30 == pointer);
            NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(current.Effect.Pools).Read(model.TaskAddress);
            ImmutableArray<short> rotation = Enumerable.Range(0, 9).Select(index => unchecked((short)task.ReadWord(0x70 + index * 2))).ToImmutableArray();
            NativeBattleActionMatrix world = new(rotation, unchecked((int)task.ReadDword(0x84)), unchecked((int)task.ReadDword(0x88)), unchecked((int)task.ReadDword(0x8C)), 4096);
            return NativeBattlePoseMatrices.ReadEffectOwnedBone(resources.Model.PoseSource, model.DecodedPose ?? throw new NotSupportedException("Native Rush requires its current independent model pose."), world, selector);
        }

        NativeBattleRush91ModelDrawStep DrawModel(NativeBattleRush91Frame current, NativeBattleModulePoolTask task, NativeBattleEffectOwnedModelState model)
        {
            NativeBattleRush91State shared = current.Memory.Rush91!;
            int size = checked(resources.Model.Geometry[0].Vertices.Length * 8);
            if (shared.ProjectedScratch is null && (task.ReadDword(0xAC) & 1) == 0)
            {
                throw new NotSupportedException("Native Rush cannot read an uninitialized projected scratch owner.");
            }
            NativeBattleEffectOwnedModelDraw drawn = NativeBattleEffectOwnedModelDrawing.Draw7558A0(task, model, resources.Model, view, projection, definition.Rules.ShadowSource ?? throw new NotSupportedException("Native Rush requires its original shadow source."), definition.PrimitiveCapabilities ?? throw new NotSupportedException("Native Rush requires its selected device capabilities."), colors, shared.ProjectedScratch ?? new byte[size].ToImmutableArray());
            if (shared.ProjectedScratch is null && drawn.ProjectedBytesWritten != size)
            {
                throw new InvalidDataException("Native Rush's first body draw did not establish every projected record.");
            }
            current = current with { Memory = current.Memory with { Rush91 = shared with { ProjectedScratch = drawn.ProjectedScratch } } };
            return new(Append(current, drawn.Packets), drawn.Task);
        }

        NativeBattleRush91ProgramStep AdvanceProgram(NativeBattleRush91Frame current, NativeBattleModulePoolTask task)
        {
            NativeBattleRush91ProgramDraw drawn = NativeBattleRush91ProgramDrawing.Advance761BB0(task, resources.ProgramSource, current.Memory.Rush91MorphScratch ?? throw new NotSupportedException("Native Rush requires its retained static morph bank."), view, projection, colors);
            current = current with { Memory = current.Memory with { Rush91MorphScratch = drawn.MorphScratch } };
            return new(Append(current, drawn.Packets), drawn.Task, drawn.Remaining);
        }

        NativeBattleRush91NodesStep AdvanceNodes(NativeBattleRush91Frame current, uint address)
        {
            NativeBattleRush91State shared = current.Memory.Rush91!;
            NativeBattleRecover93NodeState nodes = shared.Nodes with { PrivateDrawCursor = current.Effect.DrawArenaOffset ?? throw new NotSupportedException("Native Rush requires its current private cursor.") };
            NativeBattleRecover93NodeFrame nodeFrame = new(current.Memory, current.Effect, nodes, current.Random);
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Rush requires its initialized ordering table.");
            NativeBattleRecover93NodeStep advanced = NativeBattleRush91Nodes.AdvanceContext(nodeFrame, address, actor => graphics.Models[actor], projection, NativeCamera!.Roll, primitive.Pending, colors);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = advanced.Ordering } };
            NativeBattleEffectMemory memory = advanced.Frame.Memory with { Rush91 = advanced.Frame.Memory.Rush91! with { Nodes = advanced.Frame.State } };
            return new(new(memory, advanced.Frame.Effect with { DrawArenaOffset = advanced.Frame.State.PrivateDrawCursor }, advanced.Frame.Random), advanced.Complete);
        }

        NativeBattleRush91OverlayStep AdvanceOverlay(NativeBattleRush91Frame current, uint address, uint pause)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Rush requires its initialized ordering table.");
            NativeBattleFullscreenOverlayStep advanced = NativeBattleFullscreenOverlay.Advance8DDEC0(new(current.Memory, current.Effect.Pools, primitive.Pending), address, pause, resources.OverlayProgram);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = advanced.Frame.Ordering } };
            return new(current with { Memory = advanced.Frame.Memory, Effect = current.Effect with { Pools = advanced.Frame.Pools } }, advanced.Result);
        }

        NativeBattleRush91Frame Execute(NativeBattleRush91Frame current, NativeBattleRush91Operation operation)
        {
            NativeEffectMemory = current.Memory;
            switch (operation)
            {
                case NativeBattleRush91Contact contact:
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.Contact)] });
                    graphics = graphics with { PendingContacts = pending };
                    return current with { Memory = NativeEffectMemory! };
                case NativeBattleRush91Sound sound:
                    PublishNativeModuleSound(new(sound.Descriptor, unchecked((uint)sound.First), null) { Pan = checked((byte)sound.Second) });
                    return current;
                case NativeBattleRush91UploadModelTim:
                    NativeBattleEffectTextureState textures = current.Memory.Textures ?? throw new NotSupportedException("Native Rush requires its texture owner.");
                    (textures, _) = textures.EnqueueTim505E30(resources.ModelTim);
                    return current with { Memory = current.Memory with { Textures = textures } };
                case NativeBattleRush91Sprite sprite:
                    return Append(current, NativeBattleSearchSprites.Draw7582A0(sprite.Task, resources.Sprite12.Bytes, view, projection, NativeCamera!.Roll));
                case NativeBattleRush91Mesh mesh:
                    return Append(current, NativeBattleSearchMeshDrawing.Draw755FC0(mesh.Task, resources.ParticleMesh, view, projection, colors));
                case NativeBattleRush91CommonTextureLoad load:
                    (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)load.DestinationArenaOffset), load.CompletionAddress, (address, value) => current = current with { Effect = NativeBattleRush91Controller.WriteCompletionByte(current.Effect, address, value) });
                    graphics = graphics with { Auxiliary = auxiliary };
                    return current;
                default:
                    throw new NotSupportedException("Native Rush operation requires its original callback.");
            }
        }

        NativeBattleRush91Frame Append(NativeBattleRush91Frame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Rush requires its initialized ordering table.");
            NativeBattleEffectPacketWrite privateWrite = NativeBattleEffectPacketArena.Append(current.Memory, current.Effect.DrawArenaOffset!.Value, primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = privateWrite.Ordering } };
            return current with { Memory = privateWrite.Memory, Effect = current.Effect with { DrawArenaOffset = privateWrite.Cursor } };
        }
    }

    private bool TryWriteNativeRushCompletion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState updated)
    {
        updated = graphics;
        uint delta = unchecked(address - NativeBattleRush91Controller.MainBank);
        if (delta >= 0xC8 || delta % 0x64 != 0x62)
        {
            return false;
        }
        if (NativeEffectMemory?.DetachedReturnedTask is { Binding.Entry: 0x759370 } detached)
        {
            NativeEffectMemory = NativeEffectMemory with { DetachedReturnedTask = NativeBattleRush91Controller.WriteCompletionByte(detached, address, value) };
        }
        else
        {
            NativeBattleOriginalEffectState effect = graphics.OriginalEffect is { Binding.Entry: 0x759370 } current ? current : throw new NotSupportedException("Native Rush completion lost its physical task owner.");
            updated = graphics with { OriginalEffect = NativeBattleRush91Controller.WriteCompletionByte(effect, address, value) };
        }
        return true;
    }
}
