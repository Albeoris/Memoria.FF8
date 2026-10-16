using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record BattleNativeRenzokukenResource(ushort MagicId, uint ServiceAddress, uint ResourceAddress, ImmutableArray<byte> RawResource, ImmutableArray<byte> AnimationFrameCounts, NativeBattleGraphicsResource GraphicsResource, int AbOffset, byte ShowBar, ImmutableArray<byte> Segments, BattleNativeRenzokukenTimeline Timeline)
{
    public NativeBattleGraphicsResource BindGraphicsResource(NativeBattlePoseSource original)
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
        return GraphicsResource with { PoseSource = pose };
    }
}
