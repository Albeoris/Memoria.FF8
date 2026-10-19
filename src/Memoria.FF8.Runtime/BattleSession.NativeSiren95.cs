using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeSiren95(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native Siren requires its selected effect.");
        NativeBattleSiren95Frame frame = new(NativeEffectMemory ?? throw new NotSupportedException("Native Siren requires its retained effect memory."), effect, nativeCrtRandom ?? throw new NotSupportedException("Native Siren requires its retained CRT random state."));
        if (initialize)
        {
            frame = NativeBattleSiren95Controller.Initialize739D80(frame);
            frame = NativeBattleSiren95Controller.Initialize739DA0(frame, nativeActionPackets);
        }
        else
        {
            NativeBattleSiren95Resources resources = effect.Binding.Siren95Resources ?? throw new NotSupportedException("Native Siren requires its original programme and IO resources.");
            resources.Validate();
            NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native Siren requires its current camera owner.");
            NativeBattleSiren95DirectorServices director = new(nativeActionPackets, StartVibration, StopVibration, SpawnStage, PlaySound, actor => graphics.Models[actor], WriteModel);
            frame = NativeBattleSiren95Controller.Advance739F40(frame, new(nativeActionPackets, camera.CapturePackedView1D97778(), actor => graphics.Models[actor], ReadPoseSource, InitializeTarget, AdvanceLeaf, ApplyContact, Execute));

            NativeBattlePoseSource ReadPoseSource(int actor) => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Native Siren requires its selected actor pose source.");

            NativeBattleSiren95Frame InitializeTarget(NativeBattleSiren95Frame current, uint address)
            {
                current = NativeBattleSiren95Director.Initialize73A6E0(current, actor => graphics.Models[actor]);
                return NativeBattleSiren95Io.Initialize73A270(current, resources.IoScript);
            }

            NativeBattleSiren95TaskStep AdvanceLeaf(NativeBattleSiren95Frame current, uint address)
            {
                NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(current.Effect.Pools).Read(address);
                switch (task.Callback)
                {
                    case 0x745250:
                    case 0x7452F0:
                        return NativeBattleSiren95Waves.Advance(current, address, new(nativeActionPackets, DrawWave));
                    case 0x745AD0:
                        return NativeBattleSiren95Screens.Advance745AD0(current, address, new(DrawScreen));
                    case 0x744F90:
                        return NativeBattleSiren95ScheduledNodes.Advance744F90(current, address, new(nativeActionPackets, (active, pointer, length) =>
                        {
                            NativeBattleEffectHeapAllocation schedule = resources.NodeSchedule ?? throw new NotSupportedException("Native Siren schedule requires its original records; migrate the named binding explicitly.");
                            int offset = checked((int)((long)pointer - 0x15339D8));
                            if (offset < 0 || length < 0 || offset > schedule.Data.Length - length)
                            {
                                throw new InvalidDataException("Native Siren schedule read leaves its supplied current records.");
                            }
                            return schedule.Data.AsSpan().Slice(offset, length).ToArray();
                        }));
                    case 0x745180:
                        NativeBattlePrimitiveFrame nodePrimitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Siren nodes require their current ordering table.");
                        NativeBattleCameraState nodeCamera = NativeCamera ?? throw new NotSupportedException("Native Siren nodes require their current camera.");
                        NativeBattleSiren95NodeStep nodes = NativeBattleSiren95Nodes.Advance745180(current, address, nodePrimitive.Pending, new(nativeActionPackets, actor => graphics.Models[actor], ReadPoseSource, nodeCamera.Projection ?? throw new NotSupportedException("Native Siren nodes require their actual projection."), nodeCamera.Roll, definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native Siren nodes require their color constants."), resources.StaticNodeSource));
                        graphics = graphics with { PrimitiveFrame = nodePrimitive with { Pending = nodes.Ordering } };
                        return new(nodes.Frame, nodes.Complete);
                    case 0x8DDC30:
                        NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Siren overlay requires its current ordering table.");
                        NativeBattleFullscreenOverlayStep overlay = NativeBattleFullscreenOverlay.Advance8DDC30(new(current.Memory, current.Effect.Pools, primitive.Pending), address, current.Memory.FullscreenOverlayPause2795914 ?? throw new NotSupportedException("Native Siren overlay pause owner is unknown."), resources.OverlayProgram ?? throw new NotSupportedException("Native Siren overlay requires its original programme; migrate the named resource binding explicitly."));
                        graphics = graphics with { PrimitiveFrame = primitive with { Pending = overlay.Frame.Ordering } };
                        return new(current with { Memory = overlay.Frame.Memory, Effect = current.Effect with { Pools = overlay.Frame.Pools } }, overlay.Result == 2);
                    case 0x746B60:
                        NativeBattleEffectOwnedModelState model = (current.Memory.Siren95 ?? throw new NotSupportedException("Native Siren model requires its raw owner.")).ModelState ?? throw new NotSupportedException("Native Siren model requires its actual constructor state.");
                        return NativeBattleSiren95Model.Advance746B60(current, address, NativeBattleSiren95Model.ReadCurrentResource(current.Memory, model.ResourceAddress), new(DrawModel, PlaySound, PlayModelStream, EmitModelParticles));
                    case 0x73EF60:
                    case 0x73F110:
                    case 0x73F6A0:
                    case 0x73F250:
                    case 0x73F370:
                    case 0x73F500:
                        return NativeBattleSiren95LayerProgrammes.Advance(current, address, NativeBattleSiren95Io.ReadProgrammeSource(current.Memory, task.ReadDword(0x74)), DrawLayer, () => (NativeCamera ?? throw new NotSupportedException("Native Siren facing layer requires its current camera.")).CapturePackedView1D97778().ReadMatrix());
                    case 0x73A7B0:
                        return NativeBattleSiren95Director.Advance73A7B0(current, address, director);
                    case 0x743C70:
                    case 0x744060:
                    case 0x744450:
                        return NativeBattleSiren95Decorations.AdvanceEmitter(current, address, nativeActionPackets);
                    case 0x743DD0:
                    case 0x7441D0:
                    case 0x744570:
                        return NativeBattleSiren95Decorations.AdvanceChild(current, address, DrawDecoration);
                    case 0x743250:
                        return NativeBattleSiren95TargetParticles.Advance743250(current, address, nativeActionPackets, actor => graphics.Models[actor]);
                    case 0x7447C0:
                        return NativeBattleSiren95Contacts.Advance7447C0(current, address, new(nativeActionPackets, actor => graphics.Models[actor], SelectContactSequence));
                    case 0x744AA0:
                    case 0x744C30:
                    case 0x746FD0:
                    case 0x747220:
                    case 0x7466A0:
                    case 0x7433C0:
                        return NativeBattleSiren95Contacts.AdvanceParticle(current, address, DrawContactParticle);
                    case 0x73C1A0:
                        NativeBattleGfAnimationSource surface = resources.PhaseOneSource ?? throw new NotSupportedException("Native Siren surface requires its original programme source; migrate the named binding explicitly.");
                        return NativeBattleSiren95Actors.Advance73C1A0(current, address, surface, DrawSurface);
                    case 0x73EE50:
                        NativeBattleGfAnimationSource loaded = NativeBattleSiren95Io.ReadProgrammeSource(current.Memory, task.ReadDword(0x74));
                        return NativeBattleSiren95Actors.Advance73EE50(current, address, loaded, DrawSurface);
                    case 0x746920:
                        NativeBattleSiren95StageStep companion = NativeBattleSiren95Actors.Advance746920(current, address, NativeStage ?? throw new NotSupportedException("Native Siren stage companion requires its canonical stage groups."));
                        NativeStage = companion.Stage;
                        return new(companion.Frame, companion.Complete);
                    case 0x73A9E0:
                        return NativeBattleSiren95Programme.Advance73A9E0(current, address, new(ReadProgramAddress, PublishCamera));
                    case 0x73A380:
                        NativeBattleSiren95IoAdvance advanced = NativeBattleSiren95Io.Advance73A380(current, task);
                        current = advanced.Frame with { Effect = advanced.Frame.Effect with { Pools = new NativeBattleEffectHierarchy(advanced.Frame.Effect.Pools).Write(address, advanced.Task).Pools } };
                        if (advanced.Request is { } request)
                        {
                            NativeBattleEffectFileAsset file = resources.Files.SingleOrDefault(file => file.FileId == request.FileId) ?? throw new NotSupportedException($"Native Siren archive file {request.FileId} is not supplied.");
                            NativeBattleSiren95IoQueued queued = NativeBattleSiren95Io.CopyAndQueueFile(current, NativeFileIo ?? throw new NotSupportedException("Native Siren requires its asynchronous file scheduler."), file);
                            NativeFileIo = queued.FileIo;
                            current = queued.Frame;
                        }
                        return new(current, advanced.ReadyToComplete);
                    default:
                        throw new NotSupportedException($"Native Siren callback 0x{task.Callback:X8} at task 0x{address:X8}, signed phase {unchecked((sbyte)task.ReadByte(0x29))} requires its complete actor contract.");
                }
            }

            NativeBattleSiren95Frame SelectContactSequence(NativeBattleSiren95Frame current, byte actor, int sequence)
            {
                NativeBattleGraphicsModel target = graphics.Models[actor];
                if (((target.ResourceFlags44 ?? throw new NotSupportedException("Native505CE0 requires its selected resource flags.")) & 0x140) != 0)
                {
                    graphics = SelectNativeHeaderSequence(graphics, actor, sequence);
                }
                return current;
            }

            NativeBattleSiren95Frame DrawScreen(NativeBattleSiren95Frame current, uint address, uint row)
            {
                NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native Siren screen requires its actual camera owner.");
                NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Native Siren screen requires its current GTE projection.");
                return NativeBattleSiren95ScreenDrawing.Draw745F50(current, address, row, new(nativeActionPackets, camera.CapturePackedView1D97778().ReadMatrix(), projection, current.Memory.ScriptedGraphics, SubmitScreen));
            }

            NativeBattleSiren95Frame SubmitScreen(NativeBattleSiren95Frame current, NativeBattleGfGeometryBatch batch)
            {
                NativeBattlePrimitiveProcess process = current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Siren screen requires its actual COMMON packet owner.");
                NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Siren screen requires its current ordering table.");
                NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(process, primitive.Pending, batch);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                return current with { Memory = current.Memory with { PrimitiveProcess = written.Process } };
            }

            NativeBattleSiren95Frame DrawWave(NativeBattleSiren95Frame current, uint address)
            {
                NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(current.Effect.Pools).Read(address);
                if ((task.ReadByte(0x26) & 4) != 0)
                {
                    return current;
                }
                NativeBattleGfAnimationSource source = NativeBattleSiren95Io.ReadProgrammeSource(current.Memory, task.ReadDword(0x4C));
                NativeBattleTextureWindow full = new(0, 0, 256, 256);
                NativeBattleEsunaMeshContext operands = new(0xF0, task.ReadDword(0x40), unchecked((short)task.ReadWord(0x50)), unchecked((short)task.ReadWord(0x52)), 0, 0, full, full, 17, 2);
                NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native Siren wave requires its current projection.");
                NativeBattleGfGeometryBatch batch = NativeBattleRestorationMeshPackets.EmitBatch88E240(source.Data, NativeBattleSiren95Waves.Matrix745380(task), camera.Projection ?? throw new NotSupportedException("Native Siren wave requires its actual projection."), operands, definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native Siren wave requires its color constants."));
                NativeBattlePrimitiveProcess process = (current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Siren wave requires its actual COMMON packet owner.")).PublishModelColor(task.ReadDword(0x40));
                NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Siren wave requires its actual ordering table.");
                NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(process, primitive.Pending, batch);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                return current with { Memory = current.Memory with { PrimitiveProcess = written.Process, NodeScratch2793E30 = NativeBattleSiren95Scratch.Programme(current.Memory.NodeScratch2793E30, 0, full) } };
            }

            NativeBattleSiren95Frame DrawContactParticle(NativeBattleSiren95Frame current, uint address, short scale) => DrawSprite(current, address, scale);

            NativeBattleSiren95Frame DrawDecoration(NativeBattleSiren95Frame current, uint address) => DrawSprite(current, address, 4096);

            NativeBattleSiren95Frame DrawSprite(NativeBattleSiren95Frame current, uint address, short scale)
            {
                NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(current.Effect.Pools).Read(address);
                if ((task.ReadByte(0x26) & 4) != 0)
                {
                    return current;
                }
                NativeBattleEffectHeapAllocation sprites = task.ReadDword(0x4C) switch
                {
                    0x1533024 => resources.PhaseOneSprites ?? throw new NotSupportedException("Native Siren decorations require their original sprite frames; migrate the named resource binding explicitly."),
                    0x15331D0 => resources.ParticleResources?.ScreenCloudSprites ?? throw new NotSupportedException("Native Siren screen particles require their original cloud sprite frames; migrate the named binding explicitly."),
                    0x1533328 => resources.ParticleResources?.ScreenSpraySprites ?? throw new NotSupportedException("Native Siren screen particles require their original spray sprite frames; migrate the named binding explicitly."),
                    0x15334A0 => resources.ParticleResources?.ModelSprites ?? throw new NotSupportedException("Native Siren model particles require their original sprite frames; migrate the named binding explicitly."),
                    0x15336C4 => resources.ParticleResources?.AlternateModelSprites ?? throw new NotSupportedException("Native Siren model particles require their alternate sprite frames; migrate the named binding explicitly."),
                    0x15335CC => resources.PhaseTwoSprites ?? throw new NotSupportedException("Native Siren contact particles require their original gravity sprite frames; migrate the named resource binding explicitly."),
                    _ => throw new NotSupportedException("Native Siren decoration source leaves its supplied sprite owners.")
                };
                NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native Siren decorations require their current camera owner.");
                current = current with { Memory = current.Memory with { NodeScratch2793E30 = NativeBattleSiren95Scratch.Sprite(current.Memory.NodeScratch2793E30, task, camera.CapturePackedView1D97778().ReadMatrix(), scale) } };
                NativeBattleGfGeometryBatch batch = NativeBattleSearchSprites.Draw7582A0(task, sprites.Data, camera.CapturePackedView1D97778().ReadMatrix(), camera.Projection ?? throw new NotSupportedException("Native Siren decorations require their current GTE projection."), camera.Roll, scale);
                NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Siren decorations require their current ordering table.");
                NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Siren decorations require their actual COMMON packet owner."), primitive.Pending, batch);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                return current with { Memory = current.Memory with { PrimitiveProcess = written.Process } };
            }

            NativeBattleSiren95Frame DrawSurface(NativeBattleSiren95Frame current, uint address) => DrawLayer(current, address).Frame;

            NativeBattleSiren95ProgramAdvance DrawLayer(NativeBattleSiren95Frame current, uint address)
            {
                NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(current.Effect.Pools).Read(address);
                uint sourceAddress = task.ReadDword(0x74);
                NativeBattleGfAnimationSource source = resources.PhaseOneSource is { } original && original.Address == sourceAddress ? original : NativeBattleSiren95Io.ReadProgrammeSource(current.Memory, sourceAddress);
                NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native Siren surface requires its current camera view.");
                NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Native Siren surface requires its current GTE projection.");
                NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native Siren surface requires its authenticated color constants.");
                return NativeBattleSiren95ProgramDrawing.AdvanceWithResult73C280(current, address, source, camera.CapturePackedView1D97778().ReadMatrix(), projection, colors, new(SubmitSurface));
            }

            NativeBattleSiren95Frame SubmitSurface(NativeBattleSiren95Frame current, NativeBattleGfGeometryBatch batch, uint? farColor)
            {
                NativeBattlePrimitiveProcess process = current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Siren surface requires its canonical renderer process.");
                NativeBattleModelRendererRegisters registers = process.ModelRendererRegisters ?? new(null, null, null, null, null);
                registers = farColor is uint color ? registers.PublishModelColor(color) : registers with { FarRed = null, FarGreen = null, FarBlue = null };
                process = process with { ModelRendererRegisters = registers };
                NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Siren surface requires its current ordering table.");
                NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(process, primitive.Pending, batch);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                return current with { Memory = current.Memory with { PrimitiveProcess = written.Process } };
            }

            byte[] ReadProgramAddress(NativeBattleSiren95Frame current, uint address, int length)
            {
                if (address >= NativeBattleSiren95State.GlobalAddress && (ulong)address + (uint)length <= (ulong)NativeBattleSiren95State.GlobalAddress + NativeBattleSiren95State.GlobalLength)
                {
                    return (current.Memory.Siren95 ?? throw new NotSupportedException("Native Siren camera requires its raw module owner.")).Read(address, length);
                }
                NativeBattleEffectHeapAllocation data = resources.CameraPrograms ?? throw new NotSupportedException("Native Siren requires its original camera programmes.");
                long offset = (long)address - 0x1533A98;
                if (data.Data.IsDefault || offset < 0 || length < 0 || offset > data.Data.Length - length)
                {
                    throw new NotSupportedException("Native Siren camera read leaves its original resource span.");
                }
                return data.Data.AsSpan().Slice((int)offset, length).ToArray();
            }

            NativeBattleSiren95Frame PublishCamera(NativeBattleSiren95Frame current, NativeBattleMoogleCameraStep step)
            {
                NativeCamera = NativeBattleMoogleCamera.Publish733150(step, NativeCamera ?? throw new NotSupportedException("Native Siren camera publication requires its current camera owner."));
                return current;
            }

            NativeBattleSiren95VibrationStep StartVibration(NativeBattleSiren95Frame current, uint source, int index, int strength)
            {
                NativeBattleMoogle96VibrationResource vibration = resources.Vibration ?? throw new NotSupportedException("Native Siren requires its original vibration bank.");
                if (source != vibration.Address)
                {
                    throw new NotSupportedException("Native Siren vibration address leaves its supplied bank.");
                }
                NativeVibrationEnqueue queued = (NativeVibrationProcess ?? throw new NotSupportedException("Native Siren requires its vibration process.")).Enqueue(vibration.Bank, index, strength);
                NativeVibrationProcess = queued.State;
                return new(current, unchecked((short)queued.Handle));
            }

            NativeBattleSiren95Frame StopVibration(NativeBattleSiren95Frame current, short handle)
            {
                NativeVibrationProcess = (NativeVibrationProcess ?? throw new NotSupportedException("Native Siren requires its vibration process.")).Stop4A2940(handle);
                return current;
            }

            NativeBattleSiren95Frame PlaySound(NativeBattleSiren95Frame current, uint source, int flags, int pan)
            {
                NativeBattleEffectHeapAllocation bytes = resources.SoundDescriptors ?? throw new NotSupportedException("Native Siren requires its original sound descriptors.");
                long offset = (long)source - 0x1533014;
                if (bytes.Data.IsDefault || offset < 0 || offset > bytes.Data.Length - 4)
                {
                    throw new NotSupportedException("Native Siren sound descriptor leaves its supplied bank.");
                }
                uint sound = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Data.AsSpan().Slice((int)offset, 4));
                PublishNativeModuleSound(new(sound, unchecked((uint)flags), null) { Pan = checked((byte)pan) });
                return current;
            }

            NativeBattleSiren95Frame WriteModel(NativeBattleSiren95Frame current, int actor, NativeBattleGraphicsModel model)
            {
                graphics = graphics with { Models = graphics.Models.SetItem(actor, model) };
                return current;
            }

            NativeBattleSiren95Frame InitializeModel(NativeBattleSiren95Frame current, uint address, uint source, int mode)
            {
                NativeBattleEffectOwnedModelResource model = NativeBattleSiren95Model.ReadCurrentResource(current.Memory, source);
                uint color = (NativeStage ?? throw new NotSupportedException("Native Siren model requires its canonical default stage color.")).ModelColors?.Color44 ?? throw new NotSupportedException("Native Siren model default color is unknown.");
                return NativeBattleSiren95Model.Initialize8DD0F0(current, address, model, checked((ushort)mode), color);
            }

            NativeBattleSiren95Frame DrawModel(NativeBattleSiren95Frame current, uint address, NativeBattleEffectOwnedModelState model)
            {
                NativeBattleEffectOwnedModelResource resource = NativeBattleSiren95Model.ReadCurrentResource(current.Memory, model.ResourceAddress);
                NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(current.Effect.Pools).Read(address);
                NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native Siren model requires its current camera.");
                NativeBattleGraphicsDefinition profile = definition.NativeGraphics ?? throw new NotSupportedException("Native Siren model requires its graphics profile.");
                NativeBattlePrimitiveCapabilities capabilities = profile.PrimitiveCapabilities ?? throw new NotSupportedException("Native Siren model requires its actual device capabilities.");
                NativeBattlePrimitiveProcess process = current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Siren model requires its canonical renderer process.");
                int start = process.ByteCursor ?? throw new NotSupportedException("Native Siren model requires its actual COMMON packet cursor.");
                bool renderedBody = false;
                int size = checked(resource.Geometry.Select(mesh => mesh.Vertices.Length).DefaultIfEmpty(0).Max() * 8);
                NativeBattleEffectOwnedModelDraw draw = NativeBattleEffectOwnedModelDrawing.Draw7558A0(task, model, resource, camera.CapturePackedView1D97778().ReadMatrix(), camera.Projection ?? throw new NotSupportedException("Native Siren model requires its actual projection."), profile.Rules.ShadowSource ?? throw new NotSupportedException("Native Siren model requires its original shadow source."), capabilities, profile.Rules.SummonColors ?? throw new NotSupportedException("Native Siren model requires its color constants."), new byte[size].ToImmutableArray(), EnterRenderer);
                current = NativeBattleSiren95Model.PublishProjected746C10(current, draw.ProjectedScratch, draw.ProjectedBytesWritten);
                NativeBattleEffectNodeScratch scratch = NativeBattleSiren95Scratch.Model(current.Memory.NodeScratch2793E30, draw.Task, camera.CapturePackedView1D97778().ReadMatrix(), resource, renderedBody, draw.Bounds, camera.Projection ?? throw new NotSupportedException("Native Siren shadow requires its actual projection."), profile.Rules.ShadowSource ?? throw new NotSupportedException("Native Siren shadow requires its original source."), capabilities.GouraudAlpha, current.Memory.ScriptedGraphics, out uint? sourceCursor);
                current = current with { Memory = current.Memory with { NodeScratch2793E30 = scratch } };
                if (renderedBody)
                {
                    process = process.Publish50FDF0(unchecked((uint)(start + draw.BodyAllocationBytes)), sourceCursor);
                }
                current = current with { Memory = current.Memory with { PrimitiveProcess = process } };
                NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Siren model requires its actual ordering table.");
                NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(process, primitive.Pending, draw.Packets);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                return current with { Memory = current.Memory with { PrimitiveProcess = written.Process }, Effect = current.Effect with { Pools = new NativeBattleEffectHierarchy(current.Effect.Pools).Write(address, draw.Task).Pools } };

                bool EnterRenderer()
                {
                    (process, renderedBody) = process.Enter5099D0(capabilities.HardwareEnabled ?? throw new NotSupportedException("Native Siren renderer query requires its configured hardware capability."));
                    if (renderedBody)
                    {
                        process = process.PublishModelColor(task.ReadDword(0x58));
                    }
                    return renderedBody;
                }
            }

            NativeBattleSiren95Frame PlayModelStream(NativeBattleSiren95Frame current)
            {
                PublishNativeModuleSound((current.Memory.SummonStream ?? throw new NotSupportedException("Native Siren model requires its retained summon stream.")).Play5018C0(128, 127));
                return current;
            }

            NativeBattleSiren95Frame EmitModelParticles(NativeBattleSiren95Frame current, uint address, bool secondary)
            {
                NativeBattleSiren95ParticleResources particles = resources.ParticleResources ?? throw new NotSupportedException("Native Siren model particles require their original bone selectors and sprite frames; migrate the named binding explicitly.");
                NativeBattleEffectOwnedModelState model = current.Memory.Siren95?.ModelState ?? throw new NotSupportedException("Native Siren particle emission requires its current model pose.");
                return NativeBattleSiren95ModelParticles.Emit(current, address, secondary, NativeBattleSiren95Model.ReadCurrentResource(current.Memory, model.ResourceAddress), nativeActionPackets, particles.BoneSelectors);
            }

            NativeBattleSiren95Frame SpawnStage(NativeBattleSiren95Frame current, uint address)
            {
                return NativeBattleSiren95Actors.Spawn73BCF0(current, address, new(nativeActionPackets, InitializeModel));
            }
        }
        NativeEffectMemory = frame.Memory;
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleSiren95Frame ApplyContact(NativeBattleSiren95Frame current, int contact)
        {
            NativeEffectMemory = current.Memory;
            var pending = graphics.PendingContacts;
            graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact)] });
            graphics = graphics with { PendingContacts = pending };
            return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Native Siren contact lost its process memory.") };
        }

        NativeBattleSiren95Frame Execute(NativeBattleSiren95Frame current, NativeBattleChoco97Operation operation)
        {
            if (operation is not NativeBattleChoco97CommonTextureLoad load)
            {
                throw new NotSupportedException("Native Siren master operation requires its actual resource completion contract.");
            }
            (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)load.DestinationArenaOffset), load.CompletionAddress, (address, value) => current = current with { Effect = NativeBattleSiren95Controller.WriteCompletionByte(current.Effect, address, value) });
            graphics = graphics with { Auxiliary = auxiliary };
            return current;
        }
    }

    private void CompleteNativeSiren95File(uint callback)
    {
        NativeBattleOriginalEffectState effect = NativeGraphics?.OriginalEffect ?? throw new NotSupportedException("Native Siren file completion requires its selected effect.");
        NativeBattleSiren95Frame frame = NativeBattleSiren95Io.Complete(new(NativeEffectMemory ?? throw new NotSupportedException("Native Siren file completion requires its current memory."), effect, nativeCrtRandom ?? throw new NotSupportedException("Native Siren file completion requires its CRT owner.")), callback);
        NativeEffectMemory = frame.Memory;
    }
}
