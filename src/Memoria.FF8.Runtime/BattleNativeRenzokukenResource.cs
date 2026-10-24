using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record BattleNativeRenzokukenResource(ushort MagicId, uint ServiceAddress, uint ResourceAddress, ImmutableArray<byte> RawResource, ImmutableArray<byte> AnimationFrameCounts, NativeBattleGraphicsResource GraphicsResource, int AbOffset, byte ShowBar, ImmutableArray<byte> Segments, BattleNativeRenzokukenTimeline Timeline)
{
    public ImmutableArray<BattleNativeRenzokukenTargetResource> TargetResources { get; init; } = [];

    public NativeBattleGraphicsResource BindGraphicsResource(NativeBattlePoseSource original, NativeBattlePoseSource? originalLinked = null)
    {
        original.Validate();
        if (RawResource.Length < 20 || BinaryPrimitives.ReadUInt32LittleEndian(RawResource.AsSpan()) != 3)
        {
            throw new InvalidDataException("Native Renzokuken animation swap requires its original three-section resource.");
        }
        int animations = BinaryPrimitives.ReadInt32LittleEndian(RawResource.AsSpan().Slice(4));
        int sequences = BinaryPrimitives.ReadInt32LittleEndian(RawResource.AsSpan().Slice(8));
        if (animations < 20 || sequences <= animations || sequences >= RawResource.Length)
        {
            throw new InvalidDataException("Native Renzokuken animation section exceeds its resource.");
        }
        NativeBattlePoseSource pose = original with { Animations = RawResource.AsSpan().Slice(animations, sequences - animations).ToArray().ToImmutableArray() };
        pose.Validate();
        NativeBattlePoseSource? linked = null;
        if (originalLinked is not null)
        {
            int linkedAnimations = BinaryPrimitives.ReadInt32LittleEndian(RawResource.AsSpan().Slice(12));
            if (linkedAnimations <= sequences || linkedAnimations >= RawResource.Length)
            {
                throw new InvalidDataException("Native Renzokuken linked animation section exceeds its resource.");
            }
            linked = originalLinked with { Animations = RawResource.AsSpan().Slice(linkedAnimations).ToArray().ToImmutableArray() };
            linked.Validate();
        }
        return GraphicsResource with { PoseSource = pose, LinkedPoseSource = linked, LinkedAnimationFrames = linked is null ? [] : NativeBattlePoseDecoder.ReadFrameCounts(linked.Animations.AsSpan()) };
    }
}

public sealed record BattleNativeRenzokukenTargetResource(uint ResourceAddress, ImmutableArray<byte> RawResource, ImmutableArray<byte> AnimationFrameCounts, NativeBattleGraphicsResource GraphicsResource)
{
    public NativeBattleGraphicsResource BindGraphicsResource(NativeBattlePoseSource original)
    {
        original.Validate();
        if (RawResource.Length < 16 || BinaryPrimitives.ReadUInt32LittleEndian(RawResource.AsSpan()) != 2)
        {
            throw new InvalidDataException("Native Renzokuken target animation swap requires its original two-section resource.");
        }
        int animations = BinaryPrimitives.ReadInt32LittleEndian(RawResource.AsSpan().Slice(4));
        int sequences = BinaryPrimitives.ReadInt32LittleEndian(RawResource.AsSpan().Slice(8));
        int length = BinaryPrimitives.ReadInt32LittleEndian(RawResource.AsSpan().Slice(12));
        if (animations < 16 || sequences <= animations || sequences >= length || length != RawResource.Length)
        {
            throw new InvalidDataException("Native Renzokuken target animation section exceeds its resource.");
        }
        NativeBattlePoseSource pose = original with { Animations = RawResource.AsSpan().Slice(animations, sequences - animations).ToArray().ToImmutableArray() };
        pose.Validate();
        return GraphicsResource with { PoseSource = pose };
    }
}
