using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBlizzagaRibbonNodes(int Count, ImmutableArray<byte> Nodes);
public sealed record NativeBattleBlizzagaRibbonResult(int Count, ImmutableArray<byte> Nodes, NativeBattleGfGeometryBatch Packets);

/// <summary>Retained36-byte ribbon nodes and6D9A40's GT4 submissions.</summary>
public static class NativeBattleBlizzagaRibbon
{
    public static NativeBattleBlizzagaRibbonNodes Insert6D99D0(int count, ImmutableArray<byte> nodes, ImmutableArray<byte> firstPoint, ImmutableArray<byte> secondPoint, byte brightness, sbyte velocity, byte uBasis)
    {
        Validate(count, nodes);
        if (firstPoint.IsDefault || firstPoint.Length != 8 || secondPoint.IsDefault || secondPoint.Length != 8)
        {
            throw new InvalidDataException("Native ribbon insertion requires both complete8-byte source points.");
        }
        byte[] next = nodes.ToArray();
        if (count < 30)
        {
            next.AsSpan(0, count * 36).CopyTo(next.AsSpan(36));
            count++;
        }
        firstPoint.AsSpan().CopyTo(next);
        secondPoint.AsSpan().CopyTo(next.AsSpan(8));
        next[0x20] = brightness;
        next[0x21] = unchecked((byte)velocity);
        next[0x22] = uBasis;
        return new(count, next.ToImmutableArray());
    }

    public static ImmutableArray<byte> Point6D9CF0(ImmutableArray<byte> initialPoint, int angle, int height, int radius)
    {
        if (initialPoint.IsDefault || initialPoint.Length != 8)
        {
            throw new InvalidDataException("Native ribbon point construction requires its retained fourth WORD.");
        }
        byte[] point = initialPoint.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(point, unchecked((short)(unchecked(NativeBattleCameraMath.Cosine(angle) * radius) >> 14)));
        BinaryPrimitives.WriteInt16LittleEndian(point.AsSpan(2), unchecked((short)(unchecked(NativeBattleCameraMath.Sine(angle) * radius) >> 14)));
        BinaryPrimitives.WriteInt16LittleEndian(point.AsSpan(4), unchecked((short)((height >> 2) - height)));
        return point.ToImmutableArray();
    }

    public static NativeBattleBlizzagaRibbonResult Emit6D9A40(int count, ImmutableArray<byte> nodes, sbyte velocityDelta, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection)
    {
        Validate(count, nodes);
        matrix.Validate();
        byte[] next = nodes.ToArray();
        for (int index = 0; index < count; index++)
        {
            int offset = index * 36;
            sbyte velocity = unchecked((sbyte)(next[offset + 0x21] + velocityDelta));
            next[offset + 0x21] = unchecked((byte)velocity);
            int brightness = next[offset + 0x20] + (velocity >> 3);
            next[offset + 0x20] = brightness < 0 ? (byte)0 : unchecked((byte)brightness);
        }
        if (count >= 2 && next[(count - 1) * 36 + 0x20] == 0 && next[(count - 2) * 36 + 0x20] == 0)
        {
            count--;
        }
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        if (count >= 2)
        {
            for (int index = 0; index < count; index++)
            {
                Project(index * 36, 0, 0x10);
                Project(index * 36, 8, 0x18);
            }
            for (int index = 0; index < count - 1; index++)
            {
                int a = index * 36;
                int b = a + 36;
                int[] depths = [I16(a + 0x14), I16(a + 0x1C), I16(b + 0x14), I16(b + 0x1C)];
                if (depths.Any(depth => depth <= 0))
                {
                    continue;
                }
                uint[] screens = [U32(a + 0x10), U32(a + 0x18), U32(b + 0x10), U32(b + 0x18)];
                bool swap = (NativeBattleModulePackets.Nclip(screens[0], screens[1], screens[2]) > 0) != (NativeBattleModulePackets.Nclip(screens[1], screens[3], screens[2]) > 0);
                if (swap)
                {
                    (screens[2], screens[3]) = (screens[3], screens[2]);
                }
                uint colorA = (uint)next[a + 0x20] * 0x10101;
                uint colorB = (uint)next[b + 0x20] * 0x10101;
                uint u = next[a + 0x22];
                uint endU = u + 31;
                ImmutableArray<uint> words = [0x0C000000, 0x3E000000 | colorA, screens[0], 0x3E144000 | u, colorA, screens[1], 0x00B87F00 | u, colorB, screens[2], (swap ? 0x7F00u : 0x4000u) | endU, colorB, screens[3], (swap ? 0x4000u : 0x7F00u) | endU];
                NativeBattleGeometryPrimitive primitive = new(17 + (depths.Sum() >> 4), words, depths.Select(depth => 17 * 4 + depth * 4).ToImmutableArray());
                byte[] data = new byte[52];
                for (int word = 0; word < words.Length; word++)
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(word * 4), words[word]);
                }
                attempts.Add(new(allocated, data.ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 52).ToImmutableArray(), primitive));
                allocated += 52;
            }
        }
        return new(count, next.ToImmutableArray(), new(attempts.ToImmutable(), allocated));

        short I16(int offset) => BinaryPrimitives.ReadInt16LittleEndian(next.AsSpan(offset));
        uint U32(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(next.AsSpan(offset));
        void Project(int node, int source, int destination)
        {
            NativeBattleModuleProjectedVertex vertex = NativeBattleModuleProjection.Project45FE10(matrix, new(I16(node + source), I16(node + source + 2), I16(node + source + 4)), projection);
            BinaryPrimitives.WriteUInt32LittleEndian(next.AsSpan(node + destination), vertex.Screen);
            BinaryPrimitives.WriteUInt32LittleEndian(next.AsSpan(node + destination + 4), vertex.Depth);
        }
    }

    private static void Validate(int count, ImmutableArray<byte> nodes)
    {
        if ((uint)count > 30 || nodes.IsDefault || nodes.Length != 30 * 36)
        {
            throw new InvalidDataException("Native ribbon drawing requires its complete30-node retained owner and current bounded count.");
        }
    }
}
