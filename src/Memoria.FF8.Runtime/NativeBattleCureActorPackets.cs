using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public enum NativeBattleCurePacketBank : byte
{
    Process,
    EffectPrimary
}

public sealed record NativeBattleCureAllocatedPrimitive(NativeBattleGeometryPrimitive Primitive, NativeBattleCurePacketBank Bank, int ByteOffset);
public sealed record NativeBattleCureActorPacketBatch(ImmutableArray<NativeBattleCureAllocatedPrimitive> Primitives, int ProcessAllocationBytes, int EffectAllocationBytes);
public sealed record NativeBattleCureTextureProjection(ushort TexturePage, ushort Clut, short MinimumX, short MaximumX, short MinimumY, short MaximumY, short OffsetU, short OffsetV);

/// <summary>8D7050/8D7430's original actor texture projection, two packet cursors and interleaved45C7A0/45C870 submissions.</summary>
public static class NativeBattleCureActorPackets
{
    public static NativeBattleCureTextureProjection TextureProjection8D7050(short textureX, short textureY, short clutX, short clutY, short width, short height)
    {
        int page = unchecked((short)((textureX & 0x3C0) | 0x2800)) >> 6;
        page |= textureY >> 4 & 0x10;
        page |= (textureY & 0x200) << 2;
        ushort clut = unchecked((ushort)((clutX >> 4 & 63) | clutY << 6));
        int halfWidth = width / 2;
        int halfHeight = height / 2;
        return new(unchecked((ushort)page), clut, unchecked((short)(160 - halfWidth)), unchecked((short)(159 + halfWidth)), unchecked((short)(120 - halfHeight)), unchecked((short)(119 + halfHeight)), unchecked((short)(2 * (textureX & 63) + halfWidth - 160)), unchecked((short)((textureY & 255) + halfHeight - 120)));
    }

    public static NativeBattleCureActorPacketBatch Emit8D7430(ImmutableArray<NativeBattleRawMesh> meshes, IReadOnlyList<NativeBattleActionMatrix> bodyBones, NativeBattleActionMatrix mainCamera, NativeBattleActionMatrix overlayCamera, NativeBattleCameraProjection projection, uint objectMask, uint baseRgb, uint overlayRgb, NativeBattleCureTextureProjection texture, ushort? rejectOverlayBackfaces, NativeBattleModelTextureBinding? textureBinding, int tableBaseBucket, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        if (meshes.IsDefault || bodyBones.Count == 0)
        {
            throw new NotSupportedException("Native8D7430 requires its original actor geometry and current posed body-bone matrices.");
        }
        mainCamera.Validate();
        overlayCamera.Validate();
        int processBytes = 0;
        int effectBytes = 0;
        ImmutableArray<NativeBattleCureAllocatedPrimitive>.Builder result = ImmutableArray.CreateBuilder<NativeBattleCureAllocatedPrimitive>();
        for (int meshIndex = 0; meshIndex < meshes.Length; meshIndex++)
        {
            if ((objectMask & (1u << (meshIndex & 31))) == 0)
            {
                continue;
            }
            NativeBattleRawMesh mesh = meshes[meshIndex];
            mesh.Validate();
            NativeBattleVector[] vertices = mesh.Vertices.Select(vertex => TransformBone4607E0(vertex, bodyBones)).ToArray();
            //The native function visits only plain FT3/FT4 groups, in that order; colored groups are not submitted.
            foreach ((NativeBattleRawPolygon original, int polygonIndex) in mesh.Polygons.Select((polygon, index) => (polygon, index)).Where(item => !item.polygon.Quad).Concat(mesh.Polygons.Select((polygon, index) => (polygon, index)).Where(item => item.polygon.Quad)))
            {
                int count = original.Quad ? 4 : 3;
                int sourceLength = original.Quad ? 20 : 16;
                if (original.Bytes.Length != sourceLength)
                {
                    continue;
                }
                ImmutableArray<byte> source = textureBinding is null ? original.Bytes : NativeBattleRawGeometryPackets.Relocate507550(original, textureBinding);
                NativeBattleVector[] corners = new NativeBattleVector[count];
                for (int corner = 0; corner < count; corner++)
                {
                    int index = BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan().Slice(2 * corner)) & 0xFFF;
                    corners[corner] = (uint)index < vertices.Length ? vertices[index] : throw new InvalidDataException("Native8D7430 polygon references a missing original vertex.");
                }
                NativeBattleModuleProjectedVertex[] actual = corners.Select(vertex => NativeBattleModuleProjection.Project45FE10(mainCamera, vertex, projection)).ToArray();
                if (Nclip(actual) <= 0)
                {
                    continue;
                }
                int sum = actual.Sum(vertex => vertex.Depth);
                int otz = original.Quad ? sum >> 4 : Math.Clamp(unchecked(sum * 0x155) >> 12, 0, 65535);
                int bucket = checked(tableBaseBucket + (otz >> 2));
                NativeBattleModuleProjectedVertex[] projected = corners.Select(vertex => NativeBattleModuleProjection.Project45FE10(overlayCamera, vertex, projection)).ToArray();
                if (WithinProjection(projected, texture))
                {
                    int facing = Nclip(projected);
                    if (facing <= 0 && rejectOverlayBackfaces is null)
                    {
                        throw new NotSupportedException("Native8D7430 requires the retained contextWORD F4 for this projected backface.");
                    }
                    if (facing > 0 || rejectOverlayBackfaces != 1)
                    {
                        NativeBattleGeometryPrimitive overlay = Capture(Overlay(original.Quad, actual, projected, bucket, overlayRgb, texture), 1);
                        result.Add(new(overlay, NativeBattleCurePacketBank.EffectPrimary, effectBytes));
                        effectBytes = checked(effectBytes + overlay.Words.Length * 4);
                    }
                }
                NativeBattleGeometryPrimitive body = Capture(Body(original.Quad, source.AsSpan(), actual, bucket, baseRgb), 0);
                result.Add(new(body, NativeBattleCurePacketBank.Process, processBytes));
                processBytes = checked(processBytes + body.Words.Length * 4);

                NativeBattleGeometryPrimitive Capture(NativeBattleGeometryPrimitive primitive, int layer)
                {
                    if (pilotIdentity is not { } identity)
                    {
                        return primitive;
                    }
                    System.Numerics.Vector3 World(int corner) => NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(mainCamera, corners[corner]), projection);
                    ImmutableArray<int> depths = primitive.VertexPayload4;
                    return primitive with { PilotGeometry = new(identity with { Primitive = unchecked((identity.Primitive * 397 + meshIndex) * 397 + polygonIndex * 2 + layer) }, World(0), World(1), World(2), original.Quad ? World(3) : default, checked((byte)count)) { SourceDepths = new(depths[0], depths[1], depths[2], depths[3]), UseSourceDepths = true } };
                }
            }
        }
        return new(result.ToImmutable(), processBytes, effectBytes);
    }

    public static NativeBattleVector TransformBone4607E0(NativeBattleRawVertex vertex, IReadOnlyList<NativeBattleActionMatrix> bodyBones)
    {
        if (vertex.Bone >= bodyBones.Count)
        {
            throw new InvalidDataException("Native8D7430 vertex group exceeds the posed body skeleton.");
        }
        NativeBattleActionMatrix bone = bodyBones[vertex.Bone];
        bone.Validate();
        int Row(int row) => NativeBattlePoseMatrices.RoundNative((float)(bone.Rotation[row * 3 + 2] / 4096d) * (double)vertex.Position.Z + (float)(bone.Rotation[row * 3 + 1] / 4096d) * (double)vertex.Position.Y + (float)(bone.Rotation[row * 3] / 4096d) * (double)vertex.Position.X);
        return new((short)Math.Clamp(unchecked(Row(0) + bone.X), short.MinValue, short.MaxValue), (short)Math.Clamp(unchecked(Row(1) + bone.Y), short.MinValue, short.MaxValue), (short)Math.Clamp(unchecked(Row(2) + bone.Z), short.MinValue, short.MaxValue));
    }

    private static bool WithinProjection(IReadOnlyList<NativeBattleModuleProjectedVertex> vertices, NativeBattleCureTextureProjection texture)
    {
        return vertices.All(vertex => unchecked((short)vertex.Screen) >= texture.MinimumX && unchecked((short)vertex.Screen) <= texture.MaximumX && unchecked((short)(vertex.Screen >> 16)) >= texture.MinimumY && unchecked((short)(vertex.Screen >> 16)) <= texture.MaximumY);
    }

    private static int Nclip(IReadOnlyList<NativeBattleModuleProjectedVertex> vertices)
    {
        int ax = unchecked((short)vertices[0].Screen);
        int ay = unchecked((short)(vertices[0].Screen >> 16));
        int bx = unchecked((short)vertices[1].Screen);
        int by = unchecked((short)(vertices[1].Screen >> 16));
        int cx = unchecked((short)vertices[2].Screen);
        int cy = unchecked((short)(vertices[2].Screen >> 16));
        return unchecked(ax * (by - cy) + bx * (cy - ay) + cx * (ay - by));
    }

    private static NativeBattleGeometryPrimitive Body(bool quad, ReadOnlySpan<byte> source, IReadOnlyList<NativeBattleModuleProjectedVertex> actual, int bucket, uint rgb)
    {
        uint color = (quad ? 0x2C000000u : 0x24000000u) | (rgb & 0xFFFFFF);
        if ((source[15] & 2) != 0)
        {
            color |= 0x2000000;
        }
        uint[] words = new uint[quad ? 10 : 8];
        words[0] = (uint)(words.Length - 1) << 24;
        words[1] = color;
        words[2] = actual[0].Screen;
        words[3] = BinaryPrimitives.ReadUInt32LittleEndian(source[8..]);
        words[4] = actual[1].Screen;
        words[5] = BinaryPrimitives.ReadUInt32LittleEndian(source[12..]);
        words[6] = actual[2].Screen;
        words[7] = BinaryPrimitives.ReadUInt16LittleEndian(source[(quad ? 16 : 6)..]);
        if (quad)
        {
            words[8] = actual[3].Screen;
            words[9] = BinaryPrimitives.ReadUInt16LittleEndian(source[18..]);
        }
        ImmutableArray<int> depths = actual.Select(vertex => (int)vertex.Depth).ToImmutableArray();
        return new(bucket, words.ToImmutableArray(), quad ? depths : depths.Add(0));
    }

    private static NativeBattleGeometryPrimitive Overlay(bool quad, IReadOnlyList<NativeBattleModuleProjectedVertex> actual, IReadOnlyList<NativeBattleModuleProjectedVertex> projected, int bucket, uint rgb, NativeBattleCureTextureProjection texture)
    {
        uint color = (quad ? 0x3E000000u : 0x36000000u) | (rgb & 0xFFFFFF);
        uint[] words = new uint[quad ? 13 : 10];
        words[0] = (uint)(words.Length - 1) << 24;
        for (int corner = 0; corner < actual.Count; corner++)
        {
            words[1 + corner * 3] = color;
            words[2 + corner * 3] = actual[corner].Screen;
            byte u = unchecked((byte)(unchecked((short)projected[corner].Screen) + texture.OffsetU));
            byte v = unchecked((byte)(unchecked((short)(projected[corner].Screen >> 16)) + texture.OffsetV));
            uint uv = (uint)(u | v << 8);
            if (corner == 0)
            {
                uv |= (uint)texture.Clut << 16;
            }
            else if (corner == 1)
            {
                uv |= (uint)texture.TexturePage << 16;
            }
            words[3 + corner * 3] = uv;
        }
        //45C7A0 captures the SECOND projection's depth FIFO, while45C870 receives saved actual-view depths.
        ImmutableArray<int> depths = projected.Select(vertex => (int)vertex.Depth).ToImmutableArray();
        return new(bucket, words.ToImmutableArray(), quad ? depths : depths.Add(0));
    }
}
