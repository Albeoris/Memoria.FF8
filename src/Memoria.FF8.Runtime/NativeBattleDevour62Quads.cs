using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleDevour62Quads
{
    public static NativeBattleGfGeometryBatch Emit(NativeBattleModulePoolTask task, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        if (task.Callback != 0x7E4F60 || task.PayloadLength != 72)
        {
            throw new InvalidDataException("Devour screen drawing requires its selected84-byte task.");
        }
        int x = unchecked((short)(task.ReadWord(0x1C) + 96));
        int intensity = task.ReadWord(0x50);
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>(4);
        Add(x, 16, 384, 256, 256, 40, 0, 216, 320, 241, 1);
        Add(unchecked((short)(x + 256)), 16, 512, 256, 192, 40, 0, 216, 320, 241, 1);
        Add(0, 0, 384, 256, 128, 216, 0, 0, 320, 240, 0);
        Add(128, 0, 448, 256, 192, 216, 0, 0, 320, 240, 0);
        return new(attempts.MoveToImmutable(), 160);

        void Add(int screenX, int screenY, int textureX, int textureY, int width, int height, int u, int v, int clutX, int clutY, int selector)
        {
            NativeBattleGfGeometryBatch quad = EmitQuad(screenX, screenY, textureX, textureY, width, height, u, v, clutX, clutY, intensity, selector, attempts.Count * 40, pilotIdentity: pilotIdentity is { } identity ? identity with { Primitive = unchecked(identity.Primitive * 397 + attempts.Count) } : null);
            attempts.Add(quad.Attempts[0]);
        }
    }

    public static NativeBattleGfGeometryBatch EmitQuad(int screenX, int screenY, int textureX, int textureY, int width, int height, int u, int v, int clutX, int clutY, int intensity, int semitransparentSelector, int byteOffset = 0, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        SkinWrites packet = new(40, byteOffset);
        packet.Word(0, 0x09000000);
        packet.Byte(7, unchecked((ushort)semitransparentSelector) == 1 ? (byte)0x2E : (byte)0x2C);
        ushort page = unchecked((ushort)((unchecked((short)((textureX & 0x3C0) | 0x2800)) >> 6) | ((unchecked((short)textureY) >> 4) & 0x10) | ((textureY & 0x200) << 2)));
        ushort clut = unchecked((ushort)(((clutX >> 4) & 0x3F) | (clutY << 6)));
        packet.Word16(22, page);
        packet.Word16(14, clut);
        byte right = unchecked((short)width) + unchecked((short)u) >= 256 ? (byte)255 : unchecked((byte)(width + u));
        byte bottom = unchecked((short)height) + unchecked((short)v) >= 256 ? (byte)255 : unchecked((byte)(height + v));
        packet.Byte(12, unchecked((byte)u));
        packet.Byte(28, unchecked((byte)u));
        packet.Byte(20, right);
        packet.Byte(36, right);
        packet.Byte(13, unchecked((byte)v));
        packet.Byte(21, unchecked((byte)v));
        packet.Byte(29, bottom);
        packet.Byte(37, bottom);
        packet.Word16(8, unchecked((ushort)screenX));
        packet.Word16(24, unchecked((ushort)screenX));
        packet.Word16(16, unchecked((ushort)(screenX + width)));
        packet.Word16(32, unchecked((ushort)(screenX + width)));
        packet.Word16(10, unchecked((ushort)screenY));
        packet.Word16(18, unchecked((ushort)screenY));
        packet.Word16(26, unchecked((ushort)(screenY + height)));
        packet.Word16(34, unchecked((ushort)(screenY + height)));
        packet.Byte(4, unchecked((byte)intensity));
        packet.Byte(5, unchecked((byte)intensity));
        packet.Byte(6, unchecked((byte)intensity));
        NativeBattleGeometryPrimitive primitive = new(5, packet.Words(), [0, 0, 0, 0]);
        if (pilotIdentity is { } identity)
        {
            System.Numerics.Vector3 Point(int word) => new(unchecked((short)primitive.Words[word]), unchecked((short)(primitive.Words[word] >> 16)), 0);
            primitive = primitive with { PilotGeometry = new(identity, Point(2), Point(4), Point(6), Point(8), 4) { ScreenSpace = true, ScreenScale = 8, SourceDepths = System.Numerics.Vector4.Zero, UseSourceDepths = true } };
        }
        return new([packet.Attempt(primitive)], 40);
    }
}
