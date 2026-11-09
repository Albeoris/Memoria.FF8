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
