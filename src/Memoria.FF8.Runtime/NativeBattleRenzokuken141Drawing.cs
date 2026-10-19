namespace Memoria.FF8.Runtime;

public static partial class NativeBattleRenzokukenVariantController
{
    public static NativeBattleRenzokuken160ChildStep Advance61F590(NativeBattleRenzokuken160Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleRenzokuken160Inputs inputs, NativeBattleRenzokuken160Resources resources, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        NativeBattleRenzokukenVariantState state = ReadState(frame);
        if (resources.ImpactAxis.Length != 3 || resources.Meshes.Length < 3)
        {
            throw new NotSupportedException("Native61F590 requires its original alignment vector and static mesh.");
        }
        NativeBattleModulePoolTask task = pool.Slots[slot];
        NativeBattleModuleAxisAngle alignment = NativeBattleModuleAlignment.Between571480(resources.ImpactAxis[0], resources.ImpactAxis[1], resources.ImpactAxis[2], state.LateDirection.X, state.LateDirection.Y, state.LateDirection.Z);
        NativeBattleActionMatrix rotation = new(NativeBattleModuleAlignment.Rotation5714F0(alignment.Angle, alignment.Axis), 0, 0, 0, 4096);
        NativeBattleVector offset = NativeBattleGfMatrices.RotateShort56C4F0(rotation, new(-700, 0, -400));
        short scale = unchecked((short)task.ReadWord(28));
        NativeBattleActionMatrix world = NativeBattlePoseMatrices.Scale56BEF0(rotation, scale, scale, scale) with { X = state.LateOrigin.X + offset.X, Y = state.LateOrigin.Y, Z = state.LateOrigin.Z + offset.Z };
        NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.Compose56C2F0(view, world);
        short counter = unchecked((short)task.ReadWord(12));
        NativeBattleModuleMeshContext context = new(counter >= 6 ? 0xF3u : 0x33u, 0, counter >= 6 ? (counter - 6) << 9 : null, null, null, null, 17, 2);
        NativeBattleGfGeometryBatch packets = NativeBattleModulePackets.EmitBatch572200(resources.Meshes[2].Bytes, 0, default, matrix, projection, context, colors);
        frame = inputs.Execute(frame, new NativeBattleRenzokuken160MeshDraw(packets));
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return new(frame, pool, 0);
        }
        short speed = unchecked((short)task.ReadWord(30));
        task = task.WriteWord(28, unchecked((ushort)(task.ReadWord(28) + speed))).WriteWord(12, unchecked((ushort)(counter + 1))).WriteWord(30, unchecked((ushort)(speed - speed / 4)));
        return new(frame, pool with { Slots = pool.Slots.SetItem(slot, task) }, task.ReadWord(12) >= 14 ? 2 : 0);
    }
}
