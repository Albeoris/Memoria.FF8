using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private void DrawNativeEffect311Hierarchy(ref NativeBattleGraphicsState graphics, ref NativeBattleOriginalEffectState effect, NativeBattleEffect311Resources resources, NativeBattleGfAnimationNode node, NativeBattleVector center)
    {
        NativeBattleVector scale = node.Vector24;
        if (scale is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0)
        {
            return;
        }
        ImmutableArray<byte> source = resources.Hierarchy.Data;
        int geometry = BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan().Slice(8 + node.GeometryIndex * 4));
        int count = BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan().Slice(geometry + 4));
        if (count < 0 || count > 4096)
        {
            throw new InvalidDataException("Native665970 requires its bounded original vertex bank.");
        }
        int first = node.Word40;
        int second = node.Word42;
        int ratio = node.Scalar38;
        ImmutableArray<byte> vertices;
        if (first == second || ratio is 0 or 4096)
        {
            int shape = first == second || ratio == 0 ? first : second;
            vertices = source.AsSpan().Slice(geometry + 12 + shape * count * 8, count * 8).ToArray().ToImmutableArray();
        }
        else
        {
            NativeBattleEffectArena arena = NativeEffectMemory!.Arena!;
            var morphed = NativeBattleGfMorphGeometry.MorphVertices701390(source, geometry, first, second, ratio);
            for (int index = 0; index < morphed.Length; index++)
            {
                byte[] bytes = new byte[6];
                var vertex = morphed[index];
                BinaryPrimitives.WriteInt16LittleEndian(bytes, vertex.X);
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2), vertex.Y);
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), vertex.Z);
                arena = arena.Write(0xA48 + index * 8, bytes);
            }
            NativeEffectMemory = NativeEffectMemory with { Arena = arena };
            vertices = arena.Read(0xA48, count * 8).ToImmutableArray();
        }
        NativeBattleActionMatrix matrix = (node.Header & 0x400) != 0 ? NativeBattleGfMatrices.Euler701430(node.Vector16) : (node.Header & 0x40000) != 0 ? NativeBattleGfMatrices.Euler7015B0(node.Vector16) : NativeBattleGfMatrices.Euler701310(node.Vector16);
        NativeBattleCameraProjection projection = NativeCamera!.Projection!;
        NativeBattleActionMatrix view = projection.View!;
        int x;
        int y;
        int z;
        if ((node.Header & 0x200) != 0)
        {
            (x, y, z) = NativeBattleGfMatrices.Rotate56C3D0(view, center);
            x = unchecked(x + node.Vector8.X);
            y = unchecked(y + node.Vector8.Y);
            z = unchecked(z + node.Vector8.Z);
        }
        else
        {
            NativeBattleVector point = new(unchecked((short)(node.Vector8.X + center.X)), unchecked((short)(node.Vector8.Y + center.Y)), unchecked((short)(node.Vector8.Z + center.Z)));
            (x, y, z) = NativeBattleGfMatrices.Rotate56C3D0(view, point);
            matrix = NativeBattleGfMatrices.Multiply56C090(view, matrix);
        }
        matrix = matrix with { X = unchecked(x + view.X), Y = unchecked(y + view.Y), Z = unchecked(z + view.Z) };
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            matrix = matrix with { Rotation = matrix.Rotation.Select((value, index) => unchecked((short)(value * (index % 3 == 0 ? scale.X : index % 3 == 1 ? scale.Y : scale.Z) >> 12))).ToImmutableArray() };
        }
        bool cue = node.Scalar36 != 0;
        var batch = NativeBattleGfGeometryPackets.EmitBatch7043B0(source, geometry, vertices, matrix, projection, new(cue ? 0x20F0u : 0x2030u, cue ? node.Color32 : 0, node.Scalar36, null, null, -128, 17, 2), definition.NativeGraphics!.Rules.SummonColors);
        NativeBattlePrimitiveFrame frame = graphics.PrimitiveFrame!;
        NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(NativeEffectMemory!, effect.DrawArenaOffset!.Value, frame.Pending, batch);
        NativeEffectMemory = written.Memory;
        effect = effect with { DrawArenaOffset = written.Cursor };
        graphics = graphics with { PrimitiveFrame = frame with { Pending = written.Ordering } };
    }
}
