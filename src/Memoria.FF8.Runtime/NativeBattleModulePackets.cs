using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleAllocatedPrimitive(NativeBattleGeometryPrimitive Primitive, int ByteOffset, ushort Flags20);
public sealed record NativeBattleModulePacketBatch(ImmutableArray<NativeBattleAllocatedPrimitive> Primitives, int AllocationBytes)
{
    public NativeBattleGfGeometryBatch? CapturedWrites { get; init; }
}

/// <summary>572950/572DE0/571DC0 packets with original GTE depth, physical allocation spacing and insertion order.</summary>
public static partial class NativeBattleModulePackets
{
    public static NativeBattleModulePacketBatch EmitSprite571DC0(NativeBattleModuleDraw draw, NativeBattleCameraProjection projection, short cameraRoll, int tableBaseBucket, byte depthShift, NativeBattleActionMatrix? currentMatrix = null)
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
        NativeBattleActionMatrix main = currentMatrix ?? NativeBattlePoseMatrices.ModuleSpriteCamera(draw, projection.View ?? throw new NotSupportedException("Native571DC0 requires its previous published camera matrix."), cameraRoll);
        main.Validate();
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
            NativeBattleGeometryPrimitive primitive = new(bucket, NativeBattleTelemetry.Immutable(1119, words), vertices.Select(vertex => (int)vertex.Depth).ToImmutableArray());
            if (draw.PilotIdentity is { } identity)
            {
                NativeBattleCameraProjection pilotProjection = draw.PilotProjection ?? projection;
                System.Numerics.Vector3 World(int x, int y) => NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(matrix, new(unchecked((short)x), unchecked((short)y), 0)), pilotProjection);
                System.Numerics.Vector3 a = World(-width, -height);
                System.Numerics.Vector3 b = World(width, -height);
                System.Numerics.Vector3 c = World(-width, height);
                System.Numerics.Vector3 d = World(width, height);
                NativeBattlePilotSpriteGeometry? sprite = !draw.PilotContinuous ? null : draw.PilotProjection is null ? NativeBattlePilotSpriteGeometry.Capture(main, quad, width, height, projection) : CaptureActualSprite(main, pilotProjection, a, b, c, d);
                primitive = primitive with { PilotGeometry = new(identity with { Primitive = unchecked(identity.Primitive * 397 + offset / 20) }, a, b, c, d, 4, draw.PilotContinuous) { Sprite = sprite } };
            }
            result.Add(new(primitive, allocated, 0));
            allocated += extended ? 52 : 40;
            //571DC0 stores the first accepted AVSZ4 in contextDWORD60 and reuses its bucket for subsequent quads.
            retainedBucket = bucket;

            NativeBattleProjectedVertex Project(int x, int y) => NativeBattleModuleProjection.Project45FE10(matrix, new(unchecked((short)x), unchecked((short)y), 0), projection);
        }
        return new(result.ToImmutable(), allocated);
    }

    private static NativeBattlePilotSpriteGeometry? CaptureActualSprite(NativeBattleActionMatrix main, NativeBattleCameraProjection projection, System.Numerics.Vector3 a, System.Numerics.Vector3 b, System.Numerics.Vector3 c, System.Numerics.Vector3 d)
    {
        System.Numerics.Vector3 origin = NativeBattlePilotVisualGeometry.CameraToWorld(new(main.X, main.Y, main.Z), projection);
        System.Numerics.Vector3 Axis(int column) => NativeBattlePilotVisualGeometry.CameraToWorldDirection(new(main.Rotation[column] / 4096f, main.Rotation[3 + column] / 4096f, main.Rotation[6 + column] / 4096f), projection);
        System.Numerics.Vector3 x = Axis(0);
        System.Numerics.Vector3 y = Axis(1);
        System.Numerics.Vector3 z = Axis(2);
        System.Numerics.Matrix4x4 basis = new(x.X, x.Y, x.Z, 0, y.X, y.Y, y.Z, 0, z.X, z.Y, z.Z, 0, origin.X, origin.Y, origin.Z, 1);
        if (!System.Numerics.Matrix4x4.Invert(basis, out System.Numerics.Matrix4x4 inverse))
        {
            return null;
        }
        System.Numerics.Vector3 Local(System.Numerics.Vector3 point) => System.Numerics.Vector3.Transform(point, inverse);
        return new(Local(a), Local(b), Local(c), Local(d), origin, x, y, z);
    }

    private static int Bucket(IReadOnlyList<NativeBattleProjectedVertex> vertices, int tableBaseBucket, byte depthShift)
    {
        //571DC0 retains the first accepted quad's original ZSF4=256 depth bucket.
        int depth = vertices.Sum(vertex => (int)vertex.Depth) >> 4;
        return checked(tableBaseBucket + (depth >> depthShift));
    }

    public static int Nclip(uint a, uint b, uint c)
    {
        int ax = unchecked((short)a);
        int ay = unchecked((int)a) >> 16;
        int bx = unchecked((short)b);
        int by = unchecked((int)b) >> 16;
        int cx = unchecked((short)c);
        int cy = unchecked((int)c) >> 16;
        return unchecked(bx * (cy - ay) + ax * (by - cy) + cx * (ay - by));
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
