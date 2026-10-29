using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleShadowDrawStep(NativeBattleFileStaging Staging, NativeBattleGeometryRegisterStep Registers, NativeBattleEffectArena Temporary78, uint TemporaryAddress, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleShadowDrawing
{
    public static NativeBattleShadowDrawStep Prepare5088A0(NativeBattleFileStaging staging, NativeBattleGeometryRegisterStep registers, NativeBattleGraphicsModel actor, NativeBattleCameraProjection projection, NativeBattleScriptedGraphicsMemory controls, NativeBattleShadowSource shadowSource, bool gouraud, int tableBaseBucket, byte depthShift, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        return Prepare5088A0(staging, registers, actor.GeometryWorldMatrix ?? throw new NotSupportedException("Native shadow requires its retained model world matrix."), actor.Transform?.Angle36 ?? throw new NotSupportedException("Native shadow requires its actual ground-height WORD."), actor.GeometryBounds52 ?? throw new NotSupportedException("Native shadow requires its current model bounds."), actor.Color44 ?? throw new NotSupportedException("Native shadow requires its current model shadow color."), projection, controls, shadowSource, gouraud, tableBaseBucket, depthShift, pilotIdentity);
    }

    public static NativeBattleShadowDrawStep Prepare5088A0(NativeBattleFileStaging staging, NativeBattleGeometryRegisterStep registers, NativeBattleActionMatrix world, short groundY, NativeBattleEffectLocalBounds bounds, uint color44, NativeBattleCameraProjection projection, NativeBattleScriptedGraphicsMemory controls, NativeBattleShadowSource shadowSource, bool gouraud, int tableBaseBucket, byte depthShift, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native shadow requires its actual camera view.");
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(staging, 0x78);
        staging = allocation.Staging;
        NativeBattleEffectArena temporary = NativeBattleTemporaryMemory.ReadView(staging, allocation.Address, 0x78);
        int centerX = (bounds.Minimum.X + bounds.Maximum.X) / 2;
        int centerZ = (bounds.Minimum.Z + bounds.Maximum.Z) / 2;
        staging = staging.Write(0xB8B9E4, DwordBytes(unchecked((uint)centerX))).Write(0xB8B9EC, DwordBytes(unchecked((uint)centerZ)));
        NativeBattleShadowGeometry shape = NativeBattlePoseMatrices.ShadowGeometry5088A0(world, groundY, bounds, view, shadowSource, gouraud);
        temporary = Matrix(temporary, shape.CameraMatrix, null);
        registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, shape.CameraMatrix, true, true, null) };
        (int Vertex, int Axis)[] stores = [(1, 2), (2, 0), (2, 2), (3, 0), (4, 0), (4, 2), (5, 2), (6, 0), (6, 2), (7, 0), (8, 0), (8, 2), (9, 2)];
        foreach ((int vertex, int axis) in stores)
        {
            NativeBattleVector vector = shape.Vertices[vertex];
            short value = axis == 0 ? vector.X : vector.Z;
            staging = staging.Write(0x1D999D0u + (uint)(vertex * 8 + axis * 2), WordBytes(unchecked((ushort)value)));
        }
        NativeBattleModuleProjectedVertex[] projected = new NativeBattleModuleProjectedVertex[10];
        for (int vertex = 0; vertex < 10; vertex++)
        {
            (byte[] bytes, bool[] known) = staging.ReadSnapshot(0x1D999D0u + (uint)(vertex * 8 + 6), 2);
            short? w = known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null;
            registers = NativeBattleGeometry.LoadVector(registers, 0, shape.Vertices[vertex], w);
            registers = NativeBattleGeometry.ProjectSingle(registers, projection, controls.Dqa, controls.Dqb);
            uint screen = registers.Geometry.Sxy[2]!.Value;
            uint depth = registers.Geometry.Sz[3]!.Value;
            uint flags = registers.Colors.Flags!.Value;
            temporary = temporary.Write(0x20 + vertex * 8, DwordBytes(screen)).Write(0x24 + vertex * 8, WordBytes(unchecked((ushort)(depth >> 2))));
            temporary = temporary.Write(0x70, DwordBytes(flags)).Write(0x74, DwordBytes(registers.Colors.Ir0!.Value));
            projected[vertex] = new(screen, unchecked((ushort)depth), flags);
        }
        NativeBattleGfGeometryBatch batch = NativeBattleShadowPackets.EmitBatch5088A0(projected, color44, gouraud, tableBaseBucket, depthShift, pilotIdentity is null ? null : shape.Vertices.Select(vertex => NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(shape.CameraMatrix, vertex), projection)).ToArray(), pilotIdentity);
        return new(staging, registers, temporary, allocation.Address, batch);
    }

    private static byte[] DwordBytes(uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return bytes;
    }

    private static byte[] WordBytes(ushort value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return bytes;
    }

    private static NativeBattleEffectArena Matrix(NativeBattleEffectArena arena, NativeBattleActionMatrix matrix, short? padding)
    {
        for (int lane = 0; lane < 9; lane++)
        {
            arena = arena.Write(lane * 2, WordBytes(unchecked((ushort)matrix.Rotation[lane])));
        }
        arena = padding is short value ? arena.Write(18, WordBytes(unchecked((ushort)value))) : arena.MarkUnknown(18, 2);
        return arena.Write(20, DwordBytes(unchecked((uint)matrix.X))).Write(24, DwordBytes(unchecked((uint)matrix.Y))).Write(28, DwordBytes(unchecked((uint)matrix.Z)));
    }
}
