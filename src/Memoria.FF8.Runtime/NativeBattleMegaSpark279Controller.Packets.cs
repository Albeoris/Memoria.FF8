using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleMegaSpark279Controller
{
    private static NativeBattleMegaSpark279Frame DrawStrip279(NativeBattleMegaSpark279Frame frame, IReadOnlyList<uint> rows, bool attachment, NativeBattleMegaSpark279Resources resources, NativeBattleMegaSpark279Inputs inputs)
    {
        int stride = attachment ? 16 : 40;
        int xy = attachment ? 0 : 16;
        int depth = attachment ? 4 : 20;
        int other = attachment ? 8 : 24;
        int color = attachment ? 14 : 32;
        int[] tangent = new int[2];
        frame = Offset279(frame, rows[0], rows[1], null, tangent, attachment, resources);
        for (int index = 1; index < rows.Count - 1; index++)
        {
            frame = Offset279(frame, rows[index], rows[index + 1], tangent, tangent, attachment, resources);
        }
        frame = Offset279(frame, rows[^1], 0, tangent, null, attachment, resources);
        var attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        for (int index = 0; index < rows.Count - 1; index++)
        {
            NativeBattleEffectArena a = StripRead279(frame, rows[index], stride, attachment);
            NativeBattleEffectArena b = StripRead279(frame, rows[index + 1], stride, attachment);
            int za = Word279(a, depth);
            int zb = Word279(b, depth);
            if (za <= 0 || zb <= 0)
            {
                continue;
            }
            byte[] data = new byte[52];
            NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[52];
            void Store(int at, ReadOnlySpan<byte> bytes)
            {
                bytes.CopyTo(data.AsSpan(at));
                Array.Fill(mask, NativeBattleGfPacketByteState.Known, at, bytes.Length);
            }
            void Dword(int at, uint value) => Store(at, BitConverter.GetBytes(value));
            int ca = Word279(a, color);
            int cb = Word279(b, color);
            if (!attachment)
            {
                ca >>= 4;
                cb >>= 4;
            }
            uint Replicate(int value) => unchecked((uint)((((value << 8) | value) << 8) | value));
            Dword(0, 0x0C000000);
            Dword(4, Replicate(ca));
            Store(7, new byte[] { 0x3E });
            Store(8, a.Read(xy, 4));
            Dword(12, 0x3D948048);
            Dword(16, Replicate(ca));
            Store(20, a.Read(other, 4));
            Dword(24, 0x00B68078);
            Dword(28, Replicate(cb));
            Store(32, b.Read(xy, 4));
            Store(36, BitConverter.GetBytes((ushort)0x8048));
            Dword(40, Replicate(cb));
            Store(44, b.Read(other, 4));
            Store(48, BitConverter.GetBytes((ushort)0x8078));
            ImmutableArray<uint> words = Enumerable.Range(0, 13).Select(word => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(word * 4))).ToImmutableArray();
            ImmutableArray<int> payload = (frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Mega Spark packets require their current depth FIFO.")).Sz.Select(value => unchecked((int)(value ?? throw new NotSupportedException("Mega Spark quad insertion requires four current depths.")))).ToImmutableArray();
            var primitive = new NativeBattleGeometryPrimitive(unchecked(17 + ((za + zb) >> 3)), words, payload);
            attempts.Add(new(attempts.Count * 52, data.ToImmutableArray(), mask.ToImmutableArray(), primitive));
        }
        if (attempts.Count == 0)
        {
            return frame;
        }
        return inputs.Execute(frame, new NativeBattleMegaSpark279DeliverBatch(new(attempts.ToImmutable(), attempts.Count * 52), attachment ? 0x1D8E054u : 0x2508104u));
    }

    private static NativeBattleMegaSpark279Frame Offset279(NativeBattleMegaSpark279Frame frame, uint address, uint next, int[]? previous, int[]? output, bool attachment, NativeBattleMegaSpark279Resources resources)
    {
        int xy = attachment ? 0 : 16;
        int depth = attachment ? 4 : 20;
        int other = attachment ? 8 : 24;
        NativeBattleEffectArena a = StripRead279(frame, address, attachment ? 16 : 40, attachment);
        int z = Word279(a, depth);
        if (z < 0)
        {
            return frame;
        }
        int sx = 0;
        int sy = 0;
        if (next != 0)
        {
            NativeBattleEffectArena b = StripRead279(frame, next, attachment ? 16 : 40, attachment);
            if (Word279(b, depth) < 0)
            {
                return frame;
            }
            int dx = Word279(b, xy) - Word279(a, xy);
            int dy = Word279(a, xy + 2) - Word279(b, xy + 2);
            int distance = resources.ReadDistanceByte(Math.Min(Math.Abs(dy), 127), Math.Min(Math.Abs(dx), 127));
            if (previous is not null)
            {
                if (distance == 0)
                {
                    sx = previous[0];
                    sy = previous[1];
                    if (output is not null)
                    {
                        output[0] = sx;
                        output[1] = sy;
                    }
                }
                else
                {
                    int tx = unchecked(dy << 12) / distance;
                    int ty = unchecked(dx << 12) / distance;
                    int x = unchecked(tx + previous[0]);
                    int y = unchecked(ty + previous[1]);
                    if (output is not null)
                    {
                        output[0] = unchecked((short)tx);
                        output[1] = unchecked((short)ty);
                    }
                    if (x == 0 && y == 0)
                    {
                        x = unchecked(tx * 2);
                        y = unchecked(ty * 2);
                    }
                    int factor = unchecked(16384 - (unchecked(x * x + y * y) >> 13));
                    sx = unchecked(factor * x) >> 13;
                    sy = unchecked(factor * y) >> 13;
                }
            }
            else
            {
                if (distance == 0)
                {
                    return StripWrite279(frame, address + (uint)depth, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((short)-1)), 2, attachment);
                }
                sx = unchecked(dy << 12) / distance;
                sy = unchecked(dx << 12) / distance;
                if (output is not null)
                {
                    output[0] = unchecked((short)sx);
                    output[1] = unchecked((short)sy);
                }
            }
            int width = (attachment ? 8320 : 8000) / (z + (attachment ? 1024 : 512));
            sx = unchecked(width * sx) >> 12;
            sy = unchecked(width * sy) >> 12;
        }
        int x0 = Word279(a, xy);
        int y0 = Word279(a, xy + 2);
        a = a.Write(xy, BitConverter.GetBytes(unchecked((short)(x0 - sx)))).Write(xy + 2, BitConverter.GetBytes(unchecked((short)(y0 - sy)))).Write(other, BitConverter.GetBytes(unchecked((short)(x0 + sx)))).Write(other + 2, BitConverter.GetBytes(unchecked((short)(y0 + sy))));
        return StripWrite279(frame, address, a, attachment ? 16 : 40, attachment);
    }

    private static NativeBattleEffectArena StripRead279(NativeBattleMegaSpark279Frame frame, uint address, int length, bool temporary) => temporary ? Context(frame, address, length) : ReadBytes(frame, address, length);
    private static NativeBattleMegaSpark279Frame StripWrite279(NativeBattleMegaSpark279Frame frame, uint address, NativeBattleEffectArena view, int length, bool temporary) => temporary ? PublishContext(frame, address, view, length) : WriteBytes(frame, address, view, length);
    private static short Word279(NativeBattleEffectArena view, int offset) => BinaryPrimitives.ReadInt16LittleEndian(view.Read(offset, 2));
    private static uint Dword279(NativeBattleEffectArena view, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(view.Read(offset, 4));
    private static NativeBattleVector Vector279(NativeBattleEffectArena view) => new(Word279(view, 0), Word279(view, 2), Word279(view, 4));
    private static short? Padding279(NativeBattleEffectArena view)
    {
        (byte[] bytes, bool[] known) = view.ReadSnapshot(6, 2);
        return known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null;
    }
}
