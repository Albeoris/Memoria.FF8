namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect326Controller
{
    public static NativeBattleEffect326Step AdvanceMorphMesh62F400(NativeBattleEffect326Frame frame, uint address, NativeBattleEffect326Resources resources, NativeBattleEffect326Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int age = Signed(task, 12);
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(new(0, 0, 0));
        local = NativeBattlePoseMatrices.Scale56BEF0(local, Signed(task, 0x1C), Signed(task, 0x20), Signed(task, 0x20));
        NativeBattleCameraPose camera = inputs.ReadCameraPose(frame);
        var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(camera.Target.X - camera.Eye.X, camera.Target.Y - camera.Eye.Y, camera.Target.Z - camera.Eye.Z);
        NativeBattleVector point = new(unchecked((short)(camera.Target.X + (direction.X * 200 >> 13))), unchecked((short)(camera.Target.Y + (direction.Y * 200 >> 13))), unchecked((short)(camera.Target.Z + (direction.Z * 200 >> 13))));
        NativeBattleActionMatrix view = inputs.ReadView(frame).ReadMatrix();
        NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(view, point);
        local = local with { X = unchecked(rotated.X + view.X), Y = unchecked(rotated.Y + view.Y), Z = unchecked(rotated.Z + view.Z) };
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(local, null));
        (frame, uint temporary) = Allocate(frame, 88);
        NativeBattleEffectArena context = Context(frame, temporary, 88).Write(0, BitConverter.GetBytes(0xE148E8u)).Write(8, new byte[4]).Write(0x1C, BitConverter.GetBytes(age < 6 ? 0x2033u : 0x20F3u));
        if (age >= 6)
        {
            context = context.Write(12, BitConverter.GetBytes(unchecked((age - 6) * 682)));
        }
        uint vertices = frame.State.ReadDword(0xE196B0);
        NativeBattleEffectArena morphed = NativeBattleEffect326MorphVertices.Interpolate62F400(resources.ReadStatic(0xE148F0, 1008), resources.ReadStatic(0xE15668, 1008), ReadBytes(frame, vertices, 1008), age);
        frame = WriteBytes(frame, vertices, morphed, 1008);
        context = context.Write(4, BitConverter.GetBytes(vertices));
        for (int pass = 0; pass < 2; pass++)
        {
            NativeBattleEffect326Frame current = frame;
            NativeBattleDefaultActMeshStep drawn = NativeBattleEffect326MeshContext88.DrawLoaded(frame.Memory, resources.Mesh(0xE148E8), inputs.ReadProjection(frame), context, inputs.Colors, 2, offset => ReadBytes(current, checked(vertices + (uint)offset), 8));
            frame = frame with { Memory = drawn.Memory };
            context = drawn.Context88;
            frame = PublishContext(frame, temporary, context, 88);
            frame = inputs.Execute(frame, new NativeBattleEffect326DeliverBatch(drawn.Batch, 0x1D8E054));
        }
        frame = Release(frame, 88);
        return IncrementMeshClock(frame, address, inputs, 12);
    }
}
