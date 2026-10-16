using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleAllocatedPrimitive(NativeBattleGeometryPrimitive Primitive, int ByteOffset, ushort Flags20);
public sealed record NativeBattleModulePacketBatch(ImmutableArray<NativeBattleAllocatedPrimitive> Primitives, int AllocationBytes);

/// <summary>572950/572DE0/571DC0 packets with original GTE depth, physical allocation spacing and insertion order.</summary>
public static partial class NativeBattleModulePackets
{
    public static NativeBattleModulePacketBatch EmitMesh572200(NativeBattleModuleDraw draw, NativeBattleStaticMeshResource resource, NativeBattleCameraProjection projection, int tableBaseBucket, byte depthShift)
    {
        if ((draw.MeshFlags & ~0x2FFu) != 0)
        {
            throw new NotSupportedException("Native572200 requires an unowned retained UV or texture-page override.");
        }
        NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.ModuleMeshCamera(draw, projection.View ?? throw new NotSupportedException("Native572200 requires its previous published camera matrix."));
        byte[] bytes = resource.Bytes.ToArray();
        int cursor = checked((int)Read32(bytes, 0));
        int allocated = 0;
        ImmutableArray<NativeBattleAllocatedPrimitive>.Builder result = ImmutableArray.CreateBuilder<NativeBattleAllocatedPrimitive>();
        Dictionary<ushort, NativeBattleProjectedVertex> projected = [];
        for (int group = 0; group < 8; group++)
        {
            int count = checked((int)Read32(bytes, cursor));
            cursor += 4;
            if (count != 0 && group is not (3 or 4 or 5))
            {
                throw new NotSupportedException($"Native572200 primitive group{group} requires its original packet emitter.");
            }
            for (int face = 0; face < count; face++)
            {
                int stride = group == 4 ? 20 : 24;
                Require(bytes, cursor, stride);
                NativeBattleProjectedVertex[] vertices = new NativeBattleProjectedVertex[group == 4 ? 3 : 4];
                for (int corner = 0; corner < vertices.Length; corner++)
                {
                    ushort index = Read16(bytes, cursor + 4 + corner * 2);
                    if (!projected.TryGetValue(index, out NativeBattleProjectedVertex vertex))
                    {
                        int address = checked(8 + index * 4);
                        Require(bytes, address, 6);
                        vertex = NativeBattleModuleProjection.Project45FE10(matrix, new(ReadSigned16(bytes, address), ReadSigned16(bytes, address + 2), ReadSigned16(bytes, address + 4)), projection);
                        projected.Add(index, vertex);
                    }
                    vertices[corner] = vertex;
                }
                NativeBattleGeometryPrimitive? primitive = group == 4 ? EmitGouraudTriangle572BE0(bytes.AsSpan(cursor, stride), vertices, draw.MeshFlags, draw.MeshFade, tableBaseBucket, depthShift) : EmitQuad(bytes.AsSpan(cursor, stride), vertices, group == 3, draw.MeshFlags, draw.MeshClut, draw.MeshFade, tableBaseBucket, depthShift);
                if (primitive is not null)
                {
                    result.Add(new(primitive, allocated, 0));
                    allocated += primitive.Words.Length * 4;
                }
                cursor += stride;
            }
        }
        return new(result.ToImmutable(), allocated);
    }

    public static NativeBattleGeometryPrimitive? EmitQuad(ReadOnlySpan<byte> face, IReadOnlyList<NativeBattleProjectedVertex> vertices, bool textured, uint flags, ushort clut, int fade, int tableBaseBucket, byte depthShift)
    {
        if (face.Length != 24 || vertices.Count != 4 || depthShift > 31)
        {
            throw new InvalidDataException("Native module quad requires its original24-byte face, four projected vertices and depth shift.");
        }
        if (((vertices[0].DepthAndClip | vertices[1].DepthAndClip | vertices[2].DepthAndClip) & 0x60000) != 0)
        {
            return null;
        }
        if (Nclip(vertices[0].Screen, vertices[1].Screen, vertices[2].Screen) < 0 && (flags & (textured ? 0x10u : 0x20u)) == 0)
        {
            return null;
        }
        if (vertices.All(vertex => unchecked((ushort)vertex.Screen) > 0xA00) || vertices.All(vertex => (vertex.Screen >> 16) > 0x6C0))
        {
            return null;
        }
        uint first = BinaryPrimitives.ReadUInt32LittleEndian(face);
        if ((flags & (textured ? 1u : 2u)) != 0)
        {
            first |= 0x2000000;
        }
        if ((flags & (textured ? 4u : 8u)) != 0)
        {
            first &= 0xFDFFFFFF;
        }
        uint[] words = new uint[textured ? 10 : 9];
        words[0] = (uint)(words.Length - 1) << 24;
        words[1] = (flags & (textured ? 0x40u : 0x80u)) != 0 ? Fade(first, fade) : first;
        if (textured)
        {
            words[2] = vertices[0].Screen;
            words[3] = BinaryPrimitives.ReadUInt32LittleEndian(face[12..]);
            if ((flags & 0x200) != 0)
            {
                words[3] = (words[3] & 0xFFFF) | (uint)clut << 16;
            }
            words[4] = vertices[1].Screen;
            words[5] = BinaryPrimitives.ReadUInt32LittleEndian(face[16..]);
            words[6] = vertices[2].Screen;
            words[7] = BinaryPrimitives.ReadUInt32LittleEndian(face[20..]);
            words[8] = vertices[3].Screen;
            words[9] = BinaryPrimitives.ReadUInt16LittleEndian(face[22..]);
        }
        else
        {
            for (int corner = 0; corner < 4; corner++)
            {
                if (corner != 0)
                {
                    uint color = BinaryPrimitives.ReadUInt32LittleEndian(face[(8 + corner * 4)..]);
                    words[1 + corner * 2] = (flags & 0x80) != 0 ? (Fade(color, fade) & 0xFFFFFF) | (uint)face[23] << 24 : color;
                }
                words[2 + corner * 2] = vertices[corner].Screen;
            }
        }
        return new(Bucket(vertices, tableBaseBucket, depthShift), words.ToImmutableArray(), vertices.Select(vertex => (int)vertex.Depth).ToImmutableArray());
    }

    public static NativeBattleModulePacketBatch EmitSprite571DC0(NativeBattleModuleDraw draw, NativeBattleCameraProjection projection, short cameraRoll, int tableBaseBucket, byte depthShift)
    {
        if (depthShift > 31)
        {
            throw new InvalidDataException("Native571DC0 requires its original depth shift.");
        }
        NativeBattleModuleResource resource = draw.Resource ?? throw new InvalidDataException("Native571DC0 lacks its original sprite resource.");
        NativeBattleModuleSpriteFrame frame = draw.SpriteFrame ?? throw new InvalidDataException("Native571DC0 lacks its selected frame.");
        if (frame.Quads.IsDefault || frame.Quads.Length % 20 != 0)
        {
            throw new InvalidDataException("Native571DC0 quad cursor leaves its original frame.");
        }
        NativeBattleActionMatrix main = NativeBattlePoseMatrices.ModuleSpriteCamera(draw, projection.View ?? throw new NotSupportedException("Native571DC0 requires its previous published camera matrix."), cameraRoll);
        ImmutableArray<NativeBattleAllocatedPrimitive>.Builder result = ImmutableArray.CreateBuilder<NativeBattleAllocatedPrimitive>();
        int allocated = 0;
        int? retainedBucket = null;
        for (int offset = 0; offset < frame.Quads.Length; offset += 20)
        {
            ReadOnlySpan<byte> quad = frame.Quads.AsSpan().Slice(offset, 20);
            NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.ModuleSpriteQuadCamera(main, quad);
            short width = (short)(8 * quad[0]);
            short height = (short)(8 * quad[2]);
            NativeBattleProjectedVertex[] vertices = [Project(-width, -height), Project(width, -height), Project(-width, height), Project(width, height)];
            if (((vertices[0].DepthAndClip | vertices[1].DepthAndClip | vertices[2].DepthAndClip) & 0x60000) != 0)
            {
                continue;
            }
            ushort flags = BinaryPrimitives.ReadUInt16LittleEndian(quad[14..]);
            int selector = flags >> 12;
            if (selector > 3 || resource.Bytes.Length < 8)
            {
                throw new NotSupportedException("Native571DC0 requires its actual four palette BYTE registers.");
            }
            int palette;
            if ((draw.SpriteFlags & (0x10 << selector)) != 0)
            {
                if (draw.SpritePaletteOffsets.Length != 4)
                {
                    throw new NotSupportedException("Native571C80 preserved an unobserved explicit palette BYTE.");
                }
                palette = unchecked((sbyte)draw.SpritePaletteOffsets[selector]);
            }
            else
            {
                palette = unchecked((sbyte)resource.Bytes[4 + selector]);
            }
            uint color = (draw.SpriteFlags & 4) != 0 ? draw.SpriteColor : 0x808080;
            uint command = (uint)quad[3] << 24;
            for (int channel = 0; channel < 3; channel++)
            {
                command |= (uint)unchecked((byte)(((color >> (8 * channel)) & 255) * quad[1] >> 7)) << (8 * channel);
            }
            int inset = (draw.SpriteFlags & 8) != 0 ? 1 : 0;
            byte u1 = unchecked((byte)Math.Min(quad[0] + quad[4] - inset, 255));
            byte v1 = unchecked((byte)Math.Min(quad[2] + quad[5] - inset, 255));
            uint[] words = [0x9000000, command, vertices[0].Screen, (uint)(quad[4] | quad[5] << 8) | (uint)unchecked((ushort)(BinaryPrimitives.ReadUInt16LittleEndian(quad[6..]) + (palette << 6))) << 16, vertices[1].Screen, (uint)(u1 | quad[5] << 8) | (uint)(flags & 0x1FF) << 16, vertices[2].Screen, (uint)(quad[4] | v1 << 8), vertices[3].Screen, (uint)(u1 | v1 << 8)];
            int bucket = retainedBucket ?? Bucket(vertices, tableBaseBucket, depthShift);
            bool extended = retainedBucket is null && (frame.ExtendedPacket || (draw.SpriteFlags & 0x100) != 0);
            if (extended)
            {
                //571DC0 reserves52 bytes, puts E1 at+40, inserts it first, then inserts the FT4 at+0.
                result.Add(new(new(bucket, [0x1000000, 0xE1000220], [0, 0, 0, 0]), allocated + 40, 0));
            }
            result.Add(new(new(bucket, words.ToImmutableArray(), vertices.Select(vertex => (int)vertex.Depth).ToImmutableArray()), allocated, 0));
            allocated += extended ? 52 : 40;
            //571DC0 stores the first accepted AVSZ4 in contextDWORD60 and reuses its bucket for subsequent quads.
            retainedBucket = bucket;

            NativeBattleProjectedVertex Project(int x, int y) => NativeBattleModuleProjection.Project45FE10(matrix, new(unchecked((short)x), unchecked((short)y), 0), projection);
        }
        return new(result.ToImmutable(), allocated);
    }

    private static int Bucket(IReadOnlyList<NativeBattleProjectedVertex> vertices, int tableBaseBucket, byte depthShift)
    {
        //56BAA0 establishes ZSF4=256.45E610 sums all four unsigned depth WORDs before shifting12.
        int depth = vertices.Sum(vertex => (int)vertex.Depth) >> 4;
        return checked(tableBaseBucket + (depth >> depthShift));
    }

    private static int Nclip(uint a, uint b, uint c)
    {
        int ax = unchecked((short)a);
        int ay = unchecked((int)a) >> 16;
        int bx = unchecked((short)b);
        int by = unchecked((int)b) >> 16;
        int cx = unchecked((short)c);
        int cy = unchecked((int)c) >> 16;
        return unchecked(bx * (cy - ay) + ax * (by - cy) + cx * (ay - by));
    }

    private static uint Fade(uint color, int fade)
    {
        uint result = color & 0xFF000000;
        for (int channel = 0; channel < 3; channel++)
        {
            int value = (int)((color >> (channel * 8)) & 255) << 4;
            int interpolated = unchecked(value + NativeBattlePoseMatrices.RoundNative(-value * (unchecked((short)fade) / 4096d))) >> 4;
            result |= (uint)Math.Clamp(interpolated, 0, 255) << (channel * 8);
        }
        return result;
    }

    private static ushort Read16(byte[] bytes, int offset)
    {
        Require(bytes, offset, 2);
        return BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset));
    }

    private static short ReadSigned16(byte[] bytes, int offset) => unchecked((short)Read16(bytes, offset));

    private static uint Read32(byte[] bytes, int offset)
    {
        Require(bytes, offset, 4);
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset));
    }

    private static void Require(byte[] bytes, int first, int length)
    {
        if (first < 0 || length < 0 || first > bytes.Length - length)
        {
            throw new InvalidDataException("Native module mesh cursor leaves its original static resource.");
        }
    }
}
