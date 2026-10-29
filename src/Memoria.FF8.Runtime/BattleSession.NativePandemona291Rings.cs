using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattlePandemona291Frame Frame, NativeBattleGraphicsState Graphics) DrawNativePandemona291Rings(NativeBattlePandemona291Frame frame, int elapsed, NativeBattleGraphicsState graphics)
    {
        NativeBattlePandemona291Resources resources = frame.Effect.Binding.Pandemona291Resources!;
        NativeBattleStaticMeshResource geometry = resources.EndingRingGeometry ?? throw new NotSupportedException("Native Pandemona requires its actual ending ring geometry.");
        if (resources.EndingRingParameters.IsDefault || resources.EndingRingParameters.Length != 48)
        {
            throw new NotSupportedException("Native Pandemona requires its four original ring parameter rows.");
        }
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Pandemona rings require their current projection.");
        int ground = frame.Memory.Pandemona291!.Ground25562E0 ?? throw new NotSupportedException("Native Pandemona rings require their current ground coordinate.");
        int height = -5576;
        for (int row = 3; row >= 0; row--, height += 2200)
        {
            int start = row * 12;
            int begin = Word(start + 8);
            int end = Word(start + 10);
            if (elapsed <= begin || elapsed >= end + 8)
            {
                continue;
            }
            int? fade = elapsed < begin + 8 ? (begin + 8 - elapsed) << 9 : elapsed > end ? (elapsed - end) << 9 : null;
            short horizontal = unchecked((short)(unchecked((short)(elapsed - begin)) * Word(start + 2) + Word(start)));
            NativeBattleActionMatrix local = NativeBattlePandemona291Layers.Build6F29D0(resources.TrailTrigonometry, elapsed << 6, horizontal, Word(start + 4)) with { X = 0, Y = height, Z = ground };
            NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(projection.View ?? throw new NotSupportedException("Native Pandemona requires its actual current view."), local);
            NativeBattleEsunaMeshContext context = new(fade is null ? 0x30u : 0xF0u, 0, fade, 0, 0, 0, new(0, 0, 256, 256), new(0, 0, 256, 256), 17, 2);
            var drawn = DrawNativePandemona291PreparedMesh(frame, geometry, matrix, context, null, graphics, null, pilotIdentity: NativePandemona291VisualIdentity(frame, 220 + row));
            frame = drawn.Frame;
            graphics = drawn.Graphics;
        }
        return (frame, graphics);

        short Word(int offset) => BinaryPrimitives.ReadInt16LittleEndian(resources.EndingRingParameters.AsSpan().Slice(offset, 2));
    }
}
