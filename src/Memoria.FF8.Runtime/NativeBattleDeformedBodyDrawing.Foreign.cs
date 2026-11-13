using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleDeformedBodyDrawing
{
    public static NativeBattleDeformedBodyStep DrawForeign(NativeBattleEffectMemory memory, NativeBattleEffectArena temporary132, NativeBattleEffectArena vertexScratch, NativeBattleForeignModelGeometry geometry, NativeBattlePackedMatrix32 camera, NativeBattleCameraProjection projection, NativeBattleScriptedGraphicsMemory controls, uint cacheAddress, int cacheLength, NativeBattlePilotVisualIdentity identity)
    {
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Host body deformation requires its current geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Host body deformation requires its current arithmetic registers."));
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Host body deformation requires its actual coefficient cache.");
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        NativeBattleForeignModelDrawing.RequireCoefficientCache(geometry, U32(temporary132, 0x2C), cacheAddress, cacheLength);
        ImmutableArray<NativeBattleVector> vertices = geometry.Vertices;
        Vector3[] world = new Vector3[vertices.Length];
        registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, geometry.World, true, true, null) };
        for (int vertex = 0; vertex < vertices.Length; vertex++)
        {
            registers = NativeBattleGeometry.LoadVector(registers, 0, vertices[vertex], 0);
            registers = NativeBattleGeometry.Transform(registers);
            byte[] transformed = new byte[6];
            for (int axis = 0; axis < 3; axis++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(transformed.AsSpan(axis * 2), unchecked((short)(registers.Colors.Ir[axis] ?? throw new NotSupportedException("Host body deformation requires its transformed IR values."))));
            }
            vertexScratch = vertexScratch.Write(vertex * 8, transformed);
        }
        registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, camera.ReadMatrix(), true, true, Padding(camera)) };
        for (int vertex = 0; vertex < vertices.Length; vertex++)
        {
            int output = vertex * 8;
            uint coefficient = U32(temporary132, 0x2C);
            if (coefficient < cacheAddress || cacheLength < 0 || (ulong)coefficient - cacheAddress >= (ulong)cacheLength)
            {
                throw new InvalidDataException("Host body deformation leaves its bounded coefficient cache.");
            }
            int delta = unchecked(arena.Read(checked((int)(coefficient - NativeBattleEffectNodeMemory.ArenaAddress)), 1)[0] * Word(temporary132, 0x80)) >> 10;
            short y = unchecked((short)(Word(vertexScratch, output + 2) + delta));
            vertexScratch = vertexScratch.Write(output + 2, BitConverter.GetBytes(y));
            temporary132 = Dword(temporary132, 0x2C, checked(coefficient + 1));
            byte[] xyz = vertexScratch.Read(output, 6);
            (byte[] w, bool[] known) = vertexScratch.ReadSnapshot(output + 6, 2);
            registers = NativeBattleGeometry.LoadVector(registers, 0, Vector(xyz), known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(w) : null);
            world[vertex] = NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(camera.ReadMatrix(), Vector(xyz)), projection);
            registers = NativeBattleGeometry.ProjectSingle(registers, projection, controls.Dqa, controls.Dqb);
            uint screen = registers.Geometry.Sxy[2] ?? throw new NotSupportedException("Host body deformation requires its reached screen FIFO.");
            uint depth = registers.Geometry.Sz[3] ?? throw new NotSupportedException("Host body deformation requires its reached depth FIFO.");
            temporary132 = Dword(temporary132, 0x50, screen);
            temporary132 = Dword(temporary132, 0x54, unchecked((uint)(unchecked((int)depth) >> 2)));
            if (U16(temporary132, 0x54) == 0)
            {
                vertexScratch = vertexScratch.Write(output + 6, BitConverter.GetBytes((ushort)0x10));
            }
            else
            {
                short x = unchecked((short)screen);
                short sy = unchecked((short)(screen >> 16));
                byte clip = temporary132.Read(0x56, 1)[0];
                if (x < Word(temporary132, 0x40) * 8)
                {
                    clip |= 1;
                }
                else if (x >= Word(temporary132, 0x44) * 8)
                {
                    clip |= 2;
                }
                if (sy < Word(temporary132, 0x42) * 8)
                {
                    clip |= 4;
                }
                else if (sy >= Word(temporary132, 0x46) * 8)
                {
                    clip |= 8;
                }
                temporary132 = temporary132.Write(0x56, [clip]);
                vertexScratch = vertexScratch.Write(output, temporary132.Read(0x50, 8));
            }
        }
        NativeBattleEffectArena faceScratch = NativeBattleEffectArena.CreateUnknown(24);
        for (int faceIndex = 0; faceIndex < geometry.Faces.Length; faceIndex++)
        {
            NativeBattleForeignModelFace face = geometry.Faces[faceIndex];
            byte[] faceBytes = face.Triangle().Polygons[0].Bytes.ToArray();
            for (int corner = 0; corner < 3; corner++)
            {
                faceScratch = NativeBattleEffectNodeContextMemory.Copy(vertexScratch, checked(face.VertexIndices[corner] * 8), faceScratch, corner * 8, 8);
            }
            var drawn = DrawFace(faceBytes, false, faceScratch, 3, temporary132, registers, controls, 17, 0, allocated);
            temporary132 = drawn.Temporary;
            registers = drawn.Registers;
            NativeBattleGfGeometryAttempt attempt = drawn.Attempt;
            if (attempt.Primitive is { } primitive)
            {
                NativeBattlePilotVisualIdentity selected = identity with { Primitive = unchecked(identity.Primitive * 397 + faceIndex) };
                primitive = primitive with { PilotGeometry = new(selected, world[face.VertexIndices[0]], world[face.VertexIndices[1]], world[face.VertexIndices[2]], default, 3) { ForeignSurface = face.Surface, SourceDepths = new(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], 0), UseSourceDepths = true } };
                attempt = attempt with { Primitive = primitive };
                allocated = checked(allocated + attempt.PacketLength);
            }
            attempts.Add(attempt);
        }
        return new(memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors }, temporary132, vertexScratch, new(attempts.ToImmutable(), allocated));
    }
}
