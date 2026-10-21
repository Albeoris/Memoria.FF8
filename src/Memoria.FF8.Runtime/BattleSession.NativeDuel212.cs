using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeDuel212(NativeBattleGraphicsState graphics, NativeBattleDuel212Resources resources)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new InvalidDataException("Native Burning Rave requires its entered module.");
        NativeBattleDuel212Frame frame = NativeBattleDuel212Controller.Enter6A0D20(NativeEffectMemory ?? throw new NotSupportedException("Native Burning Rave requires retained effect memory."), effect, NativeFileStaging ?? NativeBattleFileStaging.Unknown(), resources, nativeCrtRandom ?? throw new NotSupportedException("Native Burning Rave requires its CRT random owner."), current =>
        {
            NativeBattleEffectResourcePointer texture = current.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == 0x25101F8) ?? throw new NotSupportedException("Native Burning Rave requires its actual initializer texture pointer.");
            NativeBattleEffectTextureState textures = current.Memory.Textures ?? throw new NotSupportedException("Native Burning Rave requires its retained texture queue.");
            (textures, _) = textures.EnqueueArenaTim505E30(current.Memory.Arena ?? throw new NotSupportedException("Native Burning Rave requires its actual initializer TIM bytes."), texture.ArenaOffset);
            return current with { Memory = current.Memory with { Textures = textures }, Staging = current.Staging.Write(0x25101F8, BitConverter.GetBytes(checked(0x20DFAB8u + (uint)texture.ArenaOffset))) };
        });
        NativeEffectMemory = frame.Memory;
        NativeFileStaging = frame.Staging.Write(0x1181078, resources.ReadActorDat().AsSpan());
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };
    }

    private NativeBattleGraphicsState AdvanceNativeDuel212(NativeBattleGraphicsState graphics)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native Burning Rave requires retained effect memory.");
        NativeBattleDuel212Resources resources = memory.Duel212?.Resources ?? throw new NotSupportedException("Native Burning Rave requires its actual entered resources.");
        NativeBattleEffectParameters parameters = memory.Duel212?.Parameters ?? throw new NotSupportedException("Native Burning Rave requires its entered parameters.");
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native Burning Rave requires its current camera owner.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Native Burning Rave requires its current projection registers.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Burning Rave requires its current view matrix.");
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native Burning Rave requires its authenticated color arithmetic.");
        NativeBattleDuel212Frame frame = new(memory, graphics.OriginalEffect ?? throw new InvalidDataException("Native Burning Rave requires its selected module."), NativeFileStaging ?? throw new NotSupportedException("Native Burning Rave requires retained shared memory."), nativeCrtRandom ?? throw new NotSupportedException("Native Burning Rave requires retained CRT state."));
        NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[parameters.HeaderIndex7C];
        int first = header.FirstContactIndex ?? throw new NotSupportedException("Native Burning Rave requires its actual action contact pointer.");
        int count = header.ReadByte(16);
        ImmutableArray<byte> targets = Enumerable.Range(0, count).Select(index => nativeActionPackets.ReadContact(checked(first + index)).Primary.ActorIndex).ToImmutableArray();
        NativeBattleDuel212PreparationServices preparation = null!;
        NativeBattleDuel212TargetServices actors = null!;
        NativeBattleDuel212Inputs drawing = new(view, Execute);
        preparation = new(index => graphics.Models[index], WriteModel, Anchor, targets, Capture, Bind, Select);
        actors = new(index => graphics.Models[index], WriteModel, Bounds, index => (NativeGraphicsPoseResource(graphics, index).PoseSource ?? throw new NotSupportedException("Native Burning Rave requires its current body skeleton.")).BoneCount, (index, bone, fraction) => ReadNativeGraphicsBone(graphics, index, bone, fraction), Body);
        NativeBattleDuel212SceneInputs scene = new(drawing, (index, bone, fraction) => ReadNativeGraphicsBone(graphics, index, bone, fraction), Anchor);
        frame = NativeBattleDuel212Controller.AdvanceRoot(frame, drawing, Child, ReloadComplete);
        Publish(frame);
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };

        void WriteModel(int index, NativeBattleGraphicsModel model) => graphics = graphics with { Models = graphics.Models.SetItem(index, model) };
        NativeBattleModuleAnchor Anchor(int index) => ReadNativeModelAnchor(graphics, index) with { W = (graphics.Models[index].Transform ?? throw new NotSupportedException("Native571400 requires the actual model WORD36.")).Angle36 };
        (short Maximum, short Minimum) Bounds(int index)
        {
            NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, index);
            return NativeBattleDiablos325Recipients.Bounds6DFD90(graphics.Models[index], resource.PoseSource ?? throw new NotSupportedException("Native6DFD90 requires its original skeleton."), DiablosActorGeometry(resource, graphics.Models[index]));
        }
        void Publish(NativeBattleDuel212Frame current)
        {
            NativeEffectMemory = current.Memory;
            NativeFileStaging = current.Staging;
            graphics = graphics with { OriginalEffect = current.Effect };
        }
        NativeBattleLogicalOrdering Ordering() => (graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Burning Rave requires its current ordering table.")).Pending;
        void StoreOrdering(NativeBattleLogicalOrdering ordering)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Burning Rave requires its current ordering table.");
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = ordering } };
        }
        NativeBattleDuel212Frame Append(NativeBattleDuel212Frame current, NativeBattleGfGeometryBatch batch)
        {
            int cursor = current.Effect.DrawArenaOffset ?? throw new NotSupportedException("Native Burning Rave requires its actual private packet cursor.");
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, cursor, Ordering(), batch);
            StoreOrdering(written.Ordering);
            return current with { Memory = written.Memory, Effect = current.Effect with { DrawArenaOffset = written.Cursor }, Staging = current.Staging.Write(0x2510258, BitConverter.GetBytes(checked(0x20DFAB8u + (uint)written.Cursor))) };
        }
        NativeBattleDuel212Frame Bind(NativeBattleDuel212Frame current, int index, uint row)
        {
            Publish(current);
            NativeBattleGraphicsModel model = RetainNativeDecodedPoseBeforeResourceChange(graphics.Models[index], NativeGraphicsPoseResource(graphics, index));
            var bound = NativeBattleVictoryResources.Bind5022C0(resources.ReadActorDat(), NativeModelCpuResources ?? throw new NotSupportedException("Native Burning Rave requires its actual CPU rows."), model, row, 0x1181078, Read, (address, pointer) => current = current with { Effect = current.Effect with { Pools = NativeBattleEffectTaskMemory.WritePointer(current.Effect.Pools, address, pointer) } });
            WriteModel(index, bound.Model);
            return current;

            NativeBattleResourcePointer? Read(uint address)
            {
                if (NativeBattleEffectTaskMemory.TryReadPointer(current.Effect.Pools, address, out NativeBattleResourcePointer? pointer))
                {
                    return pointer;
                }
                return IsNativeEffectArenaAddress(address) ? current.Memory.Arena!.ReadResourcePointer(NativeBattleWaterExecutionFields.ArenaOffset(address, 4)) : current.Staging.ReadResourcePointer(address);
            }
        }
        NativeBattleDuel212Frame Select(NativeBattleDuel212Frame current, int index, byte sequence)
        {
            Publish(current);
            graphics = SelectNativeHeaderSequence(graphics, checked((byte)index), sequence);
            return current;
        }
        NativeBattleDuel212Frame Body(NativeBattleDuel212Frame current, int index, NativeBattleVector angles, NativeBattleVector pivot)
        {
            Publish(current);
            NativeBattleActionMatrix world = graphics.Models[index].GeometryWorldMatrix ?? throw new NotSupportedException("Native Burning Rave body drawing requires its actual model matrix64.");
            NativeBattleActionMatrix matrix = NativeBattleDuel212Body.Matrix6A2F60(world, view, angles, pivot);
            graphics = DrawNativeEffectModelContext92(graphics, index, matrix, pivot, projection, -47, 4);
            return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Native Burning Rave body drawing lost its physical memory."), Staging = NativeFileStaging ?? throw new InvalidDataException("Native Burning Rave body drawing lost its canonical CPU memory.") };
        }
        NativeBattleDuel212Frame QueueFile(NativeBattleDuel212Frame current, NativeBattleEffectFileRequest request)
        {
            NativeBattleEffectFileAsset file = resources.Files.Single(file => file.FileId == request.FileId);
            NativeBattleEffectArena arena = (current.Memory.Arena ?? throw new NotSupportedException("Native Burning Rave reload requires its physical arena.")).Write(NativeBattleWaterExecutionFields.ArenaOffset(request.DestinationAddress, file.Data.Length), file.Data.AsSpan());
            NativeFileIo = (NativeFileIo ?? throw new NotSupportedException("Native Burning Rave reload requires its actual IO scheduler.")).Queue482610(file.Data.Length, request.CompletionCallback, null);
            return current with { Memory = current.Memory with { Arena = arena } };
        }
        (NativeBattleDuel212Frame Frame, bool Complete) ReloadComplete(NativeBattleDuel212Frame current)
        {
            NativeBattleEffectIoPoll poll = NativeBattleEffectIo.Select5342C0(current.Memory, 1);
            current = current with { Memory = poll.Memory };
            if (poll.Request is { } request)
            {
                current = QueueFile(current, request);
            }
            return (current, !poll.Busy);
        }
        NativeBattleDuel212Step Child(NativeBattleDuel212Frame current, NativeBattleEffectTaskPool pool, int slot)
        {
            return pool.Slots[slot].Callback switch
            {
                0x6A1660 => NativeBattleDuel212Controller.Advance6A1660(current, pool, slot, scene),
                0x6A3750 or 0x6A3800 or 0x6A38B0 => NativeBattleDuel212Controller.AdvanceSprite(current, pool, slot, drawing),
                0x6A30F0 or 0x6A3630 or 0x6A3280 or 0x6A33C0 or 0x6A33E0 => NativeBattleDuel212Controller.AdvanceFragment(current, pool, slot, drawing),
                _ => throw new NotSupportedException("Native Burning Rave selected an unsupported original callback.")
            };
        }
        NativeBattleDuel212Frame Execute(NativeBattleDuel212Frame current, NativeBattleDuel212Operation operation)
        {
            switch (operation)
            {
                case NativeBattleDuel212PrepareScene prepare:
                    return NativeBattleDuel212Preparation.Initialize6A0DC0(current, prepare.Slot, preparation);
                case NativeBattleDuel212Stage stage:
                    NativeBattleStageState retained = NativeStage ?? throw new NotSupportedException("Native Burning Rave requires its current stage groups.");
                    NativeStage = retained with { Groups = retained.Groups.Select(group => group with { ColorDepth = unchecked((short)stage.Intensity), ColorFlags = 0 }).ToImmutableArray() };
                    return current;
                case NativeBattleDuel212Program program:
                    NativeBattleDuel212WorkspaceStep advanced = NativeBattleDuel212Workspaces.Advance701970(current, program, projection, colors, Ordering());
                    StoreOrdering(advanced.Ordering);
                    return advanced.Frame;
                case NativeBattleDuel212Sprite sprite:
                    NativeBattleModulePacketBatch spriteBatch = NativeBattleModulePackets.EmitSprite571DC0(sprite.Draw, projection with { View = view }, camera.Roll, 17, 2);
                    NativeBattleEffectPacketWrite spriteWritten = NativeBattleEffectPacketArena.Append(current.Memory, current.Effect.DrawArenaOffset ?? throw new InvalidDataException("Native Burning Rave sprite lost its packet cursor."), Ordering(), spriteBatch, NativeBattleEffectGouraudWrites.FullDword);
                    StoreOrdering(spriteWritten.Ordering);
                    return current with { Memory = spriteWritten.Memory, Effect = current.Effect with { DrawArenaOffset = spriteWritten.Cursor }, Staging = current.Staging.Write(0x2510258, BitConverter.GetBytes(checked(0x20DFAB8u + (uint)spriteWritten.Cursor))) };
                case NativeBattleDuel212Mesh mesh:
                    NativeBattleModuleMeshTemporaryResult meshDraw = NativeBattleModulePackets.EmitRetainedBatch572200(current.Staging, mesh.TemporaryAddress, mesh.Address, resources.Meshes.Single(source => source.Address == mesh.Address).Bytes, mesh.Matrix, projection, mesh.Context, colors);
                    return Append(current with { Staging = meshDraw.Staging }, meshDraw.Batch);
                case NativeBattleDuel212GroundBirths births:
                    NativeBattleEffectTaskPool fragments = current.Effect.Pools.Single(pool => pool.DescriptorAddress == NativeBattleDuel212Controller.Fragments);
                    NativeBattleDuel212Step spawned = NativeBattleDuel212Controller.Spawn6A3400(current, fragments, 1, births.Origin, 5, 15, 500, 500, 1000, 1000, 0x6A30F0);
                    return spawned.Frame with { Effect = spawned.Frame.Effect with { Pools = spawned.Frame.Effect.Pools.Select(pool => pool.DescriptorAddress == fragments.DescriptorAddress ? spawned.Pool : pool).ToImmutableArray() } };
                case NativeBattleDuel212LateBirths births:
                    return NativeBattleDuel212Controller.SpawnLate6A1660(current, births.Origin, TargetTask(current).ReadWord(12) == 134);
                case NativeBattleDuel212RestoreModels:
                    return NativeBattleDuel212Targets.Restore6A1660(current, count, actors, true);
                case NativeBattleDuel212RestoreTargetVisibility:
                    return NativeBattleDuel212Targets.Restore6A1660(current, count, actors, false);
                case NativeBattleDuel212DrawTargets targetsDraw:
                    return NativeBattleDuel212Targets.Draw6A1660(current, targetsDraw.Tick, count, actors);
                case NativeBattleDuel212Contacts:
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = Enumerable.Range(0, count).Select(index => nativeActionPackets.ReadContact(checked(first + index))).ToImmutableArray() });
                    graphics = graphics with { PendingContacts = pending };
                    return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Native Burning Rave contact lost its current effect memory.") };
                case NativeBattleDuel212Sound sound:
                    PublishNativeModuleSound(new(sound.Descriptor, 0, null) { Pan = 128, RawPan = 128 });
                    return current;
                case NativeBattleDuel212UpdateScene:
                    NativeBattleEffectIoAdvance io = NativeBattleEffectIo.Advance534210(current.Memory);
                    current = current with { Memory = io.Memory };
                    return io.Request is { } request ? QueueFile(current, request) : current;
                case NativeBattleDuel212CloseScene:
                    NativeBattleCameraState active = NativeCamera ?? throw new NotSupportedException("Native Burning Rave closure requires its actual camera owner.");
                    NativeBattleCameraPose captured = active.CapturedPose ?? throw new NotSupportedException("Native Burning Rave closure requires the original503300 camera capture.");
                    NativeCamera = active.WriteEffectGlobals(captured with { Roll = active.Roll }) with { EyePaddingF6 = active.CapturedEyePadding806, TargetPaddingFE = active.CapturedTargetPadding80E };
                    return current;
                case NativeBattleDuel212RestoreParty:
                    for (int index = 0; index < 3; index++)
                    {
                        NativeBattleGraphicsModel model = graphics.Models[index];
                        WriteModel(index, model with { Flags0 = (ushort)(model.Flags0 & ~4) });
                    }
                    return current;
                default:
                    return ExecuteScene(current, operation);
            }
        }
        NativeBattleDuel212Frame ExecuteScene(NativeBattleDuel212Frame current, NativeBattleDuel212Operation operation)
        {
            int actor = parameters.Actor78;
            NativeBattleModulePoolTask task = TargetTask(current);
            switch (operation)
            {
                case NativeBattleDuel212RingProgram ring:
                    NativeBattleModelTransform transform = graphics.Models[actor].Transform ?? throw new NotSupportedException("Native Burning Rave ring requires its actual caster position.");
                    NativeBattleActionMatrix basis = NativeBattleGfMatrices.Euler701310(new(0, 0, 0)) with { X = transform.Position28.X - 195, Z = transform.Position28.Z - 1167 };
                    NativeBattleDuel212Program program = new(0x1184ACC, 2720, 560, NativeBattleGfMatrices.Compose56C2F0(view, basis), "0000", null, null, -256, 0, 0, ring.Tick == 84);
                    return Execute(current, program);
                case NativeBattleDuel212BodyProgramTransition transition:
                    if (transition.Tick < 109)
                    {
                        current = Body(current, actor, new(0, 0, 0), new(0, 0, 0));
                    }
                    else if (transition.Tick == 109)
                    {
                        NativeBattleThundaga105Bounds firstBounds = NativeBattleDuel212Preparation.Bounds69A700(resources, checked(0x118BA10u + resources.Dword(0x118BA18)));
                        NativeBattleThundaga105Bounds secondBounds = NativeBattleDuel212Preparation.Bounds69A700(resources, checked(0x118BA10u + resources.Dword(0x118BA1C)));
                        current = Capture(current, new(unchecked((short)task.ReadWord(328)), 0, unchecked((short)task.ReadWord(332))), firstBounds with { Maximum = firstBounds.Maximum with { X = secondBounds.Maximum.X } });
                    }
                    NativeBattleGraphicsModel hidden = graphics.Models[actor];
                    WriteModel(actor, hidden with { Flags0 = (ushort)(hidden.Flags0 | 4) });
                    return current;
                case NativeBattleDuel212Pulse pulse:
                    NativeBattleGraphicsModel caster = graphics.Models[actor];
                    NativeBattlePoseSource pose = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Native Burning Rave pulse requires its actual current skeleton.");
                    ImmutableArray<NativeBattleActionMatrix> bones = NativeBattleBodyBoneMatrices.Require(caster, pose);
                    if (bones.Length <= 18)
                    {
                        throw new InvalidDataException("Native Burning Rave pulse requires original skeleton matrix at byte896.");
                    }
                    NativeBattleDuel212HierarchyStep drawn = NativeBattleDuel212PulseDrawing.Draw6A1660(current, pulse.Phase, bones[18], caster.GeometryWorldMatrix ?? throw new NotSupportedException("Native Burning Rave pulse requires its actual model matrix64."), view, projection, colors);
                    return Append(drawn.Frame, drawn.Batch);
                case NativeBattleDuel212BoneEmission emission:
                    _ = ReadNativeGraphicsBone(graphics, actor, emission.First, 0);
                    _ = ReadNativeGraphicsBone(graphics, actor, emission.Second, 0);
                    NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Native Burning Rave seed requires its current skeleton.");
                    NativeBattleGraphicsModel published = NativeBattleBodyBoneMatrices.Publish508C90(graphics.Models[actor], source);
                    WriteModel(actor, published);
                    int offset = checked((int)(emission.PointAddress - 0x11AC200));
                    ImmutableArray<byte> seed = resources.SelectorTable.AsSpan().Slice(offset, 8).ToArray().ToImmutableArray();
                    short bone = BinaryPrimitives.ReadInt16LittleEndian(seed.AsSpan().Slice(6));
                    ImmutableArray<NativeBattleActionMatrix> matrices = NativeBattleBodyBoneMatrices.Require(published, source);
                    if ((uint)bone >= matrices.Length)
                    {
                        throw new InvalidDataException("Native Burning Rave seed selects an absent original bone.");
                    }
                    NativeBattleActionMatrix seedMatrix = NativeBattleGfMatrices.Compose56C2F0(published.GeometryWorldMatrix ?? throw new NotSupportedException("Native Burning Rave seed requires its actual model matrix64."), matrices[bone]);
                    NativeBattleVector point = NativeBattleGfMatrices.Transform460840(seedMatrix, new(BinaryPrimitives.ReadInt16LittleEndian(seed.AsSpan()), BinaryPrimitives.ReadInt16LittleEndian(seed.AsSpan().Slice(2)), BinaryPrimitives.ReadInt16LittleEndian(seed.AsSpan().Slice(4))));
                    point = point with { Y = unchecked((short)(point.Y - 100)) };
                    current = current with { Random = current.Random.Draw(out ushort speed) };
                    NativeBattleEffectTaskPool sprites = current.Effect.Pools.Single(pool => pool.DescriptorAddress == NativeBattleDuel212Controller.Sprites);
                    NativeBattleDuel212Step spawned = NativeBattleDuel212Controller.SpawnSprites(current, sprites, 1, 16, point, 100, -50, 0, unchecked((sbyte)(-4 - (speed & 3))), 0, 255, 800, 0x6A3750);
                    return spawned.Frame with { Effect = spawned.Frame.Effect with { Pools = spawned.Frame.Effect.Pools.Select(pool => pool.DescriptorAddress == sprites.DescriptorAddress ? spawned.Pool : pool).ToImmutableArray() } };
                case NativeBattleDuel212Camera update:
                    NativeBattleCameraState active = NativeCamera ?? throw new NotSupportedException("Native Burning Rave requires its actual camera owner.");
                    if (update.Tick == 1)
                    {
                        active = active.Apply50A730();
                    }
                    if (update.Tick == 2)
                    {
                        for (int index = 0; index < 7; index++)
                        {
                            NativeBattleGraphicsModel model = graphics.Models[index];
                            if (index < 3 || (model.Flags0 & 2) != 0 || targets.Contains(checked((byte)index)))
                            {
                                WriteModel(index, model with { Flags0 = (ushort)(model.Flags0 | 4) });
                            }
                        }
                        NativeBattleGraphicsModel visible = graphics.Models[actor];
                        WriteModel(actor, visible with { Flags0 = (ushort)(visible.Flags0 & ~4) });
                    }
                    int phase = update.Tick - 81;
                    if ((uint)phase < 6)
                    {
                        int shake = (100 * NativeBattleCameraMath.Cosine(341 * phase + 170)) >> 12;
                        NativeBattleCameraViewTranslationScratch scratch = active.ViewTranslationScratch ?? throw new NotSupportedException("Native Burning Rave camera shake requires its actual view-translation owner.");
                        active = active with { ViewTranslationScratch = scratch with { Y = unchecked((short)((phase & 1) != 0 ? -shake : shake)) } };
                    }
                    NativeCamera = NativeBattleDuel212Controller.AdvanceCamera6A1660(active, update.Tick, Word(0x2510260), Word(0x2510264), Word(0x2510268), Word(0x251026C));
                    return current;
                case NativeBattleDuel212DrawEcho:
                    var echo = DrawNativeDuel212Echo(current, graphics, actor, view, projection);
                    graphics = echo.Graphics;
                    return echo.Frame;
                case NativeBattleDuel212CaptureEcho:
                    NativeBattlePoseSource echoSource = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Native Burning Rave echo requires its current skeleton.");
                    ImmutableArray<NativeBattleActionMatrix> currentBones = NativeBattleBodyBoneMatrices.Require(graphics.Models[actor], echoSource);
                    NativeBattleModulePoolTask captured = NativeBattleDuelEcho.Capture68ADF0(task, 336, echoSource.BoneCount, index =>
                    {
                        NativeBattleActionMatrix matrix = currentBones[index];
                        byte[] bytes = new byte[32];
                        bool[] known = Enumerable.Repeat(true, 32).ToArray();
                        for (int lane = 0; lane < 9; lane++)
                        {
                            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(lane * 2), matrix.Rotation[lane]);
                        }
                        known[18] = false;
                        known[19] = false;
                        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20), matrix.X);
                        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(24), matrix.Y);
                        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(28), matrix.Z);
                        return (bytes, known);
                    });
                    return current with { Effect = current.Effect with { Pools = current.Effect.Pools.Select(pool => pool.DescriptorAddress == NativeBattleDuel212Controller.Targets ? pool with { Slots = pool.Slots.SetItem(0, captured) } : pool).ToImmutableArray() } };
                default:
                    throw new NotSupportedException("Native Burning Rave selected an unsupported original scene operation.");
            }

            short Word(uint address) => BinaryPrimitives.ReadInt16LittleEndian(current.Staging.Read(address, 2));
        }
        NativeBattleDuel212Frame Capture(NativeBattleDuel212Frame current, NativeBattleVector point, NativeBattleThundaga105Bounds bounds)
        {
            NativeBattleThundaga105CaptureTransform transform = NativeBattleDuel212Capture.Transform6A12B0(point, bounds, projection, camera.FocalLength ?? throw new NotSupportedException("Native Burning Rave capture requires its actual focal WORD."));
            NativeBattleDuel212CaptureStep result = NativeBattleDuel212Capture.Append6A12B0(current, Ordering(), transform, DrawGround);
            StoreOrdering(result.Ordering);
            return result.Frame;
        }
        NativeBattleDuel212CaptureStep DrawGround(NativeBattleDuel212Frame current, NativeBattleThundaga105CaptureTransform transform, NativeBattleLogicalOrdering ordering)
        {
            NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native Burning Rave capture requires its actual stage groups.");
            NativeBattleStageGroup group = stage.Groups[1];
            if ((group.Flags & 1) == 0)
            {
                return new(current, ordering);
            }
            NativeBattleTemporaryAllocation temporary = NativeBattleTemporaryMemory.Allocate5082B0(current.Staging, 44);
            uint context = temporary.Address;
            int capacity = checked((NativeModelCpuResources?.ScratchMaximum ?? throw new NotSupportedException("Native Burning Rave capture requires its actual CPU workspace.")) * 8);
            uint address = checked(0x196B018u - (uint)capacity);
            NativeBattleFileStaging prepared = temporary.Staging.Write(context + 4, BitConverter.GetBytes(address)).Write(context + 16, current.Staging.Read(0x251025C, 4));
            prepared = prepared.Write(context + 20, BitConverter.GetBytes((short)0)).Write(context + 22, BitConverter.GetBytes((short)0)).Write(context + 24, BitConverter.GetBytes((short)320)).Write(context + 26, BitConverter.GetBytes((short)216));
            prepared = prepared.Write(context + 28, BitConverter.GetBytes(group.ColorFlags)).Write(context + 32, BitConverter.GetBytes(uint.MaxValue)).Write(context + 36, BitConverter.GetBytes(group.ColorDepth)).Write(context + 40, new byte[4]);
            current = current with { Staging = prepared };
            NativeBattlePoseSource source = group.Source ?? throw new NotSupportedException("Native Burning Rave capture requires the current ground skeleton.");
            NativeBattleDecodedPose pose = source.Sample(group.Animation, group.Counter, group.PoseFlags);
            group = group with { Matrices = NativeBattlePoseMatrices.ComposeStageCamera(NativeBattlePoseMatrices.StageBoneMatrices(source, pose), transform.Matrix, 0) };
            NativeStage = stage with { Groups = stage.Groups.SetItem(1, group) };
            NativeBattlePrimitiveProcess process = current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Burning Rave capture requires its current primitive process.");
            var gate = process.Enter5099D0(definition.NativeGraphics?.PrimitiveCapabilities?.HardwareEnabled ?? throw new NotSupportedException("Native Burning Rave capture requires its actual renderer capability."));
            current = current with { Memory = current.Memory with { PrimitiveProcess = gate.Process } };
            if (!gate.Draw)
            {
                return new(current with { Staging = NativeBattleTemporaryMemory.Release5082D0(current.Staging, 44) }, ordering);
            }
            ImmutableArray<NativeBattleRawMesh> meshes = RequireNativeStageGeometry().Where(mesh => mesh.StageGroup == 1).ToImmutableArray();
            (byte[] bytes, _) = current.Staging.ReadSnapshot(address, capacity);
            NativeBattleRawModelBodyDraw drawn = NativeBattleRawModelBodyDrawing.Draw5099D0(meshes, group.Matrices, transform.Projection, group.ColorFlags, uint.MaxValue, bytes.ToImmutableArray(), colors, 16, new(0, 0, 320, 216), 17, group.ColorDepth);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, current.Effect.DrawArenaOffset ?? throw new InvalidDataException("Native Burning Rave capture lost its cursor."), ordering, drawn.Packets);
            NativeBattleFileStaging staging = current.Staging.Write(address, drawn.ProjectedScratch.AsSpan().Slice(0, drawn.ProjectedBytesWritten)).Write(0x2510258, BitConverter.GetBytes(checked(0x20DFAB8u + (uint)written.Cursor)));
            current = current with { Memory = written.Memory with { PrimitiveProcess = gate.Process.PublishModelColor(group.ColorFlags).Publish50FDF0(checked(0x20DFAB8u + (uint)written.Cursor), null) }, Staging = staging, Effect = current.Effect with { DrawArenaOffset = written.Cursor } };
            return new(current with { Staging = NativeBattleTemporaryMemory.Release5082D0(current.Staging, 44) }, written.Ordering);
        }
        NativeBattleModulePoolTask TargetTask(NativeBattleDuel212Frame current) => current.Effect.Pools.Single(pool => pool.DescriptorAddress == NativeBattleDuel212Controller.Targets).Slots[0];
    }
}
