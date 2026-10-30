using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeChoco729A60(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleChoco97Frame frame = NativeBattleChoco97Controller.Initialize729A60(NativeEffectMemory ?? throw new NotSupportedException("Native Choco requires retained effect memory."), effect, nativeActionPackets, effect.Binding.Choco97Resources ?? throw new NotSupportedException("Native Choco requires its original resources."), nativeCrtRandom ?? throw new NotSupportedException("Native Choco requires retained CRT random state."));
        NativeEffectMemory = frame.Memory;
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };
    }

    private NativeBattleGraphicsState AdvanceNativeChoco729BD0(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect! with { PresentationPackets = [] };
        NativeBattleChoco97Resources resources = effect.Binding.Choco97Resources ?? throw new NotSupportedException("Native Choco requires its original resources.");
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Choco requires its published projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Choco requires its retained view matrix.");
        NativeBattleGraphicsDefinition definition = this.definition.NativeGraphics ?? throw new NotSupportedException("Native Choco requires original graphics rules.");
        NativeBattleSummonColors colors = definition.Rules.SummonColors ?? throw new NotSupportedException("Native Choco requires its depth-cue constants.");
        NativeBattleChoco97Frame frame = new(NativeEffectMemory ?? throw new NotSupportedException("Native Choco requires retained effect memory."), effect, nativeCrtRandom ?? throw new NotSupportedException("Native Choco requires retained CRT random state."));
        frame = NativeBattleChoco97Controller.Advance729BD0(frame, new(nativeActionPackets, view, actor => graphics.Models[actor], actor => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Native Choco requires the selected actor pose source."), DrawModel, AdvanceProgram, AdvanceNodes, AdvanceSprite, StartVibration, Execute), resources);
        NativeEffectMemory = frame.Memory;
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleChoco97ModelDrawStep DrawModel(NativeBattleChoco97Frame current, NativeBattleModulePoolTask task, NativeBattleEffectOwnedModelState model)
        {
            NativeBattleChoco97State shared = Shared(current);
            int size = checked(resources.Model.Model.Geometry[0].Vertices.Length * 8);
            if (shared.ProjectedScratch is null && (task.ReadDword(0xAC) & 1) == 0)
            {
                throw new NotSupportedException("Native Choco cannot read an uninitialized projected scratch owner.");
            }
            NativeBattleEffectOwnedModelDraw drawn = NativeBattleEffectOwnedModelDrawing.Draw7558A0(task, model, resources.Model.Model, view, projection, definition.Rules.ShadowSource ?? throw new NotSupportedException("Native Choco requires its original shadow source."), definition.PrimitiveCapabilities ?? throw new NotSupportedException("Native Choco requires its device capabilities."), colors, shared.ProjectedScratch ?? new byte[size].ToImmutableArray(), pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(97, task) with { ResourceGeneration = model.ResourceAddress });
            if (shared.ProjectedScratch is null && drawn.ProjectedBytesWritten != size)
            {
                throw new InvalidDataException("Native Choco's first body draw did not establish every projected record.");
            }
            current = current with { Memory = current.Memory with { Choco97 = shared with { ProjectedScratch = drawn.ProjectedScratch } } };
            return new(AppendCommon(current, drawn.Packets), drawn);
        }

        NativeBattleChoco97ProgramStep AdvanceProgram(NativeBattleChoco97Frame current, NativeBattleModulePoolTask task)
        {
            NativeBattleChoco97State shared = Shared(current);
            uint frozen = shared.FreezeSecondary ?? throw new NotSupportedException("Native Choco requires its retained program pause operand.");
            if (task.ReadDword(0x74) == resources.ProgramSource.Address)
            {
                NativeBattleChoco97ProgramDraw drawn = NativeBattleChoco97ProgramDrawing.Advance72A720(task, shared.ProgramSource ?? throw new NotSupportedException("Native Choco requires its retained static program source."), shared.MorphScratch, view, projection, colors, frozen, pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(97, task));
                current = current with { Memory = current.Memory with { Choco97 = shared with { MorphScratch = drawn.MorphScratch } } };
                return new(AppendCommon(current, drawn.Packets), drawn.Task, drawn.Remaining);
            }
            NativeBattleChoco97LoadedProgram profile = task.Callback switch
            {
                0x731840 => NativeBattleChoco97LoadedProgram.File445,
                0x731950 => NativeBattleChoco97LoadedProgram.File449,
                0x731100 => NativeBattleChoco97LoadedProgram.File450,
                _ => throw new NotSupportedException("Native Choco program requires its actual loaded-file callback.")
            };
            var selected = NativeBattleChoco97ProgramDrawing.LoadedProfile(profile);
            NativeBattleChoco97IoState io = shared.Io ?? throw new NotSupportedException("Native Choco requires its loaded program IO owner.");
            NativeBattleChoco97Context context = shared.Context ?? throw new NotSupportedException("Native Choco requires its current context origins.");
            NativeBattleGfAnimationSource source = NativeBattleGfAnimationSource.Borrow(selected.Address, io.Banks.Bytes, checked((int)(selected.Address - io.Banks.BaseAddress)), selected.Length);
            NativeBattleChoco97LoadedProgramDraw loaded = NativeBattleChoco97ProgramDrawing.AdvanceLoaded(profile, task, source, context.Data.Slice(0, 8), context.Data.Slice(8, 8), shared.MorphScratch, view, projection, colors, frozen, pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(97, task));
            current = current with { Memory = current.Memory with { Choco97 = shared with { Io = io, MorphScratch = loaded.MorphScratch } } };
            return new(AppendCommon(current, loaded.Packets), loaded.Task, loaded.Remaining);
        }

        NativeBattleChoco97NodesStep AdvanceNodes(NativeBattleChoco97Frame current, uint address)
        {
            NativeBattleChoco97State shared = Shared(current);
            NativeBattleChoco97NodeState nodes = shared.Nodes with { Work = shared.Nodes.Work with { PrivateDrawCursor = current.Effect.DrawArenaOffset ?? throw new NotSupportedException("Native Choco requires its current private cursor.") } };
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Choco requires its initialized ordering table.");
            NativeBattleChoco97NodeStep advanced = NativeBattleChoco97Nodes.AdvanceContext(new(current.Memory, current.Effect, nodes, shared.Io, current.Random), address, actor => graphics.Models[actor], projection, NativeCamera!.Roll, primitive.Pending, colors);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = advanced.Ordering } };
            NativeBattleEffectMemory memory = advanced.Frame.Memory with { Choco97 = advanced.Frame.Memory.Choco97! with { Nodes = advanced.Frame.State, Io = advanced.Frame.Io } };
            return new(new(memory, advanced.Frame.Effect with { DrawArenaOffset = advanced.Frame.State.Work.PrivateDrawCursor }, advanced.Frame.Random), advanced.Complete);
        }

        NativeBattleChoco97SpriteDrawStep AdvanceSprite(NativeBattleChoco97Frame current, NativeBattleModulePoolTask task)
        {
            NativeBattleChoco97Context context = Shared(current).Context ?? throw new NotSupportedException("Native Choco sprite requires its current actor origin.");
            NativeBattleChoco97SpriteStep step = NativeBattleChoco97Sprite.Advance730C10(task, resources.Sprite, context.Data.Slice(0, 8), view, projection, NativeCamera!.Roll, pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(97, task));
            if (step.Packets is { } packets)
            {
                NativeBattlePrimitiveProcess process = current.Memory.PrimitiveProcess!;
                current = current with { Effect = current.Effect with { PresentationPackets = current.Effect.PresentationPackets.AddRange(packets.Primitives.Select(item => new NativeBattlePrimitiveReference(process.PacketBank!, process.PacketBuffer, checked(process.ByteCursor!.Value + item.ByteOffset)))) } };
                NativeEffectMemory = current.Memory;
                graphics = AppendNativeModulePacketBatch(graphics, packets, null, null);
                current = current with { Memory = NativeEffectMemory! };
            }
            return new(current, step.Task, step.ReadyToComplete);
        }

        NativeBattleChoco97VibrationStep StartVibration(NativeBattleChoco97Frame current)
        {
            NativeVibrationEnqueue queued = (NativeVibrationProcess ?? throw new NotSupportedException("Native Choco requires its vibration process.")).Enqueue(resources.Vibration.Bank, 1, 128);
            NativeVibrationProcess = queued.State;
            return new(current, unchecked((short)queued.Handle));
        }

        NativeBattleChoco97Frame Execute(NativeBattleChoco97Frame current, NativeBattleChoco97Operation operation)
        {
            NativeEffectMemory = current.Memory;
            switch (operation)
            {
                case NativeBattleChoco97Contact contact:
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.Contact)] });
                    graphics = graphics with { PendingContacts = pending };
                    return current with { Memory = NativeEffectMemory! };
                case NativeBattleChoco97Sound sound:
                    uint descriptor = sound.Descriptor switch
                    {
                        0x152B5BC => resources.SoundInitial,
                        0x152B5C0 => resources.SoundFirst,
                        0x152B5C4 => resources.SoundSecond,
                        _ => throw new NotSupportedException("Native Choco sound requires its original descriptor address.")
                    };
                    PublishNativeModuleSound(new(descriptor, sound.Flags, null) { Pan = sound.Pan });
                    return current;
                case NativeBattleChoco97CameraPublish camera:
                    NativeCamera = NativeBattleMoogleCamera.Publish733150(camera.Step, NativeCamera ?? throw new NotSupportedException("Native Choco requires its current camera globals."));
                    return current;
                case NativeBattleChoco97StageDepth fade:
                    NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native Choco requires its four stage groups.");
                    NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = fade.Depth ?? group.ColorDepth, ColorFlags = fade.ClearRgb ? group.ColorFlags & 0xFF000000 : group.ColorFlags }).ToImmutableArray() };
                    return current;
                case NativeBattleChoco97VibrationStop vibration:
                    NativeVibrationProcess = (NativeVibrationProcess ?? throw new NotSupportedException("Native Choco requires its vibration process.")).Stop4A2940(vibration.Handle);
                    return current;
                case NativeBattleChoco97TextureCopy copy:
                    NativeBattleEffectTextureState textures = current.Memory.Textures ?? throw new NotSupportedException("Native Choco requires its texture copy queue.");
                    return current with { Memory = current.Memory with { Textures = textures.EnqueueCopy505EB0(copy.Copy.SourceX, copy.Copy.SourceY, copy.Copy.Width, copy.Copy.Height, copy.Copy.DestinationX, copy.Copy.DestinationY) } };
                case NativeBattleChoco97FileRequest request:
                    NativeBattleChoco97State shared = Shared(current);
                    NativeBattleChoco97IoQueued queued = NativeBattleChoco97Io.CopyAndQueueFile(shared.Io ?? throw new NotSupportedException("Native Choco requires its current IO script."), current.Memory, NativeFileIo ?? throw new NotSupportedException("Native Choco requires its actual file completion scheduler."), resources.Io.Files.Single(file => file.FileId == request.Request.FileId));
                    NativeFileIo = queued.FileIo;
                    return current with { Memory = queued.Memory with { Choco97 = Shared(new(queued.Memory, current.Effect, current.Random)) with { Io = queued.State } } };
                case NativeBattleChoco97CommonTextureLoad load:
                    (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)load.DestinationArenaOffset), load.CompletionAddress, (address, value) => current = current with { Effect = NativeBattleChoco97Controller.WriteCompletionByte(current.Effect, address, value) });
                    graphics = graphics with { Auxiliary = auxiliary };
                    return current;
                default:
                    throw new NotSupportedException("Native Choco operation requires its original callback.");
            }
        }

        NativeBattleChoco97Frame AppendCommon(NativeBattleChoco97Frame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Choco requires its initialized ordering table.");
            NativeBattlePrimitiveProcess process = current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Choco requires its common packet owner.");
            current = current with { Effect = current.Effect with { PresentationPackets = NativeBattleEffectPresentationPackets.Record(current.Effect.PresentationPackets, process.PacketBank!, process.PacketBuffer, process.ByteCursor!.Value, batch) } };
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(process, primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = current.Memory with { PrimitiveProcess = written.Process } };
        }

        static NativeBattleChoco97State Shared(NativeBattleChoco97Frame current) => current.Memory.Choco97 ?? throw new NotSupportedException("Native Choco lost its retained process owners.");
    }

    private bool TryWriteNativeChoco97Completion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState updated)
    {
        updated = graphics;
        uint delta = unchecked(address - NativeBattleChoco97Controller.MainBank);
        if (delta >= 0xC8 || delta % 0x64 != 0x62)
        {
            return false;
        }
        if (NativeEffectMemory?.DetachedReturnedTask is { Binding.Entry: 0x729A60 } detached)
        {
            NativeEffectMemory = NativeEffectMemory with { DetachedReturnedTask = NativeBattleChoco97Controller.WriteCompletionByte(detached, address, value) };
        }
        else
        {
            NativeBattleOriginalEffectState effect = graphics.OriginalEffect is { Binding.Entry: 0x729A60 } current ? current : throw new NotSupportedException("Native Choco completion lost its physical root task.");
            updated = graphics with { OriginalEffect = NativeBattleChoco97Controller.WriteCompletionByte(effect, address, value) };
        }
        return true;
    }
}
