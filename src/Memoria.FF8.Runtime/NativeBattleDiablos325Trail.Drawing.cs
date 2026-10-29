using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleDiablos325Trail
{
    private sealed class ProjectedPoint(short x, short y, short depth, short width, uint color)
    {
        public short X = x;
        public short Y = y;
        public short Depth = depth;
        public short Width = width;
        public readonly uint Color = color;
        public System.Numerics.Vector3 World;
        public uint SourceScreen;
        public uint Address;
        public short? RightX;
        public short? RightY;
    }

    public static NativeBattleDiablos325BodyDraw Draw657F00(NativeBattleDiablos325Frame frame, NativeBattleCameraProjection projection)
    {
        NativeBattleDiablos325State state = NativeBattleDiablos325Entry.RequireState(frame.Memory);
        NativeBattleDiablos325Trail parameters = state.Trail ?? throw new NotSupportedException("Native Diablos trail drawing requires its executed module initialization.");
        NativeBattleDiablos325LateResources tables = frame.Effect.Binding.Diablos325Resources?.LateResources ?? throw new NotSupportedException("Native Diablos trail drawing requires its original static tables.");
        ReadOnlySpan<byte> widths = tables.Read(0xFDFF74, 4098);
        ReadOnlySpan<byte> edgeLengths = tables.Read(0xFDBDF8, 16384);
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Diablos trail drawing requires its retained physical records.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Diablos trail drawing requires its prepared current camera matrix.");
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        int bias = unchecked((short)parameters.Parameter5206);
        NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Diablos trail drawing requires its current temporary memory owner.");
        uint? normal = process.Temporary5082B0Word4;
        for (int blockIndex = 0; blockIndex < 64; blockIndex++)
        {
            uint block = checked(state.ModelBuffer5208 + 0x7C5Cu + (uint)blockIndex * 48);
            uint pointer = Dword(block);
            if (pointer == 0)
            {
                continue;
            }
            int count = Short(block + 44);
            if (count < 2)
            {
                continue;
            }
            int fade = Short(block + 46);
            uint[] colors = [0, 0x0080F0F0, 0x000040F0, 0x0080F0F0, unchecked((uint)bias)];
            if (fade < 4096)
            {
                for (int index = 0; index < 4; index++)
                {
                    colors[index] = Mix(colors[index], 0, fade, 0);
                }
            }
            List<ProjectedPoint> points = [];
            int step = 2048 / count;
            do
            {
                if (points.Count >= 32)
                {
                    throw new InvalidDataException("Native Diablos trail projection leaves its original temporary record allocation.");
                }
                RequirePoint(pointer);
                NativeBattleVector position = new(Short(pointer), Short(pointer + 2), Short(pointer + 4));
                NativeBattleModuleProjectedVertex projected = NativeBattleModuleProjection.Project45FE10(view, position, projection);
                int tableIndex = checked(points.Count * step);
                short scale = BinaryPrimitives.ReadInt16LittleEndian(widths.Slice(tableIndex * 2, 2));
                short width = unchecked((short)(Short(pointer + 6) * scale >> 12));
                int colorPhase = Short(pointer + 16);
                int segment = colorPhase >> 12;
                int fraction = colorPhase & 4095;
                if (segment < 0 || segment + 1 >= colors.Length)
                {
                    throw new InvalidDataException("Native Diablos trail color leaves its original prepared color ramp.");
                }
                uint color = Mix(colors[segment], colors[segment + 1], 4096 - fraction, fraction);
                points.Add(new(unchecked((short)(unchecked((short)projected.Screen) / 8)), unchecked((short)(unchecked((short)(projected.Screen >> 16)) / 8)), (short)(projected.Depth >> 2), width, color) { World = new(position.X, position.Y, position.Z), SourceScreen = projected.Screen, Address = pointer });
                pointer = Dword(pointer + 20);
            }
            while (pointer != 0);
            if (points.Count < count)
            {
                throw new InvalidDataException("Native Diablos trail count exceeds its current physical linked records.");
            }
            Edge658260(points[0], points[1], false, ref normal, edgeLengths);
            for (int index = 1; index < count - 1; index++)
            {
                Edge658260(points[index], points[index + 1], true, ref normal, edgeLengths);
            }
            Edge658260(points[count - 1], null, false, ref normal, edgeLengths);
            for (int index = 0; index < count - 1; index++)
            {
                ProjectedPoint first = points[index];
                ProjectedPoint second = points[index + 1];
                if (first.Depth <= 0 || second.Depth <= 0)
                {
                    continue;
                }
                (short firstRightX, short firstRightY) = ReadRight(first);
                (short secondRightX, short secondRightY) = ReadRight(second);
                int bucket = Math.Max(0, ((first.Depth + second.Depth) >> 3) + bias);
                uint[] words = [0x0C000000, first.Color | 0x3E000000, XY(first.X, first.Y), 0x3DD47048, first.Color, XY(second.X, second.Y), 0x00B67078, second.Color, XY(firstRightX, firstRightY), 0x00007048, second.Color, XY(secondRightX, secondRightY), 0x00007078];
                byte[] bytes = new byte[52];
                for (int word = 0; word < words.Length; word++)
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(word * 4), words[word]);
                }
                NativeBattleGfPacketByteState[] mask = Enumerable.Repeat(NativeBattleGfPacketByteState.Known, bytes.Length).ToArray();
                foreach (int unknown in new[] { 19, 31, 43 })
                {
                    mask[unknown] = NativeBattleGfPacketByteState.Unknown;
                }
                foreach (int untouched in new[] { 38, 39, 50, 51 })
                {
                    mask[untouched] = NativeBattleGfPacketByteState.Untouched;
                }
                NativeBattleGeometryPrimitive primitive = new(bucket, words.ToImmutableArray(), Enumerable.Repeat(checked(bucket * 4), 4).ToImmutableArray());
                int firstSlot = checked((int)(first.Address - state.ModelBuffer5208 - 0x4C5C) / 24);
                int secondSlot = checked((int)(second.Address - state.ModelBuffer5208 - 0x4C5C) / 24);
                NativeBattlePilotVisualIdentity identity = NativeBattlePilotVisualLifetimes.Identity(325, arena, state.ModelBuffer5208 + 0x4C5C, firstSlot, secondSlot) with { ResourceGeneration = NativeBattlePilotVisualLifetimes.Identity(325, arena, state.ModelBuffer5208 + 0x4C5C, secondSlot).Generation };
                System.Numerics.Vector2 Offset(short x, short y, uint center) => new(unchecked((short)(x << 3)) - unchecked((short)center), unchecked((short)(y << 3)) - unchecked((short)(center >> 16)));
                primitive = primitive with { PilotGeometry = new(identity, first.World, second.World, first.World, second.World, 4)
                {
                    ScreenOffsets = [Offset(first.X, first.Y, first.SourceScreen), Offset(second.X, second.Y, second.SourceScreen), Offset(firstRightX, firstRightY, first.SourceScreen), Offset(secondRightX, secondRightY, second.SourceScreen)],
                    SourceDepths = new System.Numerics.Vector4(checked(bucket * 4)),
                    UseSourceDepths = true
                } };

                attempts.Add(new(allocated, bytes.ToImmutableArray(), mask.ToImmutableArray(), primitive));
                allocated = checked(allocated + bytes.Length);
            }
        }
        return new(frame.Memory with { PrimitiveProcess = process with { Temporary5082B0Word4 = normal } }, new(attempts.ToImmutable(), allocated));

        static (short X, short Y) ReadRight(ProjectedPoint point)
        {
            if (point.RightX is short x && point.RightY is short y)
            {
                return (x, y);
            }
            throw new InvalidDataException("Native trail projection must write both edge outputs before an accepted segment consumes them.");
        }

        short Short(uint address) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(NativeBattleWaterExecutionFields.ArenaOffset(address, 2), 2));
        uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(NativeBattleWaterExecutionFields.ArenaOffset(address, 4), 4));
        void RequirePoint(uint address)
        {
            long offset = (long)address - state.ModelBuffer5208 - 0x4C5C;
            if (offset < 0 || offset >= 512 * 24 || offset % 24 != 0)
            {
                throw new InvalidDataException("Native Diablos trail drawing leaves its physical point bank.");
            }
        }
    }

    private static void Edge658260(ProjectedPoint current, ProjectedPoint? next, bool previousPointer, ref uint? output, ReadOnlySpan<byte> edgeLengths)
    {
        if (current.Depth < 0)
        {
            return;
        }
        int offsetX = 0;
        int offsetY = 0;
        if (next is not null)
        {
            if (next.Depth < 0)
            {
                return;
            }
            int dx = next.X - current.X;
            int dy = current.Y - next.Y;
            int length = edgeLengths[Math.Min(Math.Abs(dy), 127) * 128 + Math.Min(Math.Abs(dx), 127)];
            int nx;
            int ny;
            if (previousPointer)
            {
                uint preceding = output ?? throw new NotSupportedException("Native Diablos trail consumes an uninitialized previous temporary edge normal.");
                int precedingX = unchecked((short)preceding);
                int precedingY = unchecked((short)(preceding >> 16));
                if (length == 0)
                {
                    output = preceding;
                    nx = precedingX;
                    ny = precedingY;
                }
                else
                {
                    int currentX = unchecked(dy << 12) / length;
                    int currentY = unchecked(dx << 12) / length;
                    output = PackNormal(currentX, currentY);
                    int joinedX = unchecked(currentX + precedingX);
                    int joinedY = unchecked(currentY + precedingY);
                    if (joinedX == 0 && joinedY == 0)
                    {
                        joinedX = unchecked(currentX * 2);
                        joinedY = unchecked(currentY * 2);
                    }
                    int correction = unchecked(16384 - (unchecked(joinedX * joinedX + joinedY * joinedY) >> 13));
                    nx = unchecked(correction * joinedX) >> 13;
                    ny = unchecked(correction * joinedY) >> 13;
                }
            }
            else
            {
                if (length == 0)
                {
                    current.Depth = -1;
                    return;
                }
                nx = unchecked(dy << 12) / length;
                ny = unchecked(dx << 12) / length;
                output = PackNormal(nx, ny);
            }
            int quotient = next.Width / (current.Depth + 128);
            offsetX = unchecked(quotient * nx) >> 12;
            offsetY = unchecked(quotient * ny) >> 12;
        }
        current.RightX = unchecked((short)(current.X + offsetX));
        current.RightY = unchecked((short)(current.Y + offsetY));
        current.X = unchecked((short)(current.X - offsetX));
        current.Y = unchecked((short)(current.Y - offsetY));
    }

    private static uint PackNormal(int x, int y) => unchecked((ushort)x) | unchecked((uint)(ushort)y << 16);

    private static uint Mix(uint first, uint second, int firstWeight, int secondWeight)
    {
        uint result = 0;
        for (int channel = 0; channel < 3; channel++)
        {
            int a = (byte)(first >> (channel * 8));
            int b = (byte)(second >> (channel * 8));
            int value = unchecked(NativePrecision53.RoundInteger(a * (unchecked((short)firstWeight) / 4096d)) + NativePrecision53.RoundInteger(b * (unchecked((short)secondWeight) / 4096d)));
            result |= (uint)Math.Clamp(value, 0, 255) << (channel * 8);
        }
        return result;
    }

    private static uint XY(short x, short y) => unchecked((ushort)(x << 3)) | unchecked((uint)(ushort)(y << 3) << 16);
}
