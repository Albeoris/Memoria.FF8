using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleGraphicsState Graphics, NativeBattleEffect20Frame Frame) DrawNativeEffect20Sprite(NativeBattleGraphicsState graphics, NativeBattleEffect20Frame current, uint address)
    {
        NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(current.Effect.Pools).Read(address);
        if ((task.ReadByte(0x26) & 4) != 0)
        {
            return (graphics, current);
        }
        NativeBattleEffect20Resources resources = current.Effect.Binding.Effect20Resources ?? throw new NotSupportedException("Effect20 requires its original sprite resource.");
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Effect20 requires its current camera.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Effect20 requires its published projection.");
        (uint temporary, NativeBattleEffectArena retained) = AllocateNativeEffect20Temporary(216);
        NativeBattleProjectedSpriteHead prepared = NativeBattleProjectedSpriteD8.PrepareHead(current.Memory, task, camera.CapturePackedView1D97778(), retained);
        NativeBattleProjectedSpriteDraw drawn = NativeBattleProjectedSpriteD8.Draw(prepared, selected => selected == resources.Sprite.Address ? resources.Sprite : throw new NotSupportedException("Effect20 sprite selected an unavailable original resource."), projection, camera.Roll, 17);
        current = current with { Memory = drawn.Memory };
        (graphics, current) = AppendNativeEffect20Private(graphics, current, drawn.Batch);
        ReleaseNativeEffect20Temporary(temporary, drawn.TemporaryD8, 216);
        return (graphics, current);
    }

    private (NativeBattleGraphicsState Graphics, NativeBattleEffect20Frame Frame) DrawNativeEffect20Particle(NativeBattleGraphicsState graphics, NativeBattleEffect20Frame current, uint address)
    {
        NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(current.Effect.Pools).Read(address);
        if ((task.ReadByte(0x26) & 4) != 0)
        {
            return (graphics, current);
        }
        NativeBattleEffect20Resources resources = current.Effect.Binding.Effect20Resources ?? throw new NotSupportedException("Effect20 requires its original mesh resource.");
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Effect20 requires its current camera.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Effect20 requires its published projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Effect20 requires its actual view matrix.");
        NativeBattleEffect20MeshPreparation prepared = NativeBattleEffect20MeshRendering.Prepare(task, view, NativeBattleEffectArena.Clear().MarkUnknown(0, 104), camera.CanonicalViewPadding);
        foreach (NativeBattleDrawStock15ParticleOperation operation in prepared.Operations)
        {
            current = current with { Memory = ObserveNativeEffect20Geometry(current.Memory, operation) };
        }
        (uint temporary, NativeBattleEffectArena retained) = AllocateNativeEffect20Temporary(104);
        prepared = NativeBattleEffect20MeshRendering.Prepare(task, view, retained, camera.CanonicalViewPadding);
        NativeBattlePrimitiveProcess process = current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Effect20 requires its packet process.");
        NativeBattleScriptedGraphicsMemory controls = current.Memory.ScriptedGraphics ?? throw new NotSupportedException("Effect20 requires its projection controls.");
        NativeBattleActorWaveMeshStep drawn = NativeBattleActorWaveMesh.EmitBatch(resources.Mesh.Bytes, resources.Mesh.Address, prepared.Matrix, projection, prepared.Context104, new(current.Memory.GeometryRegisters!, current.Memory.ScriptedColorRegisters!), process.ModelRendererRegisters ?? throw new NotSupportedException("Effect20 requires its retained far colors."), controls.Dqa, controls.Dqb, controls.Zsf4, definition.NativeGraphics!.Rules.SummonColors ?? throw new NotSupportedException("Effect20 requires its geometry constants."), null, controls.Zsf3);
        current = current with { Memory = current.Memory with { GeometryRegisters = drawn.Registers.Geometry, ScriptedColorRegisters = drawn.Registers.Colors, PrimitiveProcess = process with { ModelRendererRegisters = drawn.FarControls } } };
        (graphics, current) = AppendNativeEffect20Private(graphics, current, drawn.Batch);
        ReleaseNativeEffect20Temporary(temporary, drawn.Context104, 104);
        return (graphics, current);
    }

    private (NativeBattleGraphicsState Graphics, NativeBattleEffect20Frame Frame) DrawNativeEffect20Body(NativeBattleGraphicsState graphics, NativeBattleEffect20Frame current, uint address)
    {
        NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(current.Effect.Pools).Read(address);
        int index = task.ReadByte(0x2C);
        NativeBattleGraphicsModel actor = graphics.Models[index];
        NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, index);
        NativeBattlePoseSource source = resource.PoseSource ?? throw new NotSupportedException("Effect20 body requires its actual original rig.");
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Effect20 body requires its current camera.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Effect20 body requires its actual published projection.");
        NativeBattleSequenceState sequence = actor.SequenceState ?? throw new NotSupportedException("Effect20 body requires its current decoded sequence.");
        uint savedMask = sequence.MeshMask124;
        (uint temporary, NativeBattleEffectArena retained) = AllocateNativeEffect20Temporary(140);
        actor = actor with { SequenceState = sequence with { MeshMask124 = 2 } };
        PublishBones();
        ImmutableArray<NativeBattleActionMatrix> matrices = NativeBattleBodyBoneMatrices.Require(actor, source);
        ushort maximum = NativeModelCpuResources?.ScratchMaximum ?? throw new NotSupportedException("Effect20 body requires its loaded projected workspace maximum.");
        uint scratch = checked(0x196B018u - (uint)maximum * 8);
        NativeBattleEffectArena copied = NativeBattleTemporaryMemory.ReadView(NativeFileStaging!, 0x1D969A8, 4);
        copied = NativeBattleEffectNodeContextMemory.Copy(copied, 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 4), 0, 4);
        NativeBattleEffect20BodyInputs inputs = new(actor.GeometryWorldMatrix ?? throw new NotSupportedException("Effect20 body requires its retained model matrix."), null, matrices[0], camera.CapturePackedView1D97778(), scratch, copied, actor.Color40 ?? throw new NotSupportedException("Effect20 body requires its actual model color."), actor.Alpha7 ?? throw new NotSupportedException("Effect20 body requires its actual secondary color producer."), (memory, rotate) => ObserveNativeEffect20Geometry(memory, rotate));
        NativeBattleEffect20BodyPreparation prepared = NativeBattleEffect20BodyRendering.Prepare(current.Memory, task, retained, inputs);
        current = current with { Memory = prepared.Memory };
        retained = prepared.Temporary8C;
        actor = actor with { CurrentBodyBoneMatrices = actor.CurrentBodyBoneMatrices! with { Matrices = matrices.SetItem(0, prepared.FirstBone) } };
        NativeFileStaging = NativeBattleTemporaryMemory.PublishView(NativeFileStaging!, temporary, retained, 140);
        DrawBody();
        PublishBones();
        actor = actor with { SequenceState = actor.SequenceState! with { MeshMask124 = savedMask } };
        ReleaseNativeEffect20Temporary(temporary, retained, 140);
        return (graphics with { Models = graphics.Models.SetItem(index, actor) }, current);

        void PublishBones()
        {
            (uint boneTemporary, NativeBattleEffectArena boneRetained) = AllocateNativeEffect20Temporary(88);
            NativeBattleBodyBonePreparation bones = NativeBattleBodyBoneTemporary.Prepare508C90(actor, source, boneRetained);
            actor = bones.Model;
            ReleaseNativeEffect20Temporary(boneTemporary, bones.Temporary58, 88);
        }

        void DrawBody()
        {
            NativeBattlePrimitiveProcess process = current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Effect20 body requires its current renderer process.");
            NativeBattlePrimitiveCapabilities capabilities = definition.NativeGraphics?.PrimitiveCapabilities ?? throw new NotSupportedException("Effect20 body requires its renderer capabilities.");
            var gate = process.Enter5099D0(capabilities.HardwareEnabled ?? throw new NotSupportedException("Effect20 body requires its actual hardware capability."));
            process = gate.Process;
            current = current with { Memory = current.Memory with { PrimitiveProcess = process } };
            if (!gate.Draw)
            {
                return;
            }
            if (inputs.Fade7 != 0)
            {
                throw new NotSupportedException("Effect20 body requires the native secondary-color packet branch for a nonzero retained secondary color.");
            }
            var cpu = NativeModelCpuResources ?? throw new NotSupportedException("Effect20 body requires its actual CPU resource bank.");
            NativeBattleResourcePointer pointer = GeometryPointer();
            uint physical = pointer.Cpu is { } cpuPointer ? cpuPointer.PhysicalAddress : pointer.StagingAddress ?? throw new NotSupportedException("Effect20 body requires its physical model source.");
            Dictionary<int, (ImmutableArray<ushort> Counts, int First, int End)> metadata = [];
            ImmutableArray<NativeBattleRawMesh> meshes = NativeBattleRawGeometryReader.Read(Read, source.BoneCount, (mesh, counts, first, end) => metadata.Add(mesh, (counts, first, end)));
            process = process.PublishModelColor(inputs.ModelColor28);
            current = current with { Memory = current.Memory with { PrimitiveProcess = process } };
            NativeBattleSummonColors colors = definition.NativeGraphics!.Rules.SummonColors ?? throw new NotSupportedException("Effect20 body requires its original geometry constants.");
            ImmutableArray<NativeBattleActionMatrix> bones = NativeBattleBodyBoneMatrices.Require(actor, source);
            NativeBattleDecodedPose decoded = NativeBattlePoseMatrices.ReadDecodedPose(actor.SequenceState!, source);
            for (int meshIndex = 0; meshIndex < meshes.Length; meshIndex++)
            {
                if ((2u & (1u << (meshIndex & 31))) == 0)
                {
                    continue;
                }
                NativeBattleRawMesh mesh = meshes[meshIndex];
                NativeBattleGeometryRegisterStep step = new(current.Memory.GeometryRegisters!, current.Memory.ScriptedColorRegisters!);
                foreach (NativeBattleRawVertexGroup group in mesh.PackedVertexGroups ?? throw new NotSupportedException("Effect20 body requires its original complete bone group directory."))
                {
                    short parent = BinaryPrimitives.ReadInt16LittleEndian(source.Skeleton.AsSpan().Slice(16 + group.Bone * 48));
                    short? padding = group.Bone != 0 && !decoded.HasScale && parent < 0 ? (short)0 : null;
                    step = step with { Geometry = NativeBattleGeometry.LoadMatrix(step.Geometry, bones[group.Bone], true, true, padding) };
                }
                current = current with { Memory = current.Memory with { GeometryRegisters = step.Geometry, ScriptedColorRegisters = step.Colors } };
                var data = metadata[meshIndex];
                retained = WriteDword(retained, 0x60, checked(physical + (uint)data.First));
                for (int group = 0; group < 4; group++)
                {
                    byte[] count = new byte[2];
                    BinaryPrimitives.WriteUInt16LittleEndian(count, data.Counts[group]);
                    retained = retained.Write(0x68 + group * 2, count);
                }
                (byte[] previous, _) = NativeFileStaging!.ReadSnapshot(scratch, maximum * 8);
                NativeBattleRawModelBodyDraw drawn = NativeBattleRawModelBodyDrawing.Draw5099D0(mesh, bones, projection, inputs.ModelColor28, 1, previous.ToImmutableArray(), colors, 4, new(0, 0, 320, 216), 17, 0);
                if (drawn.ProjectedBytesWritten != 0)
                {
                    NativeFileStaging = NativeFileStaging.Write(scratch, drawn.ProjectedScratch.AsSpan().Slice(0, drawn.ProjectedBytesWritten));
                }
                NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect20 body requires its ordering table.");
                NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(current.Memory.PrimitiveProcess!, primitive.Pending, drawn.Packets);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                current = current with { Memory = current.Memory with { PrimitiveProcess = written.Process.Publish50FDF0(NativeEffect20CommonAddress(written.Process), checked(physical + (uint)data.End)) } };
            }

            NativeBattleResourcePointer GeometryPointer()
            {
                if (actor.ActiveStagedResourceRow100 is uint staged)
                {
                    uint selected = checked(staged + 4);
                    if (NativeBattleEffectTaskMemory.TryReadPointer(current.Effect.Pools, selected, out NativeBattleResourcePointer? taskPointer))
                    {
                        return taskPointer ?? throw new NotSupportedException("Effect20 body selected a null model source.");
                    }
                    return (IsNativeEffectArenaAddress(selected) ? current.Memory.Arena!.ReadResourcePointer(NativeBattleWaterExecutionFields.ArenaOffset(selected, 4)) : NativeFileStaging!.ReadResourcePointer(selected)) ?? throw new NotSupportedException("Effect20 body source pointer is unknown.");
                }
                NativeBattleModelCpuResourceRow row = cpu.Rows[actor.ActiveResourceRow100 ?? throw new NotSupportedException("Effect20 body requires its current original resource row.")];
                return new(row.Require(16), null, row.Lengths.TryGetValue(16, out int length) ? length : null);
            }

            byte[] Read(int offset, int length)
            {
                if (pointer.Length is int available && (offset < 0 || length < 0 || offset > available - length))
                {
                    throw new InvalidDataException("Effect20 body read leaves its original geometry source.");
                }
                if (pointer.Cpu is { } selected)
                {
                    return NativeBattleModelEnemyHeap.Read(cpu, NativeFileStaging!, selected with { Offset = checked(selected.Offset + offset) }, length);
                }
                uint selectedAddress = checked(physical + (uint)offset);
                if (NativeBattleEffectTaskMemory.TryRead(current.Effect.Pools, selectedAddress, length, out byte[] taskBytes))
                {
                    return taskBytes;
                }
                return IsNativeEffectArenaAddress(selectedAddress) ? current.Memory.Arena!.Read(NativeBattleWaterExecutionFields.ArenaOffset(selectedAddress, length), length) : NativeFileStaging!.Read(selectedAddress, length);
            }
        }
    }

    private NativeBattleEffectMemory ObserveNativeEffect20Geometry(NativeBattleEffectMemory memory, NativeBattleDrawStock15ParticleOperation operation)
    {
        if (operation is NativeBattleDrawStock15Rotate rotate)
        {
            NativeBattleGeometryRegisterStep rotated = ObserveNativeGeometryRotation(new(memory.GeometryRegisters ?? throw new NotSupportedException("Effect20 requires its retained geometry controls."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect20 requires its retained geometry data.")), rotate.Matrix, rotate.Axis, rotate.Angle, rotate.Padding18);
            return memory with { GeometryRegisters = rotated.Geometry, ScriptedColorRegisters = rotated.Colors };
        }
        return ExecuteNativeDrawStock15Geometry(memory, operation, null);
    }

    private (NativeBattleGraphicsState Graphics, NativeBattleEffect20Frame Frame) AppendNativeEffect20Private(NativeBattleGraphicsState graphics, NativeBattleEffect20Frame current, NativeBattleGfGeometryBatch batch)
    {
        NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect20 requires its current ordering table.");
        int cursor = current.State.PrimaryDrawCursor ?? throw new NotSupportedException("Effect20 requires its selected private stream cursor.");
        NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, batch);
        return (graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } }, current with { Memory = written.Memory, State = current.State with { PrimaryDrawCursor = written.Cursor } });
    }

    private (uint Address, NativeBattleEffectArena Arena) AllocateNativeEffect20Temporary(int length)
    {
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(NativeFileStaging ?? throw new NotSupportedException("Effect20 requires its retained physical temporary memory."), length);
        NativeFileStaging = allocation.Staging;
        NativeBattleEffectArena physical = NativeBattleTemporaryMemory.ReadView(NativeFileStaging, allocation.Address, length);
        return (allocation.Address, NativeBattleEffectNodeContextMemory.Copy(physical, 0, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length));
    }

    private void ReleaseNativeEffect20Temporary(uint address, NativeBattleEffectArena arena, int length)
    {
        NativeFileStaging = NativeBattleTemporaryMemory.PublishView(NativeFileStaging!, address, arena, length);
        NativeFileStaging = NativeBattleTemporaryMemory.Release5082D0(NativeFileStaging, length);
    }

    private static NativeBattleEffectArena WriteDword(NativeBattleEffectArena arena, int offset, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return arena.Write(offset, bytes);
    }

    private static uint NativeEffect20CommonAddress(NativeBattlePrimitiveProcess process)
    {
        uint bank = process.PacketBank switch { "off_B6D070" => 0x1901018, "off_B6D094" => 0x199B018, _ => throw new NotSupportedException("Effect20 body requires its actual common packet bank.") };
        return checked(bank + (uint)process.PacketBuffer * (uint)process.CommonPacketNominalCapacity + (uint)(process.ByteCursor ?? throw new NotSupportedException("Effect20 body requires its actual common cursor.")));
    }
}
