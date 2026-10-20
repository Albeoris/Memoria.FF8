using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeRinoa89(NativeBattleGraphicsState graphics, NativeBattleRinoa89Resources resources)
    {
        NativeBattleRinoa89Frame frame = NativeBattleRinoa89Controller.Enter6F9D00(new(NativeEffectMemory ?? throw new NotSupportedException("Native Wishing Star requires retained effect memory."), graphics.OriginalEffect ?? throw new InvalidDataException("Native Wishing Star requires its entered module."), NativeFileStaging ?? NativeBattleFileStaging.Unknown(), nativeCrtRandom ?? throw new NotSupportedException("Native Wishing Star requires retained CRT state.")), resources);
        NativeEffectMemory = frame.Memory;
        NativeFileStaging = frame.Staging;
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };
    }

    private NativeBattleGraphicsState AdvanceNativeRinoa89(NativeBattleGraphicsState graphics)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native Wishing Star requires retained effect memory.");
        NativeBattleRinoa89Resources resources = memory.Rinoa89?.Resources ?? throw new NotSupportedException("Native Wishing Star requires its entered original resources.");
        NativeBattleRinoa89Frame frame = new(memory, graphics.OriginalEffect ?? throw new InvalidDataException("Native Wishing Star requires its selected module."), NativeFileStaging ?? throw new NotSupportedException("Native Wishing Star requires retained shared memory."), nativeCrtRandom ?? throw new NotSupportedException("Native Wishing Star requires retained CRT state."));
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Native Wishing Star requires its actual action parameters.");
        NativeBattleEffectOwnedModelResource body = resources.Body ?? throw new NotSupportedException("Native Wishing Star requires its embedded model.");
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native Wishing Star requires its camera owner.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Native Wishing Star requires its current projection registers.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Wishing Star requires its current view matrix.");
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native Wishing Star requires authenticated color arithmetic.");
        NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[parameters.HeaderIndex7C];
        int first = header.FirstContactIndex ?? throw new NotSupportedException("Native Wishing Star requires its actual contact pointer.");
        int count = header.ReadByte(16) >> 3;
        ImmutableArray<byte> targets = Enumerable.Range(0, count).Select(index => nativeActionPackets.ReadContact(checked(first + index)).Primary.ActorIndex).ToImmutableArray();
        NativeBattleRinoa89PreparationServices preparation = new(index => graphics.Models[index], WriteModel, index => ReadNativeModelAnchor(graphics, index), targets, checked(0x1D28344u + (uint)first * 24), body, Bind, Select, NativeBattleRinoa89Controller.InitializeShards6FA3E0);
        NativeBattleRinoa89BodyInputs inputs = new(resources, index => graphics.Models[index], WriteModel, Execute, ReadBone19, Contacts, Poll, Reload);
        NativeBattleRinoa89SpriteServices sprites = new((current, point, scale, displacement, phase) => Particle(NativeBattleRinoa89MeshDrawing.DrawBillboard6FD340(current, point, scale, displacement, phase, projection, (NativeCamera ?? camera).Roll, resources)), (current, matrix, angles) => Particle(NativeBattleRinoa89MeshDrawing.DrawSpriteMesh6FD3C0(current, matrix, angles, view, projection, colors, resources)));
        int beamMesh = checked((int)(0x1497140 - NativeBattleRinoa89Resources.StaticAddress));
        NativeBattleRinoa89BeamServices beams = new(view, projection, colors, resources.StaticImage.Data.Slice(beamMesh, resources.StaticImage.Data.Length - beamMesh), resources.Program(0x14A3EB0), Append, Contacts);
        frame = NativeBattleRinoa89Controller.AdvanceRoot6F9D80(frame, new(index => graphics.Models[index], (current, slot) => NativeBattleRinoa89Controller.PrepareBody6F9D80(current, slot, preparation), Child));
        frame = frame with { Effect = frame.Effect with { DrawArenaOffset = NativeBattleWaterExecutionFields.ArenaOffset(BinaryPrimitives.ReadUInt32LittleEndian(frame.Staging.Read(0x25565EC, 4)), 0) } };
        Publish(frame);
        nativeCrtRandom = frame.Random;
        return graphics;

        void WriteModel(int index, NativeBattleGraphicsModel model) => graphics = graphics with { Models = graphics.Models.SetItem(index, model) };
        void Publish(NativeBattleRinoa89Frame current)
        {
            NativeEffectMemory = current.Memory;
            NativeFileStaging = current.Staging;
            graphics = graphics with { OriginalEffect = current.Effect };
        }
        NativeBattleRinoa89Frame Append(NativeBattleRinoa89Frame current, NativeBattleGfGeometryBatch packets, bool common)
        {
            if (common)
            {
                return AppendCommon(current, packets, 0);
            }
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Wishing Star requires its current ordering table.");
            int cursor = NativeBattleWaterExecutionFields.ArenaOffset(BinaryPrimitives.ReadUInt32LittleEndian(current.Staging.Read(0x25565EC, 4)), 0);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, packets);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, Effect = current.Effect with { DrawArenaOffset = written.Cursor }, Staging = current.Staging.Write(0x25565EC, BitConverter.GetBytes(checked(0x20DFAB8u + (uint)written.Cursor))) };
        }
        NativeBattleRinoa89Frame AppendCommon(NativeBattleRinoa89Frame current, NativeBattleGfGeometryBatch packets, ushort flags)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Wishing Star requires its current ordering table.");
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Wishing Star requires its COMMON packet process."), primitive.Pending, packets, flags);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = current.Memory with { PrimitiveProcess = written.Process } };
        }
        NativeBattleRinoa89Frame Particle(NativeBattleRinoa89ParticleDraw drawn) => Append(drawn.Frame, drawn.Packets, false);
        NativeBattleRinoa89Frame Bind(NativeBattleRinoa89Frame current, int index, uint row)
        {
            Publish(current);
            NativeBattleGraphicsModel model = RetainNativeDecodedPoseBeforeResourceChange(graphics.Models[index], NativeGraphicsPoseResource(graphics, index));
            int actorDat = checked((int)(0x1495530 - NativeBattleRinoa89Resources.StaticAddress));
            var bound = NativeBattleVictoryResources.Bind5022C0(resources.StaticImage.Data.Slice(actorDat, 4128), NativeModelCpuResources ?? throw new NotSupportedException("Native Wishing Star requires its CPU rows."), model, row, 0x1495530, Read, (address, pointer) => current = current with { Effect = current.Effect with { Pools = NativeBattleEffectTaskMemory.WritePointer(current.Effect.Pools, address, pointer) } });
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
        NativeBattleRinoa89Frame Select(NativeBattleRinoa89Frame current, int index, byte sequence)
        {
            Publish(current);
            graphics = SelectNativeHeaderSequence(graphics, checked((byte)index), sequence);
            return current;
        }
        NativeBattleRinoa89Frame QueueFile(NativeBattleRinoa89Frame current, NativeBattleEffectFileRequest request)
        {
            NativeBattleEffectFileAsset file = resources.Files.Single(file => file.FileId == request.FileId);
            NativeBattleEffectArena arena = (current.Memory.Arena ?? throw new NotSupportedException("Native Wishing Star reload requires its physical arena.")).Write(NativeBattleWaterExecutionFields.ArenaOffset(request.DestinationAddress, file.Data.Length), file.Data.AsSpan());
            NativeFileIo = (NativeFileIo ?? throw new NotSupportedException("Native Wishing Star reload requires its IO scheduler.")).Queue482610(file.Data.Length, request.CompletionCallback, null);
            return current with { Memory = current.Memory with { Arena = arena } };
        }
        (NativeBattleRinoa89Frame Frame, bool Busy) Poll(NativeBattleRinoa89Frame current)
        {
            NativeBattleEffectIoPoll poll = NativeBattleEffectIo.Poll534270(current.Memory);
            current = current with { Memory = poll.Memory };
            return (poll.Request is { } request ? QueueFile(current, request) : current, poll.Busy);
        }
        (NativeBattleRinoa89Frame Frame, bool Complete) Reload(NativeBattleRinoa89Frame current)
        {
            NativeBattleEffectIoPoll poll = NativeBattleEffectIo.Select5342C0(current.Memory, 2);
            current = current with { Memory = poll.Memory };
            return (poll.Request is { } request ? QueueFile(current, request) : current, !poll.Busy);
        }
        NativeBattleModulePoolTask BodyTask(NativeBattleRinoa89Frame current) => current.Effect.Pools.Single(pool => pool.DescriptorAddress == NativeBattleRinoa89Controller.Body).Slots[0];
        NativeBattleRinoa89BoneStep ReadBone19(NativeBattleRinoa89Frame current, NativeBattleModulePoolTask task)
        {
            byte[] reference = new byte[8];
            BinaryPrimitives.WriteInt16LittleEndian(reference.AsSpan(6), 19);
            return NativeBattleRinoa89BodyDrawing.ReadBone6FD2D0(current, task, body, reference);
        }
        NativeBattleRinoa89Frame Contacts(NativeBattleRinoa89Frame current)
        {
            return NativeBattleRinoa89Contacts.Apply6FC410(current, nativeActionPackets, (active, pointer) =>
            {
                Publish(active);
                int index = checked((int)((pointer - 0x1D28344u) / 24));
                ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(index)] });
                graphics = graphics with { PendingContacts = pending };
                return active with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Native Wishing Star contact lost its effect memory.") };
            });
        }
        NativeBattleRinoa89TaskStep Child(NativeBattleRinoa89Frame current, uint descriptor, int slot)
        {
            NativeBattleEffectTaskPool pool = current.Effect.Pools.Single(bank => bank.DescriptorAddress == descriptor);
            return pool.Slots[slot].Callback switch
            {
                0x6FA780 => NativeBattleRinoa89Controller.AdvanceBody6FA780(current, pool, slot, inputs),
                0x6FD340 => NativeBattleRinoa89Controller.AdvanceSprite6FD340(current, pool, slot, sprites),
                0x6FD3C0 => NativeBattleRinoa89Controller.AdvanceSprite6FD3C0(current, pool, slot, sprites),
                0x6FD580 => NativeBattleRinoa89Controller.AdvanceBeam6FD580(current, pool, slot, beams),
                0x6FDC20 => NativeBattleRinoa89Controller.AdvanceBeam6FDC20(current, pool, slot, beams),
                _ => throw new NotSupportedException("Native Wishing Star selected an unsupported original child callback.")
            };
        }
        NativeBattleRinoa89Frame Execute(NativeBattleRinoa89Frame current, NativeBattleRinoa89Operation operation)
        {
            switch (operation)
            {
                case NativeBattleRinoa89Sequence sequence:
                    return Select(current, parameters.Actor78, sequence.Sequence);
                case NativeBattleRinoa89Visibility visibility:
                    return Visibility(current, visibility.Callback);
                case NativeBattleRinoa89BoneEmission emission:
                    return BoneEmission(current, emission);
                case NativeBattleRinoa89Program program:
                    NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(view, NativeBattlePandemona291Restoration.Rotation701270(0) with { X = program.Anchor.X, Y = program.Anchor.Y, Z = program.Anchor.Z });
                    return NativeBattleRinoa89Programme.Execute701970(current, NativeBattleRinoa89Controller.Body, 0, program.WorkOffset, program.WorkOffset == 816 ? 236 : 772, program.ClearSize, resources.Program(program.Source), program.Initialize, program.Advance, new(matrix, 0x20DFAB8 + 100492, program.Depth), projection, colors, program.Common, Append);
                case NativeBattleRinoa89ApproachMesh mesh:
                    return Particle(NativeBattleRinoa89MeshDrawing.DrawApproach6FA780(current, mesh.Anchor.X, mesh.Anchor.Y, mesh.Anchor.Z, mesh.Delta, view, projection, colors, resources));
                case NativeBattleRinoa89ReturnMeshes mesh:
                    return Particle(NativeBattleRinoa89MeshDrawing.DrawReturnBurst6FA780(current, mesh.Anchor.X, mesh.Anchor.Y, mesh.Anchor.Z, mesh.Age, view, projection, colors, resources));
                case NativeBattleRinoa89SpawnBeam beam:
                    NativeBattleEffectTaskPool beamPool = current.Effect.Pools.Single(pool => pool.DescriptorAddress == NativeBattleRinoa89Controller.Beams);
                    NativeBattleRinoa89BeamBirth birth = beam.Wide ? NativeBattleRinoa89Controller.SpawnBeam6FD4D0(current, beamPool, beam.X, beam.Y, resources.Program(0x14A3EB0)) : NativeBattleRinoa89Controller.SpawnBeam6FDB80(current, beamPool, beam.X, beam.Y, resources.Program(0x14A3EB0));
                    return birth.Frame with { Effect = birth.Frame.Effect with { Pools = birth.Frame.Effect.Pools.Select(pool => pool.DescriptorAddress == beamPool.DescriptorAddress ? birth.Pool : pool).ToImmutableArray() } };
                case NativeBattleRinoa89Stars stars:
                    NativeBattleRinoa89StarDraw drawnStars = NativeBattleRinoa89StarDrawing.Draw6FCD50(current, view, projection, stars.Anchor.X, stars.Anchor.Y, stars.Anchor.Z, 0, 65536);
                    return AppendCommon(drawnStars.Frame, drawnStars.Packets, 1);
                case NativeBattleRinoa89CaptureScene:
                    (current, graphics) = CaptureNativeRinoa89Scene(current, graphics, resources, projection, colors);
                    return current;
                case NativeBattleRinoa89Fade fade:
                    return AppendCommon(current, NativeBattleRinoa89FadeDrawing.Draw7040B0(fade.Red, fade.Green, fade.Blue, NativeBattleRinoa89FadeDrawing.Factor6FC450(fade.Age, fade.In, fade.Hold, fade.Out)), 1);
                case NativeBattleRinoa89DrawShards:
                    return AppendCommon(current, NativeBattleRinoa89ShardDrawing.Draw6FCFD0(current.Memory.Arena ?? throw new NotSupportedException("Native Wishing Star shards require their physical arena.")), 1);
                case NativeBattleRinoa89PublishView:
                    NativeBattlePcRenderRequest render = current.Memory.PcRenderRequest ?? throw new NotSupportedException("Native6E2AC0 requires its retained PC render request.");
                    return current with { Memory = current.Memory with { PcRenderRequest = render.Request47CF50(0, 255, 255, 255, 63) } };
                case NativeBattleRinoa89DrawBody:
                    NativeBattleRinoa89BodyDraw drawn = NativeBattleRinoa89BodyDrawing.Draw6A79B0(current, BodyTask(current), body, projection, definition.NativeGraphics?.PrimitiveCapabilities?.HardwareEnabled ?? throw new NotSupportedException("Native Wishing Star requires its renderer capability."));
                    return Append(drawn.Frame, drawn.Packets, false);
                case NativeBattleRinoa89RestoreCamera:
                    NativeBattleCameraState active = NativeCamera ?? throw new NotSupportedException("Native Wishing Star closure requires its camera owner.");
                    NativeBattleCameraPose captured = active.CapturedPose ?? throw new NotSupportedException("Native Wishing Star closure requires the original503300 capture.");
                    NativeCamera = active.WriteEffectGlobals(captured with { Roll = active.Roll }) with { EyePaddingF6 = active.CapturedEyePadding806, TargetPaddingFE = active.CapturedTargetPadding80E };
                    return current;
                case NativeBattleRinoa89DrawParticles particles:
                    return particles.Callback switch
                    {
                        0x6FC500 => Particle(NativeBattleRinoa89ParticleDrawing.DrawEmitters6FC500(current, view, projection, colors, current.Staging.Read)),
                        0x6FC8B0 => Particle(NativeBattleRinoa89ParticleDrawing.DrawSparks6FC8B0(current, view, projection, current.Staging.Read)),
                        _ => throw new NotSupportedException("Native Wishing Star selected an unsupported particle renderer.")
                    };
                case NativeBattleRinoa89IoEvents ioEvents:
                    return NativeBattleRinoa89Controller.QueueBodyIo6FA780(current, ioEvents.Age);
                case NativeBattleRinoa89AdvanceIo:
                    NativeBattleEffectIoAdvance advanced = NativeBattleEffectIo.Advance534210(current.Memory);
                    current = current with { Memory = advanced.Memory };
                    return advanced.Request is { } request ? QueueFile(current, request) : current;
                case NativeBattleRinoa89Sound sound:
                    PublishNativeModuleSound(new(resources.Sounds[sound.Source], 1, null) { Pan = 128, RawPan = 128 });
                    return current;
                case NativeBattleRinoa89CameraUpdate change:
                    NativeBattleCameraState owner = NativeCamera ?? throw new NotSupportedException("Native Wishing Star requires its camera owner.");
                    if (change.Age == 0)
                    {
                        owner = owner.Apply50A730() with { FocalLength = 288, Roll = 0 };
                    }
                    NativeBattleCameraPose pose = owner.CurrentPose ?? throw new NotSupportedException("Native Wishing Star requires its current eye and target.");
                    pose = pose with { FocalLength = owner.FocalLength, Roll = owner.Roll };
                    NativeCamera = owner.WriteEffectGlobals(NativeBattleRinoa89Camera.Advance(pose, change.Age, change.Center, change.Caster));
                    return current;
                default:
                    throw new NotSupportedException("Native Wishing Star selected an unsupported original scene operation.");
            }
        }
        NativeBattleRinoa89Frame BoneEmission(NativeBattleRinoa89Frame current, NativeBattleRinoa89BoneEmission emission)
        {
            byte[] reference = current.Staging.Read(emission.Reference, 8);
            NativeBattleVector point;
            NativeBattleEffectTaskPool bodyPool = current.Effect.Pools.Single(pool => pool.DescriptorAddress == NativeBattleRinoa89Controller.Body);
            if (emission.Model == bodyPool.BankAddress + 52)
            {
                NativeBattleRinoa89BoneStep bone = NativeBattleRinoa89BodyDrawing.ReadBone6FD2D0(current, bodyPool.Slots[0], body, reference);
                current = bone.Frame;
                point = bone.Position;
            }
            else
            {
                uint relative = emission.Model - 0x1D972C0;
                if (relative % 156 != 0 || relative / 156 >= 7)
                {
                    throw new InvalidDataException("Native6FD2D0 selected an unknown model address.");
                }
                int index = checked((int)(relative / 156));
                NativeBattleGraphicsModel model = graphics.Models[index];
                NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, index).PoseSource ?? throw new NotSupportedException("Native6FD2D0 requires its actor skeleton.");
                ImmutableArray<NativeBattleActionMatrix> bones = NativeBattleBodyBoneMatrices.Require(model, source);
                short selector = BinaryPrimitives.ReadInt16LittleEndian(reference.AsSpan(6));
                if (selector < 0 || selector >= bones.Length)
                {
                    throw new InvalidDataException("Native6FD2D0 selected a bone outside its actor skeleton.");
                }
                NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(model.GeometryWorldMatrix ?? throw new NotSupportedException("Native6FD2D0 requires its actor world matrix."), bones[selector]);
                point = NativeBattleGfMatrices.Transform460840(matrix, new(BinaryPrimitives.ReadInt16LittleEndian(reference), BinaryPrimitives.ReadInt16LittleEndian(reference.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(reference.AsSpan(4))));
            }
            NativeBattleEffectTaskPool pool = current.Effect.Pools.Single(bank => bank.DescriptorAddress == NativeBattleRinoa89Controller.Sprites);
            NativeBattleRinoa89SpriteBirth spawned = NativeBattleRinoa89Controller.SpawnSprites6FD1B0(current, pool, point, emission.Extras);
            return spawned.Frame with { Effect = spawned.Frame.Effect with { Pools = spawned.Frame.Effect.Pools.Select(bank => bank.DescriptorAddress == pool.DescriptorAddress ? spawned.Pool : bank).ToImmutableArray() } };
        }
        NativeBattleRinoa89Frame Visibility(NativeBattleRinoa89Frame current, uint callback)
        {
            if (callback == 0x6FBE80)
            {
                NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native6FBE80 requires its four stage rows.");
                for (int index = 0; index < stage.Groups.Length; index++)
                {
                    NativeBattleStageGroup group = stage.Groups[index];
                    NativeBattlePoseSource source = group.Source ?? throw new NotSupportedException("Native5095B0 requires its stage skeleton.");
                    NativeBattleDecodedPose pose = source.Sample(group.Animation, group.Counter, group.PoseFlags);
                    group = group with { Matrices = NativeBattlePoseMatrices.ComposeStageCamera(NativeBattlePoseMatrices.StageBoneMatrices(source, pose), view, 0), Flags = (byte)((group.Flags & 1) != 0 ? group.Flags | 2 : group.Flags) };
                    stage = stage with { Groups = stage.Groups.SetItem(index, group) };
                }
                NativeStage = stage;
            }
            int start = callback == 0x6FBE40 ? 0 : 3;
            int end = callback == 0x6FBE40 ? 3 : 7;
            if (callback is not (0x6FBDE0 or 0x6FBE00 or 0x6FBE40 or 0x6FBE80))
            {
                throw new NotSupportedException("Native Wishing Star selected an unknown visibility callback.");
            }
            for (int index = start; index < end; index++)
            {
                NativeBattleGraphicsModel model = graphics.Models[index];
                if ((model.Flags0 & 2) == 0)
                {
                    continue;
                }
                ushort flags = callback == 0x6FBDE0 ? (ushort)(model.Flags0 | 4) : (ushort)(model.Flags0 ^ ((model.Flags0 ^ current.Staging.Read(checked(0x2556604u + (uint)index * 4), 1)[0]) & 4));
                WriteModel(index, model with { Flags0 = flags });
            }
            return current;
        }
    }
}
