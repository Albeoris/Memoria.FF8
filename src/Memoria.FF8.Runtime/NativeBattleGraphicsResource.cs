using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Original model sequence directory/bytecode at graphics resource+12.</summary>
public sealed record NativeBattleGraphicsResource(ImmutableArray<byte> SequenceSection)
{
    public ImmutableArray<byte> AnimationFrames { get; init; }
    public NativeBattlePoseSource? PoseSource { get; init; }
    public ImmutableArray<byte> LinkedAnimationFrames { get; init; } = [];
    public NativeBattlePoseSource? LinkedPoseSource { get; init; }
    public bool? HasActionCamera { get; init; }
    public NativeBattleCameraBank? ActionCameraBank { get; init; }
    public ImmutableArray<byte>? TextureAnimations { get; init; }

    public void Validate()
    {
        PoseSource?.Validate();
        LinkedPoseSource?.Validate();
        if (SequenceSection.IsDefault || SequenceSection.Length < 2 || BinaryPrimitives.ReadUInt16LittleEndian(SequenceSection.AsSpan()) * 2 + 2 > SequenceSection.Length)
        {
            throw new InvalidDataException("Native graphics resource lacks its original bounded sequence directory.");
        }
    }

    public bool Contains(byte sequence)
    {
        Validate();
        return sequence != 0 && sequence <= BinaryPrimitives.ReadUInt16LittleEndian(SequenceSection.AsSpan()) && BinaryPrimitives.ReadUInt16LittleEndian(SequenceSection.AsSpan().Slice(sequence * 2)) != 0;
    }

    public int SequenceStart(byte sequence)
    {
        if (!Contains(sequence))
        {
            throw new InvalidDataException($"Original graphics resource has no sequence{sequence}.");
        }
        return BinaryPrimitives.ReadUInt16LittleEndian(SequenceSection.AsSpan().Slice(sequence * 2));
    }

    public int SequenceEnd(byte sequence)
    {
        int start = SequenceStart(sequence);
        int end = SequenceSection.Length;
        int count = BinaryPrimitives.ReadUInt16LittleEndian(SequenceSection.AsSpan());
        for (int index = 1; index <= count; index++)
        {
            int offset = BinaryPrimitives.ReadUInt16LittleEndian(SequenceSection.AsSpan().Slice(index * 2));
            if (offset > start)
            {
                end = Math.Min(end, offset);
            }
        }
        return end;
    }
}
