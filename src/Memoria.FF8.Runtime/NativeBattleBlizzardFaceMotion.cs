using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>61B190's complete face writes and61B9A0's current physical record motion before RTPT.</summary>
public static class NativeBattleBlizzardFaceMotion
{
    public static (NativeBattleBlizzardFaces Faces, NativeCrtRandomState Random) Initialize61B190(NativeBattleBlizzardFaces? faces, int sequence, NativeBattleStaticMeshResource mesh, NativeCrtRandomState random)
    {
        ReadOnlySpan<byte> source = mesh.Bytes.AsSpan();
        int directory = BinaryPrimitives.ReadInt32LittleEndian(source) + 8;
        int count = BinaryPrimitives.ReadInt32LittleEndian(source[directory..]);
        if (count != 80)
        {
            throw new InvalidDataException("Native Blizzard61B190 requires its actual80 physical FT3 face records.");
        }
        byte[] bytes = new byte[NativeBattleBlizzardFaces.SliceLength];
        for (int index = 0; index < count; index++)
        {
            ReadOnlySpan<byte> face = source.Slice(directory + 4 + 20 * index, 20);
            NativeBattleVector[] vertices = Enumerable.Range(0, 3).Select(corner => Vertex(mesh.Bytes, BinaryPrimitives.ReadUInt16LittleEndian(mesh.Bytes.AsSpan().Slice(directory + 4 + 20 * index + 4 + 2 * corner)))).ToArray();
            int sumX = vertices.Sum(vertex => vertex.X);
            int sumY = vertices.Sum(vertex => vertex.Y);
            int sumZ = vertices.Sum(vertex => vertex.Z);
            random = random.Draw(out ushort value);
            int row = index * 32;
            int eligibility = unchecked(sumY * 8 + 0x4780);
            int quotient = unchecked((int)(((long)unchecked((int)0x84767DFF) * eligibility) >> 32) + eligibility) >> 11;
            quotient += (int)((uint)quotient >> 31);
            int velocityX = (value & 7) << 5;
            int velocityZ = ((value >> 3) & 7) << 4;
            if ((value & 0x800) != 0)
            {
                velocityX = -velocityX;
            }
            if ((value & 0x4080) != 0)
            {
                velocityZ = -velocityZ;
            }
            int duration = value % 8 + 12;
            int angleY = value % 1000 + 1000;
            Write(0, sumX / 3);
            Write(2, sumY / 3);
            Write(4, sumZ / 3);
            Write(6, value % 3 - quotient + 8);
            Write(8, velocityX * duration);
            Write(10, angleY);
            Write(12, velocityZ * duration);
            Write(14, 0);
            Write(16, velocityX);
            Write(18, angleY / duration);
            Write(20, velocityZ);
            Write(22, duration);
            var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(unchecked((short)(sumX / 3)), 0, unchecked((short)(sumZ / 3)));
            Write(24, direction.X);
            Write(28, direction.Z);
            int scale = value % 5000 + 3500;
            Write(30, unchecked((int)Math.Sqrt(scale) + scale));
            Write(26, scale / duration);

            void Write(int offset, int word) => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(row + offset), unchecked((ushort)word));
        }
        return ((faces ?? new([])).Write(sequence, bytes), random);
    }

    public static NativeBattleEffectMemory Draw61B9A0(NativeBattleEffectMemory memory, NativeBattleModulePoolTask task, NativeBattleBlizzardResources resources, uint graphicsFlags, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, Action<NativeBattleGfGeometryBatch> emit)
    {
        short phase = Word(task, 12);
        int sequence = Word(task, 14);
        NativeBattleBlizzardFaces faces = memory.BlizzardFaces ?? throw new NotSupportedException("Native61B740 requires its executed61B190 current face bank.");
        byte[] bytes = faces.Read(sequence);
        int group = BinaryPrimitives.ReadInt32LittleEndian(resources.PrimaryMesh.Bytes.AsSpan()) + 8;
        int partnerGroup = BinaryPrimitives.ReadInt32LittleEndian(resources.PartnerMesh.Bytes.AsSpan()) + 8;
        NativeBattleActionMatrix outer = Compose(view, World(task, new(0, Word(task, 24), 0), Word(task, 28), Word(task, 28), Word(task, 28)));
        for (int index = 0; index < 80; index++)
        {
            int row = index * 32;
            if (Read(6) > phase)
            {
                continue;
            }
            ImmutableArray<byte> primary = NativeBattleTelemetry.Immutable(1222, NativeBattleTelemetry.Materialize(1220, resources.PrimaryMesh.Bytes.AsSpan().Slice(group + 4 + index * 20, 20)));
            ImmutableArray<byte> partner = NativeBattleTelemetry.Immutable(1223, NativeBattleTelemetry.Materialize(1221, resources.PartnerMesh.Bytes.AsSpan().Slice(partnerGroup + 4 + index * 20, 20)));
            NativeBattleVector[] vertices = Enumerable.Range(0, 3).Select(corner => Vertex(resources.PrimaryMesh.Bytes, BinaryPrimitives.ReadUInt16LittleEndian(primary.AsSpan().Slice(4 + corner * 2)))).ToArray();
            NativeBattleActionMatrix matrix = outer;
            uint primaryColor = 0x24808080;
            if (Read(14) < Read(22))
            {
                short x = Read(0);
                short y = Read(2);
                short z = Read(4);
                for (int corner = 0; corner < vertices.Length; corner++)
                {
                    NativeBattleVector vertex = vertices[corner];
                    vertices[corner] = new(unchecked((short)(vertex.X - x)), unchecked((short)(vertex.Y - y)), unchecked((short)(vertex.Z - z)));
                }
                (short tx, short tz) = Orbit61BAC7(Read(10), Read(24), Read(28));
                int oldScale = Read(30);
                int localX = tx * oldScale >> 12;
                int localZ = tz * oldScale >> 12;
                if ((graphicsFlags & 0x201) == 0)
                {
                    Write(8, Read(8) - Read(16));
                    Write(10, Read(10) - Read(18));
                    Write(12, Read(12) - Read(20));
                    Write(14, Read(14) + 1);
                    Write(30, oldScale - Read(26));
                }
                NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CD50(new(Read(8), Read(10), Read(12))) with { X = localX, Y = y, Z = localZ };
                matrix = Compose(outer, local);
                int intensity = 128 / Read(22) * Read(14);
                primaryColor = unchecked((uint)((((intensity | 0x2600) << 8) | intensity) << 8 | intensity));
            }
            ImmutableArray<NativeBattleModuleProjectedVertex> corners = vertices.Select(vertex => NativeBattleModuleProjection.Project45FE10(matrix, vertex, projection)).ToImmutableArray();
            NativeBattleBlizzardFacePacketContext context = new(primaryColor, phase >= 40 ? 0xC3u : 0, phase >= 40 ? 0u : null, phase >= 40 ? (phase - 40) << 9 : null, phase * 8 % 128, new(0, 128, 128, 128), new(0, 0, 0, 0), 17, 2);
            emit(NativeBattleBlizzardFacePackets.EmitFace61B9A0(primary, partner, corners, Read(14), Read(22), context, colors));

            short Read(int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(row + offset));
            void Write(int offset, int value) => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(row + offset), unchecked((ushort)value));
        }
        return memory with { BlizzardFaces = faces.Write(sequence, bytes) };
    }

    public static NativeBattleActionMatrix World(NativeBattleModulePoolTask task, NativeBattleVector angles, int scaleX, int scaleY, int scaleZ)
    {
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Euler56CD50(angles);
        int[] scales = [scaleX, scaleY, scaleZ];
        return matrix with { Rotation = matrix.Rotation.Select((word, index) => unchecked((short)(word * scales[index % 3] >> 12))).ToImmutableArray(), X = Word(task, 16), Y = Word(task, 18), Z = Word(task, 20) };
    }

    public static (short X, short Z) Orbit61BAC7(short angle, short directionX, short directionZ)
    {
        int cosine = NativeBattleCameraMath.Cosine(angle);
        int sine = NativeBattleCameraMath.Sine(angle);
        return (unchecked((short)((directionX * cosine >> 12) + (-directionZ * sine >> 12))), unchecked((short)((directionX * sine >> 12) + (directionZ * cosine >> 12))));
    }

    public static NativeBattleActionMatrix Compose(NativeBattleActionMatrix left, NativeBattleActionMatrix right)
    {
        (int x, int y, int z) = NativeBattleGfMatrices.RotateLong56C600(left, right.X, right.Y, right.Z);
        return NativeBattleGfMatrices.Multiply56C090(left, right) with { X = unchecked(left.X + x), Y = unchecked(left.Y + y), Z = unchecked(left.Z + z) };
    }

    private static NativeBattleVector Vertex(ImmutableArray<byte> source, ushort selector)
    {
        ReadOnlySpan<byte> bytes = source.AsSpan().Slice(checked(8 + 4 * selector), 6);
        return new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes[2..]), BinaryPrimitives.ReadInt16LittleEndian(bytes[4..]));
    }
    private static short Word(NativeBattleModulePoolTask task, int offset) => NativeBattleBlizzardController.Word(task, offset);
}
