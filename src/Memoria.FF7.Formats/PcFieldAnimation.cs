using System.Buffers.Binary;
using System.Numerics;

namespace Memoria.FF7.Formats;

public sealed record PcFieldAnimationFrame(Vector3 RootRotation, Vector3 RootTranslation, IReadOnlyList<Vector3> BoneRotations);

public sealed class PcFieldAnimation
{
    private PcFieldAnimation(uint version, uint rotationOrder, byte[] reserved, int boneCount, PcFieldAnimationFrame[] frames)
    {
        Version = version;
        RotationOrder = rotationOrder;
        ReservedHeader = Array.AsReadOnly(reserved);
        BoneCount = boneCount;
        Frames = Array.AsReadOnly(frames);
    }

    public uint Version { get; }
    public uint RotationOrder { get; }
    public IReadOnlyList<byte> ReservedHeader { get; }
    public int BoneCount { get; }
    public IReadOnlyList<PcFieldAnimationFrame> Frames { get; }

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

    public static PcFieldAnimation Read(ReadOnlySpan<byte> source, int maximumFrames = 100_000, int maximumBones = 4096, int maximumBytes = 64 * 1024 * 1024)
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
        PcFieldAnimationFrame[] frames = new PcFieldAnimationFrame[(int)frameCount];
        int cursor = 36;
        for (int i = 0; i < frames.Length; i++)
        {
            Vector3 rotation = ReadVector(source, ref cursor);
            Vector3 translation = ReadVector(source, ref cursor);
            Vector3[] bones = new Vector3[(int)boneCount];
            for (int bone = 0; bone < bones.Length; bone++)
            {
                bones[bone] = ReadVector(source, ref cursor);
            }
            frames[i] = new PcFieldAnimationFrame(rotation, translation, Array.AsReadOnly(bones));
        }
        return new PcFieldAnimation(version, rotationOrder, source.Slice(16, 20).ToArray(), (int)boneCount, frames);
    }

    private static Vector3 ReadVector(ReadOnlySpan<byte> source, ref int cursor)
    {
        float x = BinaryPrimitives.ReadSingleLittleEndian(source[cursor..]);
        float y = BinaryPrimitives.ReadSingleLittleEndian(source[(cursor + 4)..]);
        float z = BinaryPrimitives.ReadSingleLittleEndian(source[(cursor + 8)..]);
        cursor += 12;
        if (!float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(z))
        {
            throw new InvalidDataException("PC field animation contains non-finite coordinates.");
        }
        return new Vector3(x, y, z);
    }
}
