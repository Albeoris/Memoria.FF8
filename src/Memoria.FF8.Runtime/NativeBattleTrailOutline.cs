using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Shared6C8DE0/6C8620 projected ribbon outline, retaining each call's actual width/depth bias.</summary>
internal static class NativeBattleTrailOutline
{
    public static uint? Expand(IReadOnlyList<Point> outline, ImmutableArray<byte> distanceBytes16384, int depthBias, uint? incomingDirectionDword = null)
    {
        if (outline.Count < 2 || distanceBytes16384.IsDefault || distanceBytes16384.Length != 16384 || depthBias is not (512 or 1024))
        {
            throw new InvalidDataException("Native trail outline requires its reached chain, original distance bytes and actual depth bias.");
        }
        (short X, short Y)? direction = incomingDirectionDword is uint prior ? (unchecked((short)prior), unchecked((short)(prior >> 16))) : null;
        ExpandPoint(0, 1, false);
        for (int index = 1; index < outline.Count - 1; index++)
        {
            ExpandPoint(index, index + 1, true);
        }
        ExpandPoint(outline.Count - 1, null, true);
        return direction is { } final ? (ushort)final.X | ((uint)(ushort)final.Y << 16) : null;

        void ExpandPoint(int index, int? nextIndex, bool priorPointer)
        {
            Point current = outline[index];
            if (current.QuarterDepth < 0)
            {
                return;
            }
            int perpendicularX = 0;
            int perpendicularY = 0;
            if (nextIndex is int next)
            {
                Point following = outline[next];
                if (following.QuarterDepth < 0)
                {
                    return;
                }
                int dx = unchecked((short)following.Left) - unchecked((short)current.Left);
                int dy = unchecked((short)(current.Left >> 16)) - unchecked((short)(following.Left >> 16));
                int lookup = Math.Min(Math.Abs(dx), 127) + 128 * Math.Min(Math.Abs(dy), 127);
                int length = distanceBytes16384[lookup];
                if (length == 0 && !priorPointer)
                {
                    current.QuarterDepth = -1;
                    current.DepthWritten = true;
                    return;
                }
                if (length == 0)
                {
                    (short previousX, short previousY) = direction ?? throw new NotSupportedException("Native6C8DE0/6C8620 reads uninitialized prior direction scratch after a degenerate segment.");
                    perpendicularX = previousX;
                    perpendicularY = previousY;
                }
                else
                {
                    int newX = unchecked(dy << 12) / length;
                    int newY = unchecked(dx << 12) / length;
                    (short X, short Y)? previous = direction;
                    direction = (unchecked((short)newX), unchecked((short)newY));
                    if (priorPointer)
                    {
                        (short previousX, short previousY) = previous ?? throw new NotSupportedException("Native6C8DE0/6C8620 reads uninitialized prior direction scratch after a rejected segment.");
                        int sumX = unchecked(newX + previousX);
                        int sumY = unchecked(newY + previousY);
                        if (sumX == 0 && sumY == 0)
                        {
                            sumX = unchecked(newX * 2);
                            sumY = unchecked(newY * 2);
                        }
                        int factor = unchecked(16384 - (unchecked(sumX * sumX + sumY * sumY) >> 13));
                        perpendicularX = unchecked(factor * sumX) >> 13;
                        perpendicularY = unchecked(factor * sumY) >> 13;
                    }
                    else
                    {
                        perpendicularX = newX;
                        perpendicularY = newY;
                    }
                }
                int width = unchecked(following.Width << 5) / (current.QuarterDepth + depthBias);
                perpendicularX = unchecked(width * perpendicularX) >> 12;
                perpendicularY = unchecked(width * perpendicularY) >> 12;
            }
            short x = unchecked((short)current.Left);
            short y = unchecked((short)(current.Left >> 16));
            current.ScreenWritten = true;
            current.Left = Screen(unchecked((short)(x - perpendicularX)), unchecked((short)(y - perpendicularY)));
            current.Right = Screen(unchecked((short)(x + perpendicularX)), unchecked((short)(y + perpendicularY)));
        }
    }

    public static NativeBattleGfGeometryAttempt Packet(int offset, uint leftA, uint rightA, uint leftB, uint rightB, short colorA, short colorB, int bucket, ImmutableArray<int> depthFifo)
    {
        SkinWrites packet = new(52, offset);
        packet.Word(0, 0x0C000000);
        packet.Word(4, (RepeatedColor(colorA) & 0xFFFFFF) | 0x3E000000);
        packet.Word(8, leftA);
        packet.Word(12, 0x3E94C008);
        packet.Word(16, RepeatedColor(colorA));
        packet.Word(20, rightA);
        packet.Word(24, 0x00B6C038);
        packet.Word(28, RepeatedColor(colorB));
        packet.Word(32, leftB);
        packet.Word16(36, 0xC008);
        packet.Word(40, RepeatedColor(colorB));
        packet.Word(44, rightB);
        packet.Word16(48, 0xC038);
        return packet.Attempt(new(bucket, packet.Words(), depthFifo));
    }

    private static uint RepeatedColor(short color)
    {
        int value = color;
        return unchecked((uint)(((value << 8 | value) << 8) | value));
    }

    private static uint Screen(short x, short y)
    {
        return (ushort)x | unchecked((uint)(ushort)y << 16);
    }

    internal sealed class Point(uint screen, short quarterDepth, Func<short> readWidth)
    {
        public uint Left { get; set; } = screen;
        public uint? Right { get; set; }
        public short QuarterDepth { get; set; } = quarterDepth;
        public short Width => readWidth();
        public bool ScreenWritten { get; set; }
        public bool DepthWritten { get; set; }
    }
}
