using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>5B5FB0's three projected corners, synthesized fourth screen pair and SZ3 ordering bucket.</summary>
public static class NativeBattleShot194Drawing
{
    public static NativeBattleGfGeometryBatch DrawQuad5B5FB0(NativeBattleVector position, short scale, uint color, NativeBattleActionMatrix view, NativeBattleCameraProjection projection)
    {
        var point = NativeBattleGfMatrices.RotateLong56C600(view, position.X, position.Y, position.Z);
        NativeBattleActionMatrix matrix = new([scale, 0, 0, 0, scale, 0, 0, 0, scale], unchecked(point.X + view.X), unchecked(point.Y + view.Y), unchecked(point.Z + view.Z), 4096);
        NativeBattleModuleProjectedVertex a = NativeBattleModuleProjection.Project45FE10(matrix, new(-256, -256, 0), projection);
        NativeBattleModuleProjectedVertex b = NativeBattleModuleProjection.Project45FE10(matrix, new(256, -256, 0), projection);
        NativeBattleModuleProjectedVertex c = NativeBattleModuleProjection.Project45FE10(matrix, new(-256, 256, 0), projection);
        byte[] bytes = new byte[40];
        NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[40];
        Word(0, 0x09000000);
        Word(4, color);
        Word(12, 0x3E144080);
        Word(20, 0x003840A0);
        Word(28, 0x00006080);
        Word(36, 0x000060A0);
        NativeBattleGeometryPrimitive? primitive = null;
        if (((a.Flags | b.Flags | c.Flags) & 0x60000) == 0)
        {
            Word(8, a.Screen);
            Word(16, b.Screen);
            Word(24, c.Screen);
            Word(32, (b.Screen & 0xFFFF) | (c.Screen & 0xFFFF0000));
            primitive = new(17 + (c.Depth >> 4), Enumerable.Range(0, 10).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(index * 4))).ToImmutableArray(), [a.Depth, b.Depth, c.Depth, c.Depth]);
        }
        return new([new(0, bytes.ToImmutableArray(), mask.ToImmutableArray(), primitive)], primitive is null ? 0 : 40);

        void Word(int offset, uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
            Array.Fill(mask, NativeBattleGfPacketByteState.Known, offset, 4);
        }
    }
}
