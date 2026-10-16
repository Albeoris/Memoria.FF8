using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleQuezacotlGeometryStep(NativeBattleEffectMemory Memory, ImmutableArray<NativeBattleGeometryPrimitive> Primitives);

/// <summary>Reached6C6060 generic first-window model draw, using current executable vertices and actual arena morph scratch.</summary>
public static class NativeBattleQuezacotlGeometry
{
    public static NativeBattleQuezacotlGeometryStep Emit6C6060(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleGfAnimationNode node, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        if (NativeBattleQuezacotlWorkspace.SkipsGeometry6C6060(node))
        {
            return new(memory, []);
        }
        NativeBattleGfAnimationSource source = NativeBattleQuezacotlWorkspace.Source(memory);
        int directory = checked(8 + 4 * node.GeometryIndex);
        int geometry = BinaryPrimitives.ReadInt32LittleEndian(Read(source.Data, directory, 4));
        int count = BinaryPrimitives.ReadInt32LittleEndian(Read(source.Data, checked(geometry + 4), 4));
        if (count < 0 || count > NativeBattleEffectArena.Length / 8)
        {
            throw new InvalidDataException("Native6C6060 geometry vertex count exceeds its observed source or arena.");
        }
        int length = checked(count * 8);
        int first = node.Word40;
        int second = node.Word42;
        int ratio = node.Scalar38;
        ImmutableArray<byte> vertices;
        if (first == second || ratio == 0 || ratio == 4096)
        {
            int shape = first == second || ratio == 0 ? first : second;
            vertices = Read(source.Data, checked(geometry + 12 + shape * length), length).ToArray().ToImmutableArray();
        }
        else
        {
            NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native6C6060 morph requires its actual effect arena scratch.");
            vertices = NativeBattleGfMorphGeometry.Morph701390(source.Data, geometry, first, second, ratio, arena.Read(0x8D0, length).ToImmutableArray());
            memory = memory with { Arena = arena.Write(0x8D0, vertices.AsSpan()) };
        }
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native6C6060 requires its prior published view matrix.");
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose6C6060(node, view, effect.Quezacotl?.Billboard17B0, new(0, -5000, 0));
        bool cue = node.Scalar36 != 0;
        NativeBattleGfGeometryContext context = new(cue ? 0x20F0u : 0x2030u, cue ? node.Color32 : 0, node.Scalar36, null, null, 0, 17, 2);
        return new(memory, NativeBattleGfGeometryPackets.Emit7043B0(source.Data, geometry, vertices, matrix, projection, context, colors));
    }

    private static ReadOnlySpan<byte> Read(ImmutableArray<byte> bytes, int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > bytes.Length - length)
        {
            throw new InvalidDataException("Native6C6060 geometry pointer leaves its current executable source owner.");
        }
        return bytes.AsSpan().Slice(offset, length);
    }
}
