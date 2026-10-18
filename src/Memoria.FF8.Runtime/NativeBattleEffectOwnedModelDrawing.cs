using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffectOwnedModelDraw(NativeBattleModulePoolTask Task, NativeBattleEffectLocalBounds Bounds, ImmutableArray<byte> ProjectedScratch, int ProjectedBytesWritten, NativeBattleGfGeometryBatch Packets);

/// <summary>7558A0's explicit posed body and shadow, sharing the caller's private packet allocation.</summary>
public static class NativeBattleEffectOwnedModelDrawing
{
    public static NativeBattleEffectOwnedModelDraw Draw7558A0(NativeBattleModulePoolTask task, NativeBattleEffectOwnedModelState state, NativeBattleEffectOwnedModelResource resource, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleShadowSource shadowSource, NativeBattlePrimitiveCapabilities capabilities, NativeBattleSummonColors colors, ImmutableArray<byte> projectedScratch)
    {
        NativeBattleEffectOwnedModel.Validate(task, state, resource);
        NativeBattleDecodedPose pose = state.DecodedPose ?? throw new NotSupportedException("Native effect-owned model drawing requires its actual current decoded pose.");
        if (task.ReadByte(0x37) != 0 || task.ReadWord(0x13E) != 4096)
        {
            throw new NotSupportedException("Native effect-owned model drawing requires the proved zero-alpha and unscaled bounds owners.");
        }
        if (resource.Geometry.IsDefault || resource.Geometry.Length != 1 || projectedScratch.IsDefault)
        {
            throw new InvalidDataException("Native effect-owned model drawing requires its one original object and explicit projected scratch owner.");
        }
        NativeBattleActionMatrix world = NativeBattleGfMatrices.Euler56CE30(new(Word(0x3C), Word(0x3E), Word(0x40)));
        world = NativeBattlePoseMatrices.Scale56BEF0(world, unchecked((int)task.ReadDword(0x114)), unchecked((int)task.ReadDword(0x118)), unchecked((int)task.ReadDword(0x11C))) with { X = Word(0x4C), Y = Word(0x4E), Z = Word(0x50) };
        for (int index = 0; index < 9; index++)
        {
            task = task.WriteWord(0x70 + index * 2, unchecked((ushort)world.Rotation[index]));
        }
        task = task.WriteWord(0x82, 0).WriteDword(0x84, unchecked((uint)world.X)).WriteDword(0x88, unchecked((uint)world.Y)).WriteDword(0x8C, unchecked((uint)world.Z));
        NativeBattleActionMatrix modelView = NativeBattlePoseMatrices.Compose56C2F0(view, world);
        ImmutableArray<NativeBattleActionMatrix> bones = NativeBattlePoseMatrices.ComposeCurrentPose(resource.PoseSource, pose, modelView);
        byte[] scratch = projectedScratch.ToArray();
        int bytesWritten = 0;
        ImmutableArray<NativeBattleGeometryPrimitive> body = [];
        if ((task.ReadDword(0xAC) & 1) != 0)
        {
            NativeBattleRawMesh mesh = resource.Geometry[0];
            mesh.Validate();
            NativeBattleProjectedVertex[] vertices = mesh.Vertices.Select(vertex => NativeBattleGeometryProjection.Project50F900(vertex.Bone < bones.Length ? bones[vertex.Bone] : throw new InvalidDataException("Native effect-owned model lacks its actual selected bone matrix."), vertex.Position, projection, new(0, 0, 320, 216))).ToArray();
            bytesWritten = checked(vertices.Length * 8);
            if (bytesWritten > scratch.Length)
            {
                throw new InvalidDataException("Native effect-owned model projection leaves its actual supplied scratch allocation.");
            }
            for (int index = 0; index < vertices.Length; index++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(scratch.AsSpan(index * 8), vertices[index].Screen);
                BinaryPrimitives.WriteUInt32LittleEndian(scratch.AsSpan(index * 8 + 4), vertices[index].DepthAndClip);
            }
            body = NativeBattleRawGeometryPackets.EmitProjected5099D0(mesh.Polygons, vertices, task.ReadDword(0x58), 17, 4, 0, null, colors);
        }
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        foreach (NativeBattleGeometryPrimitive primitive in body)
        {
            byte[] data = new byte[primitive.Words.Length * 4];
            for (int index = 0; index < primitive.Words.Length; index++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(index * 4), primitive.Words[index]);
            }
            attempts.Add(new(allocated, data.ToImmutableArray(), primitive.PhysicalWriteMask ?? Enumerable.Repeat(NativeBattleGfPacketByteState.Known, data.Length).ToImmutableArray(), primitive));
            allocated += data.Length;
        }
        NativeBattleEffectLocalBounds bounds = NativeBattlePoseMatrices.ShadowBounds5086F0(resource.PoseSource, pose);
        task = task.WriteWord(0x64, unchecked((ushort)bounds.Minimum.X)).WriteWord(0x66, unchecked((ushort)bounds.Minimum.Y)).WriteWord(0x68, unchecked((ushort)bounds.Minimum.Z)).WriteWord(0x6A, unchecked((ushort)bounds.Maximum.X)).WriteWord(0x6C, unchecked((ushort)bounds.Maximum.Y)).WriteWord(0x6E, unchecked((ushort)bounds.Maximum.Z));
        if ((task.ReadByte(0x30) & 0x20) == 0)
        {
            NativeBattleShadowGeometry shape = NativeBattlePoseMatrices.ShadowGeometry5088A0(world, Word(0x54), bounds, view, shadowSource, capabilities.GouraudAlpha);
            NativeBattleModuleProjectedVertex[] vertices = shape.Vertices.Select(vertex => NativeBattleModuleProjection.Project45FE10(shape.CameraMatrix, vertex, projection)).ToArray();
            NativeBattleGfGeometryBatch shadow = NativeBattleShadowPackets.EmitBatch5088A0(vertices, task.ReadDword(0x5C), capabilities.GouraudAlpha, 4121, 16);
            attempts.AddRange(shadow.Attempts.Select(attempt => attempt with { ByteOffsetRelative = checked(attempt.ByteOffsetRelative + allocated) }));
            allocated += shadow.AllocationBytes;
        }
        return new(task, bounds, scratch.ToImmutableArray(), bytesWritten, new(attempts.ToImmutable(), allocated));

        short Word(int offset) => unchecked((short)task.ReadWord(offset));
    }
}
