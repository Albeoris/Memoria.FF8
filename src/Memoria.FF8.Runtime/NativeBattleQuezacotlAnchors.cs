using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleQuezacotlResources
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<short>? AnchorVertexIndices11 { get; init; }
}

/// <summary>684BA0 selects one posed vertex from the original mesh directory and signed bone groups.</summary>
public static class NativeBattleGfMeshAnchors
{
    public static NativeBattleVector Resolve684BA0(ImmutableArray<byte> geometry, short meshIndex, short vertexIndex, Func<int, NativeBattleActionMatrix> readBoneMatrix)
    {
        int meshes = I32(0);
        if (meshIndex < 0 || meshIndex >= meshes || vertexIndex < 0)
        {
            throw new NotSupportedException("Native684BA0 requires its reached mesh and flattened vertex index; unrelated prefix bytes are not inferred.");
        }
        int cursor = I32(checked(4 + 4 * meshIndex));
        int groups = I16(cursor);
        cursor = checked(cursor + 2);
        int remaining = vertexIndex;
        for (int group = 0; group < groups; group++)
        {
            short bone = I16(cursor);
            int count = I16(checked(cursor + 2));
            cursor = checked(cursor + 4);
            if (count < 0)
            {
                throw new NotSupportedException("Native684BA0 negative group count requires its actual pointer traversal.");
            }
            if (remaining < count)
            {
                int address = checked(cursor + 6 * remaining);
                NativeBattleVector local = new(I16(address), I16(checked(address + 2)), I16(checked(address + 4)));
                return NativeBattleGfMatrices.Transform460840(readBoneMatrix(bone), local);
            }
            remaining -= count;
            cursor = checked(cursor + 6 * count);
        }
        throw new NotSupportedException("Native684BA0 selected vertex requires an unpopulated or out-of-range geometry group.");

        short I16(int offset)
        {
            Require(offset, 2);
            return BinaryPrimitives.ReadInt16LittleEndian(geometry.AsSpan().Slice(offset));
        }

        int I32(int offset)
        {
            Require(offset, 4);
            return BinaryPrimitives.ReadInt32LittleEndian(geometry.AsSpan().Slice(offset));
        }

        void Require(int offset, int length)
        {
            if (geometry.IsDefault || offset < 0 || offset > geometry.Length - length)
            {
                throw new InvalidDataException("Native684BA0 selected geometry bytes leave their actual source.");
            }
        }
    }
}

public sealed record NativeBattleQuezacotlAnchor(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleVector Position);

public static partial class NativeBattleQuezacotlRig86
{
    public static NativeBattleQuezacotlAnchor ReadMeshAnchor6C6440(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, short meshIndex, short vertexIndex)
    {
        NativeBattleQuezacotlRigWrite composed = Compose5095B0(memory, effect);
        NativeBattleEffectArena arena = composed.Memory.Arena!;
        int geometry = checked((int)(BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(Directory + 4, 4)) - ArenaAddress));
        int animations = checked((int)(BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(Directory + 8, 4)) - ArenaAddress));
        NativeBattleVector position = NativeBattleGfMeshAnchors.Resolve684BA0(arena.Read(geometry, animations - geometry).ToImmutableArray(), meshIndex, vertexIndex, selector => ReadBoneMatrix(composed.Memory, selector));
        //The balanced16-byte5082B0/5082D0 temporary has no escaping pointer; no physical scratch bytes are invented.
        NativeBattleQuezacotlRigWrite restored = Restore508C90(composed.Memory, composed.Effect);
        return new(restored.Memory, restored.Effect, position);
    }
}
