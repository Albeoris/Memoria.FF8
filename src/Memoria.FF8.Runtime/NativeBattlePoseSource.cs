using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public readonly record struct NativeBattleVector(short X, short Y, short Z);
public sealed record NativeBattleBonePose(NativeBattleVector Rotation, NativeBattleVector Scale);
public sealed record NativeBattleDecodedPose(NativeBattleVector Translation, ImmutableArray<NativeBattleBonePose> Bones, bool HasScale);

/// <summary>Original skeleton and compressed animation bytes consumed by508F90/508C90.</summary>
public sealed record NativeBattlePoseSource(ImmutableArray<byte> Skeleton, ImmutableArray<byte> Animations)
{
    public int BoneCount => Skeleton.IsDefaultOrEmpty ? throw new InvalidDataException("Native pose lacks its original skeleton.") : Skeleton[0];

    public void Validate()
    {
        if (Skeleton.IsDefaultOrEmpty || BoneCount is < 1 or > 128 || Skeleton.Length < 16 + 48 * BoneCount || Animations.IsDefaultOrEmpty)
        {
            throw new InvalidDataException("Native pose requires its original skeleton records and animation directory.");
        }
        for (int bone = 0; bone < BoneCount; bone++)
        {
            short parent = BinaryPrimitives.ReadInt16LittleEndian(Skeleton.AsSpan().Slice(16 + bone * 48));
            if (parent < -1 || parent >= bone)
            {
                throw new InvalidDataException("Native bone parent is not an earlier skeleton record.");
            }
        }
    }

    public NativeBattleDecodedPose Sample(byte animation, byte counter, byte poseFlags)
    {
        Validate();
        if (counter == 0)
        {
            throw new InvalidDataException("Native pose has not decoded its first frame.");
        }
        return NativeBattlePoseDecoder.Sample(Animations.AsSpan(), BoneCount, animation, counter, (poseFlags & 1) != 0);
    }
}

public static class NativeBattlePoseDecoder
{
    public static ImmutableArray<ImmutableArray<NativeBattleDecodedPose>> DecodeFrames(ReadOnlySpan<byte> section, int bones)
    {
        int count = ReadCount(section);
        ImmutableArray<ImmutableArray<NativeBattleDecodedPose>>.Builder animations = ImmutableArray.CreateBuilder<ImmutableArray<NativeBattleDecodedPose>>(count);
        for (int animation = 0; animation < count; animation++)
        {
            FrameReader reader = Open(section, bones, animation, out byte frames);
            ImmutableArray<NativeBattleDecodedPose>.Builder poses = ImmutableArray.CreateBuilder<NativeBattleDecodedPose>(frames);
            NativeBattleDecodedPose current = Empty(bones);
            for (int frame = 0; frame < frames; frame++)
            {
                current = Apply(current, reader.Read(), false);
                poses.Add(current);
            }
            animations.Add(poses.MoveToImmutable());
        }
        return animations.MoveToImmutable();
    }

    public static NativeBattleDecodedPose Sample(ReadOnlySpan<byte> section, int bones, int animation, byte counter, bool slow)
    {
        FrameReader reader = Open(section, bones, animation, out byte frames);
        int limit = slow ? unchecked((byte)(2 * frames - 1)) : frames;
        if (counter < 1 || counter > limit)
        {
            throw new InvalidDataException("Native pose frame counter exceeds its original animation.");
        }
        NativeBattleDecodedPose current = Apply(Empty(bones), reader.Read(), false);
        for (int decoded = 1; decoded < counter;)
        {
            NativeBattleDecodedPose delta = reader.Read();
            current = Apply(current, delta, slow);
            decoded++;
            if (slow && decoded < counter)
            {
                current = Apply(current, delta, true);
                decoded++;
            }
        }
        return current;
    }

    private static int ReadCount(ReadOnlySpan<byte> section)
    {
        if (section.Length < 4)
        {
            throw new InvalidDataException("Native animation lacks its directory.");
        }
        int count = BinaryPrimitives.ReadInt32LittleEndian(section);
        if (count is < 1 or > 256 || 4L + 4L * count > section.Length)
        {
            throw new InvalidDataException("Native animation directory exceeds its original section.");
        }
        return count;
    }

    private static FrameReader Open(ReadOnlySpan<byte> section, int bones, int animation, out byte frames)
    {
        int count = ReadCount(section);
        if (bones is < 1 or > 128 || (uint)animation >= count)
        {
            throw new InvalidDataException("Native animation selector or bone count is invalid.");
        }
        int pointer = BinaryPrimitives.ReadInt32LittleEndian(section.Slice(4 + animation * 4));
        if (pointer < 0 || pointer >= section.Length || section[pointer] == 0)
        {
            throw new InvalidDataException("Native animation frame-count pointer is invalid.");
        }
        frames = section[pointer];
        return new(section[(pointer + 1)..].ToArray(), bones);
    }

    private static NativeBattleDecodedPose Empty(int bones) => new(default, Enumerable.Repeat(new NativeBattleBonePose(default, new(1024, 1024, 1024)), bones).ToImmutableArray(), false);

    private static NativeBattleDecodedPose Apply(NativeBattleDecodedPose current, NativeBattleDecodedPose delta, bool half)
    {
        NativeBattleVector Add(NativeBattleVector first, NativeBattleVector second)
        {
            int divisor = half ? 2 : 1;
            return new(unchecked((short)(first.X + second.X / divisor)), unchecked((short)(first.Y + second.Y / divisor)), unchecked((short)(first.Z + second.Z / divisor)));
        }
        return new(Add(current.Translation, delta.Translation), current.Bones.Select((bone, index) => new NativeBattleBonePose(Add(bone.Rotation, delta.Bones[index].Rotation), delta.Bones[index].Scale)).ToImmutableArray(), delta.HasScale);
    }

    private sealed class FrameReader(byte[] data, int bones)
    {
        private int position;

        private int ReadBits(int count)
        {
            if (position + (long)count > data.Length * 8L)
            {
                throw new InvalidDataException("Native animation bit stream exceeds its original section.");
            }
            int value = 0;
            for (int bit = 0; bit < count; bit++, position++)
            {
                value |= (data[position >> 3] >> (position & 7) & 1) << bit;
            }
            return value;
        }

        private short Signed(int count) => unchecked((short)(ReadBits(count) << (32 - count) >> (32 - count)));
        private short Position() => Signed(new[] { 3, 6, 9, 16 }[ReadBits(2)]);
        private short Rotation() => ReadBits(1) == 0 ? (short)0 : Signed(new[] { 3, 6, 8, 12 }[ReadBits(2)]);
        private short Scale() => ReadBits(1) == 0 ? (short)1024 : unchecked((short)(Signed(16) + 1024));

        public NativeBattleDecodedPose Read()
        {
            NativeBattleVector translation = new(Position(), Position(), Position());
            bool scaled = ReadBits(1) != 0;
            ImmutableArray<NativeBattleBonePose>.Builder result = ImmutableArray.CreateBuilder<NativeBattleBonePose>(bones);
            for (int bone = 0; bone < bones; bone++)
            {
                NativeBattleVector rotation = new(Rotation(), Rotation(), Rotation());
                result.Add(new(rotation, scaled ? new(Scale(), Scale(), Scale()) : new(1024, 1024, 1024)));
            }
            return new(translation, result.MoveToImmutable(), scaled);
        }
    }
}
