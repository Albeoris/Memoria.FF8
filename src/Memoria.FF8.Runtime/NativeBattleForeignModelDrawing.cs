using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Shared native actor consumers operating on posed host triangles and their actual material owner.</summary>
public static partial class NativeBattleForeignModelDrawing
{
    internal static void RequireIndexedTopology(NativeBattleForeignModelGeometry geometry)
    {
        if (geometry.Vertices.IsDefaultOrEmpty)
        {
            throw new NotSupportedException("Host body operations require their original indexed vertex owner.");
        }
        foreach (NativeBattleForeignModelFace face in geometry.Faces)
        {
            if (face.VertexIndices.IsDefault || face.VertexIndices.Length != 3 || face.VertexIndices.Any(index => (uint)index >= (uint)geometry.Vertices.Length))
            {
                throw new InvalidDataException("Host body operation face requires its actual indexed vertices.");
            }
        }
    }

    internal static void RequireCoefficientCache(NativeBattleForeignModelGeometry geometry, uint cursor, uint address, int length)
    {
        RequireIndexedTopology(geometry);
        if (cursor < address || length < 0 || (ulong)cursor - address > (ulong)length || (ulong)geometry.Vertices.Length > (ulong)length - ((ulong)cursor - address))
        {
            throw new InvalidDataException("Host body operation leaves its bounded coefficient cache.");
        }
    }

    public static NativeBattleBreak117BodyResult Break(NativeBattleForeignModelGeometry geometry, NativeBattleCameraProjection projection, uint color, ImmutableArray<byte> dither, int level, NativeBattlePilotVisualIdentity identity)
    {
        if (dither.IsDefault || dither.Length != 512 || (uint)level > 32768)
        {
            throw new InvalidDataException("Host model petrification requires its complete dither owner and native stone level.");
        }
        NativeBattleActionMatrix camera = NativeBattleGfMatrices.Compose56C2F0(projection.View ?? throw new NotSupportedException("Host model petrification requires its current camera."), geometry.World);
        ImmutableArray<NativeBattleGeometryPrimitive>.Builder primitives = ImmutableArray.CreateBuilder<NativeBattleGeometryPrimitive>();
        byte ditherIndex = 0;
        ImmutableArray<byte> projected = [];
        for (int index = 0; index < geometry.Faces.Length; index++)
        {
            NativeBattleForeignModelFace face = geometry.Faces[index];
            NativeBattlePilotVisualIdentity selected = identity with { Primitive = unchecked(identity.Primitive * 397 + index) };
            bool stone = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(dither.AsSpan().Slice(ditherIndex * 2)) <= level;
            NativeBattleBreak117BodyResult drawn = NativeBattleBreak117Body.Draw([face.Triangle()], [camera], projection, color, 1, dither, level, null, ditherIndex, selected);
            ditherIndex = drawn.DitherIndex;
            projected = drawn.Projected;
            foreach (NativeBattleGeometryPrimitive primitive in drawn.Primitives)
            {
                primitives.Add(stone ? primitive : geometry.BindSurface(primitive, face, selected));
            }
        }
        return new(primitives.ToImmutable(), ditherIndex, projected);
    }

    public static NativeBattleRawModelBodyDraw DrawBody(NativeBattleForeignModelGeometry geometry, NativeBattleActionMatrix basis, NativeBattleCameraProjection projection, uint color, NativeBattleSummonColors colors, NativeBattlePilotVisualIdentity identity, byte depthShift = 4, NativeBattleGeometryViewport? viewport = null, int tableBaseBucket = 17, short? depthCue = 0, bool pilotScreenSpace = false, bool pilotContinuous = true)
    {
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        ImmutableArray<byte> scratch = [];
        int allocated = 0;
        int bytesWritten = 0;
        for (int index = 0; index < geometry.Faces.Length; index++)
        {
            NativeBattleForeignModelFace face = geometry.Faces[index];
            NativeBattleRawMesh triangle = face.Triangle();
            NativeBattlePilotVisualIdentity selected = identity with { Primitive = unchecked(identity.Primitive * 397 + index) };
            NativeBattleRawModelBodyDraw draw = NativeBattleRawModelBodyDrawing.Draw5099D0(triangle, [basis], projection, color, 1, new byte[checked(triangle.Vertices.Length * 8)].ToImmutableArray(), colors, depthShift, viewport, tableBaseBucket, depthCue, selected, pilotScreenSpace, pilotContinuous);
            foreach (NativeBattleGfGeometryAttempt attempt in draw.Packets.Attempts)
            {
                attempts.Add(attempt with { ByteOffsetRelative = checked(allocated + attempt.ByteOffsetRelative), Primitive = attempt.Primitive is { } primitive ? geometry.BindSurface(primitive, face, selected) : null });
            }
            allocated = checked(allocated + draw.Packets.AllocationBytes);
            scratch = draw.ProjectedScratch;
            bytesWritten = Math.Max(bytesWritten, draw.ProjectedBytesWritten);
        }
        return new(scratch, bytesWritten, new(attempts.ToImmutable(), allocated));
    }

    public static ImmutableArray<NativeBattleGeometryPrimitive> Draw(NativeBattleForeignModelGeometry geometry, NativeBattleActionMatrix basis, NativeBattleCameraProjection projection, uint color, NativeBattleSummonColors colors, NativeBattlePilotVisualIdentity identity)
    {
        ImmutableArray<NativeBattleGeometryPrimitive>.Builder primitives = ImmutableArray.CreateBuilder<NativeBattleGeometryPrimitive>();
        for (int index = 0; index < geometry.Faces.Length; index++)
        {
            NativeBattleForeignModelFace face = geometry.Faces[index];
            NativeBattlePilotVisualIdentity selected = identity with { Primitive = unchecked(identity.Primitive * 397 + index) };
            foreach (NativeBattleGeometryPrimitive primitive in NativeBattleRawGeometryPackets.Emit5099D0([face.Triangle()], [basis], projection, color, 1, 0, 17, 4, null, colors, pilotIdentity: selected))
            {
                primitives.Add(geometry.BindSurface(primitive, face, selected));
            }
        }
        return primitives.ToImmutable();
    }

    public static (short Maximum, short Minimum) VerticalBounds(NativeBattleForeignModelGeometry geometry, Func<NativeBattleVector, short> transform)
    {
        if (geometry.Faces.IsDefaultOrEmpty)
        {
            throw new NotSupportedException("Host actor bounds require its actual posed triangles.");
        }
        int maximum = short.MinValue;
        int minimum = short.MaxValue;
        foreach (NativeBattleForeignModelFace face in geometry.Faces)
        {
            Include(face.A);
            Include(face.B);
            Include(face.C);
        }
        return ((short)maximum, (short)minimum);

        void Include(NativeBattleVector vertex)
        {
            short y = transform(vertex);
            maximum = Math.Max(maximum, y);
            minimum = Math.Min(minimum, y);
        }
    }

    public static NativeBattleGfGeometryBatch Deform(NativeBattleForeignModelGeometry geometry, NativeBattleCameraProjection projection, Func<NativeBattleModuleProjectedVertex, NativeBattleProjectedVertex> deform, Func<NativeBattleRawMesh, IReadOnlyList<NativeBattleProjectedVertex>, NativeBattlePilotVisualIdentity, NativeBattleGfGeometryBatch> emit, NativeBattlePilotVisualIdentity identity)
    {
        NativeBattleActionMatrix camera = NativeBattleGfMatrices.Compose56C2F0(projection.View ?? throw new NotSupportedException("Host model projection requires its current camera."), geometry.World);
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        for (int index = 0; index < geometry.Faces.Length; index++)
        {
            NativeBattleForeignModelFace face = geometry.Faces[index];
            NativeBattleRawMesh mesh = face.Triangle();
            NativeBattleProjectedVertex[] projected = mesh.Vertices.Select(vertex => deform(NativeBattleModuleProjection.Project45FE10(camera, vertex.Position, projection))).ToArray();
            NativeBattlePilotVisualIdentity selected = identity with { Primitive = unchecked(identity.Primitive * 397 + index) };
            NativeBattleGfGeometryBatch batch = emit(mesh, projected, selected);
            foreach (NativeBattleGfGeometryAttempt attempt in batch.Attempts)
            {
                attempts.Add(attempt with { ByteOffsetRelative = attempt.ByteOffsetRelative + allocated, Primitive = attempt.Primitive is { } primitive ? geometry.BindSurface(primitive, face, selected) : null });
            }
            allocated += batch.AllocationBytes;
        }
        return new(attempts.ToImmutable(), allocated);
    }

    public static NativeBattleGfGeometryBatch DeformLocal(NativeBattleForeignModelGeometry geometry, NativeBattleCameraProjection projection, Func<NativeBattleVector, NativeBattleVector> deform, Func<NativeBattleRawMesh, NativeBattleActionMatrix, NativeBattlePilotVisualIdentity, NativeBattleGfGeometryBatch> emit, NativeBattlePilotVisualIdentity identity)
    {
        NativeBattleActionMatrix camera = NativeBattleGfMatrices.Compose56C2F0(projection.View ?? throw new NotSupportedException("Host model deformation requires its current camera."), geometry.World);
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        for (int index = 0; index < geometry.Faces.Length; index++)
        {
            NativeBattleForeignModelFace source = geometry.Faces[index];
            NativeBattleForeignModelFace face = source with { A = deform(source.A), B = deform(source.B), C = deform(source.C) };
            NativeBattlePilotVisualIdentity selected = identity with { Primitive = unchecked(identity.Primitive * 397 + index) };
            NativeBattleGfGeometryBatch batch = emit(face.Triangle(), camera, selected);
            foreach (NativeBattleGfGeometryAttempt attempt in batch.Attempts)
            {
                attempts.Add(attempt with { ByteOffsetRelative = attempt.ByteOffsetRelative + allocated, Primitive = attempt.Primitive is { } primitive ? geometry.BindSurface(primitive, face, selected) : null });
            }
            allocated += batch.AllocationBytes;
        }
        return new(attempts.ToImmutable(), allocated);
    }

    public static NativeBattleCureActorPacketBatch Cure(NativeBattleForeignModelGeometry geometry, NativeBattleActionMatrix camera, NativeBattleCameraProjection projection, NativeBattleVector center, short verticalOffset, short? contextF4, uint color, NativeBattlePilotVisualIdentity identity, NativeBattleCureTextureProjection? texture = null, uint overlayColor = 0x808080)
    {
        NativeBattleCureActorMatrices matrices = NativeBattlePoseMatrices.CureActorMatrices8D7110(geometry.World, camera, new(0, verticalOffset, 0), 1, center, 8192);
        ImmutableArray<NativeBattleCureAllocatedPrimitive>.Builder result = ImmutableArray.CreateBuilder<NativeBattleCureAllocatedPrimitive>();
        int processBytes = 0;
        int effectBytes = 0;
        for (int index = 0; index < geometry.Faces.Length; index++)
        {
            NativeBattleForeignModelFace face = geometry.Faces[index];
            NativeBattlePilotVisualIdentity selected = identity with { Primitive = unchecked(identity.Primitive * 397 + index) };
            NativeBattleCureActorPacketBatch batch = NativeBattleCureActorPackets.Emit8D7430([face.Triangle()], [NativeBattleForeignModelGeometry.Identity], matrices.MainCamera, matrices.OverlayCamera, projection, 1, color, overlayColor, texture ?? NativeBattleCureActorPackets.TextureProjection8D7050(512, 384, 320, 241, 128, 128), contextF4 is short known ? unchecked((ushort)known) : null, null, 17, selected);
            foreach (NativeBattleCureAllocatedPrimitive allocation in batch.Primitives)
            {
                bool body = allocation.Bank == NativeBattleCurePacketBank.Process;
                result.Add(allocation with { ByteOffset = allocation.ByteOffset + (body ? processBytes : effectBytes), Primitive = body ? geometry.BindSurface(allocation.Primitive, face, selected) : allocation.Primitive });
            }
            processBytes += batch.ProcessAllocationBytes;
            effectBytes += batch.EffectAllocationBytes;
        }
        return new(result.ToImmutable(), processBytes, effectBytes);
    }

    public static NativeBattleBlizzagaModelBandResult Band(NativeBattleForeignModelGeometry geometry, NativeBattleCameraProjection projection, uint color, short low, short high, NativeBattlePilotVisualIdentity identity)
    {
        NativeBattleActionMatrix camera = NativeBattleGfMatrices.Compose56C2F0(projection.View ?? throw new NotSupportedException("A host model band requires its current camera."), geometry.World);
        short minimum = 32767;
        short maximum = -32767;
        ImmutableArray<NativeBattleGeometryPrimitive>.Builder primitives = ImmutableArray.CreateBuilder<NativeBattleGeometryPrimitive>();
        for (int index = 0; index < geometry.Faces.Length; index++)
        {
            NativeBattleForeignModelFace face = geometry.Faces[index];
            NativeBattlePilotVisualIdentity selected = identity with { Primitive = unchecked(identity.Primitive * 397 + index) };
            NativeBattleBlizzagaModelBandResult batch = NativeBattleBlizzagaModelBand.Emit6D8D80([face.Triangle()], [camera], projection, color, 1, low, high, null, minimum, maximum, selected);
            minimum = batch.Minimum;
            maximum = batch.Maximum;
            foreach (NativeBattleGeometryPrimitive primitive in batch.Primitives)
            {
                primitives.Add(primitive.UsesModelMaterial ? geometry.BindSurface(primitive, face, selected) : primitive);
            }
        }
        return new(minimum, maximum, [], primitives.ToImmutable());
    }

    public static NativeBattleGfGeometryBatch Tint(NativeBattleForeignModelGeometry geometry, NativeBattleCameraProjection projection, uint color, uint tint, NativeBattleSummonColors colors, Func<IReadOnlyList<NativeBattleVector>, short[], bool> writeFactors, short zsf3, short zsf4, NativeBattlePilotVisualIdentity identity)
    {
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        for (int index = 0; index < geometry.Faces.Length; index++)
        {
            NativeBattleForeignModelFace face = geometry.Faces[index];
            NativeBattleRawMesh mesh = face.Triangle();
            byte[] vertices = new byte[24];
            for (int vertex = 0; vertex < mesh.Vertices.Length; vertex++)
            {
                NativeBattleVector point = NativeBattleGfMatrices.Transform460840(geometry.World, mesh.Vertices[vertex].Position);
                System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(vertices.AsSpan(vertex * 8), point.X);
                System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(vertices.AsSpan(vertex * 8 + 2), point.Y);
                System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(vertices.AsSpan(vertex * 8 + 4), point.Z);
            }
            NativeBattlePilotVisualIdentity selected = identity with { Primitive = unchecked(identity.Primitive * 397 + index) };
            NativeBattleGfGeometryBatch batch = NativeBattleActorTintPackets.Emit(mesh.Polygons, vertices.ToImmutableArray(), projection.View!, projection, color, tint, colors, writeFactors, zsf3, zsf4, selected);
            foreach (NativeBattleGfGeometryAttempt attempt in batch.Attempts)
            {
                NativeBattleGeometryPrimitive? primitive = attempt.Primitive;
                if (primitive is not null && (primitive.Words[1] >> 24 & 4) != 0)
                {
                    primitive = geometry.BindSurface(primitive, face, selected);
                }
                attempts.Add(attempt with { ByteOffsetRelative = attempt.ByteOffsetRelative + allocated, Primitive = primitive });
            }
            allocated += batch.AllocationBytes;
        }
        return new(attempts.ToImmutable(), allocated);
    }
}
