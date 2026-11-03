using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleModulePackets
{
    private static readonly ImmutableArray<NativeBattleGfPacketByteState> SpriteUnwrittenStoreMask = Enumerable.Repeat(NativeBattleGfPacketByteState.Untouched, 40).ToImmutableArray();
    private static readonly ImmutableArray<NativeBattleGfPacketByteState> SpriteAcceptedStoreMask = SpriteStoreMask((0, 30), (32, 6));
    private static readonly ImmutableArray<NativeBattleGfPacketByteState> SpriteRejectedStoreMask = SpriteStoreMask((0, 8), (12, 4), (20, 2), (28, 1), (36, 1));
    private static readonly ImmutableArray<NativeBattleGfPacketByteState> SpriteExtendedStoreMask = Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 8).Concat(Enumerable.Repeat(NativeBattleGfPacketByteState.Untouched, 4)).ToImmutableArray();

    private static ImmutableArray<NativeBattleGfPacketByteState> SpriteStoreMask(params (int Offset, int Length)[] ranges)
    {
        NativeBattleGfPacketByteState[] mask = Enumerable.Repeat(NativeBattleGfPacketByteState.Untouched, 40).ToArray();
        foreach ((int offset, int length) in ranges)
        {
            Array.Fill(mask, NativeBattleGfPacketByteState.Known, offset, length);
        }
        return mask.ToImmutableArray();
    }

    private static NativeBattleGfGeometryAttempt SpritePacketStore(int offset, ReadOnlySpan<uint> words, ImmutableArray<NativeBattleGfPacketByteState> mask, NativeBattleGeometryPrimitive? primitive)
    {
        byte[] bytes = new byte[words.Length * 4];
        for (int index = 0; index < words.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 4), words[index]);
        }
        return new(offset, bytes.ToImmutableArray(), mask, primitive);
    }
}
