using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleAcid170ModelDrawing
{
    public static NativeBattleAcid170ModelStep DrawForeign(NativeBattleEffectMemory memory, NativeBattleEffectArena temporary172, NativeBattleEffectArena vertexScratch, NativeBattleForeignModelGeometry geometry, NativeBattlePackedMatrix32 camera, NativeBattleCameraProjection projection, NativeBattleScriptedGraphicsMemory controls, NativeBattlePilotVisualIdentity identity)
    {
        NativeBattleForeignModelDrawing.RequireIndexedTopology(geometry);
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Host body deformation requires its current geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Host body deformation requires its current arithmetic registers."));
        memory = LoadPlaneControls(memory, temporary172);
        registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, geometry.World, true, true, null) };
        for (int index = 0; index < geometry.Vertices.Length; index++)
        {
            registers = NativeBattleGeometry.LoadVector(registers, 0, geometry.Vertices[index], 0);
            registers = NativeBattleGeometry.Transform(registers);
            byte[] packed = new byte[8];
            for (int axis = 0; axis < 3; axis++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(packed.AsSpan(axis * 2), unchecked((short)(registers.Colors.Ir[axis] ?? throw new NotSupportedException("Host body deformation requires its transformed IR values."))));
            }
            vertexScratch = vertexScratch.Write(index * 8, packed);
            memory = NativeBattleLightBackTransform.TransformIr(memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors });
            registers = new(memory.GeometryRegisters!, memory.ScriptedColorRegisters!);
            int plane = unchecked((registers.Colors.Mac[0] ?? throw new NotSupportedException("Host body deformation requires its current LIGHT/IR MAC1.")) << 12);
            temporary172 = Dword(temporary172, 0x2C, unchecked((uint)plane));
            (temporary172, vertexScratch) = DeformVertex(temporary172, vertexScratch, index * 8, plane);
        }
        registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, camera.ReadMatrix(), true, true, Padding(camera)) };
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        NativeBattleEffectArena faceScratch = NativeBattleEffectArena.CreateUnknown(24);
        for (int index = 0; index < geometry.Faces.Length; index++)
        {
            NativeBattleForeignModelFace face = geometry.Faces[index];
            for (int corner = 0; corner < 3; corner++)
            {
                faceScratch = NativeBattleEffectNodeContextMemory.Copy(vertexScratch, checked(face.VertexIndices[corner] * 8), faceScratch, corner * 8, 8);
            }
            NativeBattlePilotVisualIdentity selected = identity with { Primitive = unchecked(identity.Primitive * 397 + index) };
            var drawn = NativeBattleSkinnedBodyPackets.EmitBaseFace(face.Triangle().Polygons[0].Bytes.ToArray(), false, faceScratch, 3, temporary172, registers, projection, controls, 17, 2, allocated, 0x50, 0x44, 0x4C, selected);
            temporary172 = drawn.Temporary;
            registers = drawn.Registers;
            NativeBattleGfGeometryAttempt attempt = drawn.Attempt;
            if (attempt.Primitive is { } primitive)
            {
                attempt = attempt with { Primitive = geometry.BindSurface(primitive, face, selected) };
                allocated = checked(allocated + attempt.PacketLength);
            }
            attempts.Add(attempt);
        }
        return new(memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors }, temporary172, vertexScratch, new(attempts.ToImmutable(), allocated));
    }
}
