using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleShivaFrame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, int? DrawCursor = null);
public abstract record NativeBattleShivaOperation;
public sealed record NativeBattleShivaService(NativeBattleShivaDirectorOperation Operation) : NativeBattleShivaOperation;
public sealed record NativeBattleShivaCameraPublish(NativeBattleGuardianCameraStep Step) : NativeBattleShivaOperation;
public sealed record NativeBattleShivaTextureLoad(uint CompletionAddress) : NativeBattleShivaOperation;
public sealed record NativeBattleShivaPackets(NativeBattleGfGeometryBatch Batch, bool EffectArena) : NativeBattleShivaOperation;
public sealed record NativeBattleShivaSprites(NativeBattleModulePacketBatch Batch) : NativeBattleShivaOperation;
public sealed record NativeBattleShivaTextureCopyOperation(NativeBattleEffectVramCopy Copy) : NativeBattleShivaOperation;
public sealed record NativeBattleShivaModelService(NativeBattleShivaModelOperation Operation) : NativeBattleShivaOperation;
public sealed record NativeBattleShivaInputs(uint GraphicsFlags, NativeBattleCameraProjection Projection, NativeBattleSummonColors Colors, short Zsf3, short Zsf4, short Dqa, int Dqb, short CameraRoll, Func<int> Pending508500, Func<NativeBattleCameraPose> ReadCameraPose, Func<NativeBattleShivaFrame, NativeBattleShivaOperation, NativeBattleShivaFrame> Execute);

public static class NativeBattleShivaExecution
{
    public static NativeBattleShivaFrame Initialize5C0D50(NativeBattleShivaFrame frame, NativeBattleActionPacketsState packets, System.Collections.Immutable.ImmutableArray<NativeBattleGraphicsModel> models)
    {
        NativeBattleShivaProcess process = frame.Memory.Shiva ?? throw new NotSupportedException("Native Shiva requires its retained process vectors; explicitly migrate a needed external snapshot.");
        process.Validate();
        NativeBattleShivaEntryStep entered = NativeBattleShivaEntry.Initialize5C0D50(frame.Memory, frame.Effect, packets, models);
        return frame with { Memory = entered.Memory with { Shiva = process with { Entry = entered.State } }, Effect = entered.Effect };
    }

    public static NativeBattleShivaFrame Advance5C7F50(NativeBattleShivaFrame frame, NativeBattleShivaResources resources, NativeBattleShivaInputs inputs)
    {
        Execution execution = new(frame, resources, inputs);
        NativeBattleShivaControllerFrame root = new(new(frame.Memory, frame.Effect, frame.Memory.Shiva?.Entry ?? throw new NotSupportedException("Native Shiva requires its executed entry state.")), frame.DrawCursor);
        root = NativeBattleShivaController.Advance5C7F50(root, (current, address) =>
        {
            execution.Frame = execution.Frame with { Memory = current.Entry.Memory, Effect = current.Entry.Effect, DrawCursor = current.CurrentDrawOffset };
            int result = execution.Advance(address);
            NativeBattleShivaFrame next = execution.Frame;
            return new(new(new(next.Memory, next.Effect, next.Memory.Shiva!.Entry!), next.DrawCursor), result);
        });
        return execution.Frame with { Memory = root.Entry.Memory, Effect = root.Entry.Effect, DrawCursor = root.CurrentDrawOffset };
    }

    private sealed class Execution(NativeBattleShivaFrame frame, NativeBattleShivaResources resources, NativeBattleShivaInputs inputs)
    {
        public NativeBattleShivaFrame Frame { get; set; } = frame;
        private NativeBattleShivaProcess Shared => Frame.Memory.Shiva ?? throw new NotSupportedException("Native Shiva requires its retained process state.");
        private NativeBattleActionMatrix World => Shared.Entry?.World ?? throw new NotSupportedException("Native Shiva requires its actual entry world matrix.");
        private NativeBattleEffectTaskPool Children => Frame.Effect.Pools[1];

        public int Advance(uint address)
        {
            NativeBattleModulePoolTask task = Read(address);
            switch (task.Callback)
            {
                case 0x5C0F30:
                    NativeBattleShivaDirectorStep director = NativeBattleShivaDirector.Advance5C0F30(task, new(inputs.GraphicsFlags, () => SetShared(Shared with { WorldView = NativeBattleGfMatrices.Compose56C2F0(inputs.Projection.View ?? throw new NotSupportedException("Native Shiva requires its published current camera view."), World) }), inputs.Pending508500, () => Shared.SoundReady != 0, Director));
                    Write(address, director.Task);
                    return director.Result;
                case 0x63E9C0:
                    NativeBattleGuardianCameraTask camera = Shared.CameraTask ?? throw new NotSupportedException("Native Shiva requires its actual camera task.");
                    NativeBattleGuardianCameraStep cameraStep = NativeBattleGuardianCameraProgram.Advance63E9C0(camera, World, inputs.ReadCameraPose(), (inputs.GraphicsFlags & 1) != 0);
                    SetShared(Shared with { CameraTask = cameraStep.Task });
                    Write(address, task.WriteWord(0x0C, cameraStep.Task.Frame).WriteDword(0x14, checked(0xD32604u + (uint)cameraStep.Task.Offset)));
                    Emit(new NativeBattleShivaCameraPublish(cameraStep));
                    return cameraStep.Result;
                case 0x5C4A90:
                    if (task.ReadWord(0x0C) == 1)
                    {
                        Emit(new NativeBattleShivaTextureLoad(address + 0x0E));
                        task = Read(address);
                    }
                    if (Shared.Entry!.Counter22BD010 == 3)
                    {
                        task = task.WriteWord(0x0C, unchecked((ushort)(task.ReadWord(0x0C) + 1)));
                    }
                    Write(address, task);
                    return task.ReadWord(0x0E) != 0 ? 2 : 0;
                case 0x5C1390:
                    NativeBattleShivaEarlyTaskFrame early = new(Frame.Memory, Children, Frame.Random, Shared.Geometry);
                    NativeBattleShivaEarlyTaskStep visual = NativeBattleShivaEarlyTasks.Advance5C1390(early, Slot(address), inputs.GraphicsFlags, RegenerateGeometry);
                    Frame = Frame with { Memory = visual.Frame.Memory, Random = visual.Frame.Random };
                    SetChildren(visual.Frame.Pool);
                    SetShared(Shared with { Geometry = visual.Frame.Geometry });
                    return visual.Result;
                case 0x5C16B0:
                    NativeBattleShivaOpeningParticleInputs openingInputs = new(World, Shared.WorldView ?? throw new NotSupportedException("Native Shiva opening particles require the published world/view matrix."), resources.InitialMesh, resources.FirstSpriteD209F4, resources.SecondSpriteD20C7C, DrawOpeningMesh, DrawSprite);
                    NativeBattleShivaEarlyTaskStep opening = NativeBattleShivaOpeningParticles.Advance5C16B0(EarlyFrame(), Slot(address), inputs.GraphicsFlags, openingInputs);
                    AcceptEarly(opening.Frame);
                    return opening.Result;
                case 0x5C42A0:
                    NativeBattleShivaEarlyTaskStep firstBank = NativeBattleShivaParticleBanks.Advance5C42A0(EarlyFrame(), Slot(address), inputs.GraphicsFlags, World, resources.ParticleSpriteD20AD0, DrawSprite);
                    AcceptEarly(firstBank.Frame);
                    return firstBank.Result;
                case 0x5C4550:
                    NativeBattleShivaEarlyTaskStep secondBank = NativeBattleShivaParticleBanks.Advance5C4550(EarlyFrame(), Slot(address), inputs.GraphicsFlags, World, resources.ParticleSpriteD20AD0, DrawSprite);
                    AcceptEarly(secondBank.Frame);
                    return secondBank.Result;
                case 0x5C4900:
                    NativeBattleShivaEarlyTaskStep burst = NativeBattleShivaBurstTask.Advance5C4900(new(Frame.Memory, Children, Frame.Random, Shared.Geometry), Slot(address), inputs.GraphicsFlags, Shared.WorldView ?? throw new NotSupportedException("Native Shiva burst requires its published world/view matrix."), resources.BurstMeshD3138C, (current, draw) =>
                    {
                        Frame = Frame with { Memory = current.Memory, Random = current.Random };
                        SetChildren(current.Pool);
                        SetShared(Shared with { Geometry = current.Geometry });
                        Emit(new NativeBattleShivaPackets(NativeBattleShivaBurstTask.Emit572200(draw, inputs.Projection, inputs.Colors), false));
                        return new(Frame.Memory, Children, Frame.Random, Shared.Geometry);
                    });
                    Frame = Frame with { Memory = burst.Frame.Memory, Random = burst.Frame.Random };
                    SetChildren(burst.Frame.Pool);
                    SetShared(Shared with { Geometry = burst.Frame.Geometry });
                    return burst.Result;
                case 0x5C2260:
                    NativeBattleShivaShardTaskStep shards = NativeBattleShivaShardTask.Advance5C2260(EarlyFrame(), Slot(address), inputs.GraphicsFlags, new(Shared.WorldView ?? throw new NotSupportedException("Native Shiva shards require the published world/view matrix."), resources.ShardMeshD2F1C4, inputs.Projection, inputs.Zsf3, inputs.Colors));
                    AcceptEarly(shards.Frame);
                    Emit(new NativeBattleShivaPackets(shards.Packets, false));
                    return shards.Result;
                default:
                    throw new NotSupportedException($"Native Shiva callback{task.Callback:X8} requires its original complete child contract.");
            }
        }

        private NativeBattleShivaEarlyTaskFrame EarlyFrame() => new(Frame.Memory, Children, Frame.Random, Shared.Geometry);

        private NativeBattleShivaModelFrame ModelFrame() => new(Frame.Memory, Children, Shared.RawModel, Shared.Geometry, Shared.Entry!.Counter22BD010);

        private void AcceptModel(NativeBattleShivaModelFrame current)
        {
            Frame = Frame with { Memory = current.Memory };
            SetChildren(current.Pool);
            SetShared(Shared with { RawModel = current.RawModel, Geometry = current.Geometry, Entry = Shared.Entry! with { Counter22BD010 = current.EntryCounter22BD010 } });
        }

        private NativeBattleShivaModelFrame CopyModelTexture(NativeBattleShivaModelFrame current, int selector, int variant)
        {
            AcceptModel(current);
            NativeBattleEffectVramCopy? copy = NativeBattleShivaTextureCopy.Resolve5C4F80(current.RawModel, selector, variant, ReadModelMemory);
            if (copy is not null)
            {
                Emit(new NativeBattleShivaTextureCopyOperation(copy));
            }
            return ModelFrame();
        }

        private byte[] ReadModelMemory(uint address, int length)
        {
            if (address < NativeBattleEffectNodeMemory.ArenaAddress)
            {
                throw new NotSupportedException("Native Shiva model metadata requires its actual current effect arena.");
            }
            return (Frame.Memory.Arena ?? throw new NotSupportedException("Native Shiva model metadata requires its loaded effect arena.")).Read(checked((int)(address - NativeBattleEffectNodeMemory.ArenaAddress)), length);
        }

        private NativeBattleShivaModelFrame DrawRawModel(NativeBattleShivaModelFrame current)
        {
            AcceptModel(current);
            NativeBattleEffectArena arena = Frame.Memory.Arena ?? throw new NotSupportedException("Native Shiva model drawing requires its mutable shared vertex arena.");
            NativeBattleEffectOwnedModelResource resource = resources.Model544;
            ImmutableArray<byte> scratch = arena.Read(0x33000, checked(resource.Geometry[0].Vertices.Length * 8)).ToImmutableArray();
            NativeBattleCactuarRawModelDraw draw = NativeBattleCactuarRawModel.Draw5A9010(current.RawModel, resource, Shared.WorldView ?? throw new NotSupportedException("Native Shiva model drawing requires its published world/view matrix."), inputs.Projection, inputs.Colors, NativeBattleShivaModelTask.Binding, scratch);
            if (draw.ProjectedBytesWritten > 0)
            {
                arena = arena.Write(0x33000, draw.State.ProjectedScratch!.Value.AsSpan().Slice(0, draw.ProjectedBytesWritten));
            }
            Frame = Frame with { Memory = Frame.Memory with { Arena = arena } };
            SetShared(Shared with { RawModel = draw.State with { ProjectedScratch = null } });
            Emit(new NativeBattleShivaPackets(draw.Packets, false));
            return ModelFrame();
        }

        private NativeBattleShivaModelFrame ExecuteModel(NativeBattleShivaModelFrame current, NativeBattleShivaModelOperation operation)
        {
            AcceptModel(current);
            Emit(new NativeBattleShivaModelService(operation));
            return ModelFrame();
        }

        private void AcceptEarly(NativeBattleShivaEarlyTaskFrame current)
        {
            Frame = Frame with { Memory = current.Memory, Random = current.Random };
            SetChildren(current.Pool);
            SetShared(Shared with { Geometry = current.Geometry });
        }

        private NativeBattleShivaEarlyTaskFrame DrawOpeningMesh(NativeBattleShivaEarlyTaskFrame current, NativeBattleShivaOpeningMeshDraw draw)
        {
            AcceptEarly(current);
            NativeBattleModuleMeshContext source = draw.Context;
            NativeBattleGfGeometryContext context = new(source.Flags, source.FarColor ?? 0, source.Fade ?? 0, source.TexturePage, source.Clut, 0, source.TableBaseBucket, source.DepthShift);
            NativeBattleShivaMeshPacketResult packets = NativeBattleShivaMeshPackets.Emit5C1B50(draw.Resource, default, draw.Matrix, inputs.Projection, context, draw.MaximumCount, draw.WindowCount, inputs.Zsf4, inputs.Dqa, inputs.Dqb, inputs.Colors, readSelectedVertex: ReadVertex);
            Emit(new NativeBattleShivaPackets(packets.Batch, true));
            return EarlyFrame();

            NativeBattleVector ReadVertex(int offset)
            {
                byte[] bytes = (Frame.Memory.Arena ?? throw new NotSupportedException("Native Shiva FT4 drawing requires its actual mutable vertex arena.")).Read(checked(draw.VertexArenaOffset + offset), 6);
                return new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(4)));
            }
        }

        private NativeBattleShivaEarlyTaskFrame DrawSprite(NativeBattleShivaEarlyTaskFrame current, NativeBattleModuleDraw draw)
        {
            AcceptEarly(current);
            Emit(new NativeBattleShivaSprites(NativeBattleModulePackets.EmitSprite571DC0(draw, inputs.Projection, inputs.CameraRoll, 17, 2)));
            return EarlyFrame();
        }

        private NativeBattleShivaEarlyTaskFrame RegenerateGeometry(NativeBattleShivaEarlyTaskFrame current, int count)
        {
            NativeBattleShivaGeometryStep step = NativeBattleShivaEarlyGeometry.Regenerate5C1470(current.Memory.Arena ?? throw new NotSupportedException("Native Shiva geometry requires its current effect arena."), current.Geometry, resources.InitialVertexPairs.Data, count);
            return current with { Memory = current.Memory with { Arena = step.Arena }, Geometry = step.State };
        }

        private void Director(NativeBattleShivaDirectorOperation operation)
        {
            switch (operation.Service)
            {
                case NativeBattleShivaDirectorService.Camera:
                    NativeBattleGuardianCameraProcess process = Frame.Memory.GuardianCameraProcess ?? throw new NotSupportedException("Native Shiva requires its retained camera process.");
                    NativeBattleGuardianCameraTask camera = NativeBattleGuardianCameraProgram.Begin63E960(process, resources.CameraProgram.Data, resources.CameraRandomDivisor, Random);
                    SetShared(Shared with { CameraTask = camera });
                    uint address = Append(0x63E9C0);
                    Write(address, Read(address).WriteDword(0x14, 0xD32604).WriteDword(0x18, 0x22BD0C0).WriteDword(0x1C, 0x22BD018));
                    break;
                case NativeBattleShivaDirectorService.AppendChild:
                    Append(checked((uint)operation.First));
                    break;
                case NativeBattleShivaDirectorService.Initializer:
                    NativeBattleShivaEarlyTaskFrame spawned = operation.First switch
                    {
                        0x5C4820 => NativeBattleShivaEarlyTasks.Spawn5C4820(EarlyFrame(), resources.BurstRowsD324B8.Data),
                        0x5C1E80 => NativeBattleShivaShards.Initialize5C1E80(EarlyFrame(), resources.ShardMeshD2F1C4),
                        _ => throw new NotSupportedException($"Native Shiva initializer{operation.First:X8} requires its original reached contract.")
                    };
                    AcceptEarly(spawned);
                    break;
                default:
                    Emit(new NativeBattleShivaService(operation));
                    break;
            }
        }

        private int Random()
        {
            Frame = Frame with { Random = Frame.Random.Draw(out ushort sample) };
            return sample;
        }

        private int Slot(uint address)
        {
            long offset = (long)address - Children.BankAddress;
            if (offset < 0 || offset >= Children.Stride * Children.Slots.Length || offset % Children.Stride != 0)
            {
                throw new InvalidDataException("Native Shiva callback leaves its physical child slots.");
            }
            return checked((int)(offset / Children.Stride));
        }

        private NativeBattleModulePoolTask Read(uint address) => Children.Slots[Slot(address)];

        private void Write(uint address, NativeBattleModulePoolTask task)
        {
            int slot = Slot(address);
            NativeBattleModulePoolTask current = Children.Slots[slot];
            SetChildren(Children with { Slots = Children.Slots.SetItem(slot, task with { Next = current.Next }) });
        }

        private uint Append(uint callback)
        {
            NativeBattleEffectTaskPool pool = Children;
            int slot;
            (pool, slot) = pool.Append(callback);
            pool = pool with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteWord(0x0C, 0).WriteWord(0x0E, 0)) };
            SetChildren(pool);
            return checked(pool.BankAddress + (uint)(slot * pool.Stride));
        }

        private void SetChildren(NativeBattleEffectTaskPool pool) => Frame = Frame with { Effect = Frame.Effect with { Pools = Frame.Effect.Pools.SetItem(1, pool) } };
        private void SetShared(NativeBattleShivaProcess process) => Frame = Frame with { Memory = Frame.Memory with { Shiva = process } };
        private void Emit(NativeBattleShivaOperation operation) => Frame = inputs.Execute(Frame, operation);
    }
}
