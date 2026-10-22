using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleBadBreath300StripMesh
{
    private static void AddBracket(NativeBattleGfGeometryAttempt stores, NativeBattleGeometryPrimitive polygon, NativeBattleTextureWindow before, NativeBattleTextureWindow after, ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts)
    {
        int first = stores.ByteOffsetRelative;
        int length = stores.PacketLength;
        attempts.Add(stores);
        attempts.Add(TextureWindow(first + length, before));
        SkinWrites mode = new(8, checked(first + length + 12));
        mode.Word(0, 0x01000000);
        mode.Word(4, 0xE1000220);
        attempts.Add(mode.Attempt(new(polygon.Bucket, mode.Words(), [0, 0, 0, 0])));
        SkinWrites linkOnly = new(length, first);
        attempts.Add(linkOnly.Attempt(polygon));
        attempts.Add(TextureWindow(first + length + 20, after));

        NativeBattleGfGeometryAttempt TextureWindow(int offset, NativeBattleTextureWindow window)
        {
            SkinWrites packet = new(12, offset);
            packet.Word(0, 0x02000000);
            packet.Word(4, NativeBattleEffectTextureWindows.Encode(window));
            packet.Word(8, 0);
            return packet.Attempt(new(polygon.Bucket, packet.Words(), [0, 0, 0, 0]));
        }
    }
}
