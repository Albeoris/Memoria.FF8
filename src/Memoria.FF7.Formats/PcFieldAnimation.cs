using System.Buffers.Binary;
using System.Numerics;

namespace Memoria.FF7.Formats;

public sealed record PcFieldAnimationFrame(Vector3 RootRotation, Vector3 RootTranslation, IReadOnlyList<Vector3> BoneRotations);

public sealed class PcFieldAnimation
{
    private PcFieldAnimation(uint version, uint rotationOrder, byte[] reserved, int boneCount, IReadOnlyList<PcFieldAnimationFrame> frames)
    {
        Version = version;
        RotationOrder = rotationOrder;
        ReservedHeader = Array.AsReadOnly(reserved);
        BoneCount = boneCount;
        Frames = frames;
    }

    public uint Version { get; }
    public uint RotationOrder { get; }
    public IReadOnlyList<byte> ReservedHeader { get; }
    public int BoneCount { get; }
    public IReadOnlyList<PcFieldAnimationFrame> Frames { get; }

    public PcFieldAnimation BindSingleFrame(IReadOnlyList<Vector3> rotations)
    {
        ArgumentNullException.ThrowIfNull(rotations);
        if (Frames.Count != 1)
        {
            throw new InvalidDataException("A field bind pose requires one frame.");
        }
        return BindBoneRotations([rotations]);
    }

    public PcFieldAnimation BindBoneRotations(IReadOnlyList<IReadOnlyList<Vector3>> frames)
    {
        ArgumentNullException.ThrowIfNull(frames);
        if (frames.Count != Frames.Count)
        {
            throw new InvalidDataException("A field pose binding must preserve the original frame count.");
        }
        int bones = frames[0].Count;
        PcFieldAnimationFrame[] bound = new PcFieldAnimationFrame[frames.Count];
        for (int index = 0; index < frames.Count; index++)
        {
            IReadOnlyList<Vector3> rotations = frames[index];
            if (bones == 0 || rotations.Count != bones || rotations.Any(rotation => !float.IsFinite(rotation.X) || !float.IsFinite(rotation.Y) || !float.IsFinite(rotation.Z)))
            {
                throw new InvalidDataException("A field pose binding requires a consistent set of finite bone rotations.");
            }
            bound[index] = Frames[index] with { BoneRotations = Array.AsReadOnly(rotations.ToArray()) };
        }
        return new(Version, RotationOrder, ReservedHeader.ToArray(), bones, Array.AsReadOnly(bound));
    }

    /// <summary>Reads an ordinal channel from the native contiguous frame allocation, without extending its bounds.</summary>
    public Vector3 ReadNativeBoneRotation(int frameIndex, int boneIndex, float nativeTranslationScale)
    {
        if ((uint)frameIndex >= Frames.Count || boneIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frameIndex), "Native animation channel addressing requires an original frame, nonnegative bone.");
        }
        if (BoneCount == 0)
        {
            return Vector3.Zero;
        }
        long vectorsPerFrame = 2L + BoneCount;
        long ordinal = frameIndex * vectorsPerFrame + 2 + boneIndex;
        if (ordinal >= Frames.Count * vectorsPerFrame)
        {
            throw new NotSupportedException("The selected HRC bone channel addresses beyond the original native animation allocation.");
        }
        PcFieldAnimationFrame selected = Frames[(int)(ordinal / vectorsPerFrame)];
        int channel = (int)(ordinal % vectorsPerFrame);
        // The native loader scales root translations in place before ordinal channel traversal.
        if (channel == 1 && (!float.IsFinite(nativeTranslationScale) || nativeTranslationScale <= 0))
        {
            throw new NotSupportedException("An aliased native root-translation channel requires the resolved descriptor scale.");
        }
        Vector3 rotation = channel switch
        {
            0 => selected.RootRotation,
            1 => selected.RootTranslation * nativeTranslationScale,
            _ => selected.BoneRotations[channel - 2]
        };
        if (!float.IsFinite(rotation.X) || !float.IsFinite(rotation.Y) || !float.IsFinite(rotation.Z))
        {
            throw new InvalidDataException("The native animation channel exceeds finite coordinates after translation scaling.");
        }
        return rotation;
    }

    public static PcFieldAnimation Read(ReadOnlySpan<byte> source, int maximumFrames = 100_000, int maximumBones = 4096, int maximumBytes = 64 * 1024 * 1024, string? sourceName = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumFrames);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumBones);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumBytes);
        if (source.Length < 36 || source.Length > maximumBytes)
        {
            throw new InvalidDataException("PC field animation header is truncated or exceeds the byte limit.");
        }
        uint version = BinaryPrimitives.ReadUInt32LittleEndian(source);
        uint frameCount = BinaryPrimitives.ReadUInt32LittleEndian(source[4..]);
        uint boneCount = BinaryPrimitives.ReadUInt32LittleEndian(source[8..]);
        uint rotationOrder = BinaryPrimitives.ReadUInt32LittleEndian(source[12..]);
        if (version != 1 || frameCount == 0 || frameCount > maximumFrames || boneCount > maximumBones || frameCount > (source.Length - 36) / 24 || boneCount > maximumBytes / 12)
        {
            throw new InvalidDataException("PC field animation version, dimensions or payload length are unsupported.");
        }
        long expectedLength = 36 + (long)frameCount * (24 + 12L * boneCount);
        if (expectedLength != source.Length)
        {
            throw new InvalidDataException("PC field animation payload does not match its dimensions.");
        }
        // Native loading retains every clip's allocation, including poses that are never selected.
        // Coordinate validity belongs to the frame actually consumed, not a duration-only lookup.
        FrameList frames = new(source[36..].ToArray(), (int)frameCount, (int)boneCount, sourceName);
        return new PcFieldAnimation(version, rotationOrder, source.Slice(16, 20).ToArray(), (int)boneCount, frames);
    }

    private sealed class FrameList(byte[] payload, int count, int boneCount, string? sourceName) : IReadOnlyList<PcFieldAnimationFrame>
    {
        private readonly PcFieldAnimationFrame?[] decoded = new PcFieldAnimationFrame[count];
        public int Count => count;

        public PcFieldAnimationFrame this[int index]
        {
            get
            {
                if ((uint)index >= count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }
                PcFieldAnimationFrame? existing = Volatile.Read(ref decoded[index]);
                if (existing is not null)
                {
                    return existing;
                }
                int cursor = index * (24 + 12 * boneCount);
                Vector3 rotation = ReadVector(payload, ref cursor, sourceName, index, 0);
                Vector3 translation = ReadVector(payload, ref cursor, sourceName, index, 1);
                Vector3[] bones = new Vector3[boneCount];
                for (int bone = 0; bone < bones.Length; bone++)
                {
                    bones[bone] = ReadVector(payload, ref cursor, sourceName, index, bone + 2);
                }
                PcFieldAnimationFrame frame = new(rotation, translation, Array.AsReadOnly(bones));
                return Interlocked.CompareExchange(ref decoded[index], frame, null) ?? frame;
            }
        }

        public IEnumerator<PcFieldAnimationFrame> GetEnumerator()
        {
            for (int index = 0; index < Count; index++)
            {
                yield return this[index];
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private static Vector3 ReadVector(ReadOnlySpan<byte> source, ref int cursor, string? sourceName, int frame, int channel)
    {
        float x = BinaryPrimitives.ReadSingleLittleEndian(source[cursor..]);
        float y = BinaryPrimitives.ReadSingleLittleEndian(source[(cursor + 4)..]);
        float z = BinaryPrimitives.ReadSingleLittleEndian(source[(cursor + 8)..]);
        cursor += 12;
        if (!float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(z))
        {
            string coordinate = channel == 0 ? "root rotation" : channel == 1 ? "root translation" : $"bone {channel - 2} rotation";
            throw new InvalidDataException($"PC field animation '{sourceName ?? "<unnamed>"}', frame {frame}, {coordinate} contains non-finite coordinates.");
        }
        return new Vector3(x, y, z);
    }
}
