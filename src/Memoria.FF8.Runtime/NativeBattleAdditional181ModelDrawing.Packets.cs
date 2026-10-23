using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleAdditional181ModelDrawing
{
    private static NativeBattleGfGeometryAttempt EmitOverlay181(NativeBattleGeometryPrimitive source, bool quad, uint color, int tableBaseBucket, int offset)
    {
        uint[] words = quad ? [0x05000000u, color | 0x2A000000u, source.Words[2], source.Words[4], source.Words[6], source.Words[8]] : [0x04000000u, color | 0x22000000u, source.Words[2], source.Words[4], source.Words[6]];
        byte[] bytes = new byte[words.Length * 4];
        for (int index = 0; index < words.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 4), words[index]);
        }
        ImmutableArray<NativeBattleGfPacketByteState> mask = Enumerable.Repeat(NativeBattleGfPacketByteState.Known, bytes.Length).ToImmutableArray();
        int depth = source.Bucket - tableBaseBucket;
        NativeBattleGeometryPrimitive primitive = new(depth > 0 ? source.Bucket - 1 : source.Bucket, words.ToImmutableArray(), source.VertexPayload4) { PhysicalWriteMask = mask };
        return new(offset, bytes.ToImmutableArray(), mask, primitive);
    }
}
