using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect315BodyDrawing
{
    private static SkinWrites StartPacket(byte[] source, bool quad, NativeBattleEffectArena[] corners, NativeBattleEffectArena temporary156, int offset)
    {
        SkinWrites packet = new(quad ? 40 : 32, offset);
        uint uv1 = BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(12));
        uint color = U32(temporary156, 0x1C) | (quad ? 0x2C000000u : 0x24000000u) | (uv1 & 0x02000000u);
        uv1 &= 0x01FFFFFFu;
        if (corners.Any(corner => S16(corner, 2) == 0))
        {
            color |= 0x02000000;
            uv1 = unchecked(uv1 + 0x00200000);
        }
        packet.Word(0, quad ? 0x09000000u : 0x07000000u);
        packet.Word(4, color);
        packet.Word(12, BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(8)));
        packet.Word(20, uv1);
        return packet;
    }
}
