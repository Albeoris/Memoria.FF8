using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDeathDraw(NativeBattleDeathFrame Frame, NativeBattleModulePoolTask Task, NativeBattleGfGeometryBatch Packets);

public static class NativeBattleDeathDrawing
{
    public static NativeBattleDeathDraw Normal(NativeBattleDeathFrame frame, NativeBattleModulePoolTask task, NativeBattleEffectOwnedModelState model, NativeBattleDeathResources resources, NativeBattleCameraProjection projection, NativeBattleShadowSource shadow, NativeBattlePrimitiveCapabilities capabilities, NativeBattleSummonColors colors)
    {
        NativeBattleDeathState state = frame.Memory.Death14 ?? throw new NotSupportedException("Native Death lost its model projection byte owner.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Death requires its current camera matrix.");
        int size = checked(resources.Model.Geometry.Max(mesh => mesh.Vertices.Length) * 8);
        if (size > 0xA08)
        {
            throw new InvalidDataException("Native Death projected vertices leave their original static buffer.");
        }
        int offset = checked((int)(0x27419F0u - NativeBattleDeathController.ModuleBase));
        ImmutableArray<byte> scratch = state.Module.Read(offset, size).ToImmutableArray();
        NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Death requires its current primitive process.");
        NativeBattlePrimitiveTemporaryLanes lanes = process.Temporary5082B0Lanes ?? throw new NotSupportedException("Native Death requires its retained shared temporary lanes.");
        bool body = false;
        NativeBattleEffectOwnedModelDraw draw = NativeBattleEffectOwnedModelDrawing.Draw7558A0(task, model, resources.Model, view, projection, shadow, capabilities, colors, scratch, () =>
        {
            var gate = process.Enter5099D0(capabilities.HardwareEnabled ?? throw new NotSupportedException("Native Death requires its actual hardware renderer capability."));
            process = gate.Process;
            body = gate.Draw;
            return body;
        });
        if (draw.ProjectedBytesWritten != 0)
        {
            state = state with { Module = state.Module.Write(offset, draw.ProjectedScratch.AsSpan().Slice(0, draw.ProjectedBytesWritten)) };
        }
        NativeBattleActionMatrix world = NativeBattleGfMatrices.Euler56CE30(new(S(draw.Task, 0x3C), S(draw.Task, 0x3E), S(draw.Task, 0x40)));
        world = NativeBattlePoseMatrices.Scale56BEF0(world, unchecked((int)draw.Task.ReadDword(0x114)), unchecked((int)draw.Task.ReadDword(0x118)), unchecked((int)draw.Task.ReadDword(0x11C))) with { X = S(draw.Task, 0x4C), Y = S(draw.Task, 0x4E), Z = S(draw.Task, 0x50) };
        process = process.PublishTemporary5082B0Matrix(NativeBattleGfMatrices.Compose56C2F0(view, world));
        lanes = lanes with { Word44 = 0 };
        if (body)
        {
            bool selected = false;
            for (int index = 0; index < resources.Model.Geometry.Length; index++)
            {
                if ((draw.Task.ReadDword(0xAC) & (1u << (index & 31))) != 0)
                {
                    selected = true;
                    lanes = lanes with { Word2E = checked((ushort)resources.Model.Geometry[index].Polygons.Count(polygon => polygon.Quad && polygon.Bytes.Length == 24)) };
                }
            }
            process = process.PublishModelColor(draw.Task.ReadDword(0x58));
            if (selected)
            {
                int cursor = state.Cursor ?? throw new NotSupportedException("Native Death normal rendering requires its executed private cursor.");
                process = process.Publish50FDF0(checked((uint)(cursor + draw.BodyAllocationBytes)), null);
            }
        }
        frame = frame with { Memory = frame.Memory with { Death14 = state, PrimitiveProcess = process with { Temporary5082B0Lanes = lanes } } };
        return new(frame, draw.Task, draw.Packets);
    }

    public static NativeBattleDeathDraw Dark(NativeBattleDeathFrame frame, NativeBattleModulePoolTask task, NativeBattleEffectOwnedModelState model, NativeBattleDeathResources resources, NativeBattleCameraProjection projection, NativeBattleShadowSource shadowSource, NativeBattlePrimitiveCapabilities capabilities)
    {
        NativeBattleEffectOwnedModel.Validate(task, model, resources.Model);
        NativeBattleDeathState state = frame.Memory.Death14 ?? throw new NotSupportedException("Native Death requires its current module bytes.");
        NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Death requires its current primitive process.");
        NativeBattlePrimitiveTemporaryLanes lanes = process.Temporary5082B0Lanes ?? throw new NotSupportedException("Native Death dark drawing requires its retained shared temporary lanes.");
        NativeBattleDecodedPose pose = model.DecodedPose ?? throw new NotSupportedException("Native Death dark drawing requires its actual selected reaper pose.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Death requires its actual current camera matrix.");
        NativeBattleActionMatrix world = NativeBattleGfMatrices.Euler56CE30(new(S(task, 0x3C), S(task, 0x3E), S(task, 0x40)));
        world = NativeBattlePoseMatrices.Scale56BEF0(world, unchecked((int)task.ReadDword(0x114)), unchecked((int)task.ReadDword(0x118)), unchecked((int)task.ReadDword(0x11C))) with { X = S(task, 0x4C), Y = S(task, 0x4E), Z = S(task, 0x50) };
        for (int index = 0; index < 9; index++)
        {
            task = task.WriteWord(0x70 + index * 2, unchecked((ushort)world.Rotation[index]));
        }
        task = task.WriteDword(0x84, unchecked((uint)world.X)).WriteDword(0x88, unchecked((uint)world.Y)).WriteDword(0x8C, unchecked((uint)world.Z));
        NativeBattleEffectLocalBounds bounds = new(new(S(task, 0x64), S(task, 0x66), S(task, 0x68)), new(S(task, 0x6A), S(task, 0x6C), S(task, 0x6E)));
        NativeBattleShadowGeometry shape = NativeBattlePoseMatrices.ShadowGeometry5088A0(world, S(task, 0x54), bounds, view, shadowSource, capabilities.GouraudAlpha);
        NativeBattleModuleProjectedVertex[] shadow = shape.Vertices.Select(vertex => NativeBattleModuleProjection.Project45FE10(shape.CameraMatrix, vertex, projection)).ToArray();
        NativeBattleGfGeometryBatch shadowPackets = NativeBattleShadowPackets.EmitBatch5088A0(shadow, task.ReadDword(0x5C), capabilities.GouraudAlpha, 4112, 16);
        lanes = lanes with { Word44 = (ushort)(shadow[4].Depth >> 2) };
        process = process.PublishTemporary5082B0Matrix(shape.CameraMatrix);
        NativeBattleActionMatrix camera = NativeBattlePoseMatrices.MultiplyGteRotation45E470(view, world);
        (int tx, int ty, int tz) = NativeBattleGfMatrices.Rotate56C3D0(view, new(unchecked((short)world.X), unchecked((short)world.Y), unchecked((short)world.Z)));
        camera = camera with { X = unchecked(tx + view.X), Y = unchecked(ty + view.Y), Z = unchecked(tz + view.Z) };
        ImmutableArray<NativeBattleActionMatrix> bones = NativeBattlePoseMatrices.StageBoneMatrices(resources.Model.PoseSource, pose);
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        attempts.AddRange(shadowPackets.Attempts);
        int allocated = shadowPackets.AllocationBytes;
        for (int meshIndex = 0; meshIndex < resources.Model.Geometry.Length; meshIndex++)
        {
            if ((task.ReadDword(0xAC) & (1u << (meshIndex & 31))) == 0)
            {
                continue;
            }
            NativeBattleRawMesh mesh = resources.Model.Geometry[meshIndex];
            byte[] vertices = new byte[checked(mesh.Vertices.Length * 8)];
            for (int index = 0; index < mesh.Vertices.Length; index++)
            {
                NativeBattleRawVertex vertex = mesh.Vertices[index];
                NativeBattleFullMacVertex transformed = NativeBattleGfMatrices.TransformMac460840(bones[vertex.Bone], vertex.Position);
                BinaryPrimitives.WriteInt16LittleEndian(vertices.AsSpan(index * 8), (short)Math.Clamp(transformed.X, short.MinValue, short.MaxValue));
                BinaryPrimitives.WriteInt16LittleEndian(vertices.AsSpan(index * 8 + 2), (short)Math.Clamp(transformed.Y, short.MinValue, short.MaxValue));
                BinaryPrimitives.WriteInt16LittleEndian(vertices.AsSpan(index * 8 + 4), (short)Math.Clamp(transformed.Z, short.MinValue, short.MaxValue));
                if (lanes.Word2E is ushort padding)
                {
                    BinaryPrimitives.WriteUInt16LittleEndian(vertices.AsSpan(index * 8 + 6), padding);
                }
            }
            int offset = checked((int)(0x273B550u - NativeBattleDeathController.ModuleBase));
            state = state with { Module = state.Module.Write(offset, vertices) };
            if (lanes.Word2E is null)
            {
                for (int index = 0; index < mesh.Vertices.Length; index++)
                {
                    state = state with { Module = state.Module.MarkUnknown(offset + index * 8 + 6, 2) };
                }
            }
            foreach (NativeBattleRawPolygon polygon in mesh.Polygons)
            {
                if (polygon.Bytes.Length != (polygon.Quad ? 20 : 16))
                {
                    continue;
                }
                ReadOnlySpan<byte> face = polygon.Bytes.AsSpan();
                int corners = polygon.Quad ? 4 : 3;
                int[] indices = new int[corners];
                bool below = false;
                for (int corner = 0; corner < 3; corner++)
                {
                    indices[corner] = BinaryPrimitives.ReadUInt16LittleEndian(face[(corner * 2)..]) & 0xFFF;
                    below |= ReadVertexY((uint)indices[corner]) > 0;
                }
                if (below || polygon.Quad && ReadVertexY(lanes.Dword44) > 0)
                {
                    continue;
                }
                NativeBattleModuleProjectedVertex[] projected = new NativeBattleModuleProjectedVertex[corners];
                for (int corner = 0; corner < 3; corner++)
                {
                    projected[corner] = Project(indices[corner]);
                }
                if (NativeBattleModulePackets.Nclip(projected[0].Screen, projected[1].Screen, projected[2].Screen) <= 0)
                {
                    continue;
                }
                if (polygon.Quad)
                {
                    indices[3] = BinaryPrimitives.ReadUInt16LittleEndian(face[6..]) & 0xFFF;
                    lanes = lanes.WriteDword44((uint)indices[3]);
                    projected[3] = Project(indices[3]);
                }
                uint[] words = new uint[polygon.Quad ? 10 : 8];
                words[0] = polygon.Quad ? 0x9000000u : 0x7000000u;
                words[1] = (task.ReadDword(0x58) & 0xFFFFFF) | (polygon.Quad ? 0x2C000000u : 0x24000000u);
                words[2] = projected[0].Screen;
                words[3] = BinaryPrimitives.ReadUInt32LittleEndian(face[8..]);
                words[4] = projected[1].Screen;
                words[5] = BinaryPrimitives.ReadUInt32LittleEndian(face[12..]);
                words[6] = projected[2].Screen;
                words[7] = polygon.Quad ? BinaryPrimitives.ReadUInt16LittleEndian(face[16..]) : BinaryPrimitives.ReadUInt16LittleEndian(face[6..]);
                if (polygon.Quad)
                {
                    words[8] = projected[3].Screen;
                    words[9] = BinaryPrimitives.ReadUInt16LittleEndian(face[18..]);
                }
                byte[] data = new byte[words.Length * 4];
                for (int word = 0; word < words.Length; word++)
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(word * 4), words[word]);
                }
                NativeBattleGfPacketByteState[] mask = Enumerable.Repeat(NativeBattleGfPacketByteState.Known, data.Length).ToArray();
                mask.AsSpan(30, 2).Fill(NativeBattleGfPacketByteState.Untouched);
                if (polygon.Quad)
                {
                    mask.AsSpan(38, 2).Fill(NativeBattleGfPacketByteState.Untouched);
                }
                uint average = unchecked((uint)projected.Sum(vertex => (int)vertex.Depth) * (polygon.Quad ? 256u : 341u)) >> 12;
                ImmutableArray<int> depths = polygon.Quad ? [projected[0].Depth, projected[1].Depth, projected[2].Depth, projected[3].Depth] : [projected[0].Depth, projected[1].Depth, projected[2].Depth, 0];
                NativeBattleGeometryPrimitive primitive = new(checked(17 + ((int)average >> 2)), words.ToImmutableArray(), depths) { PhysicalWriteMask = mask.ToImmutableArray() };
                attempts.Add(new(allocated, data.ToImmutableArray(), mask.ToImmutableArray(), primitive));
                allocated += data.Length;
            }

            NativeBattleModuleProjectedVertex Project(int index)
            {
                ReadOnlySpan<byte> bytes = vertices.AsSpan(index * 8, 6);
                return NativeBattleModuleProjection.Project45FE10(camera, new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes[2..]), BinaryPrimitives.ReadInt16LittleEndian(bytes[4..])), projection);
            }
        }
        process = process with { Temporary5082B0Lanes = lanes };
        return new(frame with { Memory = frame.Memory with { Death14 = state, PrimitiveProcess = process } }, task, new(attempts.ToImmutable(), allocated));

        short ReadVertexY(uint index)
        {
            uint address = checked(0x273B550u + index * 8 + 2);
            foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
            {
                long delta = (long)address - pool.BankAddress;
                if (delta < 0 || delta + 2 > (long)pool.Stride * pool.Slots.Length)
                {
                    continue;
                }
                NativeBattleModulePoolTask selected = pool.Slots[(int)(delta / pool.Stride)];
                int field = (int)(delta % pool.Stride);
                uint next = selected.Next is int linked ? checked(pool.BankAddress + (uint)(linked * pool.Stride)) : 0;
                ushort value = field switch
                {
                    0 => selected.Flags,
                    2 => selected.State,
                    4 => unchecked((ushort)next),
                    6 => unchecked((ushort)(next >> 16)),
                    8 => unchecked((ushort)selected.Callback),
                    10 => unchecked((ushort)(selected.Callback >> 16)),
                    _ => selected.ReadWord(field)
                };
                return unchecked((short)value);
            }
            if (address < NativeBattleDeathController.ModuleBase || address > 0x274289E)
            {
                throw new NotSupportedException("Native Death previous-quad ground test leaves its retained executable owner.");
            }
            return BinaryPrimitives.ReadInt16LittleEndian(state.Module.Read(checked((int)(address - NativeBattleDeathController.ModuleBase)), 2));
        }
    }

    private static short S(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
}
