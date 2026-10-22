namespace Memoria.FF8.Runtime;

public static class NativeBattleDoom8ParticleDrawing
{
    public static NativeBattleDoom8Frame DrawDust(NativeBattleDoom8Frame frame, uint address, NativeBattleDoom8Resources resources, NativeBattlePackedMatrix32 camera, NativeBattleCameraProjection projection, short cameraRoll, Func<NativeBattleDoom8Frame, NativeBattleGfGeometryBatch, bool, NativeBattleDoom8Frame> deliver)
    {
        NativeBattleModulePoolTask task = Require(frame, address, 0x8CD670);
        if ((task.ReadByte(0x26) & 4) != 0)
        {
            return frame;
        }
        NativeBattleModuleResource sprite = resources.RequireDustSprite();
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(RequireStaging(frame), 216);
        frame = frame with { Staging = allocation.Staging };
        NativeBattleEffectArena temporary = NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 216);
        NativeBattleProjectedSpriteHead prepared = NativeBattleProjectedSpriteD8.PrepareHead(frame.Memory, task, camera, temporary);
        NativeBattleProjectedSpriteDraw drawn = NativeBattleProjectedSpriteD8.Draw(prepared, source => source == sprite.Address ? sprite : throw new InvalidDataException("Doom dust selected another sprite owner."), projection, cameraRoll, 17);
        frame = frame with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(RequireStaging(frame), allocation.Address, drawn.TemporaryD8, 216) };
        frame = deliver(frame, drawn.Batch, false);
        return frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(RequireStaging(frame), 216) };
    }

    public static NativeBattleDoom8Frame DrawSmoke(NativeBattleDoom8Frame frame, uint address, NativeBattleDoom8Resources resources, NativeBattlePackedMatrix32 camera, NativeBattleCameraProjection projection, NativeBattleSummonColors constants, Func<NativeBattleDoom8Frame, NativeBattleGfGeometryBatch, bool, NativeBattleDoom8Frame> deliver)
    {
        NativeBattleModulePoolTask task = Require(frame, address, 0x8CDFB0);
        if ((task.ReadByte(0x26) & 4) != 0)
        {
            return frame;
        }
        NativeBattleStaticMeshResource mesh = resources.RequireSmokeMesh();
        NativeBattleActionMatrix identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        NativeBattlePackedMatrix32 local = NativeBattlePackedMatrix32.FromMatrix(identity, null);
        foreach ((char axis, int offset) in new (char, int)[] { ('X', 0x44), ('Z', 0x48), ('Y', 0x46) })
        {
            var rotated = NativeBattleStatefulMatrixRotation.Rotate(frame.Memory, RequireStaging(frame), local, axis, Signed(task, offset));
            local = rotated.Matrix;
            frame = frame with { Memory = rotated.Memory, Staging = rotated.Staging };
        }
        NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.Scale56BEF0(local.ReadMatrix(), unchecked((int)task.ReadDword(0x30)), unchecked((int)task.ReadDword(0x34)), unchecked((int)task.ReadDword(0x38))) with { X = Signed(task, 0x1C), Y = Signed(task, 0x1E), Z = Signed(task, 0x20) };
        matrix = NativeBattleGfMatrices.Compose56C2F0(camera.ReadMatrix(), matrix);
        NativeBattleGeometryRegisters geometry = frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Doom smoke requires its current geometry register owner.");
        frame = frame with { Memory = frame.Memory with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(geometry, matrix, true, true, null) } };
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(RequireStaging(frame), 88);
        frame = frame with { Staging = allocation.Staging };
        NativeBattleEffectArena temporary = NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 88);
        temporary = NativeBattleStaticMeshContext88.Put(temporary, 0, task.ReadDword(0x4C));
        temporary = NativeBattleStaticMeshContext88.Put(temporary, 8, task.ReadDword(0x40));
        temporary = NativeBattleStaticMeshContext88.Put(temporary, 12, unchecked((uint)(int)Signed(task, 0x50)));
        temporary = NativeBattleStaticMeshContext88.Put(temporary, 0x1C, 0xF0);
        NativeBattleStaticMeshContext88Step drawn = NativeBattleStaticMeshContext88.Draw(frame.Memory, mesh, matrix, projection, temporary, constants, null);
        frame = frame with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(RequireStaging(frame), allocation.Address, drawn.Context, 88) };
        frame = deliver(frame, drawn.Batch, true);
        return frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(RequireStaging(frame), 88) };
    }

    private static NativeBattleModulePoolTask Require(NativeBattleDoom8Frame frame, uint address, uint callback)
    {
        NativeBattleModulePoolTask task = NativeBattleDoom8Controller.Read(frame, address);
        if (frame.Effect.Binding.Effect != 8 || frame.Effect.Binding.Entry != NativeBattleDoom8Controller.Entry || task.Callback != callback)
        {
            throw new InvalidDataException("Doom particle drawing requires its actual module callback.");
        }
        return task;
    }

    private static NativeBattleFileStaging RequireStaging(NativeBattleDoom8Frame frame) => frame.Staging ?? throw new NotSupportedException("Doom particles require the current physical temporary staging owner.");
    private static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
}
