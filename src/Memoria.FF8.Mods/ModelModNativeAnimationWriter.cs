using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Mods;

/// <summary>Writes the existing native delta bitstream for selected independently decoded clips.</summary>
internal static class ModelModNativeAnimationWriter
{
    public static void Validate(BattleAnimation animation, int bones)
    {
        _ = Encode(animation, bones);
    }
    public static NativeBattlePoseSource Rewrite(NativeBattlePoseSource source, IReadOnlyList<BattleAnimation> original, IReadOnlyList<BattleAnimation> edited)
    {
        if (edited.Count != original.Count || edited.Count is < 1 or > 256)
        {
            throw new NotSupportedException("Native animation overrides must retain the existing clip slots.");
        }
        byte[] bytes = source.Animations.ToArray();
        int directory = checked(4 + edited.Count * 4);
        List<byte[]> clips = [];
        bool changed = false;
        for (int index = 0; index < edited.Count; index++)
        {
            int start = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4 + index * 4));
            int end = Enumerable.Range(0, edited.Count).Select(slot => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4 + slot * 4))).Where(offset => offset > start).DefaultIfEmpty(bytes.Length).Min();
            if (ReferenceEquals(original[index], edited[index]))
            {
                clips.Add(bytes[start..end]);
            }
            else
            {
                clips.Add(Encode(edited[index], source.BoneCount));
                changed = true;
            }
        }
        if (!changed)
        {
            return source;
        }
        byte[] result = new byte[checked(directory + clips.Sum(clip => clip.Length))];
        BinaryPrimitives.WriteInt32LittleEndian(result, clips.Count);
        int cursor = directory;
        for (int index = 0; index < clips.Count; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(4 + index * 4), cursor);
            clips[index].CopyTo(result, cursor);
            cursor += clips[index].Length;
        }
        _ = NativeBattlePoseDecoder.DecodeFrames(result, source.BoneCount);
        return source with { Animations = result.ToImmutableArray() };
    }

    private static byte[] Encode(BattleAnimation animation, int bones)
    {
        if (animation.Frames.Count is < 1 or > 255)
        {
            throw new NotSupportedException("Native battle clips require 1 through 255 frames.");
        }
        BitWriter writer = new();
        writer.Bits(animation.Frames.Count, 8);
        NativeBattleVector previousTranslation = default;
        NativeBattleVector[] previousRotation = new NativeBattleVector[bones];
        foreach (BattlePose pose in animation.Frames)
        {
            if (pose.Rotations.Count != bones || pose.Scales.Count != bones)
            {
                throw new NotSupportedException("A native animation cannot replace the target skeleton.");
            }
            NativeBattleVector translation = new(Quantize(pose.Translation.X, 100), Quantize(pose.Translation.Y, 100), Quantize(pose.Translation.Z, 100));
            Position(translation.X - previousTranslation.X);
            Position(translation.Y - previousTranslation.Y);
            Position(translation.Z - previousTranslation.Z);
            previousTranslation = translation;
            bool scaled = pose.Scales.Any(scale => Math.Abs(scale.X - 1) > 0.00001f || Math.Abs(scale.Y - 1) > 0.00001f || Math.Abs(scale.Z - 1) > 0.00001f);
            writer.Bits(scaled ? 1 : 0, 1);
            for (int bone = 0; bone < bones; bone++)
            {
                var rotation = pose.Rotations[bone];
                NativeBattleVector quantized = new(Angle(rotation.X), Angle(rotation.Y), Angle(rotation.Z));
                Rotation(quantized.X - previousRotation[bone].X);
                Rotation(quantized.Y - previousRotation[bone].Y);
                Rotation(quantized.Z - previousRotation[bone].Z);
                previousRotation[bone] = quantized;
                if (scaled)
                {
                    Scale(pose.Scales[bone].X);
                    Scale(pose.Scales[bone].Y);
                    Scale(pose.Scales[bone].Z);
                }
            }
        }
        byte[] encoded = writer.ToArray();
        if (encoded.Length > ushort.MaxValue)
        {
            throw new NotSupportedException("The native animation cursor cannot address a clip larger than 65535 bytes.");
        }
        return encoded;

        void Position(int value)
        {
            writer.Bits(3, 2);
            writer.Bits(unchecked((ushort)(short)value), 16);
        }
        void Rotation(int value)
        {
            int wrapped = ((value + 2048) & 4095) - 2048;
            writer.Bits(wrapped == 0 ? 0 : 1, 1);
            if (wrapped != 0)
            {
                writer.Bits(3, 2);
                writer.Bits(wrapped & 4095, 12);
            }
        }
        void Scale(float value)
        {
            int quantized = Quantize(value, 1024) - 1024;
            writer.Bits(quantized == 0 ? 0 : 1, 1);
            if (quantized != 0)
            {
                writer.Bits(unchecked((ushort)(short)quantized), 16);
            }
        }
    }

    private static short Quantize(float value, float multiplier)
    {
        float scaled = MathF.Round(value * multiplier);
        if (!float.IsFinite(scaled) || scaled < short.MinValue || scaled > short.MaxValue)
        {
            throw new NotSupportedException("An animation value exceeds the native signed WORD range.");
        }
        return (short)scaled;
    }
    private static short Angle(float value) => unchecked((short)(((int)MathF.Round(value * (4096 / MathF.Tau)) + 2048 & 4095) - 2048));

    private sealed class BitWriter
    {
        private readonly List<byte> bytes = [];
        private int position;
        public void Bits(int value, int count)
        {
            for (int bit = 0; bit < count; bit++, position++)
            {
                if ((position & 7) == 0)
                {
                    bytes.Add(0);
                }
                bytes[position >> 3] |= (byte)(((value >> bit) & 1) << (position & 7));
            }
        }
        public byte[] ToArray() => bytes.ToArray();
    }
}
