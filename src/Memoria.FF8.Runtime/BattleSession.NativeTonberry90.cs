using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeTonberry90(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native Tonberry requires its selected effect.");
        NativeBattleTonberry90Frame frame = new(NativeEffectMemory ?? throw new NotSupportedException("Native Tonberry requires its retained effect memory."), effect, nativeCrtRandom ?? throw new NotSupportedException("Native Tonberry requires its retained CRT random state."));
        if (initialize)
        {
            frame = NativeBattleTonberry90Controller.Initialize762360(frame, nativeActionPackets);
        }
        else
        {
            NativeBattleTonberry90Resources resources = effect.Binding.Tonberry90Resources ?? throw new NotSupportedException("Native Tonberry requires its original programme, IO and vibration resources.");
            resources.Validate();
            NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native Tonberry requires its current camera owner.");
            NativeBattleTonberry90DirectorServices directorServices = new(nativeActionPackets, StartVibration, StopVibration, InitializeCreature);
            NativeBattleTonberry90ProgrammeServices programmeServices = new(ReadAddress, PublishCamera, PlaySound);
            NativeBattleTonberry90Inputs inputs = new(nativeActionPackets, camera.CapturePackedView1D97778(), actor => graphics.Models[actor], ReadPoseSource, InitializeTarget, AdvanceLeaf, ApplyContact, Execute);
            frame = NativeBattleTonberry90Controller.Advance7624D0(frame, inputs);

            NativeBattlePoseSource ReadPoseSource(int actor) => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Native Tonberry requires its selected current actor pose source.");

            NativeBattleTonberry90Frame InitializeTarget(NativeBattleTonberry90Frame current, uint address)
            {
                current = NativeBattleTonberry90Director.Initialize762BB0(current, address, actor => graphics.Models[actor], ReadPoseSource);
                return NativeBattleTonberry90Io.Initialize762760(current, resources.ReadStatic(NativeBattleTonberry90Io.ScriptAddress, 0x40).ToArray().ToImmutableArray());
            }

            NativeBattleTonberry90TaskStep AdvanceLeaf(NativeBattleTonberry90Frame current, uint address)
            {
                NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(current.Effect.Pools).Read(address);
                switch (task.Callback)
                {
                    case 0x762C60:
                        return NativeBattleTonberry90Director.Advance762C60(current, address, directorServices);
                    case 0x762DA0:
                        return NativeBattleTonberry90Programme.Advance762DA0(current, address, programmeServices);
                    case 0x7681B0:
                        NativeBattleTonberry90CompanionStep companion = NativeBattleTonberry90Actors.Advance7681B0(current, address, NativeStage ?? throw new NotSupportedException("Native Tonberry companion requires its canonical stage groups."));
                        NativeStage = companion.Stage;
                        return new(companion.Frame, companion.Complete);
                    case 0x7674E0:
                        NativeBattleTonberry90CreatureServices creatureServices = new(_ => NativeStage?.ModelColors?.Color40 ?? throw new NotSupportedException("Native Tonberry creature requires its canonical transition color."), PlayCreatureSound, DrawCreature);
                        return NativeBattleTonberry90Creature.Advance7674E0(current, address, RequireCreatureModel(), creatureServices);
                    case 0x7670E0:
                        return NativeBattleTonberry90Creature.Advance7670E0(current, address);
                    case 0x763EB0:
                        NativeBattleTonberry90ActorServices actorServices = new((_, source) => resources.PhaseOneSource is { } actual && actual.Address == source ? actual : throw new NotSupportedException("Native Tonberry phase actor requires its complete current programme source."), CaptureGround, DrawActor);
                        return NativeBattleTonberry90Actors.Advance763EB0(current, address, actorServices);
                    case 0x762850:
                        NativeBattleTonberry90IoAdvance advanced = NativeBattleTonberry90Io.Advance762850(current, task);
                        current = advanced.Frame with { Effect = advanced.Frame.Effect with { Pools = new NativeBattleEffectHierarchy(advanced.Frame.Effect.Pools).Write(address, advanced.Task).Pools } };
                        if (advanced.Request is { } request)
                        {
                            NativeBattleEffectFileAsset file = resources.Files.SingleOrDefault(file => file.FileId == request.FileId) ?? throw new NotSupportedException($"Native Tonberry archive file {request.FileId} is not supplied.");
                            NativeBattleTonberry90IoQueued queued = NativeBattleTonberry90Io.CopyAndQueueFile(current, NativeFileIo ?? throw new NotSupportedException("Native Tonberry requires its asynchronous file scheduler."), file);
                            NativeFileIo = queued.FileIo;
                            current = queued.Frame;
                        }
                        return new(current, advanced.ReadyToComplete);
                    default:
                        throw new NotSupportedException($"Native Tonberry callback 0x{task.Callback:X8} at task 0x{address:X8}, signed phase {unchecked((sbyte)task.ReadByte(0x29))} requires its complete actor contract.");
                }
            }

            NativeBattleEffectOwnedModelResource RequireCreatureModel() => resources.CreatureModel ?? throw new NotSupportedException("Native Tonberry creature requires its authenticated embedded model; migrate the named active binding explicitly.");

            NativeBattleTonberry90Frame InitializeCreature(NativeBattleTonberry90Frame current, uint address, uint source, int mode)
            {
                NativeBattleEffectOwnedModelResource model = RequireCreatureModel();
                if (model.Address != source)
                {
                    throw new InvalidDataException("Native Tonberry creature constructor source differs from its current selected model.");
                }
                uint color = NativeStage?.ModelColors?.Color44 ?? throw new NotSupportedException("Native Tonberry creature requires its canonical default model color.");
                return NativeBattleTonberry90Creature.Initialize8DD0F0(current, address, model, checked((ushort)mode), color);
            }

            NativeBattleTonberry90Frame PlayCreatureSound(NativeBattleTonberry90Frame current, uint descriptor, byte pan, uint volume)
            {
                uint sound = BinaryPrimitives.ReadUInt32LittleEndian(ReadAddress(current, descriptor, 4));
                PublishNativeModuleSound(new(sound, 0, null) { Pan = pan, Volume = volume });
                return current;
            }

            NativeBattleTonberry90CreatureDraw DrawCreature(NativeBattleTonberry90Frame current, uint address, NativeBattleModulePoolTask task, NativeBattleEffectOwnedModelState model)
            {
                NativeBattleEffectOwnedModelResource resource = RequireCreatureModel();
                NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native Tonberry creature requires its current camera owner.");
                NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Native Tonberry creature requires its current projection.");
                NativeBattleActionMatrix view = camera.CapturePackedView1D97778().ReadMatrix();
                NativeBattleGraphicsDefinition profile = definition.NativeGraphics ?? throw new NotSupportedException("Native Tonberry creature requires its current graphics profile.");
                NativeBattleSummonColors colors = profile.Rules.SummonColors ?? throw new NotSupportedException("Native Tonberry creature requires its authenticated color constants.");
                NativeBattlePrimitiveCapabilities capabilities = profile.PrimitiveCapabilities ?? throw new NotSupportedException("Native Tonberry creature requires its actual device capabilities.");
                NativeBattleTonberry90State shared = current.Memory.Tonberry90 ?? throw new NotSupportedException("Native Tonberry creature requires its current module state.");
                int cursor = current.Effect.DrawArenaOffset ?? throw new NotSupportedException("Native Tonberry creature requires its private packet cursor.");
                if (shared.ReadDword(0x259EEA8) != unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)cursor))
                {
                    throw new InvalidDataException("Native Tonberry private packet pointer differs from its portable effect cursor.");
                }
                int size = checked(resource.Geometry.Select(mesh => mesh.Vertices.Length).DefaultIfEmpty(0).Max() * 8);
                uint work = shared.ReadDword(0x259EFB4);
                if (work < NativeBattleEffectNodeMemory.ArenaAddress || (ulong)work + (uint)size > (ulong)NativeBattleEffectNodeMemory.ArenaAddress + NativeBattleEffectArena.Length)
                {
                    throw new NotSupportedException("Native Tonberry projected work leaves its actual current arena.");
                }
                NativeBattlePrimitiveProcess process = current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Tonberry creature requires its canonical renderer process.");
                bool renderedBody = false;
                NativeBattleEffectOwnedModelDraw draw = NativeBattleEffectOwnedModelDrawing.Draw7558A0(task, model, resource, view, projection, profile.Rules.ShadowSource ?? throw new NotSupportedException("Native Tonberry creature requires its original shadow source."), capabilities, colors, new byte[size].ToImmutableArray(), EnterRenderer);
                NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Native Tonberry creature requires its current arena bytes.");
                if (draw.ProjectedBytesWritten > 0)
                {
                    arena = arena.Write(checked((int)(work - NativeBattleEffectNodeMemory.ArenaAddress)), draw.ProjectedScratch.AsSpan().Slice(0, draw.ProjectedBytesWritten));
                }
                if (renderedBody)
                {
                    process = process.Publish50FDF0(unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)cursor + (uint)draw.BodyAllocationBytes), null);
                }
                current = current with { Memory = current.Memory with { Arena = arena, PrimitiveProcess = process } };
                NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Tonberry creature requires its current ordering table.");
                NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, draw.Packets);
                shared = written.Memory.Tonberry90 ?? throw new NotSupportedException("Native Tonberry creature lost its current private packet pointer owner.");
                shared = shared.WriteDword(0x259EEA8, unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor));
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                return new(current with { Memory = written.Memory with { Tonberry90 = shared }, Effect = current.Effect with { DrawArenaOffset = written.Cursor } }, draw.Task);

                bool EnterRenderer()
                {
                    (process, renderedBody) = process.Enter5099D0(capabilities.HardwareEnabled ?? throw new NotSupportedException("Native Tonberry renderer query requires its selected hardware capability."));
                    if (renderedBody)
                    {
                        process = process.PublishModelColor(task.ReadDword(0x58));
                    }
                    return renderedBody;
                }
            }

            NativeBattleTonberry90ActorDraw DrawActor(NativeBattleTonberry90Frame current, uint address)
            {
                NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(current.Effect.Pools).Read(address);
                NativeBattleGfAnimationSource source = resources.PhaseOneSource is { } actual && actual.Address == task.ReadDword(0x74) ? actual : throw new NotSupportedException("Native Tonberry actor draw requires its actual animation programme source.");
                NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Tonberry actor draw requires its current GTE projection.");
                NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native Tonberry actor draw requires its authenticated color constants.");
                NativeBattleTonberry90ProgramDrawServices services = new((state, pointer) => BinaryPrimitives.ReadInt16LittleEndian(ReadAddress(state, pointer, 2)), SubmitActor);
                return NativeBattleTonberry90ProgramDrawing.Advance764130(current, address, source, projection, colors, services);
            }

            NativeBattleTonberry90Frame SubmitActor(NativeBattleTonberry90Frame current, NativeBattleGfGeometryBatch batch, uint? farColor)
            {
                NativeBattlePrimitiveProcess process = current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Tonberry actor draw requires its retained far-color controls.");
                NativeBattleModelRendererRegisters registers = process.ModelRendererRegisters ?? new(null, null, null, null, null);
                registers = farColor is uint color ? registers.PublishModelColor(color) : registers with { FarRed = null, FarGreen = null, FarBlue = null };
                current = current with { Memory = current.Memory with { PrimitiveProcess = process with { ModelRendererRegisters = registers } } };
                NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Tonberry actor draw requires its current ordering table.");
                NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Tonberry actor draw requires its actual COMMON packet owner."), primitive.Pending, batch);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                return current with { Memory = current.Memory with { PrimitiveProcess = written.Process } };
            }

            NativeBattleTonberry90Frame CaptureGround(NativeBattleTonberry90Frame current, uint address, NativeBattleGfAnimationSource source, uint geometryAddress)
            {
                int geometryOffset = checked((int)(geometryAddress - source.Address));
                NativeBattleThundaga105Bounds bounds = NativeBattleGroundCapturePackets.Bounds8DE160(source.Data.AsSpan().Slice(geometryOffset));
                NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(current.Effect.Pools).Read(address);
                ImmutableArray<byte> anchor = task.Payload.AsSpan().Slice(0x1C - 12, 8).ToArray().ToImmutableArray();
                NativeBattleCameraState currentCamera = NativeCamera ?? throw new NotSupportedException("Native ground capture requires its current camera globals.");
                NativeBattleThundaga105CaptureTransform capture = NativeBattleThundaga105CaptureGeometry.TransformCapture(anchor, bounds, currentCamera.Projection ?? throw new NotSupportedException("Native ground capture requires its current GTE projection."), 128, 256, currentCamera.FocalLength ?? throw new NotSupportedException("Native ground capture requires its current global focal WORD."));
                NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native ground capture requires its current ordering table.");
                byte stageIndex = graphics.StageResourceIndex98990 ?? throw new NotSupportedException("Native ground capture requires its current stage resource selector.");
                NativeBattleGroundCaptureWrite written = NativeBattleGroundCapturePackets.Append8DE1F0(current.Memory, primitive.Pending, stageIndex, DrawGround);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                return current with { Memory = written.Memory };

                NativeBattleGroundCaptureWrite DrawGround(NativeBattleGroundCaptureWrite input)
                {
                    NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native ground capture requires its canonical stage groups.");
                    NativeBattleStageGroup group = stage.Groups[1];
                    if ((group.Flags & 1) == 0)
                    {
                        return input;
                    }
                    NativeBattlePoseSource poseSource = group.Source ?? throw new NotSupportedException("Native ground capture requires the current stage ground pose.");
                    NativeBattleDecodedPose pose = poseSource.Sample(group.Animation, group.Counter, group.PoseFlags);
                    group = group with { Matrices = NativeBattlePoseMatrices.ComposeStageCamera(NativeBattlePoseMatrices.StageBoneMatrices(poseSource, pose), capture.Matrix, 0) };
                    NativeStage = stage with { Groups = stage.Groups.SetItem(1, group) };
                    NativeBattlePrimitiveProcess process = input.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native ground capture requires its actual renderer gate.");
                    bool hardware = definition.NativeGraphics?.PrimitiveCapabilities?.HardwareEnabled ?? throw new NotSupportedException("Native ground capture requires the selected renderer capability.");
                    bool draw;
                    (process, draw) = process.Enter5099D0(hardware);
                    input = input with { Memory = input.Memory with { PrimitiveProcess = process } };
                    if (!draw)
                    {
                        return input;
                    }
                    ImmutableArray<NativeBattleRawMesh> meshes = (definition.NativeStageSource?.Geometry ?? throw new NotSupportedException("Native ground capture requires its current stage geometry.")).Where(mesh => mesh.StageGroup == 1).ToImmutableArray();
                    NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native ground capture requires its authenticated color constants.");
                    ImmutableArray<NativeBattleGeometryPrimitive> packets = NativeBattleRawGeometryPackets.Emit5099D0(meshes, group.Matrices, capture.Projection, group.ColorFlags, uint.MaxValue, group.ColorDepth, 17, 16, null, colors);
                    ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
                    int allocated = 0;
                    foreach (NativeBattleGeometryPrimitive packet in packets)
                    {
                        byte[] bytes = new byte[packet.Words.Length * 4];
                        for (int index = 0; index < packet.Words.Length; index++)
                        {
                            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 4), packet.Words[index]);
                        }
                        attempts.Add(new(allocated, bytes.ToImmutableArray(), packet.PhysicalWriteMask ?? throw new NotSupportedException("Native ground packets require their actual write masks."), packet));
                        allocated = checked(allocated + bytes.Length);
                    }
                    NativeBattleCommonPacketWrite result = NativeBattleCommonPacketArena.Append(process, input.Ordering, new NativeBattleGfGeometryBatch(attempts.ToImmutable(), allocated));
                    process = result.Process.PublishModelColor(group.ColorFlags).Publish50FDF0(unchecked((uint)(result.Process.ByteCursor ?? throw new NotSupportedException("Native ground drawing lost its COMMON cursor."))), null);
                    return new(input.Memory with { PrimitiveProcess = process }, result.Ordering);
                }
            }

            NativeBattleTonberry90VibrationStep StartVibration(NativeBattleTonberry90Frame current, uint source, int index, int strength)
            {
                if (source != resources.Vibration.Address)
                {
                    throw new NotSupportedException("Native Tonberry vibration source leaves its selected bank.");
                }
                NativeVibrationEnqueue queued = (NativeVibrationProcess ?? throw new NotSupportedException("Native Tonberry requires its vibration process.")).Enqueue(resources.Vibration.Bank, index, strength);
                NativeVibrationProcess = queued.State;
                return new(current, unchecked((short)queued.Handle));
            }

            NativeBattleTonberry90Frame StopVibration(NativeBattleTonberry90Frame current, short handle)
            {
                NativeVibrationProcess = (NativeVibrationProcess ?? throw new NotSupportedException("Native Tonberry requires its vibration process.")).Stop4A2940(handle);
                return current;
            }

            byte[] ReadAddress(NativeBattleTonberry90Frame current, uint address, int length)
            {
                NativeBattleTonberry90State shared = current.Memory.Tonberry90 ?? throw new NotSupportedException("Native Tonberry requires its current module owner.");
                if (address >= NativeBattleTonberry90State.GlobalAddress && (ulong)address + (uint)length <= (ulong)NativeBattleTonberry90State.GlobalAddress + NativeBattleTonberry90State.GlobalLength)
                {
                    return shared.Read(address, length);
                }
                if (shared.Io is { } io && address >= NativeBattleTonberry90Io.ScriptAddress && (ulong)address + (uint)length <= (ulong)NativeBattleTonberry90Io.ScriptAddress + 0x40)
                {
                    return io.Script.AsSpan().Slice(checked((int)(address - NativeBattleTonberry90Io.ScriptAddress)), length).ToArray();
                }
                if (address >= resources.Vibration.Address && (ulong)address + (uint)length <= (ulong)resources.Vibration.Address + (uint)resources.Vibration.Bank.Bytes.Length)
                {
                    return resources.Vibration.Bank.Bytes.AsSpan().Slice(checked((int)(address - resources.Vibration.Address)), length).ToArray();
                }
                if (address == 0x154716C && length == 4)
                {
                    return BitConverter.GetBytes(resources.CreatureEntrySound ?? throw new NotSupportedException("Native Tonberry creature requires its authenticated entry sound descriptor; migrate the named active binding explicitly."));
                }
                return resources.ReadStatic(address, length).ToArray();
            }

            NativeBattleTonberry90Frame PublishCamera(NativeBattleTonberry90Frame current, NativeBattleMoogleCameraStep step)
            {
                NativeCamera = NativeBattleMoogleCamera.Publish733150(step, NativeCamera ?? throw new NotSupportedException("Native Tonberry camera publication requires its current camera owner."));
                return current;
            }

            NativeBattleTonberry90Frame PlaySound(NativeBattleTonberry90Frame current, uint descriptor, int flags, int pan)
            {
                uint sound = BinaryPrimitives.ReadUInt32LittleEndian(ReadAddress(current, descriptor, 4));
                PublishNativeModuleSound(new(sound, unchecked((uint)flags), null) { Pan = checked((byte)pan) });
                return current;
            }
        }
        NativeEffectMemory = frame.Memory;
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleTonberry90Frame ApplyContact(NativeBattleTonberry90Frame current, int contact)
        {
            NativeEffectMemory = current.Memory;
            ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
            graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact)] });
            graphics = graphics with { PendingContacts = pending };
            return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Native Tonberry contact lost its process memory.") };
        }

        NativeBattleTonberry90Frame Execute(NativeBattleTonberry90Frame current, NativeBattleChoco97Operation operation)
        {
            if (operation is not NativeBattleChoco97CommonTextureLoad load)
            {
                throw new NotSupportedException("Native Tonberry master operation requires its actual resource completion contract.");
            }
            (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)load.DestinationArenaOffset), load.CompletionAddress, (address, value) => current = current with { Effect = NativeBattleTonberry90Controller.WriteCompletionByte(current.Effect, address, value) });
            graphics = graphics with { Auxiliary = auxiliary };
            return current;
        }
    }

    private void CompleteNativeTonberry90File(uint callback)
    {
        NativeBattleGraphicsState graphics = NativeGraphics ?? throw new NotSupportedException("Native Tonberry file completion requires its current graphics owner.");
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native Tonberry file completion requires its selected effect.");
        NativeBattleTonberry90Frame frame = NativeBattleTonberry90Io.Complete(new(NativeEffectMemory ?? throw new NotSupportedException("Native Tonberry file completion requires its current effect memory."), effect, nativeCrtRandom ?? throw new NotSupportedException("Native Tonberry file completion requires its CRT owner.")), callback);
        NativeEffectMemory = frame.Memory;
    }
}
