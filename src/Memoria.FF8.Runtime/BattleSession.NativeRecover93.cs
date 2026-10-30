using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeAngeloRestoration(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect! with { PresentationPackets = [] };
        NativeBattleAngeloRestorationProfile profile = NativeBattleAngeloRestorationProfile.ForBinding(effect.Binding);
        NativeBattleRecover93Resources resources = (profile.Effect == 93 ? effect.Binding.Recover93Resources : effect.Binding.Reverse94Resources) ?? throw new NotSupportedException("Native Angelo restoration requires its original per-module resources.");
        NativeBattleRecover93Frame frame = NativeBattleRecover93Controller.Initialize74E390(NativeEffectMemory ?? throw new NotSupportedException("Native Angelo restoration requires retained effect memory."), effect, nativeActionPackets, resources, nativeCrtRandom ?? throw new NotSupportedException("Native Angelo restoration requires its CRT random owner."));
        NativeEffectMemory = frame.Memory;
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };
    }

    private NativeBattleGraphicsState AdvanceNativeAngeloRestoration(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect! with { PresentationPackets = [] };
        NativeBattleAngeloRestorationProfile profile = NativeBattleAngeloRestorationProfile.ForBinding(effect.Binding);
        NativeBattleRecover93Resources resources = (profile.Effect == 93 ? effect.Binding.Recover93Resources : effect.Binding.Reverse94Resources) ?? throw new NotSupportedException("Native Angelo restoration requires its original per-module resources.");
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Angelo restoration requires its published projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Angelo restoration requires its retained view matrix.");
        NativeBattleGraphicsDefinition definition = this.definition.NativeGraphics ?? throw new NotSupportedException("Native Angelo restoration requires original graphics rules.");
        NativeBattleSummonColors colors = definition.Rules.SummonColors ?? throw new NotSupportedException("Native Angelo restoration requires its depth-cue constants.");
        NativeBattleRecover93Frame frame = new(NativeEffectMemory ?? throw new NotSupportedException("Native Angelo restoration requires retained effect memory."), effect, nativeCrtRandom ?? throw new NotSupportedException("Native Angelo restoration requires retained CRT random state."));
        frame = NativeBattleRecover93Controller.Advance74E520(frame, new(nativeActionPackets, view, actor => graphics.Models[actor], actor => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Native Angelo restoration requires the selected actor pose source."), ReadWorld, ReadBone, DrawModel, AdvanceProgram, AdvanceNodes, Execute), resources);
        NativeEffectMemory = frame.Memory;
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };

        ImmutableArray<byte> ReadWorld(int actor)
        {
            NativeBattleModelTransform transform = graphics.Models[actor].Transform ?? throw new NotSupportedException("Native Angelo restoration requires the actual target transform.");
            byte[] bytes = new byte[8];
            BinaryPrimitives.WriteInt16LittleEndian(bytes, transform.Position28.X);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2), transform.Position28.Y);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), transform.Position28.Z);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(6), transform.Vertical34);
            return bytes.ToImmutableArray();
        }

        NativeBattleVector ReadBone(NativeBattleRecover93Frame current, uint pointer, byte selector)
        {
            NativeBattleRecover93State shared = profile.Read(current.Memory)!;
            NativeBattleEffectOwnedModelState model = shared.Models.Single(candidate => candidate.TaskAddress + 0x30 == pointer);
            NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(current.Effect.Pools).Read(model.TaskAddress);
            ImmutableArray<short> rotation = Enumerable.Range(0, 9).Select(index => unchecked((short)task.ReadWord(0x70 + index * 2))).ToImmutableArray();
            NativeBattleActionMatrix world = new(rotation, unchecked((int)task.ReadDword(0x84)), unchecked((int)task.ReadDword(0x88)), unchecked((int)task.ReadDword(0x8C)), 4096);
            return NativeBattlePoseMatrices.ReadEffectOwnedBone(resources.Model.PoseSource, model.DecodedPose ?? throw new NotSupportedException("Native Angelo restoration requires its current independent model pose."), world, selector);
        }

        NativeBattleRecover93ModelDrawStep DrawModel(NativeBattleRecover93Frame current, NativeBattleModulePoolTask task, NativeBattleEffectOwnedModelState model)
        {
            NativeBattleRecover93State shared = profile.Read(current.Memory)!;
            int size = checked(resources.Model.Geometry[0].Vertices.Length * 8);
            if (shared.ProjectedScratch is null && (task.ReadDword(0xAC) & 1) == 0)
            {
                throw new NotSupportedException("Native Angelo restoration cannot read an uninitialized projected scratch owner.");
            }
            NativeBattleEffectOwnedModelDraw drawn = NativeBattleEffectOwnedModelDrawing.Draw7558A0(task, model, resources.Model, view, projection, definition.Rules.ShadowSource ?? throw new NotSupportedException("Native Angelo restoration requires its original shadow source."), definition.PrimitiveCapabilities ?? throw new NotSupportedException("Native Angelo restoration requires its selected device capabilities."), colors, shared.ProjectedScratch ?? new byte[size].ToImmutableArray(), pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(profile.Effect, task) with { ResourceGeneration = resources.Model.Address });
            if (shared.ProjectedScratch is null && drawn.ProjectedBytesWritten != size)
            {
                throw new InvalidDataException("Native Angelo restoration's first body draw did not establish every projected record.");
            }
            current = current with { Memory = profile.Write(current.Memory, shared with { ProjectedScratch = drawn.ProjectedScratch }) };
            return new(Append(current, drawn.Packets), drawn.Task);
        }

        NativeBattleRecover93ProgramStep AdvanceProgram(NativeBattleRecover93Frame current, NativeBattleModulePoolTask task)
        {
            NativeBattleSearchProgramDraw drawn = NativeBattleSearchProgramDrawing.Advance758D70(task, resources.ProgramSource, view, projection, colors, pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(profile.Effect, task));
            return new(Append(current, drawn.Packets), drawn.Task, drawn.Remaining);
        }

        NativeBattleRecover93NodesStep AdvanceNodes(NativeBattleRecover93Frame current, uint address)
        {
            NativeBattleRecover93State shared = profile.Read(current.Memory)!;
            NativeBattleRecover93NodeState nodes = shared.Nodes with { PrivateDrawCursor = current.Effect.DrawArenaOffset ?? throw new NotSupportedException("Native Angelo restoration requires its current private cursor.") };
            NativeBattleRecover93NodeFrame nodeFrame = new(current.Memory, current.Effect, nodes, current.Random);
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Angelo restoration requires its initialized ordering table.");
            NativeBattleRecover93NodeStep advanced = NativeBattleRecover93Nodes.Advance74F6E0(nodeFrame, address, actor => graphics.Models[actor], projection, NativeCamera!.Roll, primitive.Pending, colors, capturePresentation: true);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = advanced.Ordering } };
            NativeBattleEffectMemory memory = profile.Write(advanced.Frame.Memory, profile.Read(advanced.Frame.Memory)! with { Nodes = advanced.Frame.State });
            return new(new(memory, advanced.Frame.Effect with { DrawArenaOffset = advanced.Frame.State.PrivateDrawCursor }, advanced.Frame.Random), advanced.Complete);
        }

        NativeBattleRecover93Frame Execute(NativeBattleRecover93Frame current, NativeBattleRecover93Operation operation)
        {
            NativeEffectMemory = current.Memory;
            switch (operation)
            {
                case NativeBattleRecover93Contact contact:
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.Contact)] });
                    graphics = graphics with { PendingContacts = pending };
                    return current with { Memory = NativeEffectMemory! };
                case NativeBattleRecover93Sound sound:
                    PublishNativeModuleSound(new(sound.Descriptor, unchecked((uint)sound.First), null) { Pan = checked((byte)sound.Second) });
                    return current;
                case NativeBattleRecover93Voice voice:
                    if (voice.Second != 1)
                    {
                        throw new NotSupportedException("Native Angelo restoration requires its reached voice logging operand.");
                    }
                    PublishNativeModuleSound(new(voice.Descriptor, 0, null) { RawPan = checked((uint)voice.First), Volume = checked((uint)voice.Third) });
                    return current;
                case NativeBattleRecover93UploadModelTim:
                    NativeBattleEffectTextureState textures = current.Memory.Textures ?? throw new NotSupportedException("Native Angelo restoration requires its texture owner.");
                    (textures, _) = textures.EnqueueTim505E30(resources.ModelTim);
                    return current with { Memory = current.Memory with { Textures = textures } };
                case NativeBattleRecover93StageClearRgb:
                    NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native Angelo restoration requires its actual stage group owners.");
                    NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = 0, ColorFlags = group.ColorFlags & 0xFF000000 }).ToImmutableArray() };
                    return current;
                case NativeBattleRecover93StageDepth depth:
                    NativeBattleStageState depthStage = NativeStage ?? throw new NotSupportedException("Native Angelo restoration requires its actual stage group owners.");
                    NativeStage = depthStage with { Groups = depthStage.Groups.Select(group => group with { ColorDepth = depth.Value }).ToImmutableArray() };
                    return current;
                case NativeBattleRecover93CommonTextureLoad load:
                    (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)load.DestinationArenaOffset), load.CompletionAddress, (address, value) => current = current with { Effect = NativeBattleRecover93Controller.WriteCompletionByte(current.Effect, address, value) });
                    graphics = graphics with { Auxiliary = auxiliary };
                    return current;
                default:
                    throw new NotSupportedException("Native Angelo restoration operation requires its original callback.");
            }
        }

        NativeBattleRecover93Frame Append(NativeBattleRecover93Frame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Angelo restoration requires its initialized ordering table.");
            int cursor = current.Effect.DrawArenaOffset!.Value;
            current = current with { Effect = current.Effect with { PresentationPackets = NativeBattleEffectPresentationPackets.Record(current.Effect.PresentationPackets, "20DFAB8", 0, cursor, batch) } };
            NativeBattleEffectPacketWrite privateWrite = NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = privateWrite.Ordering } };
            return current with { Memory = privateWrite.Memory, Effect = current.Effect with { DrawArenaOffset = privateWrite.Cursor } };
        }
    }

    private bool TryWriteNativeAngeloRestorationCompletion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState updated)
    {
        updated = graphics;
        foreach (NativeBattleAngeloRestorationProfile profile in new[] { NativeBattleAngeloRestorationProfile.Recover93, NativeBattleAngeloRestorationProfile.Reverse94 })
        {
            uint delta = unchecked(address - profile.MainBank);
            if (delta >= 0xC8 || delta % 0x64 != 0x62)
            {
                continue;
            }
            if (NativeEffectMemory?.DetachedReturnedTask is { } detached && detached.Binding.Entry == profile.Entry)
            {
                NativeEffectMemory = NativeEffectMemory with { DetachedReturnedTask = NativeBattleRecover93Controller.WriteCompletionByte(detached, address, value) };
            }
            else
            {
                NativeBattleOriginalEffectState effect = graphics.OriginalEffect is { } current && current.Binding.Entry == profile.Entry ? current : throw new NotSupportedException("Native Angelo restoration completion lost its physical task owner.");
                updated = graphics with { OriginalEffect = NativeBattleRecover93Controller.WriteCompletionByte(effect, address, value) };
            }
            return true;
        }
        return false;
    }
}
