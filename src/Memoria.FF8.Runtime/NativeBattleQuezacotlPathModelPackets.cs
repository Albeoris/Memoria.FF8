using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleQuezacotlPathModelBatch(ImmutableArray<NativeBattleGfGeometryAttempt> VertexScratchWrites, ImmutableArray<NativeBattleGeometryPrimitive> Primitives);

/// <summary>6A79B0's ordinary5099D0 bone groups and all four raw polygon groups, sharing50F900/50FDF0.</summary>
public static class NativeBattleQuezacotlPathModelPackets
{
    public static NativeBattleQuezacotlPathModelBatch Emit6A79B0(ImmutableArray<byte> geometry, Func<short, NativeBattleActionMatrix> readComposedBone, NativeBattleCameraProjection projection, uint modelRgb, uint objectMask)
    {
        int meshes = I32(0);
        if (meshes < 0 || meshes > (geometry.Length - 4) / 4)
        {
            throw new InvalidDataException("Native6A79B0 geometry directory exceeds its current resource.");
        }
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder writes = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        ImmutableArray<NativeBattleGeometryPrimitive>.Builder primitives = ImmutableArray.CreateBuilder<NativeBattleGeometryPrimitive>();
        for (int mesh = 0; mesh < meshes; mesh++)
        {
            //5099D0 advances the directory for masked objects without reading their bodies.
            int cursor = I32(checked(4 + mesh * 4));
            if ((objectMask & (1u << mesh)) == 0)
            {
                continue;
            }
            int groups = I16(cursor);
            cursor = checked(cursor + 2);
            List<NativeBattleProjectedVertex> projected = [];
            for (int group = 0; group < groups; group++)
            {
                short selector = I16(cursor);
                int count = I16(checked(cursor + 2));
                cursor = checked(cursor + 4);
                if (count < 0)
                {
                    throw new NotSupportedException("Native6A79B0 negative50F900 vertex count reaches an unsupported original loop.");
                }
                NativeBattleActionMatrix bone = readComposedBone(selector);
                for (int vertex = 0; vertex < count; vertex++)
                {
                    NativeBattleVector point = new(I16(cursor), I16(checked(cursor + 2)), I16(checked(cursor + 4)));
                    cursor = checked(cursor + 6);
                    NativeBattleProjectedVertex result = NativeBattleGeometryProjection.Project50F900(bone, point, projection, new(0, 0, 320, 216));
                    SkinWrites scratch = new(8, checked(projected.Count * 8));
                    scratch.Word(0, result.Screen);
                    scratch.Word(4, result.DepthAndClip);
                    writes.Add(scratch.Attempt(null));
                    projected.Add(result);
                }
            }
            cursor = checked((cursor + 3) & ~3);
            ushort[] counts = Enumerable.Range(0, 4).Select(group => U16(checked(cursor + group * 2))).ToArray();
            cursor = checked(cursor + 12);
            ImmutableArray<NativeBattleRawPolygon>.Builder polygons = ImmutableArray.CreateBuilder<NativeBattleRawPolygon>();
            for (int group = 0; group < 4; group++)
            {
                bool quad = (group & 1) != 0;
                int length = (quad ? 20 : 16) + (group >= 2 ? 4 : 0);
                for (int polygon = 0; polygon < counts[group]; polygon++)
                {
                    polygons.Add(new(quad, Read(cursor, length).ToArray().ToImmutableArray()));
                    cursor = checked(cursor + length);
                }
            }
            //6A79B0 explicitly writes cueWORD0. No507550 relocation applies to this resource.
            primitives.AddRange(NativeBattleRawGeometryPackets.EmitProjected5099D0(polygons.ToImmutable(), projected, modelRgb, 17, 4, 0));
        }
        return new(writes.ToImmutable(), primitives.ToImmutable());

        short I16(int offset) => BinaryPrimitives.ReadInt16LittleEndian(Read(offset, 2));
        ushort U16(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(Read(offset, 2));
        int I32(int offset) => BinaryPrimitives.ReadInt32LittleEndian(Read(offset, 4));

        ReadOnlySpan<byte> Read(int offset, int length)
        {
            if (geometry.IsDefault || offset < 0 || length < 0 || offset > geometry.Length - length)
            {
                throw new InvalidDataException("Native6A79B0 read exceeds its current loaded geometry bytes.");
            }
            return geometry.AsSpan().Slice(offset, length);
        }
    }
}

public static partial class NativeBattleQuezacotlRig86
{
    public static NativeBattleQuezacotlRigWrite ComposePathModel5095B0(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleActionMatrix priorView)
    {
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native6A79B0 requires its current rig arena.");
        int skeleton = checked((int)(BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(Directory, 4)) - ArenaAddress));
        int count = arena.Read(skeleton, 1)[0];
        NativeBattleActionMatrix modelView = NativeBattlePoseMatrices.ComposeStageCamera([ReadMatrix(arena, 0x68)], priorView, 0)[0];
        for (int index = 0; index < count; index++)
        {
            NativeBattleActionMatrix bone = ReadBoneMatrix(memory with { Arena = arena }, index);
            arena = WriteMatrix(arena, skeleton + 32 + index * 48, NativeBattlePoseMatrices.ComposeStageCamera([bone], modelView, 0)[0], true);
        }
        return new(memory with { Arena = arena }, effect);
    }
}
