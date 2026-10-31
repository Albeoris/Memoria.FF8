using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleXatmDeath276ArcResources(ImmutableArray<ushort> Lengths, ImmutableArray<byte> Distance);
public sealed record NativeBattleXatmDeath276ArcStep(NativeBattleModulePoolTask Task, NativeBattleSharedEffectRandomState Random, ImmutableArray<NativeBattleGeometryPrimitive> FramePrimitives, int Result);

public static class NativeBattleXatmDeath276Arcs
{
    public static bool CanSpawn(ImmutableArray<byte> triangles, int first, int second)
    {
        bool distant = false;
        for (int axis = 0; axis < 3; axis++)
        {
            int a = 0;
            int b = 0;
            for (int corner = 0; corner < 3; corner++)
            {
                a += ReadWord(triangles, checked(first * 24 + corner * 8 + axis * 2));
                b += ReadWord(triangles, checked(second * 24 + corner * 8 + axis * 2));
            }
            distant |= Math.Abs(a - b) >= 256;
        }
        return distant;
    }

    public static NativeBattleModulePoolTask Initialize(NativeBattleModulePoolTask task, uint model, uint firstTriangle, uint secondTriangle, byte amplitudeShift, byte fadeShift)
    {
        task = task.WriteWord(12, 0).WriteByte(14, amplitudeShift).WriteByte(15, fadeShift).WriteDword(16, firstTriangle).WriteDword(20, secondTriangle).WriteDword(24, model);
        for (int index = 0; index < 8; index++)
        {
            int sine = NativeBattleCameraMath.Sine((index << 11) / 7);
            task = task.WriteWord(0x1C + index * 6, unchecked((ushort)(sine * 260 >> 12))).WriteWord(0x1E + index * 6, 128).WriteWord(0x20 + index * 6, 0);
        }
        return task;
    }

    public static NativeBattleXatmDeath276ArcStep Advance(NativeBattleModulePoolTask task, NativeBattleXatmDeath276ArcResources resources, ImmutableArray<short> profile, NativeBattleCameraProjection projection, NativeBattleSharedEffectRandomState random, Func<uint, NativeBattleQuezacotlRibbonSample> triangleFrame, uint? stackResidue = null)
    {
        return Advance(task, resources, profile, projection, random, triangleFrame, stackResidue, null);
    }

    public static NativeBattleXatmDeath276ArcStep Advance(NativeBattleModulePoolTask task, NativeBattleXatmDeath276ArcResources resources, ImmutableArray<short> profile, NativeBattleCameraProjection projection, NativeBattleSharedEffectRandomState random, Func<uint, NativeBattleQuezacotlRibbonSample> triangleFrame, uint? stackResidue, NativeBattlePilotVisualIdentity? pilotIdentity)
    {
        if (profile.IsDefault || profile.Length != 8 || resources.Lengths.IsDefault || resources.Lengths.Length != 256)
        {
            throw new InvalidDataException("Native X-ATM092 arc requires its original profile and square-root table.");
        }
        uint firstSource = task.ReadDword(16);
        NativeBattleQuezacotlRibbonSample first = triangleFrame(firstSource);
        uint lastSource = task.ReadDword(20);
        NativeBattleQuezacotlRibbonSample last = triangleFrame(lastSource);
        if (pilotIdentity is { } owner)
        {
            pilotIdentity = NativeBattleSelectedProgrammeVisuals.Resource(owner, firstSource, lastSource);
        }
        int squared = NativeBattleQuezacotlRibbonMath.SquaredLength(last.Position.X - first.Position.X, last.Position.Y - first.Position.Y, last.Position.Z - first.Position.Z);
        int length = NativeBattleQuezacotlRibbonMath.SqrtQ12(squared, resources.Lengths);
        int amplitude = length >> 12;
        int weight = length >> task.ReadByte(14);
        NativeBattleVector firstControl = Add(first.Position, NativeBattleQuezacotlRibbonMath.Scale(first.Normal, weight));
        NativeBattleVector lastControl = Add(last.Position, NativeBattleQuezacotlRibbonMath.Scale(last.Normal, weight));
        NativeBattleVector[] points = new NativeBattleVector[8];
        points[0] = first.Position;
        points[7] = last.Position;
        for (int index = 1; index < 7; index++)
        {
            int t = (index << 12) / 7;
            int u = 4096 - t;
            NativeBattleVector a = NativeBattleQuezacotlRibbonMath.Blend(first.Position, firstControl, u, t);
            NativeBattleVector b = NativeBattleQuezacotlRibbonMath.Blend(last.Position, lastControl, t, u);
            NativeBattleVector point = NativeBattleQuezacotlRibbonMath.Blend(a, b, u, t);
            int factor = unchecked(profile[index] * amplitude) >> 12;
            short Jitter(short value)
            {
                random = random.Draw7059E0(out ushort draw);
                return unchecked((short)(value + (unchecked((draw - 0x4000) * factor) >> 18)));
            }
            short x = Jitter(point.X);
            short y = Jitter(point.Y);
            short z = Jitter(point.Z);
            points[index] = new(x, y, z);
        }
        ImmutableArray<short> widths = Enumerable.Range(0, 8).Select(index => unchecked((short)task.ReadWord(0x1C + index * 6))).ToImmutableArray();
        ImmutableArray<short> brightness = Enumerable.Range(0, 8).Select(index => unchecked((short)task.ReadWord(0x1E + index * 6))).ToImmutableArray();
        ImmutableArray<NativeBattleGeometryPrimitive> primitives = Draw686DB0(points.ToImmutableArray(), widths, brightness, projection, resources.Distance, stackResidue, pilotIdentity);
        int counter = unchecked((short)task.ReadWord(12));
        if (counter >= 10)
        {
            int lit = 0;
            for (int index = 0; index < 8; index++)
            {
                int decrement = (0x1400 - profile[index]) >> task.ReadByte(15);
                short value = unchecked((short)(brightness[index] - decrement));
                if (value < 0)
                {
                    value = 0;
                }
                else
                {
                    lit++;
                }
                task = task.WriteWord(0x1E + index * 6, unchecked((ushort)value));
            }
            if (lit <= 1)
            {
                return new(task, random, primitives, 2);
            }
        }
        return new(task.WriteWord(12, unchecked((ushort)(counter + 1))), random, primitives, 0);
    }

    public static ImmutableArray<NativeBattleGeometryPrimitive> Draw686DB0(ImmutableArray<NativeBattleVector> positions, ImmutableArray<short> widths, ImmutableArray<short> brightness, NativeBattleCameraProjection projection, ImmutableArray<byte> distances, uint? stackResidue = null)
    {
        return Draw686DB0(positions, widths, brightness, projection, distances, stackResidue, null);
    }

    public static ImmutableArray<NativeBattleGeometryPrimitive> Draw686DB0(ImmutableArray<NativeBattleVector> positions, ImmutableArray<short> widths, ImmutableArray<short> brightness, NativeBattleCameraProjection projection, ImmutableArray<byte> distances, uint? stackResidue, NativeBattlePilotVisualIdentity? pilotIdentity)
    {
        if (positions.Length != 8 || widths.Length != 8 || brightness.Length != 8 || distances.Length != 16384)
        {
            throw new InvalidDataException("Native X-ATM092 arc requires eight ribbon points and its complete distance lookup.");
        }
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native X-ATM092 arc requires its actual camera.");
        short[] x = new short[8];
        short[] y = new short[8];
        short[] depth = new short[8];
        short[] rightX = new short[8];
        short[] rightY = new short[8];
        uint[]? centers = pilotIdentity.HasValue ? new uint[8] : null;
        for (int index = 0; index < 8; index++)
        {
            NativeBattleModuleProjectedVertex point = NativeBattleModuleProjection.Project45FE10(view, positions[index], projection);
            if (centers is not null)
            {
                centers[index] = point.Screen;
            }
            x[index] = unchecked((short)point.Screen);
            y[index] = unchecked((short)(point.Screen >> 16));
            depth[index] = unchecked((short)(point.Depth >> 2));
        }
        (short X, short Y)? direction = stackResidue is uint residue ? (unchecked((short)residue), unchecked((short)(residue >> 16))) : null;
        Edge(0, true);
        for (int index = 1; index < 7; index++)
        {
            Edge(index, false);
        }
        rightX[7] = x[7];
        rightY[7] = y[7];
        ImmutableArray<NativeBattleGeometryPrimitive>.Builder result = ImmutableArray.CreateBuilder<NativeBattleGeometryPrimitive>();
        for (int index = 0; index < 7; index++)
        {
            if (depth[index] <= 0 || depth[index + 1] <= 0)
            {
                continue;
            }
            uint firstColor = Color(brightness[index]);
            uint nextColor = Color(brightness[index + 1]);
            uint[] words = [0x0C000000, (firstColor & 0xFFFFFF) | 0x3E000000, Screen(x[index], y[index]), 0x3CD4C008, firstColor, Screen(rightX[index], rightY[index]), 0x00BAC038, nextColor, Screen(x[index + 1], y[index + 1]), 0xC008, nextColor, Screen(rightX[index + 1], rightY[index + 1]), 0xC038];
            ImmutableArray<NativeBattleGfPacketByteState> mask = Enumerable.Range(0, 52).Select(offset => offset is 38 or 39 or 50 or 51 ? NativeBattleGfPacketByteState.Untouched : NativeBattleGfPacketByteState.Known).ToImmutableArray();
            NativeBattleGeometryPrimitive primitive = new(17 + ((depth[index] + depth[index + 1]) >> 3), words.ToImmutableArray(), [depth[index], depth[index], depth[index + 1], depth[index + 1]]) { PhysicalWriteMask = mask };
            if (pilotIdentity is { } identity && centers is not null)
            {
                System.Numerics.Vector3 World(int row) => new(positions[row].X, positions[row].Y, positions[row].Z);
                System.Numerics.Vector2 Offset(int row, bool right) => new System.Numerics.Vector2(right ? rightX[row] : x[row], right ? rightY[row] : y[row]) - new System.Numerics.Vector2(unchecked((short)centers[row]), unchecked((short)(centers[row] >> 16)));
                primitive = primitive with { PilotGeometry = new(identity with { Primitive = unchecked(identity.Primitive * 397 + index) }, World(index), World(index), World(index + 1), World(index + 1), 4) { ScreenOffsets = [Offset(index, false), Offset(index, true), Offset(index + 1, false), Offset(index + 1, true)], SourceDepths = new(depth[index], depth[index], depth[index + 1], depth[index + 1]), UseSourceDepths = true } };
            }
            result.Add(primitive);
        }
        return result.ToImmutable();

        void Edge(int index, bool firstEdge)
        {
            if (depth[index] < 0 || depth[index + 1] < 0)
            {
                return;
            }
            int dx = x[index + 1] - x[index];
            int dy = y[index] - y[index + 1];
            int distance = distances[Math.Min(Math.Abs(dy), 127) * 128 + Math.Min(Math.Abs(dx), 127)];
            int normalX;
            int normalY;
            if (firstEdge)
            {
                if (distance == 0)
                {
                    depth[index] = -1;
                    return;
                }
                normalX = unchecked(dy << 12) / distance;
                normalY = unchecked(dx << 12) / distance;
                direction = (unchecked((short)normalX), unchecked((short)normalY));
            }
            else
            {
                var previous = direction ?? throw new NotSupportedException("Native X-ATM092 coincident first arc points require the actual caller stack address residue.");
                if (distance == 0)
                {
                    normalX = previous.X;
                    normalY = previous.Y;
                }
                else
                {
                    int edgeX = unchecked(dy << 12) / distance;
                    int edgeY = unchecked(dx << 12) / distance;
                    int sumX = unchecked(edgeX + previous.X);
                    int sumY = unchecked(edgeY + previous.Y);
                    direction = (unchecked((short)edgeX), unchecked((short)edgeY));
                    if ((sumX | sumY) == 0)
                    {
                        sumX = unchecked(edgeX * 2);
                        sumY = unchecked(edgeY * 2);
                    }
                    int factor = unchecked(0x4000 - (unchecked(sumX * sumX + sumY * sumY) >> 13));
                    normalX = unchecked(factor * sumX) >> 13;
                    normalY = unchecked(factor * sumY) >> 13;
                }
            }
            int width = unchecked(widths[index + 1] << 5) / (depth[index] + 1024);
            int offsetX = unchecked(width * normalX) >> 12;
            int offsetY = unchecked(width * normalY) >> 12;
            rightX[index] = unchecked((short)(x[index] + offsetX));
            rightY[index] = unchecked((short)(y[index] + offsetY));
            x[index] = unchecked((short)(x[index] - offsetX));
            y[index] = unchecked((short)(y[index] - offsetY));
        }
    }

    private static short ReadWord(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan().Slice(offset, 2));
    private static NativeBattleVector Add(NativeBattleVector a, NativeBattleVector b) => new(unchecked((short)(a.X + b.X)), unchecked((short)(a.Y + b.Y)), unchecked((short)(a.Z + b.Z)));
    private static uint Screen(short x, short y) => (uint)unchecked((ushort)x) | (uint)unchecked((ushort)y) << 16;
    private static uint Color(short value)
    {
        uint extended = unchecked((uint)(int)value);
        return (((extended << 8) | extended) << 8) | extended;
    }
}
