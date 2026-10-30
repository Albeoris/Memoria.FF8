using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleFullCure280Draw(NativeBattleEffectMemory Memory, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleFullCure280Part
{
    public static NativeBattleFullCure280Draw Draw(NativeBattleEffectMemory memory, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleActionMatrix view, NativeBattleActionMatrix billboard, NativeBattleVector anchor, int buffer, NativeBattleCameraProjection projection, NativeBattleSummonColors? colors, NativeBattlePilotVisualIdentity? pilotIdentity = null, Action<NativeBattleEffectMemory>? publishMorph = null)
    {
        int geometry = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(8 + node.GeometryIndex * 4));
        int count = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(geometry + 4));
        ImmutableArray<byte> vertices;
        if (node.Word40 == node.Word42 || node.Scalar38 is 0 or 4096)
        {
            int shape = node.Word40 == node.Word42 || node.Scalar38 == 0 ? node.Word40 : node.Word42;
            vertices = source.Data.Slice(checked(geometry + 12 + shape * count * 8), checked(count * 8));
        }
        else
        {
            NativeBattleEffectArena arena = memory.Arena!;
            vertices = NativeBattleGfMorphGeometry.Morph701390(source.Data, geometry, node.Word40, node.Word42, node.Scalar38, arena.Read(buffer + 7152, checked(count * 8)).ToImmutableArray());
            memory = memory with { Arena = arena.Write(buffer + 7152, vertices.AsSpan()) };
            publishMorph?.Invoke(memory);
            vertices = vertices.Slice(0, checked(count * 8));
        }
        NativeBattleActionMatrix matrix = NativeBattleFullCure280Geometry.Compose(node, view, billboard, anchor);
        uint flags = node.Scalar36 == 0 ? 0x2030u : 0x20F0u;
        NativeBattleGfGeometryBatch batch = NativeBattleGfGeometryPackets.EmitBatch7043B0(source.Data, geometry, vertices, matrix, projection, new(flags, node.Color32, node.Scalar36, null, null, -128, 17, 2), colors, pilotIdentity: pilotIdentity);
        return new(memory, NativeBattleSlot280288Presentation.Depths(280, batch));
    }
}
