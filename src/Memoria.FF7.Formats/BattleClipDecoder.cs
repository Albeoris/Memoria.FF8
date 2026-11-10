using System.Buffers.Binary;

namespace Memoria.FF7.Formats;

public readonly record struct BattleRotationWords(short X, short Y, short Z);
public sealed record BattleClipFrame(short X, short Y, short Z, IReadOnlyList<BattleRotationWords> Rotations);

/// <summary>Decodes original PC battle clips into native translation and rotation words.</summary>
public static class BattleClipDecoder
{
    public static IReadOnlyList<BattleClipFrame> Decode(BattlePackedClip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);
        ReadOnlySpan<byte> data = clip.PackedData.Span;
        if (data.Length == 4)
        {
            throw new NotSupportedException("This FFVII battle animation slot has no packed frames.");
        }
        if (data.Length < 5 || clip.RotationCount is 0 or > 50 || clip.FrameCount == 0)
        {
            throw new InvalidDataException("FFVII battle animation has an invalid frame or rotation allocation.");
        }
        int rotations = (int)clip.RotationCount;
        int byteCount = BinaryPrimitives.ReadUInt16LittleEndian(data[2..]);
        int key = data[4];
        long minimumBits = 48L + 3 * rotations * (12 - key) + (clip.FrameCount - 1L) * (24 + 3 * rotations);
        if (key > 11 || byteCount > data.Length - 5 || minimumBits > byteCount * 8L)
        {
            throw new InvalidDataException("FFVII packed animation exceeds its declared bitstream.");
        }
        ClipBits bits = new(data.Slice(5, byteCount));
        BattleClipFrame[] frames = new BattleClipFrame[clip.FrameCount];
        for (int frame = 0; frame < frames.Length; frame++)
        {
            BattleRotationWords[] joints = new BattleRotationWords[rotations];
            short x;
            short y;
            short z;
            if (frame == 0)
            {
                x = (short)bits.Signed(16);
                y = (short)bits.Signed(16);
                z = (short)bits.Signed(16);
                for (int index = 0; index < joints.Length; index++)
                {
                    joints[index] = new((short)(bits.Signed(12 - key) << key), (short)(bits.Signed(12 - key) << key), (short)(bits.Signed(12 - key) << key));
                }
            }
            else
            {
                BattleClipFrame prior = frames[frame - 1];
                x = unchecked((short)(prior.X + bits.TranslationDelta()));
                y = unchecked((short)(prior.Y + bits.TranslationDelta()));
                z = unchecked((short)(prior.Z + bits.TranslationDelta()));
                for (int index = 0; index < joints.Length; index++)
                {
                    BattleRotationWords joint = prior.Rotations[index];
                    joints[index] = new(unchecked((short)(joint.X + bits.RotationDelta(key))), unchecked((short)(joint.Y + bits.RotationDelta(key))), unchecked((short)(joint.Z + bits.RotationDelta(key))));
                }
            }
            frames[frame] = new(x, y, z, Array.AsReadOnly(joints));
        }
        return Array.AsReadOnly(frames);
    }

    private ref struct ClipBits
    {
        private readonly ReadOnlySpan<byte> data;
        private int position;

        public ClipBits(ReadOnlySpan<byte> data)
        {
            this.data = data;
        }

        private int Read(int count)
        {
            if (position + count > data.Length * 8)
            {
                throw new InvalidDataException("Truncated FFVII packed battle animation frame.");
            }
            int value = 0;
            for (int index = 0; index < count; index++, position++)
            {
                value = value << 1 | (data[position >> 3] >> (7 - (position & 7)) & 1);
            }
            return value;
        }

        public int Signed(int count)
        {
            int value = Read(count);
            int sign = 1 << (count - 1);
            return (value ^ sign) - sign;
        }

        public int TranslationDelta() => Signed(Read(1) == 0 ? 7 : 16);

        public int RotationDelta(int key)
        {
            if (Read(1) == 0)
            {
                return 0;
            }
            int method = Read(3);
            if (method == 0)
            {
                return -1 << key;
            }
            if (method == 7)
            {
                return Signed(12 - key) << key;
            }
            int delta = Signed(method);
            int bias = 1 << (method - 1);
            return (delta < 0 ? delta - bias : delta + bias) << key;
        }
    }
}
