using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeChoco717D30(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        NativeBattleChoco99Frame frame = NativeBattleChoco99Controller.Initialize717D30(NativeEffectMemory ?? throw new NotSupportedException("Native Choco requires retained effect memory."), effect, nativeActionPackets, effect.Binding.Choco99Resources ?? throw new NotSupportedException("Native Choco requires its original resources."), nativeCrtRandom ?? throw new NotSupportedException("Native Choco requires retained CRT random state."));
        NativeEffectMemory = frame.Memory;
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };
    }

    private NativeBattleGraphicsState AdvanceNativeChoco717ED0(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect! with { PresentationPackets = [] };
        NativeBattleChoco99Resources resources = effect.Binding.Choco99Resources ?? throw new NotSupportedException("Native Choco requires its original resources.");
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Choco requires its published projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Choco requires its retained view matrix.");
        NativeBattleGraphicsDefinition definition = this.definition.NativeGraphics ?? throw new NotSupportedException("Native Choco requires original graphics rules.");
        NativeBattleSummonColors colors = definition.Rules.SummonColors ?? throw new NotSupportedException("Native Choco requires its depth-cue constants.");
        NativeBattleChoco99Frame frame = new(NativeEffectMemory ?? throw new NotSupportedException("Native Choco requires retained effect memory."), effect, nativeCrtRandom ?? throw new NotSupportedException("Native Choco requires retained CRT random state."));
        frame = NativeBattleChoco99Controller.Advance717ED0(frame, new(nativeActionPackets, view, actor => graphics.Models[actor], actor => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Native Choco requires the selected actor pose source."), DrawModel, AdvanceProgram, AdvanceNodes, AdvanceAux, AdvanceSecondary, AdvanceOverlay, StartVibration, Execute), resources);
        NativeEffectMemory = frame.Memory;
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleChoco99ModelDrawStep DrawModel(NativeBattleChoco99Frame current, NativeBattleModulePoolTask task, NativeBattleEffectOwnedModelState model)
        {
            NativeBattleChoco99State shared = Shared(current);
            int size = checked(resources.Model.Model.Geometry[0].Vertices.Length * 8);
            if (shared.ProjectedScratch is null && (task.ReadDword(0xAC) & 1) == 0)
            {
                throw new NotSupportedException("Native Choco cannot read an uninitialized projected scratch owner.");
            }
            NativeBattleEffectOwnedModelDraw drawn = NativeBattleEffectOwnedModelDrawing.Draw7558A0(task, model, resources.Model.Model, view, projection, definition.Rules.ShadowSource ?? throw new NotSupportedException("Native Choco requires its original shadow source."), definition.PrimitiveCapabilities ?? throw new NotSupportedException("Native Choco requires its device capabilities."), colors, shared.ProjectedScratch ?? new byte[size].ToImmutableArray(), pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(99, task) with { ResourceGeneration = model.ResourceAddress });
            if (shared.ProjectedScratch is null && drawn.ProjectedBytesWritten != size)
            {
                throw new InvalidDataException("Native Choco's first body draw did not establish every projected record.");
            }
            current = current with { Memory = current.Memory with { Choco99 = shared with { ProjectedScratch = drawn.ProjectedScratch } } };
            return new(AppendCommon(current, drawn.Packets), drawn);
        }

        NativeBattleChoco99ProgramStep AdvanceProgram(NativeBattleChoco99Frame current, uint address, NativeBattleModulePoolTask task)
        {
            NativeBattleChoco99State shared = Shared(current);
            uint frozen = shared.FreezeSecondary ?? throw new NotSupportedException("Native Choco requires its retained program pause operand.");
            NativeBattleChoco97Context context = shared.Context ?? throw new NotSupportedException("Native Choco requires its current context origins.");
            NativeBattleChoco99ProgramDraw result;
            bool initial = task.ReadDword(0x74) == resources.ProgramSource.Address;
            if (initial)
            {
                result = NativeBattleChoco99ProgramDrawing.AdvanceInitial(task, shared.ProgramSource ?? throw new NotSupportedException("Native Choco requires its retained static program source."), context.Data.Slice(0, 8), shared.MorphScratch, view, projection, colors, frozen, pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(99, task));
            }
            else if (task.Callback is 0x71FFB0 or 0x720610)
            {
                NativeBattleGfAnimationSource source = ReadProgram(current, 0x19F0F40, 0x1638);
                result = task.Callback == 0x71FFB0 ? NativeBattleChoco99ProgramDrawing.AdvanceMoving71FFB0(task, source, unchecked((short)context.Word(0x40)), shared.MorphScratch, view, projection, colors, frozen, EmitParticles, pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(99, task)) : NativeBattleChoco99ProgramDrawing.AdvanceSmall720610(task, source, unchecked((short)context.Word(0x40)), shared.MorphScratch, view, projection, colors, frozen, EmitParticles, pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(99, task));
            }
            else
            {
                NativeBattleChoco99LoadedProgram profile = task.Callback switch
                {
                    0x71F950 => NativeBattleChoco99LoadedProgram.File471,
                    0x71FDC0 => NativeBattleChoco99LoadedProgram.File474,
                    0x71FEC0 => NativeBattleChoco99LoadedProgram.File475,
                    0x7201F0 => NativeBattleChoco99LoadedProgram.File482,
                    0x720320 => NativeBattleChoco99LoadedProgram.File483,
                    0x720410 => NativeBattleChoco99LoadedProgram.File484,
                    _ => throw new NotSupportedException("Native Choco program requires its actual loaded-file callback.")
                };
                var selected = NativeBattleChoco99ProgramDrawing.LoadedProfile(profile);
                NativeBattleGfAnimationSource source = ReadProgram(current, selected.Address, selected.Length);
                result = profile is NativeBattleChoco99LoadedProgram.File471 or NativeBattleChoco99LoadedProgram.File474 ? NativeBattleChoco99ProgramDrawing.AdvanceLoaded(profile, task, source, context.Data.Slice(0, 8), shared.MorphScratch, view, projection, colors, frozen, pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(99, task)) : NativeBattleChoco99ProgramDrawing.AdvanceHeldOrStatic(profile, task, source, unchecked((short)context.Word(0x40)), shared.MorphScratch, view, projection, colors, frozen, pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(99, task));
            }
            NativeBattleChoco97LoadedProgramDraw loaded = result.Draw;
            shared = Shared(current);
            if (initial)
            {
                shared = shared with { ProgramSource = loaded.Source, MorphScratch = loaded.MorphScratch };
            }
            else
            {
                NativeBattleChoco99IoState io = shared.Io ?? throw new NotSupportedException("Native Choco requires its loaded program IO owner.");
                NativeBattleOwnedGfAnimationSource.Own(loaded.Source).RequireStorage(io.Banks.Bytes, checked((int)(loaded.Source.Address - io.Banks.BaseAddress)));
                shared = shared with { MorphScratch = loaded.MorphScratch };
            }
            current = current with { Memory = current.Memory with { Choco99 = shared } };
            current = AppendCommon(current, loaded.Packets);
            if (result.SoundDescriptor is uint sound)
            {
                current = Execute(current, new NativeBattleChoco97Sound(sound, 0, 128));
            }
            return new(current, loaded.Task, loaded.ReadyToComplete);

            NativeBattleModulePoolTask EmitParticles(NativeBattleModulePoolTask parent, NativeBattleChoco99EmissionKind kind, short mode)
            {
                NativeBattleEffectHierarchy hierarchy = new(current.Effect.Pools);
                hierarchy = hierarchy.Write(address, parent);
                NativeBattleChoco99ParticleEmission emitted = kind == NativeBattleChoco99EmissionKind.Trail ? NativeBattleChoco99Particles.EmitTrail7209B0(hierarchy, address, unchecked((ushort)mode), current.Random, SelectActors) : NativeBattleChoco99Particles.EmitRadial720D70(hierarchy, address, unchecked((ushort)mode), current.Random, SelectActors);
                current = current with { Effect = current.Effect with { Pools = emitted.Hierarchy.Pools }, Random = emitted.Random };
                return emitted.Hierarchy.Read(address);
            }
        }

        NativeBattleChoco99NodesStep AdvanceNodes(NativeBattleChoco99Frame current, uint address)
        {
            NativeBattleChoco99State shared = Shared(current);
            NativeBattleChoco97NodeState nodes = shared.Nodes with { Work = shared.Nodes.Work with { PrivateDrawCursor = current.Effect.DrawArenaOffset ?? throw new NotSupportedException("Native Choco requires its current private cursor.") } };
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Choco requires its initialized ordering table.");
            NativeBattleChoco99NodeStep advanced = NativeBattleChoco99Nodes.AdvanceContext(new(current.Memory, current.Effect, nodes, shared.Io, current.Random), address, actor => graphics.Models[actor], projection, NativeCamera!.Roll, primitive.Pending, colors);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = advanced.Ordering } };
            NativeBattleEffectMemory memory = advanced.Frame.Memory with { Choco99 = advanced.Frame.Memory.Choco99! with { Nodes = advanced.Frame.State, Io = advanced.Frame.Io } };
            return new(new(memory, advanced.Frame.Effect with { DrawArenaOffset = advanced.Frame.State.Work.PrivateDrawCursor }, advanced.Frame.Random), advanced.Complete);
        }

        NativeBattleChoco99TaskStep AdvanceAux(NativeBattleChoco99Frame current, uint address, NativeBattleModulePoolTask task)
        {
            switch (task.Callback)
            {
                case 0x71EE40:
                    NativeBattleChoco97SpriteStep sprite = NativeBattleChoco99Sprite.Advance71EE40(task, resources.Sprite, Context().Data.Slice(0, 8), view, projection, NativeCamera!.Roll, NativeBattlePilotVisualLifetimes.Identity(99, task));
                    if (sprite.Packets is { } spritePackets)
                    {
                        AppendModule(spritePackets);
                    }
                    return new(current, sprite.Task, sprite.ReadyToComplete);
                case NativeBattleChoco99Particles.TrailCallback:
                case NativeBattleChoco99Particles.RadialCallback:
                    NativeBattleChoco99ParticleStep particles = NativeBattleChoco99Particles.Advance(task, resources.Particles, AtLeast, view, projection, NativeCamera!.Roll, pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(99, task));
                    foreach (NativeBattleModulePacketBatch packets in particles.Packets)
                    {
                        AppendModule(packets);
                    }
                    return new(current, particles.Task, particles.ReadyToComplete);
                case 0x720500:
                    NativeBattleChoco99IoState io = Shared(current).Io ?? throw new NotSupportedException("Native Choco meteo requires its actual IO source directory.");
                    uint source = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(io.Control.AsSpan().Slice(0x134));
                    NativeBattleChoco97SpriteStep meteo = NativeBattleChoco99Meteo.Advance720500(task, resources.Meteo, source, AppendProgram, view, projection, NativeCamera!.Roll, pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(99, task));
                    if (meteo.Packets is { } meteoPackets)
                    {
                        AppendModule(meteoPackets);
                    }
                    return new(current, meteo.Task, meteo.ReadyToComplete);
                case 0x720790:
                    NativeBattleChoco99MeteoEmitterStep emitter = NativeBattleChoco99Meteo.Advance720790(new NativeBattleEffectHierarchy(current.Effect.Pools).Write(address, task), address, resources.Meteo, AtLeast, current.Random, SelectActors);
                    current = current with { Effect = current.Effect with { Pools = emitter.Hierarchy.Pools }, Random = emitter.Random };
                    return new(current, emitter.Task, emitter.ReadyToComplete);
                case 0x7208D0:
                    NativeBattleChoco97SpriteStep secondarySprite = NativeBattleChoco99Meteo.Advance7208D0(task, resources.Meteo, AtLeast, view, projection, NativeCamera!.Roll, pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(99, task));
                    if (secondarySprite.Packets is { } secondaryPackets)
                    {
                        AppendModule(secondaryPackets);
                    }
                    return new(current, secondarySprite.Task, secondarySprite.ReadyToComplete);
                case 0x71F310:
                case 0x721010:
                case 0x721100:
                case 0x721460:
                case 0x7215A0:
                    NativeBattleChoco99State shared = Shared(current);
                    NativeBattleChoco99EnvironmentFrame environment = new(task, Context(), NativeStage ?? throw new NotSupportedException("Native Choco environment requires its actual stage rows."), graphics.Models, shared.FreezePrimary ?? throw new NotSupportedException("Native Choco primary freeze is unknown."), shared.FreezeSecondary ?? throw new NotSupportedException("Native Choco secondary freeze is unknown."));
                    NativeBattleChoco99EnvironmentStep changed = NativeBattleChoco99Environment.Advance(environment, (updated, sound) =>
                    {
                        PublishEnvironment(updated);
                        current = Execute(current, new NativeBattleChoco97Sound(sound.Descriptor, sound.Flags, sound.Pan));
                        return updated;
                    });
                    PublishEnvironment(changed.Frame);
                    return new(current, changed.Frame.Task, changed.ReadyToComplete);
                default:
                    throw new NotSupportedException("Native Choco auxiliary requires its actual callback.");
            }

            NativeBattleChoco97Context Context() => Shared(current).Context ?? throw new NotSupportedException("Native Choco auxiliary requires its actual context.");
            bool AtLeast(short mode) => NativeBattleChoco97CameraTask.ModeAtLeast730E80(Context().Word(0x40), mode);

            void AppendModule(NativeBattleModulePacketBatch batch)
            {
                NativeBattlePrimitiveProcess process = current.Memory.PrimitiveProcess!;
                current = current with { Effect = current.Effect with { PresentationPackets = current.Effect.PresentationPackets.AddRange(batch.Primitives.Select(item => new NativeBattlePrimitiveReference(process.PacketBank!, process.PacketBuffer, checked(process.ByteCursor!.Value + item.ByteOffset)))) } };
                NativeEffectMemory = current.Memory;
                graphics = AppendNativeModulePacketBatch(graphics, batch, null, null);
                current = current with { Memory = NativeEffectMemory! };
            }

            NativeBattleModulePoolTask AppendProgram(NativeBattleModulePoolTask parent, NativeBattleChoco97AppendProgram operation)
            {
                current = current with { Effect = current.Effect with { Pools = new NativeBattleEffectHierarchy(current.Effect.Pools).Write(address, parent).Pools } };
                current = NativeBattleChoco99Controller.AppendProgram(current, address, operation, nativeActionPackets);
                return new NativeBattleEffectHierarchy(current.Effect.Pools).Read(address);
            }

            void PublishEnvironment(NativeBattleChoco99EnvironmentFrame updated)
            {
                NativeStage = updated.Stage;
                graphics = graphics with { Models = updated.Models };
                current = current with { Memory = current.Memory with { Choco99 = Shared(current) with { Context = updated.Context, FreezePrimary = updated.FreezePrimary, FreezeSecondary = updated.FreezeSecondary } } };
            }
        }

        NativeBattleChoco99TaskStep AdvanceSecondary(NativeBattleChoco99Frame current, uint address, NativeBattleModulePoolTask task)
        {
            NativeBattleStaticMeshResource original = task.Callback == 0x7211D0 ? resources.Secondary.File480 : task.Callback == 0x721390 ? resources.Secondary.File481 : throw new NotSupportedException("Native Choco secondary requires its actual physical callback.");
            NativeBattleChoco99IoState io = Shared(current).Io ?? throw new NotSupportedException("Native Choco secondary requires its actual current mesh bytes.");
            NativeBattleStaticMeshResource mesh = original with { Bytes = io.Banks.Read(original.Address, original.Bytes.Length).ToImmutableArray() };
            short mode = unchecked((short)(Shared(current).Context ?? throw new NotSupportedException("Native Choco secondary requires its actual context.")).Word(0x40));
            NativeBattleChoco99SecondaryStep drawn = task.Callback == 0x7211D0 ? NativeBattleChoco99Secondary.Advance7211D0(task, mesh, mode, view, projection, colors, pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(99, task)) : NativeBattleChoco99Secondary.Advance721390(task, mesh, view, projection, colors, pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(99, task));
            return new(AppendCommon(current, drawn.Packets), drawn.Task, drawn.ReadyToComplete);
        }

        NativeBattleChoco99OverlayStep AdvanceOverlay(NativeBattleChoco99Frame current, uint address)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Choco overlay requires its current ordering table.");
            NativeBattleFullscreenOverlayStep changed = NativeBattleFullscreenOverlay.Advance8DDC30(new(current.Memory, current.Effect.Pools, primitive.Pending), address, current.Memory.FullscreenOverlayPause2795914 ?? throw new NotSupportedException("Native Choco overlay pause owner is unknown."), resources.Meteo.Overlay, NativeBattlePilotVisualLifetimes.Identity(99, new NativeBattleEffectHierarchy(current.Effect.Pools).Read(address)) with { ResourceGeneration = resources.Meteo.Overlay.Address });
            if (!ReferenceEquals(changed.Frame.Ordering, primitive.Pending))
            {
                current = current with { Effect = current.Effect with { PresentationPackets = current.Effect.PresentationPackets.AddRange(new[] { 0, 24, 48, 72, 96 }.Select(offset => new NativeBattlePrimitiveReference(NativeBattleFullscreenOverlay.PacketBank, 0, offset))) } };
            }
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = changed.Frame.Ordering } };
            return new(current with { Memory = changed.Frame.Memory, Effect = current.Effect with { Pools = changed.Frame.Pools } }, changed.Result);
        }

        NativeBattleChoco99VibrationStep StartVibration(NativeBattleChoco99Frame current)
        {
            NativeVibrationEnqueue queued = (NativeVibrationProcess ?? throw new NotSupportedException("Native Choco requires its vibration process.")).Enqueue(resources.Vibration.Bank, 1, 128);
            NativeVibrationProcess = queued.State;
            return new(current, unchecked((short)queued.Handle));
        }

        NativeBattleChoco99Frame Execute(NativeBattleChoco99Frame current, NativeBattleChoco97Operation operation)
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
                        0x1529FA4 => resources.SoundInitial,
                        0x1529FA8 => resources.SoundModel,
                        0x1529FAC => resources.SoundHide,
                        0x1529FB0 => resources.SoundRestore,
                        _ => throw new NotSupportedException("Native Choco sound requires its original descriptor address.")
                    };
                    PublishNativeModuleSound(new(descriptor, sound.Flags, null) { Pan = sound.Pan });
                    return current;
                case NativeBattleChoco97CameraPublish camera:
                    NativeCamera = NativeBattleMoogleCamera.Publish733150(camera.Step, NativeCamera ?? throw new NotSupportedException("Native Choco requires its current camera globals."));
                    return current;
                case NativeBattleChoco99ViewTranslationY translation:
                    NativeBattleCameraState cameraState = NativeCamera ?? throw new NotSupportedException("Native Choco camera translation requires its current owner.");
                    NativeCamera = cameraState with { ViewTranslationScratch = (cameraState.ViewTranslationScratch ?? throw new NotSupportedException("Native Choco camera translation scratch is unknown.")) with { Y = translation.Y } };
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
                case NativeBattleChoco99FileRequest request:
                    NativeBattleChoco99State shared = Shared(current);
                    NativeBattleChoco99IoQueued queued = NativeBattleChoco99Io.CopyAndQueueFile(shared.Io ?? throw new NotSupportedException("Native Choco requires its current IO script."), current.Memory, NativeFileIo ?? throw new NotSupportedException("Native Choco requires its actual file completion scheduler."), resources.Io.Files.Single(file => file.FileId == request.Request.FileId));
                    NativeFileIo = queued.FileIo;
                    return current with { Memory = queued.Memory with { Choco99 = Shared(new(queued.Memory, current.Effect, current.Random)) with { Io = queued.State } } };
                case NativeBattleChoco97CommonTextureLoad load:
                    (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)load.DestinationArenaOffset), load.CompletionAddress, (address, value) => current = current with { Effect = NativeBattleChoco99Controller.WriteCompletionByte(current.Effect, address, value) });
                    graphics = graphics with { Auxiliary = auxiliary };
                    return current;
                default:
                    throw new NotSupportedException("Native Choco operation requires its original callback.");
            }
        }

        NativeBattleChoco99Frame AppendCommon(NativeBattleChoco99Frame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Choco requires its initialized ordering table.");
            NativeBattlePrimitiveProcess process = current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Choco requires its common packet owner.");
            current = current with { Effect = current.Effect with { PresentationPackets = NativeBattleEffectPresentationPackets.Record(current.Effect.PresentationPackets, process.PacketBank!, process.PacketBuffer, process.ByteCursor!.Value, batch) } };
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(process, primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = current.Memory with { PrimitiveProcess = written.Process } };
        }

        NativeBattleEffectTaskActors SelectActors(uint parameters, sbyte header, sbyte target) => NativeBattleCureController.SelectActors(effect.Parameters ?? throw new NotSupportedException("Native Choco actor selection requires its original action parameters."), nativeActionPackets, parameters, header, target);

        static NativeBattleGfAnimationSource ReadProgram(NativeBattleChoco99Frame current, uint address, int length)
        {
            NativeBattleChoco99IoState io = Shared(current).Io ?? throw new NotSupportedException("Native Choco requires its current program IO owner.");
            io.Banks.Validate();
            long offset = (long)address - io.Banks.BaseAddress;
            if (length < 0 || offset < 0 || offset > io.Banks.Length - length)
            {
                throw new NotSupportedException("Native Choco program leaves its current IO bank.");
            }
            return NativeBattleGfAnimationSource.Borrow(address, io.Banks.Bytes, checked((int)offset), length);
        }

        static NativeBattleChoco99State Shared(NativeBattleChoco99Frame current) => current.Memory.Choco99 ?? throw new NotSupportedException("Native Choco lost its retained process owners.");
    }

    private bool TryWriteNativeChoco99Completion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState updated)
    {
        updated = graphics;
        uint delta = unchecked(address - NativeBattleChoco99Controller.MainBank);
        if (delta >= 0xC8 || delta % 0x64 != 0x62)
        {
            return false;
        }
        if (NativeEffectMemory?.DetachedReturnedTask is { Binding.Entry: 0x717D30 } detached)
        {
            NativeEffectMemory = NativeEffectMemory with { DetachedReturnedTask = NativeBattleChoco99Controller.WriteCompletionByte(detached, address, value) };
        }
        else
        {
            NativeBattleOriginalEffectState effect = graphics.OriginalEffect is { Binding.Entry: 0x717D30 } current ? current : throw new NotSupportedException("Native Choco completion lost its physical root task.");
            updated = graphics with { OriginalEffect = NativeBattleChoco99Controller.WriteCompletionByte(effect, address, value) };
        }
        return true;
    }
}
