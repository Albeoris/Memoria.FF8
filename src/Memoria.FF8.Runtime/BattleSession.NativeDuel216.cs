using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeDuel216(NativeBattleGraphicsState graphics, NativeBattleDuel216Resources resources)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new InvalidDataException("Native Different Beat requires its entered module.");
        NativeBattleEffectParameters parameters = effect.Parameters ?? throw new InvalidDataException("Native Different Beat requires its entered parameters.");
        NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[parameters.HeaderIndex7C];
        int first = header.FirstContactIndex ?? throw new NotSupportedException("Native Different Beat requires its actual first contact pointer.");
        byte recipient = nativeActionPackets.ReadContact(first).Primary.ActorIndex;
        uint minimum = NativeBattleScanModelSelection.Minimum501FF0(NativeBattleScanPhysicalModels.Capture(graphics.Models), NativeBattleDuel274Controller.Address(recipient));
        NativeBattleDuel216Frame frame = NativeBattleDuel216Controller.Enter695A10(NativeEffectMemory ?? throw new NotSupportedException("Native Different Beat requires retained effect memory."), effect, NativeFileStaging ?? throw new NotSupportedException("Native Different Beat requires retained shared memory."), resources, nativeCrtRandom ?? throw new NotSupportedException("Native Different Beat requires its retained CRT random owner."), NativeBattleDuel274Controller.Index(minimum));
        NativeEffectMemory = frame.Memory;
        NativeFileStaging = frame.Staging.Write(NativeBattleDuel216Resources.ActorDirectory, resources.ReadActorDat().AsSpan());
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };
    }

    private NativeBattleGraphicsState AdvanceNativeDuel216(NativeBattleGraphicsState graphics)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native Different Beat requires retained effect memory.");
        NativeBattleDuel216Resources resources = memory.Duel216?.Resources ?? throw new NotSupportedException("Native Different Beat requires its original entered resources.");
        NativeBattleEffectParameters parameters = memory.Duel216?.Parameters ?? throw new NotSupportedException("Native Different Beat requires its entered parameters.");
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native Different Beat requires its actual camera owner.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Native Different Beat requires its actual projection registers.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Different Beat requires its actual view matrix.");
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native Different Beat requires its authenticated color arithmetic.");
        NativeBattleDuel216Frame frame = new(memory, graphics.OriginalEffect ?? throw new InvalidDataException("Native Different Beat requires its selected module."), NativeFileStaging ?? throw new NotSupportedException("Native Different Beat requires its retained shared memory."), nativeCrtRandom ?? throw new NotSupportedException("Native Different Beat requires its retained CRT random owner."));
        int target = NativeBattleDuel274Controller.Index(BinaryPrimitives.ReadUInt32LittleEndian(frame.Staging.Read(0x2508884, 4)));
        ImmutableArray<byte>.Builder ring = ImmutableArray.CreateBuilder<byte>();
        int? node = target;
        while (node is int index)
        {
            if (ring.Contains((byte)index))
            {
                if (index != target)
                {
                    throw new InvalidDataException("Native Different Beat target ring repeats outside its starting row.");
                }
                break;
            }
            ring.Add(checked((byte)index));
            node = graphics.Models[index].ReadLinkedModelIndex140();
        }
        NativeBattleDuel216PreparationServices preparation = new(index => graphics.Models[index], WriteModel, Anchor, Capture, Bind, Select, Bounds, RestoreFocal);
        NativeBattleDuel216SceneServices scene = new(index => graphics.Models[index], WriteModel, Anchor, (index, bone) => ReadNativeGraphicsBone(graphics, index, checked((byte)bone), 0), ring.ToImmutable(), view, ExecuteScene);
        NativeBattleDuel216ParticleServices particles = new(Sprite, Mesh);
        frame = NativeBattleDuel216Controller.Advance695AB0(frame, new(Execute), Child);
        Publish(frame);
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };

        void WriteModel(int index, NativeBattleGraphicsModel model) => graphics = graphics with { Models = graphics.Models.SetItem(index, model) };
        NativeBattleModuleAnchor Anchor(int index) => ReadNativeModelAnchor(graphics, index) with { W = (graphics.Models[index].Transform ?? throw new NotSupportedException("Native Different Beat requires actual model WORD36.")).Angle36 };
        void Publish(NativeBattleDuel216Frame current)
        {
            NativeEffectMemory = current.Memory;
            NativeFileStaging = current.Staging;
            graphics = graphics with { OriginalEffect = current.Effect };
        }
        NativeBattleLogicalOrdering Ordering() => (graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Different Beat requires its current ordering table.")).Pending;
        void StoreOrdering(NativeBattleLogicalOrdering ordering)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Different Beat requires its current ordering table.");
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = ordering } };
        }
        NativeBattleDuel216Frame Append(NativeBattleDuel216Frame current, NativeBattleGfGeometryBatch batch, ushort flags = 0)
        {
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, current.Effect.DrawArenaOffset ?? throw new NotSupportedException("Native Different Beat requires its actual private packet cursor."), Ordering(), batch, flags);
            StoreOrdering(written.Ordering);
            return current with { Memory = written.Memory, Effect = current.Effect with { DrawArenaOffset = written.Cursor }, Staging = current.Staging.Write(0x2508874, BitConverter.GetBytes(checked(NativeBattleDuel216Controller.Arena + (uint)written.Cursor))) };
        }
        NativeBattleDuel216Frame Bind(NativeBattleDuel216Frame current, int index, uint row)
        {
            Publish(current);
            NativeBattleGraphicsModel model = RetainNativeDecodedPoseBeforeResourceChange(graphics.Models[index], NativeGraphicsPoseResource(graphics, index));
            var bound = NativeBattleVictoryResources.Bind5022C0(resources.ReadActorDat(), NativeModelCpuResources ?? throw new NotSupportedException("Native Different Beat requires its actual CPU resource rows."), model, row, NativeBattleDuel216Resources.ActorDirectory, Read, (address, pointer) => current = current with { Effect = current.Effect with { Pools = NativeBattleEffectTaskMemory.WritePointer(current.Effect.Pools, address, pointer) } });
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
        NativeBattleDuel216Frame Select(NativeBattleDuel216Frame current, int index, byte sequence)
        {
            Publish(current);
            graphics = SelectNativeHeaderSequence(graphics, checked((byte)index), sequence);
            return current;
        }
        NativeBattleDuel216PreparationBounds Bounds(NativeBattleDuel216Frame current, int index)
        {
            NativeBattleGraphicsModel model = graphics.Models[index];
            NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, index);
            NativeBattleScriptedGraphicsMemory controls = current.Memory.ScriptedGraphics ?? throw new NotSupportedException("Native Different Beat bounds require actual depth-cue controls.");
            NativeBattleScriptedColorRegisters registers = current.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native Different Beat bounds require their actual packed V0 owner.");
            NativeBattleDuel216BoundsStep result = NativeBattleDuel216Bounds.Read695F60(current.Staging, model, resource.PoseSource ?? throw new NotSupportedException("Native Different Beat bounds require their current skeleton."), DiablosActorGeometry(resource, model), (matrix, vertex) =>
            {
                byte[] input = registers.NormalInput?.ToArray() ?? new byte[8];
                bool[] known = registers.NormalInputKnown?.ToArray() ?? Enumerable.Repeat(registers.NormalInput is not null, 8).ToArray();
                BinaryPrimitives.WriteInt16LittleEndian(input, vertex.Position.X);
                BinaryPrimitives.WriteInt16LittleEndian(input.AsSpan(2), vertex.Position.Y);
                BinaryPrimitives.WriteInt16LittleEndian(input.AsSpan(4), vertex.Position.Z);
                Array.Fill(known, true, 0, 6);
                NativeBattleModuleDepthCueVertex projected = NativeBattleModuleProjection.ProjectWithCue45FAA0(matrix, vertex.Position, projection, controls.Dqa, controls.Dqb);
                registers = registers with { NormalInput = input.ToImmutableArray(), NormalInputKnown = known.ToImmutableArray(), Mac = [null, null, null], Ir = [null, projected.Ir2, null], Flags = projected.Projected.Flags };
                return projected.Ir2;
            });
            WriteModel(index, result.Model);
            return new(current with { Staging = result.Staging, Memory = current.Memory with { ScriptedColorRegisters = registers } }, result.Maximum, result.Minimum);
        }
        NativeBattleDuel216Frame RestoreFocal(NativeBattleDuel216Frame current)
        {
            NativeBattleCameraState active = NativeCamera ?? throw new NotSupportedException("Native Different Beat closure requires its current camera.");
            short focal = active.FocalLength ?? throw new NotSupportedException("Native Different Beat closure requires its actual base focal WORD.");
            NativeCamera = active with { Projection = (active.Projection ?? throw new NotSupportedException("Native Different Beat closure requires its current H register.")) with { FocalLength = unchecked((ushort)focal) } };
            return current with { Staging = current.Staging.Write(0x1D8E038, BitConverter.GetBytes(focal)) };
        }
        NativeBattleDuel216Frame Capture(NativeBattleDuel216Frame current, NativeBattleModuleAnchor anchor, NativeBattleDuel216Extents bounds)
        {
            NativeBattleDuel216CameraTransform transform = NativeBattleDuel216Camera.Transform69A790(anchor, bounds, projection, camera.FocalLength ?? throw new NotSupportedException("Native Different Beat capture requires its actual focal WORD."));
            NativeBattleDuel216CaptureStep captured = NativeBattleDuel216Capture.Append69A790(current, Ordering(), transform, ResetStage, DrawNativeDuel216CaptureGround);
            StoreOrdering(captured.Ordering);
            return captured.Frame;
        }
        NativeBattleDuel216Step Child(NativeBattleDuel216Frame current, NativeBattleEffectTaskPool pool, int slot)
        {
            switch (pool.Slots[slot].Callback)
            {
                case 0x6960E0:
                    return NativeBattleDuel216Scene.Advance6960E0(current, pool, slot, scene);
                case 0x69A5D0:
                    NativeBattleDuel216HierarchyTaskStep hierarchy = NativeBattleDuel216Workspaces.Advance69A5D0(current, pool, slot, view, projection, colors, Ordering());
                    StoreOrdering(hierarchy.Ordering);
                    return hierarchy.Step;
                case 0x69A060:
                    return NativeBattleDuel216Particles.Advance69A060(current, pool, slot, particles);
                case 0x69A200:
                    return NativeBattleDuel216Particles.Advance69A200(current, pool, slot, particles);
                case 0x69A3F0:
                case 0x69A530:
                    return NativeBattleDuel216Particles.Advance69A410(current, pool, slot, particles);
                default:
                    throw new NotSupportedException("Native Different Beat selected an unsupported registered callback.");
            }
        }
        NativeBattleDuel216Frame ResetStage(NativeBattleDuel216Frame current)
        {
            Stage(0);
            return current;
        }
        void Stage(int intensity)
        {
            NativeBattleStageState retained = NativeStage ?? throw new NotSupportedException("Native Different Beat requires its actual stage color owners.");
            NativeStage = retained with { Groups = retained.Groups.Select(group => group with { ColorDepth = unchecked((short)intensity), ColorFlags = 0 }).ToImmutableArray() };
        }
        NativeBattleDuel216Frame Execute(NativeBattleDuel216Frame current, NativeBattleDuel216Operation operation)
        {
            switch (operation)
            {
                case NativeBattleDuel216PrepareMain prepare:
                    return NativeBattleDuel216Preparation.Prepare695AB0(current, prepare.Slot, preparation);
                case NativeBattleDuel216NormalizeBillboard:
                    return current with { Staging = NormalizeBillboard(current.Staging) };
                case NativeBattleDuel216StageIntensity intensity:
                    Stage(intensity.Current);
                    if (intensity.Current >= 4096 || intensity.Previous >= 4096)
                    {
                        NativeStage = NativeStage! with { Groups = NativeStage!.Groups.Select(group => group with { ColorFlags = intensity.Current >= 4096 ? (byte)(group.ColorFlags & ~2) : (byte)(group.ColorFlags | 2) }).ToImmutableArray() };
                    }
                    return current;
                case NativeBattleDuel216RestoreScene:
                    return NativeBattleDuel216Preparation.Restore695AB0(current, preparation);
                case NativeBattleDuel216File file:
                    NativeBattleEffectFileAsset asset = resources.Files.Single(candidate => candidate.FileId == file.Request.FileId);
                    NativeBattleEffectArena arena = (current.Memory.Arena ?? throw new NotSupportedException("Native Different Beat reload requires its physical arena.")).Write(NativeBattleWaterExecutionFields.ArenaOffset(file.Request.DestinationAddress, asset.Data.Length), asset.Data.AsSpan());
                    NativeFileIo = (NativeFileIo ?? throw new NotSupportedException("Native Different Beat reload requires its actual IO scheduler.")).Queue482610(asset.Data.Length, file.Request.CompletionCallback, null);
                    return current with { Memory = current.Memory with { Arena = arena } };
                default:
                    throw new NotSupportedException("Native Different Beat selected an unsupported root operation.");
            }
        }
        NativeBattleDuel216Frame ExecuteScene(NativeBattleDuel216Frame current, NativeBattleDuel216SceneOperation operation)
        {
            switch (operation)
            {
                case NativeBattleDuel216ProgramDraw program:
                    NativeBattleDuel216WorkspaceStep advanced = NativeBattleDuel216Workspaces.Advance701970(current, NativeBattleDuel216Controller.Main, program.Slot, program.Offset, program.Size, program.Context with { Billboard = NativeBattleGfMatrices.Normalize67AAE0(view) }, projection, colors, Ordering());
                    StoreOrdering(advanced.Ordering);
                    return advanced.Frame;
                case NativeBattleDuel216MorphDraw morph:
                    NativeBattleDuel216DrawingStep drawn = NativeBattleDuel216Drawing.DrawMorph6960E0(current, morph.Phase, morph.Divisor, morph.Fade, ReadNativeGraphicsBone(graphics, parameters.Actor78, 20, 0), view, projection, colors);
                    return Append(drawn.Frame, drawn.Batch);
                case NativeBattleDuel216Spawn69A550 birth:
                    return NativeBattleDuel216Workspaces.Spawn69A550(current, Position(parameters.Actor78), birth.X, birth.Y, birth.Z, birth.Scale);
                case NativeBattleDuel216Spawn69A110 birth:
                    return StoreBirth(NativeBattleDuel216Particles.Spawn69A110(current, Bank(current, NativeBattleDuel216Controller.Small), birth.Count, new(birth.X, birth.Y, birth.Z), _ => Position(parameters.Actor78)));
                case NativeBattleDuel216Spawn69A2D0 birth:
                    return StoreBirth(NativeBattleDuel216Particles.Spawn69A2D0(current, Bank(current, NativeBattleDuel216Controller.Particles), birth.Count, new(birth.X, birth.Y, birth.Z), birth.MinimumDuration, unchecked((sbyte)birth.MaximumDuration), birth.MinimumX, birth.MaximumX, birth.MinimumZ, unchecked((short)birth.MaximumZ)));
                case NativeBattleDuel216Spawn699F80 birth:
                    return StoreBirth(NativeBattleDuel216Particles.Spawn699F80(current, Bank(current, NativeBattleDuel216Controller.Small), birth.Count, new(birth.X, birth.Y, birth.Z), birth.MinimumX, birth.MaximumX, birth.MinimumZ, unchecked((short)birth.MaximumZ)));
                case NativeBattleDuel216Sequence sequence:
                    return Select(current, sequence.Model, sequence.Sequence);
                case NativeBattleDuel216BoundsUpdate bounds:
                    NativeBattleDuel216PreparationBounds measured = Bounds(current, bounds.Model);
                    NativeBattleEffectTaskPool main = Bank(measured.Frame, NativeBattleDuel216Controller.Main);
                    main = main.WriteSlot(bounds.Slot, main.Slots[bounds.Slot].WriteWord(6108, unchecked((ushort)measured.Maximum)).WriteWord(6110, unchecked((ushort)measured.Minimum)));
                    return StoreBank(measured.Frame, main);
                case NativeBattleDuel216BodyDraw body:
                    Publish(current);
                    NativeBattleActionMatrix world = graphics.Models[body.Model].GeometryWorldMatrix ?? throw new NotSupportedException("Native Different Beat body draw requires its actual model matrix64.");
                    NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(body.Rotation) with { X = body.Pivot.X, Y = body.Pivot.Y, Z = body.Pivot.Z };
                    NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(world, local);
                    matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, body.Scale.X, body.Scale.Y, body.Scale.Z);
                    matrix = NativeBattleGfMatrices.Compose56C2F0(view, matrix);
                    (int x, int y, int z) = NativeBattleGfMatrices.Rotate56C3D0(matrix, body.Pivot);
                    matrix = matrix with { X = unchecked(matrix.X - x), Y = unchecked(matrix.Y - y), Z = unchecked(matrix.Z - z) };
                    graphics = DrawNativeEffectModelContext92(graphics, body.Model, matrix, body.Pivot, projection, 4112, 16);
                    return current with { Memory = NativeEffectMemory!, Staging = NativeFileStaging! };
                case NativeBattleDuel216EchoDraw:
                    var echo = DrawNativeDuel216Echo(current, graphics, parameters.Actor78, view, projection);
                    graphics = echo.Graphics;
                    return echo.Frame;
                case NativeBattleDuel216EchoCapture capture:
                    return CaptureEcho(current, capture.Slot);
                case NativeBattleDuel216Flash699EE0 flash:
                    return Append(current, NativeBattleDuel216Drawing.Flash699EE0(flash.Phase), 1);
                case NativeBattleDuel216Sound501330:
                    PublishNativeModuleSound(new(resources.Dword(0x113C1A8), 0, null) { Pan = 128, RawPan = 128 });
                    return current;
                case NativeBattleDuel216Contact506690:
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [NativeBattleDuel216Contacts.Read506690(nativeActionPackets, parameters.HeaderIndex7C)] });
                    graphics = graphics with { PendingContacts = pending };
                    return current with { Memory = NativeEffectMemory! };
                case NativeBattleDuel216MainCamera update:
                    NativeBattleDuel216CameraStep cameraStep = NativeBattleDuel216Camera.AdvanceMain6960E0(current, unchecked((ushort)update.Tick), NativeCamera ?? throw new NotSupportedException("Native Different Beat requires its current camera owner."));
                    NativeCamera = cameraStep.Camera;
                    return cameraStep.Frame;
                case NativeBattleDuel216MainFile file:
                    return current with { Memory = NativeBattleEffectIo.Queue5341D0(current.Memory, file.File, file.Destination, file.Type) };
                case NativeBattleDuel216MainStageReset:
                    return ResetStage(current);
                default:
                    throw new NotSupportedException("Native Different Beat selected an unsupported scene operation.");
            }
        }
        NativeBattleFileStaging NormalizeBillboard(NativeBattleFileStaging staging)
        {
            NativeBattleActionMatrix billboard = NativeBattleGfMatrices.Normalize67AAE0(view);
            for (int lane = 0; lane < 9; lane++)
            {
                staging = staging.Write(0x2508910u + (uint)(lane * 2), BitConverter.GetBytes(billboard.Rotation[lane]));
            }
            return staging.Write(0x2508924, BitConverter.GetBytes(billboard.X)).Write(0x2508928, BitConverter.GetBytes(billboard.Y)).Write(0x250892C, BitConverter.GetBytes(billboard.Z));
        }
        NativeBattleVector Position(int index) => (graphics.Models[index].Transform ?? throw new NotSupportedException("Native Different Beat requires actual model position WORDs.")).Position28;
        NativeBattleEffectTaskPool Bank(NativeBattleDuel216Frame current, uint descriptor) => current.Effect.Pools.Single(pool => pool.DescriptorAddress == descriptor);
        NativeBattleDuel216Frame StoreBank(NativeBattleDuel216Frame current, NativeBattleEffectTaskPool pool) => current with { Effect = current.Effect with { Pools = current.Effect.Pools.Select(bank => bank.DescriptorAddress == pool.DescriptorAddress ? pool : bank).ToImmutableArray() } };
        NativeBattleDuel216Frame StoreBirth(NativeBattleDuel216ParticleBirth birth) => StoreBank(birth.Frame, birth.Pool);
        NativeBattleDuel216Frame Mesh(NativeBattleDuel216Frame current, NativeBattleDuel216MeshParticle particle)
        {
            NativeBattleDuel216DrawingStep drawn = NativeBattleDuel216Drawing.Draw69A410(current, particle, view, projection, colors);
            return Append(drawn.Frame, drawn.Batch);
        }
        NativeBattleDuel216Frame Sprite(NativeBattleDuel216Frame current, NativeBattleDuel216BillboardParticle sprite)
        {
            NativeBattleModuleResource source = new(0, sprite.Source, 0, 0, resources.Read(sprite.Source, checked((int)(NativeBattleDuel216Resources.EndAddress - sprite.Source))));
            NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = source, SpriteFrame = source.ProjectedFrame571C80(sprite.Phase), Phase = unchecked((ushort)sprite.Phase), Anchor = new(sprite.Point.X, sprite.Point.Y, sprite.Point.Z, 0), Scale = sprite.Scale, DepthOffset = sprite.DepthDisplacement, SpriteFlags = 0 };
            NativeBattleTemporaryAllocation temporary = NativeBattleTemporaryMemory.Allocate5082B0(current.Staging, 180);
            NativeBattleFileStaging staging = NativeBattleModuleSpriteTemporary.Begin571C80(temporary.Staging, temporary.Address, sprite.Source, 0).Write(temporary.Address + 4, BitConverter.GetBytes(sprite.Phase));
            NativeBattleModuleSpriteTemporaryResult drawn = NativeBattleModuleSpriteTemporary.Emit571C80(staging, temporary.Address, draw, projection with { View = view }, camera.Roll, 17, 2);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, current.Effect.DrawArenaOffset ?? throw new NotSupportedException("Native Different Beat sprite requires its private cursor."), Ordering(), drawn.Batch, NativeBattleEffectGouraudWrites.FullDword);
            StoreOrdering(written.Ordering);
            staging = drawn.Staging.Write(0x2508874, BitConverter.GetBytes(checked(NativeBattleDuel216Controller.Arena + (uint)written.Cursor)));
            return current with { Memory = written.Memory, Effect = current.Effect with { DrawArenaOffset = written.Cursor }, Staging = NativeBattleTemporaryMemory.Release5082D0(staging, 180) };
        }
        NativeBattleDuel216Frame CaptureEcho(NativeBattleDuel216Frame current, int slot)
        {
            NativeBattlePoseSource pose = NativeGraphicsPoseResource(graphics, parameters.Actor78).PoseSource ?? throw new NotSupportedException("Native Different Beat echo requires its current skeleton.");
            ImmutableArray<NativeBattleActionMatrix> bones = NativeBattleBodyBoneMatrices.Require(graphics.Models[parameters.Actor78], pose);
            NativeBattleEffectTaskPool main = Bank(current, NativeBattleDuel216Controller.Main);
            NativeBattleModulePoolTask captured = NativeBattleDuelEcho.Capture697850(main.Slots[slot], 3668, pose.BoneCount, index =>
            {
                NativeBattleActionMatrix matrix = bones[index];
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
            return StoreBank(current, main.WriteSlot(slot, captured));
        }
    }
}
