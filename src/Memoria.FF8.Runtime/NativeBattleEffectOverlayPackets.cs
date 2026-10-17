namespace Memoria.FF8.Runtime;

/// <summary>Raw8D6790 FT4 stores over its actual80-byte task, prior projection and current caller-selected packet cursor.</summary>
public static class NativeBattleEffectOverlayPackets
{
    public static NativeBattleGfGeometryBatch Emit8D6790(NativeBattleModulePoolTask task, NativeBattleCameraProjection projection, Action<ushort>? observeDepth = null)
    {
        if (task.Callback != 0x8D6790 || task.Payload.IsDefault || task.Payload.Length != 68)
        {
            throw new InvalidDataException("Native8D6790 requires its original80-byte task and callback.");
        }
        NativeBattleActionMatrix camera = projection.View ?? throw new NotSupportedException("Native8D6790 requires its prior published camera matrix.");
        NativeBattleVector anchor = new(Word(0x1C), Word(0x1E), Word(0x20));
        NativeBattleModuleProjectedVertex projected = NativeBattleModuleProjection.Project45FE10(camera, anchor, projection);
        observeDepth?.Invoke(projected.Depth);
        int x = unchecked((short)projected.Screen) >> 3;
        int y = unchecked((short)(projected.Screen >> 16)) >> 3;
        int halfWidth = Word(0x4C) / 2;
        int height = Word(0x4E);
        int textureY = Word(0x42);
        ushort page = unchecked((ushort)((((task.ReadWord(0x40) & 0x3C0) | 0x2800) >> 6) | (textureY >> 4 & 0x10) | ((textureY & 0x200) << 2)));
        ushort clut = unchecked((ushort)((Word(0x44) >> 4 & 0x3F) | unchecked((ushort)(task.ReadWord(0x46) << 6))));
        byte u = task.ReadByte(0x48);
        byte v = task.ReadByte(0x4A);
        byte right = unchecked((byte)(u + task.ReadByte(0x4C)));
        byte bottom = unchecked((byte)(v + task.ReadByte(0x4E)));
        SkinWrites packet = new(40, 0);
        packet.Word(0, 0x09000000);
        packet.Byte(7, 0x2F);
        packet.Word16(8, unchecked((ushort)(x - halfWidth)));
        packet.Word16(10, unchecked((ushort)(y - height)));
        packet.Byte(12, u);
        packet.Byte(13, v);
        packet.Word16(14, clut);
        packet.Word16(16, unchecked((ushort)(x + halfWidth)));
        packet.Word16(18, unchecked((ushort)(y - height)));
        packet.Byte(20, right);
        packet.Byte(21, v);
        packet.Word16(22, page);
        packet.Word16(24, unchecked((ushort)(x - halfWidth)));
        packet.Word16(26, unchecked((ushort)y));
        packet.Byte(28, u);
        packet.Byte(29, bottom);
        packet.Word16(32, unchecked((ushort)(x + halfWidth)));
        packet.Word16(34, unchecked((ushort)y));
        packet.Byte(36, right);
        packet.Byte(37, bottom);
        //45C8E0 inserts at the table head, with flags20=1 and four zero precision DWORDs.
        //RGB4..6 and padding30..31/38..39 are not written. Command2F and the constructed ABR1 page do not select those RGB bytes.
        NativeBattleGeometryPrimitive primitive = new(0, packet.Words(), [0, 0, 0, 0]);
        return new([packet.Attempt(primitive)], 40);

        short Word(int offset) => unchecked((short)task.ReadWord(offset));
    }
}
