using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState PublishNativeScreenBlend501E80(NativeBattleGraphicsState graphics, uint color)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native501E80 requires its current COMMON process.");
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Native501E80 requires its current COMMON cursor.");
        NativeBattlePrimitiveFrame frame = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native501E80 requires its current ordering table.");
        uint[] words = [0x03000000, color, 0, 0x00D80140];
        byte[] bytes = new byte[16];
        for (int index = 0; index < words.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 4), words[index]);
        }
        NativeBattleGeometryPrimitive primitive = new(3, words.ToImmutableArray(), [0, 0, 0, 0]);
        NativeBattleGfGeometryAttempt packet = new(0, bytes.ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 16).ToImmutableArray(), primitive);
        NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(process, frame.Pending, new([packet], 16), 1);
        NativeEffectMemory = memory with { PrimitiveProcess = written.Process };
        return graphics with { PrimitiveFrame = frame with { Pending = written.Ordering } };
    }
}
