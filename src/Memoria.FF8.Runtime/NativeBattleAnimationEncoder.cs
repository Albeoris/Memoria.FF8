using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Writes generated motion using the same absolute-first-frame and delta-frame contract as native battle banks.</summary>
public static class NativeBattleAnimationEncoder
{
    public static ImmutableArray<byte> Encode(IReadOnlyList<ImmutableArray<NativeBattleDecodedPose>> animations)
    {
        if (animations.Count is < 1 or > 256)
        {
            throw new InvalidDataException("A native animation bank requires one to 256 animations.");
        }
        using MemoryStream output = new();
        output.SetLength(4 + animations.Count * 4);
        int bones = -1;
        Span<byte> word = stackalloc byte[4];
        for (int animation = 0; animation < animations.Count; animation++)
        {
            ImmutableArray<NativeBattleDecodedPose> frames = animations[animation];
            if (frames.IsDefaultOrEmpty || frames.Length > 255)
            {
                throw new InvalidDataException("A native animation requires one to 255 frames.");
            }
            long pointer = output.Length;
            output.Position = 4 + animation * 4;
            BinaryPrimitives.WriteInt32LittleEndian(word, checked((int)pointer));
            output.Write(word);
            output.Position = pointer;
            output.WriteByte(checked((byte)frames.Length));
            BitWriter writer = new(output);
            NativeBattleDecodedPose? previous = null;
            foreach (NativeBattleDecodedPose frame in frames)
            {
                if (bones < 0)
                {
                    bones = frame.Bones.Length;
                }
                if (bones is < 1 or > 128 || frame.Bones.Length != bones)
                {
                    throw new InvalidDataException("A native animation bank requires one consistent complete skeleton.");
                }
                writer.Position(Difference(frame.Translation.X, previous?.Translation.X ?? 0));
                writer.Position(Difference(frame.Translation.Y, previous?.Translation.Y ?? 0));
                writer.Position(Difference(frame.Translation.Z, previous?.Translation.Z ?? 0));
                writer.Bits(frame.HasScale ? 1 : 0, 1);
                for (int bone = 0; bone < bones; bone++)
                {
                    NativeBattleBonePose current = frame.Bones[bone];
                    NativeBattleVector prior = previous?.Bones[bone].Rotation ?? default;
                    writer.Rotation(current.Rotation.X - prior.X);
                    writer.Rotation(current.Rotation.Y - prior.Y);
                    writer.Rotation(current.Rotation.Z - prior.Z);
                    if (frame.HasScale)
                    {
                        writer.Scale(current.Scale.X);
                        writer.Scale(current.Scale.Y);
                        writer.Scale(current.Scale.Z);
                    }
                    else if (current.Scale != new NativeBattleVector(1024, 1024, 1024))
                    {
                        throw new InvalidDataException("An unscaled frame cannot discard nonidentity bone scales.");
                    }
                }
                previous = frame;
            }
            writer.Flush();
        }
        output.Position = 0;
        Span<byte> count = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(count, animations.Count);
        output.Write(count);
        return output.ToArray().ToImmutableArray();
    }

    private static short Difference(short current, short previous) => unchecked((short)(current - previous));

    private sealed class BitWriter(Stream output)
    {
        private byte current;
        private int used;

        public void Bits(int value, int count)
        {
            for (int bit = 0; bit < count; bit++)
            {
                current |= checked((byte)(((value >> bit) & 1) << used));
                used++;
                if (used == 8)
                {
                    Flush();
                }
            }
        }

        public void Flush()
        {
            if (used != 0)
            {
                output.WriteByte(current);
                current = 0;
                used = 0;
            }
        }

        public void Position(short value)
        {
            Bits(3, 2);
            Bits(value, 16);
        }

        public void Rotation(int value)
        {
            int normalized = ((value + 2048) & 4095) - 2048;
            Bits(normalized == 0 ? 0 : 1, 1);
            if (normalized != 0)
            {
                Bits(3, 2);
                Bits(normalized, 12);
            }
        }

        public void Scale(short value)
        {
            Bits(value == 1024 ? 0 : 1, 1);
            if (value != 1024)
            {
                Bits(unchecked((short)(value - 1024)), 16);
            }
        }
    }
}
