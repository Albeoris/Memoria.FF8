using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleShivaPlane(short X, short Y, short Z, short W);
public sealed record NativeBattleShivaClipVertex(NativeBattleVector Position, ushort Uv);
public sealed record NativeBattleShivaPlaneDraw(NativeBattleEffectArena Arena, NativeBattleGfGeometryBatch Batch, int VerticesWritten);

/// <summary>5C3130/5C5060's posed XYZ workspace, signed plane clipping and unconditional clipped-polygon output.</summary>
public static class NativeBattleShivaPlaneClipping
{
    public static NativeBattleShivaPlane CreatePlane5C3130(NativeBattleVector point, NativeBattleVector normal)
    {
        int dot = unchecked(point.X * normal.X + point.Y * normal.Y + point.Z * normal.Z);
        return new(normal.X, normal.Y, normal.Z, unchecked((short)-(dot >> 12)));
    }

    public static short Distance460860(NativeBattleVector point, NativeBattleShivaPlane plane)
    {
        double dot = plane.Z * (point.Z / 4096d);
        dot += plane.Y * (point.Y / 4096d);
        dot += plane.X * (point.X / 4096d);
        return (short)Math.Clamp(unchecked(NativeBattlePoseMatrices.RoundNative(dot) + plane.W), short.MinValue, short.MaxValue);
    }

    public static NativeBattleShivaClipVertex Intersect5C35F0(NativeBattleShivaClipVertex first, NativeBattleShivaClipVertex second, NativeBattleShivaPlane plane)
    {
        int dx = second.Position.X - first.Position.X;
        int dy = second.Position.Y - first.Position.Y;
        int dz = second.Position.Z - first.Position.Z;
        int denominator = unchecked(plane.X * dx + plane.Y * dy + plane.Z * dz) >> 12;
        if (denominator == 0)
        {
            denominator = 1;
        }
        int numerator = unchecked((plane.W << 12) + plane.X * first.Position.X + plane.Y * first.Position.Y + plane.Z * first.Position.Z);
        int ratio = unchecked(-(numerator / denominator));
        NativeBattleVector position = new(unchecked((short)(first.Position.X + (unchecked(ratio * dx) >> 12))), unchecked((short)(first.Position.Y + (unchecked(ratio * dy) >> 12))), unchecked((short)(first.Position.Z + (unchecked(ratio * dz) >> 12))));
        int u = unchecked((first.Uv & 0xFF) + (unchecked(ratio * ((second.Uv & 0xFF) - (first.Uv & 0xFF))) >> 12));
        int v = unchecked((first.Uv & 0xFF00) + (unchecked(ratio * ((second.Uv & 0xFF00) - (first.Uv & 0xFF00))) >> 12));
        return new(position, unchecked((ushort)((ushort)u | (ushort)(v & 0xFF00))));
    }

    public static ImmutableArray<NativeBattleShivaClipVertex> Clip5C34B0(ImmutableArray<NativeBattleShivaClipVertex> triangle, NativeBattleShivaPlane plane, ImmutableArray<int> selectors)
    {
        if (triangle.IsDefault || triangle.Length != 3 || triangle.Any(vertex => vertex is null))
        {
            throw new InvalidDataException("Native5C34B0 requires three actual XYZ/UV input records.");
        }
        short[] distances = triangle.Select(vertex => Distance460860(vertex.Position, plane)).ToArray();
        int mask = 0;
        for (int edge = 0; edge < 3; edge++)
        {
            if (distances[edge] != 0 && (distances[edge] ^ distances[(edge + 1) % 3]) < 0)
            {
                mask |= 1 << edge;
            }
        }
        if (mask == 0)
        {
            return (distances[0] | distances[1] | distances[2]) > 0 ? triangle : [];
        }
        if (mask == 7)
        {
            return [];
        }
        if (selectors.IsDefault || selectors.Length != 56 || selectors.Any(selector => selector is < 0 or > 5))
        {
            throw new InvalidDataException("Native5C34B0 requires its complete actual clipping-selector table.");
        }
        NativeBattleShivaClipVertex?[] slots = [triangle[0], triangle[1], triangle[2], null, null, null];
        for (int edge = 0; edge < 3; edge++)
        {
            if ((mask & (1 << edge)) != 0)
            {
                slots[3 + edge] = Intersect5C35F0(triangle[edge], triangle[(edge + 1) % 3], plane);
            }
        }
        int row = mask * 8;
        int distanceSlot = selectors[row + 3];
        if (distanceSlot < 3)
        {
            throw new NotSupportedException("Native5C34B0 selector references an original slot without an owned distance suffix.");
        }
        bool positive = distances[distanceSlot - 3] > 0;
        int start = positive ? 0 : 4;
        int count = positive || mask is 1 or 2 or 4 ? 3 : 4;
        ImmutableArray<NativeBattleShivaClipVertex>.Builder result = ImmutableArray.CreateBuilder<NativeBattleShivaClipVertex>(count);
        for (int index = 0; index < count; index++)
        {
            result.Add(slots[selectors[row + start + index]] ?? throw new NotSupportedException("Native5C34B0 selector references an intersection this triangle did not construct."));
        }
        return result.MoveToImmutable();
    }

    public static NativeBattleEffectArena TransformVertices5C50D0(NativeBattleEffectArena arena, NativeBattleRawMesh mesh, ImmutableArray<NativeBattleActionMatrix> bones)
    {
        mesh.Validate();
        if (bones.IsDefaultOrEmpty)
        {
            throw new InvalidDataException("Native5C50D0 requires its actual current model-local bone matrices.");
        }
        for (int index = 0; index < mesh.Vertices.Length; index++)
        {
            NativeBattleRawVertex vertex = mesh.Vertices[index];
            if (vertex.Bone >= bones.Length)
            {
                throw new InvalidDataException("Native5C50D0 vertex leaves the current posed bone table.");
            }
            NativeBattleActionMatrix matrix = bones[vertex.Bone];
            matrix.Validate();
            byte[] bytes = new byte[6];
            for (int row = 0; row < 3; row++)
            {
                double value = matrix.Rotation[row * 3 + 2] * (vertex.Position.Z / 4096d);
                value += matrix.Rotation[row * 3 + 1] * (vertex.Position.Y / 4096d);
                value += matrix.Rotation[row * 3] * (vertex.Position.X / 4096d);
                value += row == 0 ? matrix.X : row == 1 ? matrix.Y : matrix.Z;
                short transformed = (short)Math.Clamp(NativeBattlePoseMatrices.RoundNative(value), short.MinValue, short.MaxValue);
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(row * 2), transformed);
            }
            arena = arena.Write(checked(0x33000 + index * 8), bytes);
        }
        return arena;
    }

    public static NativeBattleShivaPlaneDraw Draw5C5060(NativeBattleEffectArena arena, NativeBattleCactuarRawModelState model, NativeBattleEffectOwnedModelResource resource, NativeBattleActionMatrix viewWorld, NativeBattleCameraProjection projection, NativeBattleShivaPlane plane, ImmutableArray<int> selectors, short zsf3, short zsf4, short dqa, int dqb, Action<ushort>? observeDepth = null)
    {
        NativeBattleCactuarRawModel.Validate(model, resource, NativeBattleShivaModelTask.Binding);
        NativeBattleDecodedPose pose = model.DecodedPose ?? throw new NotSupportedException("Native5C5060 requires its current decoded model pose.");
        if (model.ProjectedScratch is not null || resource.Geometry.IsDefault || resource.Geometry.Length != 1)
        {
            throw new InvalidDataException("Native5C5060 requires its one original geometry object and shared arena XYZ workspace.");
        }
        viewWorld.Validate();
        uint mask = BinaryPrimitives.ReadUInt32LittleEndian(model.Model.AsSpan().Slice(0x7C));
        if ((mask & 1) == 0)
        {
            return new(arena, new([], 0), 0);
        }
        NativeBattleActionMatrix local = new(Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(model.Model.AsSpan().Slice(0x40 + index * 2))).ToImmutableArray(), ReadPosition(0x54), ReadPosition(0x58), ReadPosition(0x5C), 4096);
        ImmutableArray<NativeBattleActionMatrix> bones = NativeBattlePoseMatrices.ComposeCurrentPose(resource.PoseSource, pose, local);
        NativeBattleRawMesh mesh = resource.Geometry[0];
        arena = TransformVertices5C50D0(arena, mesh, bones);
        uint color = BinaryPrimitives.ReadUInt32LittleEndian(model.Model.AsSpan().Slice(0x28));
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocation = 0;
        foreach (NativeBattleRawPolygon polygon in mesh.Polygons)
        {
            ReadOnlySpan<byte> bytes = polygon.Bytes.AsSpan();
            if (bytes.Length != (polygon.Quad ? 20 : 16))
            {
                throw new NotSupportedException("Native5C53D0 requires its original packed raw FT3/FT4 records.");
            }
            uint firstMaterial = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(8));
            uint secondMaterial = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(12));
            NativeBattleShivaClipVertex first = ReadVertex(bytes, 0, 8);
            NativeBattleShivaClipVertex second = ReadVertex(bytes, 2, 12);
            NativeBattleShivaClipVertex third = ReadVertex(bytes, 4, polygon.Quad ? 16 : 6);
            NativeBattleModuleProjectedVertex[] projected = [Project(first), Project(second), Project(third)];
            if (((projected[0].Flags | projected[1].Flags | projected[2].Flags) & 0x60000) != 0 || NativeBattleModulePackets.Nclip(projected[0].Screen, projected[1].Screen, projected[2].Screen) <= 0)
            {
                continue;
            }
            Emit(Clip5C34B0([first, second, third], plane, selectors), firstMaterial, secondMaterial);
            if (polygon.Quad)
            {
                NativeBattleShivaClipVertex fourth = ReadVertex(bytes, 6, 18);
                _ = Project(fourth);
                _ = Project(second);
                _ = Project(third);
                Emit(Clip5C34B0([fourth, second, third], plane, selectors), firstMaterial, secondMaterial);
            }
        }
        return new(arena, new(attempts.ToImmutable(), allocation), mesh.Vertices.Length);

        int ReadPosition(int offset) => BinaryPrimitives.ReadInt32LittleEndian(model.Model.AsSpan().Slice(offset));

        NativeBattleShivaClipVertex ReadVertex(ReadOnlySpan<byte> face, int indexOffset, int uvOffset)
        {
            int index = BinaryPrimitives.ReadUInt16LittleEndian(face.Slice(indexOffset)) & 0xFFF;
            byte[] xyz = arena.Read(checked(0x33000 + index * 8), 6);
            return new(new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4))), BinaryPrimitives.ReadUInt16LittleEndian(face.Slice(uvOffset)));
        }

        NativeBattleModuleProjectedVertex Project(NativeBattleShivaClipVertex vertex)
        {
            NativeBattleModuleProjectedVertex projected = NativeBattleModuleProjection.Project45FE10(viewWorld, vertex.Position, projection);
            observeDepth?.Invoke(projected.Depth);
            return projected;
        }

        void Emit(ImmutableArray<NativeBattleShivaClipVertex> vertices, uint firstMaterial, uint secondMaterial)
        {
            if (vertices.IsEmpty)
            {
                return;
            }
            NativeBattleGfGeometryAttempt attempt = Emit5C3410(vertices, viewWorld, projection, color, firstMaterial, secondMaterial, zsf3, zsf4, dqa, dqb, allocation, observeDepth);
            attempts.Add(attempt);
            allocation = checked(allocation + attempt.PacketLength);
        }
    }

    public static NativeBattleGfGeometryAttempt Emit5C3410(ImmutableArray<NativeBattleShivaClipVertex> vertices, NativeBattleActionMatrix viewWorld, NativeBattleCameraProjection projection, uint color, uint firstMaterial, uint secondMaterial, short zsf3, short zsf4, short dqa, int dqb, int byteOffsetRelative = 0, Action<ushort>? observeDepth = null)
    {
        if (vertices.IsDefault || vertices.Length is not (3 or 4))
        {
            throw new InvalidDataException("Native clipped output requires its three or four selected XYZ/UV records.");
        }
        bool quad = vertices.Length == 4;
        byte[] data = new byte[quad ? 40 : 32];
        NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[data.Length];
        NativeBattleModuleProjectedVertex[] projected = new NativeBattleModuleProjectedVertex[vertices.Length];
        for (int index = 0; index < projected.Length; index++)
        {
            projected[index] = index == 3 ? NativeBattleModuleProjection.Project45FAA0(viewWorld, vertices[index].Position, projection, dqa, dqb) : NativeBattleModuleProjection.Project45FE10(viewWorld, vertices[index].Position, projection);
            observeDepth?.Invoke(projected[index].Depth);
        }
        Store32(0, quad ? 0x09000000u : 0x07000000u);
        Store32(4, color | (quad ? 0x2C000000u : 0x24000000u));
        Store32(12, firstMaterial);
        Store32(20, secondMaterial);
        for (int index = 0; index < projected.Length; index++)
        {
            Store32(8 + index * 8, projected[index].Screen);
            int uvOffset = 12 + index * 8;
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(uvOffset), vertices[index].Uv);
            Array.Fill(mask, NativeBattleGfPacketByteState.Known, uvOffset, 2);
        }
        uint average = unchecked((uint)projected.Sum(vertex => (int)vertex.Depth) * (uint)(int)(quad ? zsf4 : zsf3)) >> 12;
        int bucket = unchecked(17 + (unchecked((int)average) >> 2));
        ImmutableArray<uint> words = Enumerable.Range(0, data.Length / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(index * 4))).ToImmutableArray();
        ImmutableArray<int> payload = quad ? projected.Select(vertex => (int)vertex.Depth).ToImmutableArray() : [projected[0].Depth, projected[1].Depth, projected[2].Depth, 0];
        NativeBattleGeometryPrimitive primitive = new(bucket, words, payload) { PhysicalWriteMask = mask.ToImmutableArray() };
        return new(byteOffsetRelative, data.ToImmutableArray(), mask.ToImmutableArray(), primitive);

        void Store32(int offset, uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), value);
            Array.Fill(mask, NativeBattleGfPacketByteState.Known, offset, 4);
        }
    }
}
