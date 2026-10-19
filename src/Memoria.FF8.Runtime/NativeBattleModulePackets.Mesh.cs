using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>572200 context operands: DWORD18 is aggregate UV offset, not7043B0's depth bias.</summary>
public sealed record NativeBattleModuleMeshContext(uint Flags, uint? FarColor, int? Fade, ushort? TexturePage, ushort? Clut, uint? UvOffset, int TableBaseBucket, byte DepthShift);

public static partial class NativeBattleModulePackets
{
    public static NativeBattleGfGeometryBatch EmitBatch572200(ImmutableArray<byte> source, int geometryOffset, ImmutableArray<byte> selectedVertices, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, NativeBattleModuleMeshContext context, NativeBattleSummonColors? colors = null, Func<int, NativeBattleVector>? readSelectedVertex = null, Action<ushort>? observeDepth = null, Action<int>? observeSourceCursor = null)
    {
        return NativeBattleGfGeometryPackets.EmitBatch(source, geometryOffset, selectedVertices, matrix, projection, Normalize572200(context), colors, readSelectedVertex, observeDepth, observeSourceCursor);
    }

    public static NativeBattleGfGeometryAttempt EmitFace572200(ReadOnlySpan<byte> face, IReadOnlyList<NativeBattleModuleProjectedVertex> vertices, int group, NativeBattleModuleMeshContext context, NativeBattleSummonColors? colors = null, int byteOffset = 0, Action<ushort>? observeDepth = null)
    {
        return NativeBattleGfGeometryPackets.EmitAttempt(face, vertices, group, Normalize572200(context), colors, byteOffset, observeDepth);
    }

    public static NativeBattleModulePacketBatch EmitMesh572200(NativeBattleModuleDraw draw, NativeBattleStaticMeshResource resource, NativeBattleCameraProjection projection, int tableBaseBucket, byte depthShift, NativeBattleSummonColors? colors = null)
    {
        if ((draw.MeshFlags & ~0x2FFu) != 0)
        {
            throw new NotSupportedException("Native572200 draw receipt requires its actual retained vertex/UV/TPAGE context; use the explicit mesh context.");
        }
        if (resource.Bytes.IsDefault || resource.Bytes.Length < 8)
        {
            throw new InvalidDataException("Native572200 requires its original mesh header.");
        }
        NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.ModuleMeshCamera(draw, projection.View ?? throw new NotSupportedException("Native572200 requires its previous published camera matrix."));
        return EmitMesh572200WithVertices(draw, resource, projection, matrix, resource.Bytes.AsSpan().Slice(8).ToArray().ToImmutableArray(), tableBaseBucket, depthShift, colors: colors);
    }

    /// <summary>572200's explicit contextDWORD4 vertex pointer and already-published GTE matrix.</summary>
    public static NativeBattleModulePacketBatch EmitMesh572200WithVertices(NativeBattleModuleDraw draw, NativeBattleStaticMeshResource resource, NativeBattleCameraProjection projection, NativeBattleActionMatrix matrix, ImmutableArray<byte> vertexBytes, int tableBaseBucket, byte depthShift, uint depthColor = 0, NativeBattleSummonColors? colors = null)
    {
        if ((draw.MeshFlags & ~0x22FFu) != 0 || vertexBytes.IsDefault)
        {
            throw new NotSupportedException("Native572200 draw receipt requires its actual retained UV/TPAGE context; use the explicit mesh context.");
        }
        NativeBattleModuleMeshContext context = new(draw.MeshFlags | 0x2000, depthColor, draw.MeshFade, null, draw.MeshClut, null, tableBaseBucket, depthShift);
        NativeBattleGfGeometryBatch batch = EmitBatch572200(resource.Bytes, 0, vertexBytes, matrix, projection, context, colors ?? NativeBattleGteColorInterpolation.CodeConstants45F270);
        ImmutableArray<NativeBattleAllocatedPrimitive> primitives = batch.Attempts.Where(attempt => attempt.Primitive is not null).Select(attempt => new NativeBattleAllocatedPrimitive(attempt.Primitive!, attempt.ByteOffsetRelative, 0)).ToImmutableArray();
        return new(primitives, batch.AllocationBytes) { CapturedWrites = batch };
    }

    public static NativeBattleGeometryPrimitive? EmitQuad(ReadOnlySpan<byte> face, IReadOnlyList<NativeBattleProjectedVertex> vertices, bool textured, uint flags, ushort clut, int fade, int tableBaseBucket, byte depthShift, uint depthColor = 0)
    {
        return EmitProjected572200(face, vertices, textured ? 3 : 5, flags, clut, fade, tableBaseBucket, depthShift, depthColor);
    }

    private static NativeBattleGeometryPrimitive? EmitProjected572200(ReadOnlySpan<byte> face, IReadOnlyList<NativeBattleProjectedVertex> vertices, int group, uint flags, ushort clut, int fade, int tableBaseBucket, byte depthShift, uint depthColor)
    {
        NativeBattleModuleProjectedVertex[] projected = vertices.Select(vertex => new NativeBattleModuleProjectedVertex(vertex.Screen, vertex.Depth, vertex.DepthAndClip & 0xFFFF0000u)).ToArray();
        return EmitFace572200(face, projected, group, new(flags, depthColor, fade, null, clut, null, tableBaseBucket, depthShift), NativeBattleGteColorInterpolation.CodeConstants45F270).Primitive;
    }

    private static NativeBattleMeshPacketContext Normalize572200(NativeBattleModuleMeshContext context)
    {
        if ((context.Flags & ~0x3FFFu) != 0)
        {
            throw new NotSupportedException("Native572200 requires an unsupported context flag owner.");
        }
        return new(context.Flags, context.FarColor, context.Fade, context.TexturePage, context.Clut, (context.Flags & 0x1000) == 0 ? 0 : context.UvOffset, 0, context.TableBaseBucket, context.DepthShift);
    }
}
