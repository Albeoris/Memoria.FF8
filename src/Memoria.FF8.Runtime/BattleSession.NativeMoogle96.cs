using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeMoogle731DE0(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleMoogle96Resources resources = effect.Binding.Moogle96Resources ?? throw new NotSupportedException("Native Moogle requires its original per-module resources.");
        NativeBattleMoogle96Frame frame = NativeBattleMoogle96Controller.Initialize731DE0(NativeEffectMemory ?? throw new NotSupportedException("Native Moogle requires retained effect memory."), effect, nativeActionPackets, resources, nativeCrtRandom ?? throw new NotSupportedException("Native Moogle requires its CRT random owner."));
        NativeEffectMemory = frame.Memory;
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };
    }

    private NativeBattleGraphicsState AdvanceNativeMoogle731F70(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleMoogle96Resources resources = effect.Binding.Moogle96Resources ?? throw new NotSupportedException("Native Moogle requires its original per-module resources.");
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Moogle requires its published projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Moogle requires its retained view matrix.");
        NativeBattleGraphicsDefinition definition = this.definition.NativeGraphics ?? throw new NotSupportedException("Native Moogle requires original graphics rules.");
        NativeBattleSummonColors colors = definition.Rules.SummonColors ?? throw new NotSupportedException("Native Moogle requires its depth-cue constants.");
        uint transition = NativeStage?.ModelColors?.Color40 ?? throw new NotSupportedException("Native Moogle requires its retained process transition color.");
        NativeBattleMoogle96Frame frame = new(NativeEffectMemory ?? throw new NotSupportedException("Native Moogle requires retained effect memory."), effect, nativeCrtRandom ?? throw new NotSupportedException("Native Moogle requires retained CRT random state."));
        frame = NativeBattleMoogle96Controller.Advance731F70(frame, new(nativeActionPackets, view, actor => graphics.Models[actor], actor => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Native Moogle requires the selected actor pose source."), transition, DrawModel, AdvanceProgram, AdvanceNodes, StartVibration, Execute), resources);
        NativeEffectMemory = frame.Memory;
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleMoogle96ModelDrawStep DrawModel(NativeBattleMoogle96Frame current, NativeBattleModulePoolTask task, NativeBattleEffectOwnedModelState model)
        {
            NativeBattleMoogle96State shared = current.Memory.Moogle96!;
            int size = checked(resources.Model.Geometry[0].Vertices.Length * 8);
            if (shared.ProjectedScratch is null && (task.ReadDword(0xAC) & 1) == 0)
            {
                throw new NotSupportedException("Native Moogle cannot read an uninitialized projected scratch owner.");
            }
            NativeBattleEffectOwnedModelDraw drawn = NativeBattleEffectOwnedModelDrawing.Draw7558A0(task, model, resources.Model, view, projection, definition.Rules.ShadowSource ?? throw new NotSupportedException("Native Moogle requires its original shadow source."), definition.PrimitiveCapabilities ?? throw new NotSupportedException("Native Moogle requires its selected device capabilities."), colors, shared.ProjectedScratch ?? new byte[size].ToImmutableArray());
            if (shared.ProjectedScratch is null && drawn.ProjectedBytesWritten != size)
            {
                throw new InvalidDataException("Native Moogle's first body draw did not establish every projected record.");
            }
            current = current with { Memory = current.Memory with { Moogle96 = shared with { ProjectedScratch = drawn.ProjectedScratch } } };
            return new(AppendCommon(current, drawn.Packets), drawn.Task);
        }

        NativeBattleMoogle96ProgramStep AdvanceProgram(NativeBattleMoogle96Frame current, NativeBattleModulePoolTask task)
        {
            NativeBattleMoogle96State shared = current.Memory.Moogle96!;
            NativeBattleMoogle96ProgramDraw drawn = NativeBattleMoogle96ProgramDrawing.Advance734450(task, shared.ProgramSource ?? throw new NotSupportedException("Native Moogle requires its retained mutable program source."), view, projection, colors, shared.Global257CF24 ?? throw new NotSupportedException("Native Moogle requires its retained program pause operand."));
            return new(AppendCommon(current, drawn.Packets), drawn.Task, drawn.Remaining);
        }

        NativeBattleMoogle96NodesStep AdvanceNodes(NativeBattleMoogle96Frame current, uint address)
        {
            NativeBattleMoogle96State shared = current.Memory.Moogle96!;
            NativeBattleMoogle96NodeState nodes = shared.Nodes with { Work = shared.Nodes.Work with { PrivateDrawCursor = current.Effect.DrawArenaOffset ?? throw new NotSupportedException("Native Moogle requires its current private cursor.") } };
            NativeBattleMoogle96NodeFrame nodeFrame = new(current.Memory, current.Effect, nodes, current.Random);
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Moogle requires its initialized ordering table.");
            NativeBattleMoogle96NodeStep advanced = NativeBattleMoogle96Nodes.AdvanceContext(nodeFrame, address, actor => graphics.Models[actor], projection, NativeCamera!.Roll, primitive.Pending, colors);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = advanced.Ordering } };
            NativeBattleEffectMemory memory = advanced.Frame.Memory with { Moogle96 = advanced.Frame.Memory.Moogle96! with { Nodes = advanced.Frame.State } };
            return new(new(memory, advanced.Frame.Effect with { DrawArenaOffset = advanced.Frame.State.Work.PrivateDrawCursor }, advanced.Frame.Random), advanced.Complete);
        }

        NativeBattleMoogle96VibrationStep StartVibration(NativeBattleMoogle96Frame current)
        {
            NativeVibrationEnqueue queued = (NativeVibrationProcess ?? throw new NotSupportedException("Native Moogle requires its retained vibration process.")).Enqueue(resources.Vibration.Bank, 1, 128);
            NativeVibrationProcess = queued.State;
            return new(current, unchecked((short)queued.Handle));
        }

        NativeBattleMoogle96Frame Execute(NativeBattleMoogle96Frame current, NativeBattleMoogle96Operation operation)
        {
            NativeEffectMemory = current.Memory;
            switch (operation)
            {
                case NativeBattleMoogle96Contact contact:
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.Contact)] });
                    graphics = graphics with { PendingContacts = pending };
                    return current with { Memory = NativeEffectMemory! };
                case NativeBattleMoogle96Sound sound:
                    PublishNativeModuleSound(new(sound.Descriptor, unchecked((uint)sound.First), null) { Pan = checked((byte)sound.Second) });
                    return current;
                case NativeBattleMoogle96CameraPublish camera:
                    NativeCamera = NativeBattleMoogleCamera.Publish733150(camera.Step, NativeCamera ?? throw new NotSupportedException("Native Moogle requires its current camera globals."));
                    return current;
                case NativeBattleMoogle96StageFade fade:
                    NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native Moogle requires its four actual stage groups.");
                    NativeStage = stage with { Groups = stage.Groups.Select(group =>
                    {
                        byte flags = group.Flags;
                        if (fade.Clear0200)
                        {
                            flags = (byte)(flags & 0xFD);
                        }
                        if (fade.TransformFlagsByte1)
                        {
                            flags = (byte)((flags & 0xFD) | ((flags >> 1) & 2));
                        }
                        return group with { Flags = flags, ColorDepth = fade.Depth ?? group.ColorDepth, ColorFlags = fade.ClearRgb ? group.ColorFlags & 0xFF000000 : group.ColorFlags };
                    }).ToImmutableArray() };
                    return current;
                case NativeBattleMoogle96EnemyFlags enemy:
                    graphics = graphics with { Models = graphics.Models.SetItem(enemy.Actor, graphics.Models[enemy.Actor] with { Flags0 = enemy.Flags0 }) };
                    return current;
                case NativeBattleMoogle96EnemyFade enemy:
                    graphics = graphics with { Models = graphics.Models.SetItem(enemy.Actor, graphics.Models[enemy.Actor] with { Flags0 = enemy.Flags0, Color40 = enemy.Color40, Alpha7 = enemy.Alpha7 }) };
                    return current;
                case NativeBattleMoogle96MusicGain music:
                    NativeGlobalSoundProcess soundProcess = NativeGlobalSoundProcess ?? throw new NotSupportedException("Native Moogle requires its retained global sound process.");
                    NativeMusicPlaybackState playback = soundProcess.MusicPlayback ?? throw new NotSupportedException("Native Moogle requires its retained channel gain owner.");
                    NativeGlobalSoundProcess = soundProcess with { MusicPlayback = playback.SetGain46BB40(0, music.Value) };
                    return current;
                case NativeBattleMoogle96StopVibration vibration:
                    NativeVibrationProcess = (NativeVibrationProcess ?? throw new NotSupportedException("Native Moogle requires its retained vibration process.")).Stop4A2940(vibration.Handle);
                    return current;
                case NativeBattleMoogle96TextureCopy copy:
                    NativeBattleEffectTextureState textures = current.Memory.Textures ?? throw new NotSupportedException("Native Moogle requires its texture copy queue.");
                    return current with { Memory = current.Memory with { Textures = textures.EnqueueCopy505EB0(copy.Copy.SourceX, copy.Copy.SourceY, copy.Copy.Width, copy.Copy.Height, copy.Copy.DestinationX, copy.Copy.DestinationY) } };
                case NativeBattleMoogle96DirectorModeE1:
                    return AppendCommon(current, NativeBattleMoogle96DirectorPackets.Batch7327B0());
                case NativeBattleMoogle96FileRequest request:
                    NativeBattleMoogle96IoState io = current.Memory.Moogle96!.Io ?? throw new NotSupportedException("Native Moogle requires its current script and file destination.");
                    NativeBattleEffectFileAsset file = resources.Files.Single(candidate => candidate.FileId == request.Request.FileId);
                    NativeBattleMoogle96IoQueued queued = NativeBattleMoogle96Io.CopyAndQueueFile(io, current.Memory, NativeFileIo ?? throw new NotSupportedException("Native Moogle requires its actual file completion scheduler."), file);
                    NativeFileIo = queued.FileIo;
                    return current with { Memory = queued.Memory };
                case NativeBattleMoogle96CommonTextureLoad load:
                    (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)load.DestinationArenaOffset), load.CompletionAddress, (address, value) => current = current with { Effect = NativeBattleMoogle96Controller.WriteCompletionByte(current.Effect, address, value) });
                    graphics = graphics with { Auxiliary = auxiliary };
                    return current;
                default:
                    throw new NotSupportedException("Native Moogle operation requires its original callback.");
            }
        }

        NativeBattleMoogle96Frame AppendCommon(NativeBattleMoogle96Frame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Moogle requires its initialized ordering table.");
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Moogle requires its common packet owner."), primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = current.Memory with { PrimitiveProcess = written.Process } };
        }
    }

    private bool TryWriteNativeMoogleCompletion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState updated)
    {
        updated = graphics;
        uint delta = unchecked(address - NativeBattleMoogle96Controller.MainBank);
        if (delta >= 0xC8 || delta % 0x64 != 0x62)
        {
            return false;
        }
        if (NativeEffectMemory?.DetachedReturnedTask is { Binding.Entry: 0x731DE0 } detached)
        {
            NativeEffectMemory = NativeEffectMemory with { DetachedReturnedTask = NativeBattleMoogle96Controller.WriteCompletionByte(detached, address, value) };
        }
        else
        {
            NativeBattleOriginalEffectState effect = graphics.OriginalEffect is { Binding.Entry: 0x731DE0 } current ? current : throw new NotSupportedException("Native Moogle completion lost its physical task owner.");
            updated = graphics with { OriginalEffect = NativeBattleMoogle96Controller.WriteCompletionByte(effect, address, value) };
        }
        return true;
    }
}
