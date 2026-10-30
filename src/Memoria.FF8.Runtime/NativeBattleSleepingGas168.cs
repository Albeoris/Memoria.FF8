using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleSleepingGas168Resources(ImmutableArray<byte> Model, NativeBattleModuleResource Sprite, NativeBattleEffectHeapAllocation Camera, uint Sound, int AxisX, int AxisY, int AxisZ);

public sealed record NativeBattleSleepingGas168State([property: JsonRequired] ImmutableArray<byte> Clouds, [property: JsonRequired] ImmutableArray<byte> Offsets, [property: JsonRequired] ImmutableArray<byte> Vertices, [property: JsonRequired] ImmutableArray<byte> TargetSparkles, [property: JsonRequired] ImmutableArray<byte> CasterSparkles, [property: JsonRequired] ImmutableArray<int> PairOrder, [property: JsonRequired] NativeBattleVector Anchor, [property: JsonRequired] NativeBattleVector Center)
{
    public static NativeBattleSleepingGas168State FromColdProcess() => new(new byte[12 * 28].ToImmutableArray(), new byte[12 * 61 * 8].ToImmutableArray(), new byte[12 * 122 * 8].ToImmutableArray(), new byte[100 * 24].ToImmutableArray(), new byte[50 * 24].ToImmutableArray(), new int[122].ToImmutableArray(), default, default);
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleSleepingGas168State? SleepingGas168
    {
        get => effectState.SleepingGas168;
        init => effectState = effectState with { SleepingGas168 = value };
    }
}

public static class NativeBattleSleepingGas168
{
    public static int NormalizeSquare(int x, int y, int z) => NativePrecision53.RoundInteger((float)((double)z * (float)z + (double)(float)y * (float)y + (double)x * x));

    public static ImmutableArray<int> SortPairs(ImmutableArray<byte> model)
    {
        int[] order = Enumerable.Range(0, 122).ToArray();
        for (int first = 0; first < 121; first++)
        {
            for (int second = first + 1; second < 122; second++)
            {
                short a = BinaryPrimitives.ReadInt16LittleEndian(model.AsSpan().Slice(8 + order[first] * 8 + 4));
                short b = BinaryPrimitives.ReadInt16LittleEndian(model.AsSpan().Slice(8 + order[second] * 8 + 4));
                if (a < b)
                {
                    (order[first], order[second]) = (order[second], order[first]);
                }
            }
        }
        return order.ToImmutableArray();
    }

    public static void AdvanceWave(Span<byte> cloud, Span<byte> offsets)
    {
        short angleA = Word(cloud, 8);
        short angleB = Word(cloud, 0x12);
        short speed = Word(cloud, 10);
        PutWord(cloud, 12, angleA);
        PutWord(cloud, 0x16, angleB);
        int amplitudeA = Word(cloud, 14);
        int amplitudeB = Word(cloud, 0x18);
        for (int index = 0; index < 61; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(offsets.Slice(index * 8), unchecked(NativeBattleCameraMath.Sine(angleA) * amplitudeA) >> 12);
            angleA = unchecked((short)(angleA + speed));
            speed = unchecked((short)(speed - (short)(speed >> 5)));
            amplitudeA = unchecked(amplitudeA + Word(cloud, 0x10));
            BinaryPrimitives.WriteInt32LittleEndian(offsets.Slice(index * 8 + 4), unchecked(NativeBattleCameraMath.Sine(angleB) * amplitudeB) >> 12);
            amplitudeB = unchecked(amplitudeB + Word(cloud, 0x14));
        }
        PutWord(cloud, 8, unchecked((short)(Word(cloud, 12) + 100)));
        PutWord(cloud, 10, speed);
        PutWord(cloud, 0x12, Word(cloud, 0x16));
    }

    public static void BuildShape(Span<byte> cloud, ReadOnlySpan<byte> offsets, Span<byte> vertices, ImmutableArray<int> order, ImmutableArray<byte> model, bool paused)
    {
        int count = BinaryPrimitives.ReadInt32LittleEndian(cloud);
        if (count is < 1 or > 61 || order.Length != 122 || vertices.Length != 122 * 8)
        {
            throw new InvalidDataException("Native Sleeping Gas requires its reached positive cloud length and complete vertex-pair owner.");
        }
        int last = 0;
        for (int pair = 0; pair < count; pair++)
        {
            short x = unchecked((short)BinaryPrimitives.ReadInt32LittleEndian(offsets.Slice(pair * 8)));
            short z = unchecked((short)BinaryPrimitives.ReadInt32LittleEndian(offsets.Slice(pair * 8 + 4)));
            for (int corner = 0; corner < 2; corner++)
            {
                int index = order[pair * 2 + corner];
                last = index * 8;
                ReadOnlySpan<byte> source = model.AsSpan().Slice(8 + last, 8);
                PutWord(vertices, last, unchecked((short)(Word(source, 0) + x)));
                PutWord(vertices, last + 2, unchecked((short)(Word(source, 2) + (x >> 1))));
                PutWord(vertices, last + 4, unchecked((short)(Word(source, 4) + z)));
            }
        }
        byte[] tail = vertices.Slice(last, 8).ToArray();
        for (int pair = count; pair < 61; pair++)
        {
            tail.CopyTo(vertices.Slice(order[pair * 2] * 8, 8));
            tail.CopyTo(vertices.Slice(order[pair * 2 + 1] * 8, 8));
        }
        if (!paused)
        {
            BinaryPrimitives.WriteInt32LittleEndian(cloud, Math.Min(61, unchecked(count + BinaryPrimitives.ReadInt32LittleEndian(cloud[4..]))));
        }
    }

    public static NativeBattleGfGeometryBatch DrawCloud(ImmutableArray<byte> model, ImmutableArray<byte> vertices, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, short frame, NativeBattleSummonColors colors, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        uint flags = frame < 24 || frame >= 52 ? 0x20F3u : 0x2033u;
        int fade = frame < 24 ? 4096 - frame * 170 : frame >= 52 ? (frame - 52) << 9 : 0;
        int cursor = checked(BinaryPrimitives.ReadInt32LittleEndian(model.AsSpan()) + 12);
        int count = BinaryPrimitives.ReadInt32LittleEndian(model.AsSpan().Slice(cursor));
        cursor += 4;
        int allocated = 0;
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        for (int index = 0; index < count; index++)
        {
            ImmutableArray<byte> face = model.Slice(cursor, 24);
            ushort[] selectors = Enumerable.Range(0, 4).Select(corner => BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan().Slice(4 + corner * 2))).ToArray();
            Dictionary<ushort, NativeBattleVector>? reached = pilotIdentity.HasValue ? [] : null;
            NativeBattleGfGeometryPackets.ProjectedCorners corners = new(selectors, offset => Project(offset, reached));
            NativeBattleGfGeometryAttempt packet = NativeBattleGfGeometryPackets.EmitAttempt(face.AsSpan(), corners, 3, new(flags, 0, fade, null, null, 0, 0, 17, 2), colors, allocated, variant: new(null, null, false, true));
            if (pilotIdentity is { } identity && packet.Primitive is { } primitive)
            {
                System.Numerics.Vector3 World(int corner) => NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(matrix, reached![selectors[corner]]), projection);
                packet = packet with { Primitive = primitive with { PilotGeometry = new(identity with { Primitive = unchecked(identity.Primitive * 65537 + cursor) }, World(0), World(1), World(2), World(3), 4) { SourceDepths = new(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]), UseSourceDepths = true } } };
            }
            attempts.Add(packet);
            if (packet.Primitive is not null)
            {
                allocated += packet.PacketLength;
            }
            cursor += 24;
        }
        return new(attempts.ToImmutable(), allocated);

        NativeBattleModuleProjectedVertex Project(ushort offset, Dictionary<ushort, NativeBattleVector>? reached)
        {
            ReadOnlySpan<byte> source = vertices.AsSpan().Slice(offset * 4, 6);
            NativeBattleVector point = new(Word(source, 0), Word(source, 2), Word(source, 4));
            reached?.TryAdd(offset, point);
            return NativeBattleModuleProjection.Project45FE10(matrix, point, projection);
        }
    }

    public static short Word(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes[offset..]);
    public static void PutWord(Span<byte> bytes, int offset, short value) => BinaryPrimitives.WriteInt16LittleEndian(bytes[offset..], value);
}
