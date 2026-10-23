using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>571250/571300 common-heap tiles and7011A0's private-arena fullscreen polygon store every packet byte before45C8E0.</summary>
public static class NativeBattleFlashPackets
{
    public static NativeBattleModulePacketBatch Emit571250(int red, int green, int blue)
    {
        uint rgb = unchecked((uint)(byte)red | (uint)(byte)green << 8 | (uint)(byte)blue << 16);
        return Packet([0x03000000, 0x62000000 | rgb, 0, 216u << 16 | 320], 17);
    }

    public static NativeBattleModulePacketBatch Emit(NativeBattleModuleDraw draw)
    {
        if (draw.Kind != NativeBattleModuleDrawKind.Flash)
        {
            throw new InvalidDataException("Native fullscreen packet production requires its actual flash receipt.");
        }
        uint rgb = (uint)(draw.Intensity | draw.Intensity << 8 | draw.Intensity << 16);
        byte command = draw.NativePrimitiveCommand ?? 0x62;
        int bucket = draw.NativeOrderingBucket ?? 17;
        ImmutableArray<uint> words;
        if (command == 0x62 && draw.NativeEffectArenaOffset is null)
        {
            words = [0x3000000, 0x62000000 | rgb, 0, 216u << 16 | 320];
        }
        else if (command == 0x2A && draw.NativeScreenVertices.Length == 4 && draw.NativeEffectArenaOffset is int offset && offset >= 0 && (offset & 3) == 0 && offset <= NativeBattleEffectArena.Length - 24)
        {
            words = ImmutableArray.Create(0x5000000u, 0x2A000000 | rgb).AddRange(draw.NativeScreenVertices.Select(point => unchecked((ushort)point.X) | unchecked((uint)point.Y << 16)));
        }
        else
        {
            throw new NotSupportedException("Native fullscreen packet requires its original command, vertices and physical arena owner.");
        }
        return Packet(words, bucket);
    }

    private static NativeBattleModulePacketBatch Packet(ImmutableArray<uint> words, int bucket)
    {
        NativeBattleGeometryPrimitive primitive = new(bucket, words, [0, 0, 0, 0]) { PhysicalWriteMask = Enumerable.Repeat(NativeBattleGfPacketByteState.Known, words.Length * 4).ToImmutableArray() };
        return new([new(primitive, 0, 1)], words.Length * 4);
    }
}
