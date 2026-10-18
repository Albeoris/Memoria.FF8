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
        if (task.ReadByte(0x37) != 0)
        {
            throw new NotSupportedException("Native effect-owned model drawing requires its proved zero-alpha owner.");
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
        NativeBattleRawModelBodyDraw body = NativeBattleRawModelBodyDrawing.Draw5099D0(resource.Geometry[0], bones, projection, task.ReadDword(0x58), task.ReadDword(0xAC), projectedScratch, colors);
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        attempts.AddRange(body.Packets.Attempts);
        int allocated = body.Packets.AllocationBytes;
        NativeBattleEffectLocalBounds bounds = ScaleShadowBounds7156EE(NativeBattlePoseMatrices.ShadowBounds5086F0(resource.PoseSource, pose), Word(0x13E));
        task = task.WriteWord(0x64, unchecked((ushort)bounds.Minimum.X)).WriteWord(0x66, unchecked((ushort)bounds.Minimum.Y)).WriteWord(0x68, unchecked((ushort)bounds.Minimum.Z)).WriteWord(0x6A, unchecked((ushort)bounds.Maximum.X)).WriteWord(0x6C, unchecked((ushort)bounds.Maximum.Y)).WriteWord(0x6E, unchecked((ushort)bounds.Maximum.Z));
        if ((task.ReadByte(0x30) & 0x20) == 0)
        {
            NativeBattleShadowGeometry shape = NativeBattlePoseMatrices.ShadowGeometry5088A0(world, Word(0x54), bounds, view, shadowSource, capabilities.GouraudAlpha);
            NativeBattleModuleProjectedVertex[] vertices = shape.Vertices.Select(vertex => NativeBattleModuleProjection.Project45FE10(shape.CameraMatrix, vertex, projection)).ToArray();
            NativeBattleGfGeometryBatch shadow = NativeBattleShadowPackets.EmitBatch5088A0(vertices, task.ReadDword(0x5C), capabilities.GouraudAlpha, 4121, 16);
            attempts.AddRange(shadow.Attempts.Select(attempt => attempt with { ByteOffsetRelative = checked(attempt.ByteOffsetRelative + allocated) }));
            allocated += shadow.AllocationBytes;
        }
        return new(task, bounds, body.ProjectedScratch, body.ProjectedBytesWritten, new(attempts.ToImmutable(), allocated));

        short Word(int offset) => unchecked((short)task.ReadWord(offset));
    }

    public static NativeBattleEffectLocalBounds ScaleShadowBounds7156EE(NativeBattleEffectLocalBounds bounds, short scale)
    {
        if (scale == 4096)
        {
            return bounds;
        }
        short centerX = (short)((bounds.Minimum.X + bounds.Maximum.X) / 2);
        short centerZ = (short)((bounds.Minimum.Z + bounds.Maximum.Z) / 2);
        return new(new(Scale(bounds.Minimum.X, centerX), bounds.Minimum.Y, Scale(bounds.Minimum.Z, centerZ)), new(Scale(bounds.Maximum.X, centerX), bounds.Maximum.Y, Scale(bounds.Maximum.Z, centerZ)));

        short Scale(short bound, short center) => unchecked((short)(center + unchecked((bound - center) * scale) / 4096));
    }

}
