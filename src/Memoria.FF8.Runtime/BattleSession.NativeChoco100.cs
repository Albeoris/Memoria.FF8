using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeChoco70D390(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleChoco100Frame frame = NativeBattleChoco100Controller.Initialize70D390(NativeEffectMemory ?? throw new NotSupportedException("Native Choco requires retained effect memory."), effect, nativeActionPackets, effect.Binding.Choco100Resources ?? throw new NotSupportedException("Native Choco requires its original resources."), nativeCrtRandom ?? throw new NotSupportedException("Native Choco requires retained CRT random state."));
        NativeEffectMemory = frame.Memory;
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };
    }

    private NativeBattleGraphicsState AdvanceNativeChoco70EB00(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleChoco100Resources resources = effect.Binding.Choco100Resources ?? throw new NotSupportedException("Native Choco requires its original resources.");
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Choco requires its published projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Choco requires its retained view matrix.");
        NativeBattleGraphicsDefinition definition = this.definition.NativeGraphics ?? throw new NotSupportedException("Native Choco requires original graphics rules.");
        NativeBattleSummonColors colors = definition.Rules.SummonColors ?? throw new NotSupportedException("Native Choco requires its depth-cue constants.");
        NativeBattleChoco100Frame frame = new(NativeEffectMemory ?? throw new NotSupportedException("Native Choco requires retained effect memory."), effect, nativeCrtRandom ?? throw new NotSupportedException("Native Choco requires retained CRT random state."));
        frame = NativeBattleChoco100Controller.Advance70EB00(frame, new(nativeActionPackets, view, actor => graphics.Models[actor], actor => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Native Choco requires the selected actor pose source."), DrawModel, AdvanceProgram, AdvanceNodes, AdvanceAux, AdvanceSecondary, AdvanceSmall, StartVibration, Execute), resources);
        NativeEffectMemory = frame.Memory;
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleChoco100ModelDrawStep DrawModel(NativeBattleChoco100Frame current, NativeBattleModulePoolTask task, NativeBattleEffectOwnedModelState model)
        {
            NativeBattleChoco100State shared = Shared(current);
            NativeBattleEffectOwnedModelResource resource = model.ResourceAddress switch
            {
                0x1656DE4 => resources.Model.Model,
                0x19E8678 => resources.LoadedModel.Model,
                _ => throw new NotSupportedException("Native Choco model requires its actual owned resource.")
            };
            NativeBattleEffectOwnedModelDraw drawn = NativeBattleEffectOwnedModelDrawing.Draw7558A0(task, model, resource, view, projection, definition.Rules.ShadowSource ?? throw new NotSupportedException("Native Choco requires its original shadow source."), definition.PrimitiveCapabilities ?? throw new NotSupportedException("Native Choco requires its device capabilities."), colors, shared.ProjectedScratch);
            current = current with { Memory = current.Memory with { Choco100 = shared with { ProjectedScratch = drawn.ProjectedScratch } } };
            return new(AppendCommon(current, drawn.Packets), drawn);
        }

        NativeBattleChoco100ProgramStep AdvanceProgram(NativeBattleChoco100Frame current, uint address, NativeBattleModulePoolTask task)
        {
            NativeBattleChoco100State shared = Shared(current);
            uint frozen = shared.FreezeSecondary ?? throw new NotSupportedException("Native Choco requires its retained program pause operand.");
            NativeBattleChoco97Context context = shared.Context ?? throw new NotSupportedException("Native Choco requires its current context origins.");
            NativeBattleChoco99ProgramDraw result;
            bool initial = task.ReadDword(0x74) == resources.ProgramSource.Address;
            if (initial)
            {
                result = NativeBattleChoco100ProgramDrawing.AdvanceInitial(task, shared.ProgramSource ?? throw new NotSupportedException("Native Choco requires its retained static program."), context.Data.Slice(0, 8), shared.MorphScratch, view, projection, colors, frozen);
            }
            else
            {
                NativeBattleChoco100LoadedProgram profile = task.Callback switch
                {
                    0x7163D0 => NativeBattleChoco100LoadedProgram.File492,
                    0x716840 => NativeBattleChoco100LoadedProgram.File496,
                    0x716950 => NativeBattleChoco100LoadedProgram.File502,
                    _ => throw new NotSupportedException("Native Choco program requires its actual loaded callback.")
                };
                var selected = NativeBattleChoco100ProgramDrawing.LoadedProfile(profile);
                result = NativeBattleChoco100ProgramDrawing.AdvanceLoaded(profile, task, ReadProgram(current, selected.Address, selected.Length), context.Data.Slice(0, 8), context.Data.Slice(8, 8), unchecked((short)context.Word(0x40)), shared.MorphScratch, view, projection, colors, frozen);
            }
            NativeBattleChoco97LoadedProgramDraw loaded = result.Draw;
            shared = Shared(current);
            if (initial)
            {
                shared = shared with { ProgramSource = loaded.Source, MorphScratch = loaded.MorphScratch };
            }
            else
            {
                NativeBattleChoco100IoState io = shared.Io ?? throw new NotSupportedException("Native Choco requires its loaded program IO owner.");
                shared = shared with { Io = io with { Banks = io.Banks.Write(loaded.Source.Address, loaded.Source.Data) }, MorphScratch = loaded.MorphScratch };
            }
            current = current with { Memory = current.Memory with { Choco100 = shared } };
            current = AppendCommon(current, loaded.Packets);
            if (result.SoundDescriptor is uint sound)
            {
                current = Execute(current, new NativeBattleChoco97Sound(sound, 0, 128));
            }
            return new(current, loaded.Task, loaded.ReadyToComplete);
        }

        NativeBattleChoco100NodesStep AdvanceNodes(NativeBattleChoco100Frame current, uint address)
        {
            NativeBattleChoco100State shared = Shared(current);
            NativeBattleChoco97NodeState nodes = shared.Nodes with { Work = shared.Nodes.Work with { PrivateDrawCursor = current.Effect.DrawArenaOffset ?? throw new NotSupportedException("Native Choco requires its current private cursor.") } };
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Choco requires its initialized ordering table.");
            NativeBattleChoco100NodeStep advanced = NativeBattleChoco100Nodes.AdvanceContext(new(current.Memory, current.Effect, nodes, shared.Io, current.Random), address, actor => graphics.Models[actor], projection, NativeCamera!.Roll, primitive.Pending, colors);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = advanced.Ordering } };
            NativeBattleEffectMemory memory = advanced.Frame.Memory with { Choco100 = advanced.Frame.Memory.Choco100! with { Nodes = advanced.Frame.State, Io = advanced.Frame.Io } };
            return new(new(memory, advanced.Frame.Effect with { DrawArenaOffset = advanced.Frame.State.Work.PrivateDrawCursor }, advanced.Frame.Random), advanced.Complete);
        }

        NativeBattleChoco100TaskStep AdvanceAux(NativeBattleChoco100Frame current, uint address, NativeBattleModulePoolTask task)
        {
            if (task.Callback == 0x715AB0)
            {
                NativeBattleChoco97SpriteStep sprite = NativeBattleChoco100Sprite.Advance715AB0(task, resources.Sprite, (Shared(current).Context ?? throw new NotSupportedException("Native Choco sprite requires its current context.")).Data.Slice(0, 8), view, projection, NativeCamera!.Roll);
                if (sprite.Packets is { } batch)
                {
                    AppendModule(batch);
                }
                return new(current, sprite.Task, sprite.ReadyToComplete);
            }
            NativeBattleChoco97StageFadeStep fade = NativeBattleChoco100Aux.AdvanceStage(task);
            if (fade.Depth.HasValue || fade.ClearRgb)
            {
                current = Execute(current, new NativeBattleChoco97StageDepth(fade.Depth, fade.ClearRgb));
            }
            return new(current, fade.Task, fade.ReadyToComplete);

            void AppendModule(NativeBattleModulePacketBatch batch)
            {
                NativeEffectMemory = current.Memory;
                graphics = AppendNativeModulePacketBatch(graphics, batch, null, null);
                current = current with { Memory = NativeEffectMemory! };
            }

        }

        NativeBattleChoco100TaskStep AdvanceSecondary(NativeBattleChoco100Frame current, uint address, NativeBattleModulePoolTask task)
        {
            int actorIndex = task.ReadByte(0x2D);
            if ((uint)actorIndex >= graphics.Models.Length)
            {
                throw new NotSupportedException("Native Choco secondary requires its actual selected actor.");
            }
            NativeBattleChoco100State shared = Shared(current);
            NativeBattleChoco100IoState io = shared.Io ?? throw new NotSupportedException("Native Choco secondary requires its actual TIM work allocation.");
            uint work = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(io.Control.AsSpan().Slice(0x100));
            NativeBattleChoco100SecondaryStep changed = NativeBattleChoco100Secondary.Advance716C20(new(task, graphics.Models[actorIndex], shared.SecondarySettings), work, DrawActor);
            graphics = graphics with { Models = graphics.Models.SetItem(actorIndex, changed.Frame.Actor) };
            current = current with { Memory = current.Memory with { Choco100 = Shared(current) with { SecondarySettings = changed.Frame.Settings } } };
            return new(current, changed.Frame.Task, changed.ReadyToComplete);

            NativeBattleChoco100SecondaryFrame DrawActor(NativeBattleChoco100SecondaryFrame secondary)
            {
                NativeBattleGraphicsModel actor = secondary.Actor;
                graphics = graphics with { Models = graphics.Models.SetItem(actorIndex, actor) };
                NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, actorIndex);
                NativeBattlePoseSource source = resource.PoseSource ?? throw new NotSupportedException("Native Choco actor drawing requires its actual skeleton.");
                NativeBattleSequenceState sequence = actor.SequenceState ?? throw new NotSupportedException("Native Choco actor drawing requires its current body pose.");
                NativeBattleDecodedPose pose = source.Sample(sequence.Pose ?? throw new NotSupportedException("Native Choco actor drawing requires its selected pose."), sequence.PoseFrame114 ?? throw new NotSupportedException("Native Choco actor drawing requires its current pose frame."), sequence.PoseFlags109);
                ImmutableArray<NativeBattleActionMatrix> bones = NativeBattlePoseMatrices.StageBoneMatrices(source, pose);
                NativeBattleActionMatrix world = actor.GeometryWorldMatrix ?? throw new NotSupportedException("Native Choco actor drawing requires its already published world matrix.");
                (int x, int y, int z) = NativeBattleGfMatrices.RotateLong56C600(view, world.X, world.Y, world.Z);
                NativeBattleActionMatrix camera = NativeBattlePoseMatrices.MultiplyGteRotation45E470(view, world) with { X = unchecked(view.X + x), Y = unchecked(view.Y + y), Z = unchecked(view.Z + z) };
                NativeBattleModulePoolTask drawnTask = secondary.Task;
                PublishMatrix(0x6C, world, true);
                PublishMatrix(0x8C, camera, false);
                NativeEffectMemory = current.Memory;
                graphics = SubmitNativeShadowPackets5088A0(graphics, actor, projection with { View = view }, 4112, false);
                current = current with { Memory = NativeEffectMemory ?? throw new NotSupportedException("Native Choco shadow lost its retained packet memory.") };
                NativeBattleChoco100SecondarySettings settings = secondary.Settings ?? throw new NotSupportedException("Native Choco actor drawing requires its executed shared settings.");
                int offset = checked((int)(settings.WorkAddress() - NativeBattleEffectNodeMemory.ArenaAddress));
                NativeBattleChoco100Wave wave = new(unchecked((short)drawnTask.ReadWord(0xC2)), drawnTask.ReadWord(0xC4), drawnTask.ReadWord(0xC6), drawnTask.ReadWord(0xC8), drawnTask.ReadWord(0xC0));
                foreach (NativeBattleCureActorGeometry geometry in NativeBattleCureActorAssembly.SelectGeometry8D7110(actor, resource, sequence.MeshMask124))
                {
                    NativeBattleChoco100ActorDraw drawn = NativeBattleChoco100ActorPackets.Emit716DD0(geometry.Mesh, bones, camera, projection, actor.Color40 ?? throw new NotSupportedException("Native Choco actor drawing requires its actual body color."), wave, current.Memory.Arena ?? throw new NotSupportedException("Native Choco actor drawing requires its current TIM work arena."), offset, geometry.Binding);
                    current = current with { Memory = current.Memory with { Arena = drawn.Arena } };
                    current = AppendCommon(current, drawn.Packets);
                }
                return secondary with { Task = drawnTask };

                void PublishMatrix(int start, NativeBattleActionMatrix matrix, bool canonicalUnconsumedPadding)
                {
                    for (int index = 0; index < 9; index++)
                    {
                        drawnTask = drawnTask.WriteWord(start + index * 2, unchecked((ushort)matrix.Rotation[index]));
                    }
                    if (canonicalUnconsumedPadding)
                    {
                        //The copied source padding is unconsumed; this canonical snapshot WORD makes no native byte claim.
                        drawnTask = drawnTask.WriteWord(start + 0x12, 0);
                    }
                    drawnTask = drawnTask.WriteDword(start + 0x14, unchecked((uint)matrix.X)).WriteDword(start + 0x18, unchecked((uint)matrix.Y)).WriteDword(start + 0x1C, unchecked((uint)matrix.Z));
                }
            }
        }

        NativeBattleChoco100TaskStep AdvanceSmall(NativeBattleChoco100Frame current, uint address, NativeBattleModulePoolTask task)
        {
            NativeBattleChoco99CameraYStep step = NativeBattleChoco100Aux.Advance716300(task, resources.CameraY);
            if (step.Y is short y)
            {
                NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native Choco view translation requires its current camera.");
                NativeCamera = camera with { ViewTranslationScratch = (camera.ViewTranslationScratch ?? throw new NotSupportedException("Native Choco view translation scratch is unknown.")) with { Y = y } };
            }
            return new(current, step.Task, step.ReadyToComplete);
        }

        NativeBattleChoco100VibrationStep StartVibration(NativeBattleChoco100Frame current)
        {
            NativeVibrationEnqueue queued = (NativeVibrationProcess ?? throw new NotSupportedException("Native Choco requires its vibration process.")).Enqueue(resources.Vibration.Bank, 1, 128);
            NativeVibrationProcess = queued.State;
            return new(current, unchecked((short)queued.Handle));
        }

        NativeBattleChoco100Frame Execute(NativeBattleChoco100Frame current, NativeBattleChoco97Operation operation)
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
                        0x15297F4 => resources.SoundInitial,
                        0x15297F8 => resources.SoundModel,
                        0x15297FC => resources.SoundDirector,
                        0x1529800 => resources.SoundCamera,
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
                case NativeBattleChoco100FileRequest request:
                    NativeBattleChoco100State shared = Shared(current);
                    NativeBattleChoco100IoQueued queued = NativeBattleChoco100Io.CopyAndQueueFile(shared.Io ?? throw new NotSupportedException("Native Choco requires its current IO script."), current.Memory, NativeFileIo ?? throw new NotSupportedException("Native Choco requires its actual file completion scheduler."), resources.Io.Files.Single(file => file.FileId == request.Request.FileId));
                    NativeFileIo = queued.FileIo;
                    return current with { Memory = queued.Memory with { Choco100 = Shared(new(queued.Memory, current.Effect, current.Random)) with { Io = queued.State } } };
                case NativeBattleChoco97CommonTextureLoad load:
                    (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)load.DestinationArenaOffset), load.CompletionAddress, (address, value) => current = current with { Effect = NativeBattleChoco100Controller.WriteCompletionByte(current.Effect, address, value) });
                    graphics = graphics with { Auxiliary = auxiliary };
                    return current;
                default:
                    throw new NotSupportedException("Native Choco operation requires its original callback.");
            }
        }

        NativeBattleChoco100Frame AppendCommon(NativeBattleChoco100Frame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Choco requires its initialized ordering table.");
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Choco requires its common packet owner."), primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = current.Memory with { PrimitiveProcess = written.Process } };
        }


        static NativeBattleGfAnimationSource ReadProgram(NativeBattleChoco100Frame current, uint address, int length)
        {
            NativeBattleChoco100IoState io = Shared(current).Io ?? throw new NotSupportedException("Native Choco requires its current program IO owner.");
            io.Banks.Validate();
            long offset = (long)address - io.Banks.BaseAddress;
            if (length < 0 || offset < 0 || offset > io.Banks.Length - length)
            {
                throw new NotSupportedException("Native Choco program leaves its current IO bank.");
            }
            return NativeBattleGfAnimationSource.Borrow(address, io.Banks.Bytes, checked((int)offset), length);
        }

        static NativeBattleChoco100State Shared(NativeBattleChoco100Frame current) => current.Memory.Choco100 ?? throw new NotSupportedException("Native Choco lost its retained process owners.");
    }

    private bool TryWriteNativeChoco100Completion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState updated)
    {
        updated = graphics;
        uint delta = unchecked(address - NativeBattleChoco100Controller.MainBank);
        if (delta >= 0xC8 || delta % 0x64 != 0x62)
        {
            return false;
        }
        if (NativeEffectMemory?.DetachedReturnedTask is { Binding.Entry: 0x70D390 } detached)
        {
            NativeEffectMemory = NativeEffectMemory with { DetachedReturnedTask = NativeBattleChoco100Controller.WriteCompletionByte(detached, address, value) };
        }
        else
        {
            NativeBattleOriginalEffectState effect = graphics.OriginalEffect is { Binding.Entry: 0x70D390 } current ? current : throw new NotSupportedException("Native Choco completion lost its physical root task.");
            updated = graphics with { OriginalEffect = NativeBattleChoco100Controller.WriteCompletionByte(effect, address, value) };
        }
        return true;
    }
}
