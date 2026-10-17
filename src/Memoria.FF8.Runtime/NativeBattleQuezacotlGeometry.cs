using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleQuezacotlGeometryStep(NativeBattleEffectMemory Memory, NativeBattleGfGeometryBatch Batch)
{
    public bool CapturedPackets { get; init; }
}

/// <summary>Reached6C6060 generic first-window model draw, using current executable vertices and actual arena morph scratch.</summary>
public static class NativeBattleQuezacotlGeometry
{
    public static NativeBattleQuezacotlGeometryStep Emit6C6060(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleGfAnimationNode node, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        return Emit(memory, effect, node, projection, colors, NativeBattleQuezacotlExecutableMemory.Address, null, false, 0);
    }

    public static NativeBattleQuezacotlGeometryStep Emit6C53A0(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleGfAnimationNode node, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, NativeBattleActionMatrix contextMatrix, bool capturePrimary, byte control)
    {
        return Emit(memory, effect, node, projection, colors, 0x12A9664, contextMatrix, capturePrimary, control);
    }

    private static NativeBattleQuezacotlGeometryStep Emit(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleGfAnimationNode node, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, uint sourceAddress, NativeBattleActionMatrix? contextMatrix, bool capturePrimary, byte control)
    {
        if (NativeBattleQuezacotlWorkspace.SkipsGeometry6C6060(node))
        {
            return new(memory, new([], 0));
        }
        ImmutableArray<byte> source = (memory.QuezacotlExecutableMemory ?? throw new NotSupportedException("Native Quezacotl draw requires its current executable bytes.")).Data;
        int sourceOffset = checked((int)(sourceAddress - NativeBattleQuezacotlExecutableMemory.Address));
        int directory = checked(sourceOffset + 8 + 4 * node.GeometryIndex);
        int geometry = checked(sourceOffset + BinaryPrimitives.ReadInt32LittleEndian(Read(source, directory, 4)));
        int count = BinaryPrimitives.ReadInt32LittleEndian(Read(source, checked(geometry + 4), 4));
        if (count < 0 || count > NativeBattleEffectArena.Length / 8)
        {
            throw new InvalidDataException("Native6C6060 geometry vertex count exceeds its observed source or arena.");
        }
        int length = checked(count * 8);
        int first = node.Word40;
        int second = node.Word42;
        int ratio = node.Scalar38;
        ImmutableArray<byte> vertices;
        Func<int, NativeBattleVector>? readMorphedVertex = null;
        if (first == second || ratio == 0 || ratio == 4096)
        {
            int shape = first == second || ratio == 0 ? first : second;
            vertices = Read(source, checked(geometry + 12 + shape * length), length).ToArray().ToImmutableArray();
        }
        else
        {
            NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native6C6060 morph requires its actual effect arena scratch.");
            ImmutableArray<NativeBattleVector> positions = NativeBattleGfMorphGeometry.MorphVertices701390(source, geometry, first, second, ratio);
            for (int index = 0; index < positions.Length; index++)
            {
                NativeBattleVector position = positions[index];
                byte[] raw = new byte[6];
                BinaryPrimitives.WriteInt16LittleEndian(raw, position.X);
                BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(2), position.Y);
                BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(4), position.Z);
                arena = arena.Write(checked(0x8D0 + index * 8), raw);
            }
            vertices = default;
            NativeBattleEffectArena actualScratch = arena;
            readMorphedVertex = offset =>
            {
                byte[] raw = actualScratch.Read(checked(0x8D0 + offset), 6);
                return new(BinaryPrimitives.ReadInt16LittleEndian(raw), BinaryPrimitives.ReadInt16LittleEndian(raw.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(raw.AsSpan(4)));
            };
            memory = memory with { Arena = arena };
        }
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native6C6060 requires its prior published view matrix.");
        NativeBattleActionMatrix matrix = contextMatrix is null ? NativeBattleGfMatrices.Compose6C6060(node, view, effect.Quezacotl?.Billboard17B0, new(0, -5000, 0)) : NativeBattleGfMatrices.Compose6C53A0(node, contextMatrix, effect.Quezacotl?.Billboard17B0);
        if (capturePrimary && node.GeometryIndex == 0)
        {
            memory = memory with { QuezacotlCapturedMatrix1780 = matrix };
            NativeBattleQuezacotlGeometryContext capturedContext = new(node.Color32, node.Scalar36, (ushort)((control & 7) << 4), new(0, 0, 256, 256), new(128, 128, 256, 128), 17, 2);
            return new(memory, NativeBattleQuezacotlGeometryPackets.EmitBatch6C5710(source, geometry, vertices, matrix, projection, capturedContext, colors, readMorphedVertex)) { CapturedPackets = true };
        }
        bool cue = node.Scalar36 != 0;
        NativeBattleGfGeometryContext context = new(cue ? 0x20F0u : 0x2030u, cue ? node.Color32 : 0, node.Scalar36, null, null, 0, 17, 2);
        return new(memory, NativeBattleGfGeometryPackets.EmitBatch7043B0(source, geometry, vertices, matrix, projection, context, colors, readMorphedVertex));
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

public sealed partial record NativeBattleEffectMemory
{
    //6C566D copies the composed matrix; selecting a new effect does not initialize this retained global.
    public NativeBattleActionMatrix? QuezacotlCapturedMatrix1780 { get; init; }
}
