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
public sealed record NativeBattleShivaInputs(uint GraphicsFlags, NativeBattleCameraProjection Projection, NativeBattleSummonColors Colors, short Zsf3, short Zsf4, short Dqa, int Dqb, Func<int> Pending508500, Func<NativeBattleCameraPose> ReadCameraPose, Func<NativeBattleShivaFrame, NativeBattleShivaOperation, NativeBattleShivaFrame> Execute);

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
                case 0x5C4AD0:
                    NativeBattleShivaModelStep model = NativeBattleShivaModelTask.Advance5C4AD0(ModelFrame(), Slot(address), new(inputs.GraphicsFlags, resources.Model544, resources.TextureValuesD3250C, InitializePlane, CopyModelTexture, DrawPlane, DrawRawModel, ExecuteModel));
                    AcceptModel(model.Frame);
                    return model.Result;
                case 0x5C5950:
                    NativeBattleShivaEarlyTaskStep modelParticles = NativeBattleShivaModelParticles.Advance5C5950(EarlyFrame(), Slot(address), inputs.GraphicsFlags, World, checked((byte)resources.Model544.PoseSource.BoneCount), resources.ModelParticleSpriteD208B0, SampleModelBone, DrawSprite);
                    AcceptEarly(modelParticles.Frame);
                    return modelParticles.Result;
                case 0x5C5BE0:
                    NativeBattleShivaEarlyTaskStep anchor = NativeBattleShivaModelParticles.Advance5C5BE0(EarlyFrame(), Slot(address), inputs.GraphicsFlags, World, resources.AnchorMeshD31A74, SampleModelBone, DrawBillboardMesh);
                    AcceptEarly(anchor.Frame);
                    return anchor.Result;
                case 0x5C5D90:
                    NativeBattleShivaEarlyTaskStep radial = NativeBattleShivaModelParticles.Advance5C5D90(EarlyFrame(), Slot(address), inputs.GraphicsFlags, CurrentView(), resources.RingMeshD3048C, DrawModelMesh);
                    AcceptEarly(radial.Frame);
                    return radial.Result;
                case 0x5C6040:
                    NativeBattleShivaEarlyTaskStep ring = NativeBattleShivaModelParticles.Advance5C6040(EarlyFrame(), Slot(address), inputs.GraphicsFlags, Shared.WorldView ?? throw new NotSupportedException("Native Shiva model rings require the published world/view matrix."), resources.RingMeshD309A4, DrawModelMesh);
                    AcceptEarly(ring.Frame);
                    return ring.Result;
                case 0x5C6FF0:
                    NativeBattleShivaEarlyTaskStep scrolling = NativeBattleShivaScrollingTask.Advance5C6FF0(EarlyFrame(), Slot(address), inputs.GraphicsFlags, Shared.WorldView ?? throw new NotSupportedException("Native Shiva scrolling requires its published world/view matrix."), resources.ScrollMeshD2A50C, DrawScrollingMesh);
                    AcceptEarly(scrolling.Frame);
                    return scrolling.Result;
                case 0x5C6690:
                    NativeBattleShivaEarlyTaskStep spawner = NativeBattleShivaLateChildren.Advance5C6690(EarlyFrame(), Slot(address), inputs.GraphicsFlags, resources.SpawnRowsD324F8.Data);
                    AcceptEarly(spawner.Frame);
                    return spawner.Result;
                case 0x5C6870:
                    NativeBattleShivaEarlyTaskStep column = NativeBattleShivaLateChildren.Advance5C6870(EarlyFrame(), Slot(address), inputs.GraphicsFlags, Shared.WorldView ?? throw new NotSupportedException("Native Shiva columns require the published world/view matrix."), resources.ChildMeshD21B9C, DrawModelMesh);
                    AcceptEarly(column.Frame);
                    return column.Result;
                case 0x5C69E0:
                    NativeBattleShivaEarlyTaskStep delayed = NativeBattleShivaLateChildren.Advance5C69E0(EarlyFrame(), Slot(address), inputs.GraphicsFlags, Shared.WorldView ?? throw new NotSupportedException("Native Shiva delayed geometry requires the published world/view matrix."), resources.BurstMeshD3138C, DrawModelMesh);
                    AcceptEarly(delayed.Frame);
                    return delayed.Result;
                case 0x5C62F0:
                case 0x5C6BB0:
                case 0x5C71B0:
                case 0x5C7CA0:
                    NativeBattleActionMatrix viewWorld = Shared.WorldView ?? throw new NotSupportedException("Native Shiva late particles require the published world/view matrix.");
                    NativeBattleShivaEarlyTaskStep late = task.Callback switch
                    {
                        0x5C62F0 => NativeBattleShivaBillboardBanks.Advance5C62F0(EarlyFrame(), Slot(address), inputs.GraphicsFlags, viewWorld, resources.LateParticleSpriteD20E10, DrawLateSprite),
                        0x5C6BB0 => NativeBattleShivaBillboardBanks.Advance5C6BB0(EarlyFrame(), Slot(address), inputs.GraphicsFlags, viewWorld, resources.LateParticleSpriteD20E10, DrawLateSprite),
                        0x5C71B0 => NativeBattleShivaBillboardBanks.Advance5C71B0(EarlyFrame(), Slot(address), inputs.GraphicsFlags, viewWorld, resources.ParticleSpriteD20AD0, DrawLateSprite),
                        _ => NativeBattleShivaBillboardBanks.Advance5C7CA0(EarlyFrame(), Slot(address), inputs.GraphicsFlags, viewWorld, resources.ModelParticleSpriteD208B0, DrawLateSprite)
                    };
                    AcceptEarly(late.Frame);
                    return late.Result;
                case 0x5C77C0:
                    NativeBattleShivaFinalStep shatter = NativeBattleShivaFinalShatter.Advance5C77C0(FinalFrame(), Slot(address), inputs.GraphicsFlags, new(Shared.WorldView ?? throw new NotSupportedException("Native Shiva final geometry requires its published world/view matrix."), resources.FinalMeshD257CC, resources.FinalMeshD27E6C, inputs.Projection, inputs.Zsf3, inputs.Colors, SubmitFinalPackets, DrawFinalMesh, DrawFinalScroll));
                    AcceptFinal(shatter.Frame);
                    Emit(new NativeBattleShivaPackets(shatter.CommonPackets, false));
                    return shatter.Result;
                case 0x5C2DE0:
                    NativeBattleShivaFinalStep secondSpawner = NativeBattleShivaSecondShatter.Advance5C2DE0(FinalFrame(), Slot(address), inputs.GraphicsFlags);
                    AcceptFinal(secondSpawner.Frame);
                    return secondSpawner.Result;
                case 0x5C2E90:
                    NativeBattleShivaFinalInputs secondGeometry = SecondGeometryInputs();
                    NativeBattleShivaFinalStep secondShatter = NativeBattleShivaSecondShatter.Advance5C2E90(FinalFrame(), Slot(address), inputs.GraphicsFlags, new(secondGeometry, resources.ClippingVertexSelectorsD32524, inputs.Zsf4, inputs.Dqa, inputs.Dqb, DrawCommonFinalMesh));
                    AcceptFinal(secondShatter.Frame);
                    Emit(new NativeBattleShivaPackets(secondShatter.CommonPackets, false));
                    return secondShatter.Result;
                case 0x5C4140:
                    NativeBattleShivaFinalStep halo = NativeBattleShivaSecondShatter.Advance5C4140(FinalFrame(), Slot(address), inputs.GraphicsFlags, SecondGeometryInputs(), DrawCommonFinalScroll);
                    AcceptFinal(halo.Frame);
                    Emit(new NativeBattleShivaPackets(halo.CommonPackets, false));
                    return halo.Result;
                default:
                    throw new NotSupportedException($"Native Shiva callback{task.Callback:X8} requires its original complete child contract.");
            }
        }

        private NativeBattleShivaEarlyTaskFrame EarlyFrame() => new(Frame.Memory, Children, Frame.Random, Shared.Geometry);

        private NativeBattleActionMatrix CurrentView() => inputs.Projection.View ?? throw new NotSupportedException("Native Shiva requires its published current camera view.");

        private NativeBattleVector SampleModelBone(NativeBattleShivaEarlyTaskFrame current, int bone, int fraction)
        {
            NativeBattleShivaProcess process = current.Memory.Shiva ?? throw new NotSupportedException("Native Shiva model particles require their actual current raw model.");
            return NativeBattleShivaModelBones.Read502170(process.RawModel, resources.Model544, bone, checked((short)fraction));
        }

        private NativeBattleShivaEarlyTaskFrame DrawModelMesh(NativeBattleShivaEarlyTaskFrame current, NativeBattleShivaModelMeshDraw draw)
        {
            AcceptEarly(current);
            Emit(new NativeBattleShivaPackets(NativeBattleModulePackets.EmitBatch572200(draw.Resource.Bytes, 0, draw.Resource.Bytes.AsSpan().Slice(8).ToArray().ToImmutableArray(), draw.Matrix, inputs.Projection, draw.Context, inputs.Colors), false));
            return EarlyFrame();
        }

        private NativeBattleShivaEarlyTaskFrame DrawBillboardMesh(NativeBattleShivaEarlyTaskFrame current, NativeBattleShivaModelBillboardDraw draw)
        {
            NativeBattleModuleDraw billboard = new(NativeBattleModuleDrawKind.Sprite, 0) { Anchor = draw.Anchor, Scale = draw.Scale, DepthOffset = draw.DepthOffset, SpriteFlags = 0 };
            NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.ModuleSpriteCamera(billboard, CurrentView(), 0);
            return DrawModelMesh(current, new(draw.Resource, matrix, draw.Context));
        }

        private NativeBattleShivaEarlyTaskFrame DrawScrollingMesh(NativeBattleShivaEarlyTaskFrame current, NativeBattleShivaScrollingDraw draw)
        {
            AcceptEarly(current);
            Emit(new NativeBattleShivaPackets(NativeBattleShivaScrollingPackets.Emit64E080(draw.Resource, draw.Matrix, inputs.Projection, draw.Context, inputs.Zsf3, inputs.Zsf4, inputs.Dqa, inputs.Dqb, inputs.Colors), false));
            return EarlyFrame();
        }

        private NativeBattleShivaEarlyTaskFrame DrawLateSprite(NativeBattleShivaEarlyTaskFrame current, NativeBattleShivaLateSpriteDraw draw)
        {
            AcceptEarly(current);
            Emit(new NativeBattleShivaSprites(NativeBattleShivaBillboardBanks.Emit571C80(draw, inputs.Projection, inputs.ReadCameraPose().Roll)));
            return EarlyFrame();
        }

        private NativeBattleShivaModelFrame ModelFrame() => new(Frame.Memory, Children, Shared.RawModel, Shared.Geometry, Shared.Entry!.Counter22BD010);

        private NativeBattleShivaFinalFrame FinalFrame() => new(Frame.Memory, Children, Frame.Random, Shared.Entry!.Counter22BD010, Frame.DrawCursor ?? throw new NotSupportedException("Native Shiva final geometry requires its current physical private draw cursor."));

        private NativeBattleShivaFinalInputs SecondGeometryInputs() => new(Shared.WorldView ?? throw new NotSupportedException("Native Shiva secondary geometry requires its published world/view matrix."), resources.SecondShatterMeshD2BA3C, resources.SecondShatterMeshD2D600, inputs.Projection, inputs.Zsf3, inputs.Colors, SubmitFinalPackets, DrawFinalMesh, DrawFinalScroll);

        private void AcceptFinal(NativeBattleShivaFinalFrame current)
        {
            Frame = Frame with { Memory = current.Memory, Random = current.Random, DrawCursor = current.PrivateDrawOffset };
            SetChildren(current.Pool);
            SetShared(Shared with { Entry = Shared.Entry! with { Counter22BD010 = current.Phase22BD010 } });
        }

        private NativeBattleShivaFinalFrame SubmitFinalPackets(NativeBattleShivaFinalFrame current, NativeBattleGfGeometryBatch batch)
        {
            AcceptFinal(current);
            Emit(new NativeBattleShivaPackets(batch, true));
            return FinalFrame();
        }

        private NativeBattleShivaFinalFrame DrawFinalMesh(NativeBattleShivaFinalFrame current, NativeBattleShivaFinalMeshDraw draw)
        {
            return SubmitFinalPackets(current, EmitStandardMesh(draw));
        }

        private NativeBattleGfGeometryBatch EmitStandardMesh(NativeBattleShivaFinalMeshDraw draw)
        {
            NativeBattleGfGeometryContext source = draw.Context;
            if (source.DepthBias != 0)
            {
                throw new NotSupportedException("Native Shiva standard mesh uses its actual zero depth bias.");
            }
            return NativeBattleModulePackets.EmitBatch572200(draw.Resource.Bytes, 0, draw.Resource.Bytes.AsSpan().Slice(8).ToArray().ToImmutableArray(), draw.Matrix, inputs.Projection, new(source.Flags, source.FarColor, source.DepthCue, source.TexturePage, source.Clut, 0, source.TableBaseBucket, source.DepthShift), inputs.Colors);
        }

        private NativeBattleShivaFinalFrame DrawCommonFinalMesh(NativeBattleShivaFinalFrame current, NativeBattleShivaFinalMeshDraw draw)
        {
            AcceptFinal(current);
            Emit(new NativeBattleShivaPackets(EmitStandardMesh(draw), false));
            return FinalFrame();
        }

        private NativeBattleShivaFinalFrame DrawFinalScroll(NativeBattleShivaFinalFrame current, NativeBattleShivaScrollingDraw draw)
        {
            return SubmitFinalPackets(current, NativeBattleShivaScrollingPackets.Emit64E080(draw.Resource, draw.Matrix, inputs.Projection, draw.Context, inputs.Zsf3, inputs.Zsf4, inputs.Dqa, inputs.Dqb, inputs.Colors));
        }

        private NativeBattleShivaFinalFrame DrawCommonFinalScroll(NativeBattleShivaFinalFrame current, NativeBattleShivaScrollingDraw draw)
        {
            AcceptFinal(current);
            Emit(new NativeBattleShivaPackets(NativeBattleShivaScrollingPackets.Emit64E080(draw.Resource, draw.Matrix, inputs.Projection, draw.Context, inputs.Zsf3, inputs.Zsf4, inputs.Dqa, inputs.Dqb, inputs.Colors), false));
            return FinalFrame();
        }

        private void AcceptModel(NativeBattleShivaModelFrame current)
        {
            Frame = Frame with { Memory = current.Memory };
            SetChildren(current.Pool);
            SetShared(Shared with { RawModel = current.RawModel, Geometry = current.Geometry, Entry = Shared.Entry! with { Counter22BD010 = current.EntryCounter22BD010 } });
        }

        private NativeBattleShivaDistortionSetup InitializePlane(NativeBattleShivaModelFrame current, NativeBattleVector point, NativeBattleVector normal)
        {
            NativeBattleShivaPlane plane = NativeBattleShivaPlaneClipping.CreatePlane5C3130(point, normal);
            byte[] bytes = new byte[0x1A8];
            bool[] known = new bool[bytes.Length];
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(0xB8), plane.X);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(0xBA), plane.Y);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(0xBC), plane.Z);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(0xBE), plane.W);
            Array.Fill(known, true, 0xB8, 8);
            return new(current, new(bytes.ToImmutableArray(), known.ToImmutableArray()));
        }

        private NativeBattleShivaModelFrame DrawPlane(NativeBattleShivaModelFrame current, NativeBattleShivaDistortionContext context)
        {
            AcceptModel(current);
            if (!context.Known.Skip(0xB8).Take(8).All(value => value) || !context.Known.Skip(4).Take(4).All(value => value) || BinaryPrimitives.ReadUInt32LittleEndian(context.Bytes.AsSpan().Slice(4)) != NativeBattleEffectNodeMemory.ArenaAddress + 0x33000)
            {
                throw new NotSupportedException("Native Shiva plane drawing requires its constructed plane and actual shared XYZ pointer.");
            }
            NativeBattleShivaPlane plane = new(Word(0xB8), Word(0xBA), Word(0xBC), Word(0xBE));
            NativeBattleShivaPlaneDraw draw = NativeBattleShivaPlaneClipping.Draw5C5060(Frame.Memory.Arena ?? throw new NotSupportedException("Native Shiva plane drawing requires its current mutable XYZ arena."), current.RawModel, resources.Model544, Shared.WorldView ?? throw new NotSupportedException("Native Shiva plane drawing requires its published world/view matrix."), inputs.Projection, plane, resources.ClippingVertexSelectorsD32524, inputs.Zsf3, inputs.Zsf4, inputs.Dqa, inputs.Dqb);
            Frame = Frame with { Memory = Frame.Memory with { Arena = draw.Arena } };
            Emit(new NativeBattleShivaPackets(draw.Batch, false));
            return ModelFrame();

            short Word(int offset) => BinaryPrimitives.ReadInt16LittleEndian(context.Bytes.AsSpan().Slice(offset));
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
            if (operation is NativeBattleShivaModelInitializer { Address: 0x5C5BB0 })
            {
                AcceptEarly(NativeBattleShivaModelParticles.Initialize5C5BB0(EarlyFrame()));
            }
            else if (operation is NativeBattleShivaModelInitializer { Address: 0x5C6FA0 })
            {
                AcceptEarly(NativeBattleShivaScrollingTask.Initialize5C6FA0(EarlyFrame()));
            }
            else if (operation is NativeBattleShivaModelInitializer { Address: 0x5C7180 })
            {
                AcceptEarly(NativeBattleShivaBillboardBanks.Initialize5C7180(EarlyFrame()));
            }
            else if (operation is NativeBattleShivaModelInitializer { Address: 0x5C7570 })
            {
                AcceptFinal(NativeBattleShivaFinalShatter.Initialize5C7570(FinalFrame(), resources.FinalMeshD257CC, resources.FinalMeshD27E6C));
            }
            else
            {
                Emit(new NativeBattleShivaModelService(operation));
            }
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
            Emit(new NativeBattleShivaPackets(packets.Batch, false));
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
            Emit(new NativeBattleShivaSprites(NativeBattleModulePackets.EmitSprite571DC0(draw, inputs.Projection, inputs.ReadCameraPose().Roll, 17, 2)));
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
            SetChildren(Children.WriteSlot(slot, task.UpdateHeader(task.Flags, task.State, current.Next, task.Callback)));
        }

        private uint Append(uint callback)
        {
            NativeBattleEffectTaskPool pool = Children;
            int slot;
            (pool, slot) = pool.Append(callback);
            pool = pool.WriteSlot(slot, pool.Slots[slot].WriteWord(0x0C, 0).WriteWord(0x0E, 0));
            SetChildren(pool);
            return checked(pool.BankAddress + (uint)(slot * pool.Stride));
        }

        private void SetChildren(NativeBattleEffectTaskPool pool) => Frame = Frame with { Effect = Frame.Effect with { Pools = Frame.Effect.Pools.SetItem(1, pool) } };
        private void SetShared(NativeBattleShivaProcess process) => Frame = Frame with { Memory = Frame.Memory with { Shiva = process } };
        private void Emit(NativeBattleShivaOperation operation) => Frame = inputs.Execute(Frame, operation);
    }
}
