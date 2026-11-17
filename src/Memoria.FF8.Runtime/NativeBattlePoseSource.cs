using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Memoria.FF8.Runtime;

public readonly record struct NativeBattleVector(short X, short Y, short Z);
public readonly record struct NativeBattleBonePose(NativeBattleVector Rotation, NativeBattleVector Scale);
public sealed record NativeBattleDecodedPose(NativeBattleVector Translation, ImmutableArray<NativeBattleBonePose> Bones, bool HasScale);
public sealed record NativeBattlePoseFirstFrame(NativeBattleDecodedPose Pose, ushort ByteOffset, ushort BitOffset);

/// <summary>Original skeleton and compressed animation bytes consumed by508F90/508C90.</summary>
public sealed record NativeBattlePoseSource(ImmutableArray<byte> Skeleton, ImmutableArray<byte> Animations)
{
    public ImmutableArray<bool> SkeletonKnownBytes { get; init; } = [];
    public ImmutableArray<byte> Model8FBoneAliases { get; init; } = [];

    public int BoneCount => Skeleton.IsDefaultOrEmpty ? throw new InvalidDataException("Native pose lacks its original skeleton.") : Skeleton[0];

    public short ReadModelScale5027D0() => unchecked((short)(BinaryPrimitives.ReadInt16LittleEndian(Skeleton.AsSpan()[2..]) * 4));

    public void Validate()
    {
        if (!Model8FBoneAliases.IsDefaultOrEmpty && Model8FBoneAliases.Length != 16)
        {
            throw new InvalidDataException("Native model8F requires its complete selectorF0..FF directory.");
        }
        if (Skeleton.IsDefaultOrEmpty || BoneCount is < 1 or > 128 || Skeleton.Length < 16 + 48 * BoneCount || Animations.IsDefaultOrEmpty)
        {
            throw new InvalidDataException("Native pose requires its original skeleton records and animation directory.");
        }
        if (!SkeletonKnownBytes.IsDefaultOrEmpty)
        {
            if (SkeletonKnownBytes.Length != Skeleton.Length)
            {
                throw new InvalidDataException("Native skeleton knownness must cover its actual bytes.");
            }
            for (int offset = 0; offset < SkeletonKnownBytes.Length; offset++)
            {
                int recordOffset = offset - 16;
                bool matrixPadding = recordOffset >= 0 && recordOffset / 48 < BoneCount && recordOffset % 48 is 34 or 35;
                if (!SkeletonKnownBytes[offset] && !matrixPadding)
                {
                    throw new NotSupportedException("Native skeleton access reaches an unknown consumed field.");
                }
            }
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
    public static NativeBattlePoseFirstFrame AdvanceRegular508F90(ReadOnlySpan<byte> section, int bones, int animation, NativeBattleDecodedPose current, ushort byteOffset, ushort bitOffset)
    {
        FrameReader reader = Open(section, bones, animation, out _);
        if (byteOffset < 1 || bitOffset > 7 || current.Bones.Length != bones)
        {
            throw new InvalidDataException("Native508F90 has an invalid current pose cursor.");
        }
        reader.SetBitPosition(checked((byteOffset - 1) * 8 + bitOffset));
        NativeBattleBonePose[] result = new NativeBattleBonePose[bones];
        (NativeBattleVector translation, bool scaled) = reader.Read(result);
        Apply(current.Bones.AsSpan(), result, result, 1);
        NativeBattleDecodedPose pose = new(Add(current.Translation, translation, 1), ImmutableCollectionsMarshal.AsImmutableArray(result), scaled);
        return new(pose, checked((ushort)(1 + (reader.BitPosition >> 3))), checked((ushort)(reader.BitPosition & 7)));
    }

    public static NativeBattlePoseFirstFrame DecodeFirst509440(ReadOnlySpan<byte> section, int bones, int animation)
    {
        FrameReader reader = Open(section, bones, animation, out _);
        NativeBattleBonePose[] result = new NativeBattleBonePose[bones];
        (NativeBattleVector translation, bool scaled) = reader.Read(result);
        NativeBattleDecodedPose pose = new(translation, ImmutableCollectionsMarshal.AsImmutableArray(result), scaled);
        return new(pose, checked((ushort)(1 + (reader.BitPosition >> 3))), checked((ushort)(reader.BitPosition & 7)));
    }

    public static ImmutableArray<byte> ReadFrameCounts(ReadOnlySpan<byte> section)
    {
        int count = ReadCount(section);
        ImmutableArray<byte>.Builder frames = ImmutableArray.CreateBuilder<byte>(count);
        for (int index = 0; index < count; index++)
        {
            frames.Add(section[FramePointer(section, index)]);
        }
        return frames.MoveToImmutable();
    }

    public static ImmutableArray<ImmutableArray<NativeBattleDecodedPose>> DecodeFrames(ReadOnlySpan<byte> section, int bones)
    {
        int count = ReadCount(section);
        ImmutableArray<ImmutableArray<NativeBattleDecodedPose>>.Builder animations = ImmutableArray.CreateBuilder<ImmutableArray<NativeBattleDecodedPose>>(count);
        for (int animation = 0; animation < count; animation++)
        {
            FrameReader reader = Open(section, bones, animation, out byte frames);
            ImmutableArray<NativeBattleDecodedPose>.Builder poses = ImmutableArray.CreateBuilder<NativeBattleDecodedPose>(frames);
            NativeBattleBonePose[] first = new NativeBattleBonePose[bones];
            (NativeBattleVector translation, bool scaled) = reader.Read(first);
            NativeBattleDecodedPose current = new(translation, ImmutableCollectionsMarshal.AsImmutableArray(first), scaled);
            poses.Add(current);
            for (int frame = 1; frame < frames; frame++)
            {
                NativeBattleBonePose[] next = new NativeBattleBonePose[bones];
                (translation, scaled) = reader.Read(next);
                Apply(current.Bones.AsSpan(), next, next, 1);
                current = new(Add(current.Translation, translation, 1), ImmutableCollectionsMarshal.AsImmutableArray(next), scaled);
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
        NativeBattleBonePose[] result = new NativeBattleBonePose[bones];
        (NativeBattleVector translation, bool scaled) = reader.Read(result);
        if (counter > 1)
        {
            const int stackBoneLimit = 64;
            Span<NativeBattleBonePose> delta = bones <= stackBoneLimit ? stackalloc NativeBattleBonePose[bones] : new NativeBattleBonePose[bones];
            int divisor = slow ? 2 : 1;
            for (int decoded = 1; decoded < counter;)
            {
                (NativeBattleVector offset, bool hasScale) = reader.Read(delta);
                scaled = hasScale;
                translation = Add(translation, offset, divisor);
                Apply(result, delta, result, divisor);
                decoded++;
                if (slow && decoded < counter)
                {
                    translation = Add(translation, offset, divisor);
                    Apply(result, delta, result, divisor);
                    decoded++;
                }
            }
        }
        return new(translation, ImmutableCollectionsMarshal.AsImmutableArray(result), scaled);
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
        int pointer = FramePointer(section, animation);
        frames = section[pointer];
        return new(section[(pointer + 1)..]);
    }

    private static int FramePointer(ReadOnlySpan<byte> section, int animation)
    {
        int pointer = BinaryPrimitives.ReadInt32LittleEndian(section.Slice(4 + animation * 4));
        if (pointer < 0 || pointer >= section.Length || section[pointer] == 0)
        {
            throw new InvalidDataException("Native animation frame-count pointer is invalid.");
        }
        return pointer;
    }

    private static NativeBattleVector Add(NativeBattleVector first, NativeBattleVector second, int divisor)
    {
        return new(unchecked((short)(first.X + second.X / divisor)), unchecked((short)(first.Y + second.Y / divisor)), unchecked((short)(first.Z + second.Z / divisor)));
    }

    // The destination may alias one input. Each iteration reads both bones before publishing that index.
    private static void Apply(ReadOnlySpan<NativeBattleBonePose> current, ReadOnlySpan<NativeBattleBonePose> delta, Span<NativeBattleBonePose> result, int divisor)
    {
        for (int index = 0; index < result.Length; index++)
        {
            result[index] = new(Add(current[index].Rotation, delta[index].Rotation, divisor), delta[index].Scale);
        }
    }

    private ref struct FrameReader
    {
        private static ReadOnlySpan<byte> PositionWidths => [3, 6, 9, 16];
        private static ReadOnlySpan<byte> RotationWidths => [3, 6, 8, 12];

        private readonly ReadOnlySpan<byte> data;
        private int position;

        public FrameReader(ReadOnlySpan<byte> data)
        {
            this.data = data;
        }

        public int BitPosition => position;

        public void SetBitPosition(int value)
        {
            if (value < 0 || value > data.Length * 8L)
            {
                throw new InvalidDataException("Native508F90 cursor exceeds its current animation section.");
            }
            position = value;
        }

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
        private short Position() => Signed(PositionWidths[ReadBits(2)]);
        private short Rotation() => ReadBits(1) == 0 ? (short)0 : Signed(RotationWidths[ReadBits(2)]);
        private short Scale() => ReadBits(1) == 0 ? (short)1024 : unchecked((short)(Signed(16) + 1024));

        public (NativeBattleVector Translation, bool Scaled) Read(scoped Span<NativeBattleBonePose> result)
        {
            NativeBattleVector translation = new(Position(), Position(), Position());
            bool scaled = ReadBits(1) != 0;
            for (int bone = 0; bone < result.Length; bone++)
            {
                NativeBattleVector rotation = new(Rotation(), Rotation(), Rotation());
                result[bone] = new(rotation, scaled ? new(Scale(), Scale(), Scale()) : new(1024, 1024, 1024));
            }
            return (translation, scaled);
        }
    }
}
