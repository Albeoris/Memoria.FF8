using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleProtectFlash
{
    public static void Emit(uint color, Action<NativeBattleProtectPackets> emit, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        emit(Packet([0x05000000, 0x2A000000 | (color & 0xFFFFFF), 0, 320, 216u << 16, (216u << 16) | 320], 1, pilotIdentity));
        emit(Packet([0x02000000, 0xE100002A, 0], 0));
    }

    private static NativeBattleProtectPackets Packet(ImmutableArray<uint> words, ushort flags, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        byte[] bytes = new byte[words.Length * 4];
        for (int index = 0; index < words.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 4), words[index]);
        }
        NativeBattleGeometryPrimitive primitive = new(7, words, [0, 0, 0, 0]);
        if (pilotIdentity is { } identity)
        {
            primitive = primitive with { PilotGeometry = new(identity, new(0, 0, 0), new(320, 0, 0), new(0, 216, 0), new(320, 216, 0), 4, false) { ScreenSpace = true, ScreenScale = 8 } };
        }
        NativeBattleGfGeometryAttempt attempt = new(0, bytes.ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, bytes.Length).ToImmutableArray(), primitive);
        return new(new([attempt], bytes.Length), flags);
    }
}
