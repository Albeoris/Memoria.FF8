using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleQuezacotlScreenPacket(NativeBattleGfGeometryBatch Batch, ushort Flags20);

/// <summary>6C6530/6C65A0 write a320x216 F4 and, for the latter, the two original draw-mode packets.</summary>
public static class NativeBattleQuezacotlFlash
{
    public static ImmutableArray<NativeBattleQuezacotlScreenPacket> Emit(ushort phase, NativeBattleQuezacotlResources resources)
    {
        int colorIndex = phase switch
        {
            73 or 79 or 248 or 250 or 253 => 0,
            78 or 80 or 227 => 1,
            81 or 228 => 2,
            229 => 3,
            _ => -1
        };
        if (colorIndex < 0)
        {
            return [];
        }
        uint color;
        if (colorIndex == 3)
        {
            color = resources.Frame229FlashColor ?? throw new NotSupportedException("Native6C6530 phase229 requires its authenticated12AE7EC color DWORD; explicitly migrate this snapshot's resource binding.");
        }
        else
        {
            ImmutableArray<uint> colors = resources.FlashColors ?? throw new NotSupportedException("Native6C6530 requires authenticated DWORD colors12AE7E0/E4/E8; explicitly migrate this snapshot's resource binding.");
            if (colors.IsDefault || colors.Length != 3)
            {
                throw new InvalidDataException("Native Quezacotl flash requires its three observed color DWORDs.");
            }
            color = colors[colorIndex];
        }
        NativeBattleQuezacotlScreenPacket flash = Packet([0x05000000, 0x2A000000 | (color & 0xFFFFFF), 0, 320, 216u << 16, (216u << 16) | 320], 1);
        return phase == 78 ? [Packet([0x01000000, 0xE10002A0], 0), flash, Packet([0x01000000, 0xE10002C0], 0)] : [flash];
    }

    private static NativeBattleQuezacotlScreenPacket Packet(ImmutableArray<uint> words, ushort flags20)
    {
        byte[] bytes = new byte[words.Length * 4];
        for (int index = 0; index < words.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 4), words[index]);
        }
        NativeBattleGeometryPrimitive primitive = new(8, words, [0, 0, 0, 0]);
        NativeBattleGfGeometryAttempt attempt = new(0, bytes.ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, bytes.Length).ToImmutableArray(), primitive);
        return new(new([attempt], bytes.Length), flags20);
    }
}
