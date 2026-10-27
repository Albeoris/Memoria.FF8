using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleRenzokuken159Controller
{
    public static NativeBattleRenzokuken159ChildStep Advance602670(NativeBattleRenzokuken159Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleRenzokuken159Inputs inputs, NativeBattleRenzokuken159Resources resources, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors) => DrawBurst(frame, pool, slot, inputs, resources.Meshes[2], view, projection, colors, 682, 10, 4, false);

    public static NativeBattleRenzokuken159ChildStep Advance6027C0(NativeBattleRenzokuken159Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleRenzokuken159Inputs inputs, NativeBattleRenzokuken159Resources resources, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors) => DrawBurst(frame, pool, slot, inputs, resources.Meshes[4], view, projection, colors, 1024, 8, 6, false, 2048);

    public static NativeBattleRenzokuken159ChildStep Advance602910(NativeBattleRenzokuken159Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleRenzokuken159Inputs inputs, NativeBattleRenzokuken159Resources resources, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors) => DrawBurst(frame, pool, slot, inputs, resources.Meshes[5], view, projection, colors, 512, 12, 3, false);

    public static NativeBattleRenzokuken159ChildStep Advance602A50(NativeBattleRenzokuken159Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleRenzokuken159Inputs inputs, NativeBattleRenzokuken159Resources resources, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors) => DrawBurst(frame, pool, slot, inputs, resources.Meshes[6], view, projection, colors, 682, 10, 3, true);

    private static NativeBattleRenzokuken159ChildStep DrawBurst(NativeBattleRenzokuken159Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleRenzokuken159Inputs inputs, NativeBattleStaticMeshResource mesh, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, int fadeStep, int lifetime, int divisor, bool delayed, int? depthScale = null)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        bool advance = (inputs.GraphicsFlags & 0x201) == 0;
        if (delayed && Word(task, 14) > 0)
        {
            if (advance)
            {
                task = task.WriteWord(14, unchecked((ushort)(Word(task, 14) - 1)));
            }
            return new(frame, pool.WriteSlot(slot, task), 0);
        }
        short counter = Word(task, 12);
        int scale = Word(task, 28);
        NativeBattleActionMatrix world = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CD50(new(0, 0, 0)), scale, delayed ? Word(task, 32) : scale, depthScale ?? scale) with { X = Word(task, 16), Y = Word(task, 18), Z = Word(task, 20) };
        NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.Compose56C2F0(view, world);
        NativeBattleModuleMeshContext context = new(counter >= 4 ? 0xF3u : 0x33u, counter >= 4 ? 0u : null, counter >= 4 ? fadeStep * (counter - 4) : null, null, null, null, 17, 2);
        NativeBattleGfGeometryBatch packets = NativeBattleModulePackets.EmitBatch572200(mesh.Bytes, 0, default, matrix, projection, context, colors);
        frame = inputs.Execute(frame, new NativeBattleRenzokuken159MeshDraw(packets));
        if (!advance)
        {
            return new(frame, pool, 0);
        }
        short speed = Word(task, 30);
        task = task.WriteWord(28, unchecked((ushort)(scale + speed))).WriteWord(30, unchecked((ushort)(speed - speed / divisor))).WriteWord(12, unchecked((ushort)(counter + 1)));
        if (delayed)
        {
            short verticalSpeed = Word(task, 34);
            task = task.WriteWord(32, unchecked((ushort)(Word(task, 32) + verticalSpeed))).WriteWord(34, unchecked((ushort)(verticalSpeed - verticalSpeed / divisor)));
        }
        return new(frame, pool.WriteSlot(slot, task), task.ReadWord(12) >= lifetime ? 2 : 0);
    }

    public static NativeBattleRenzokuken159ChildStep Advance602390(NativeBattleRenzokuken159Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleRenzokuken159Inputs inputs, NativeBattleRenzokuken159Resources resources, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        int impact = Word(task, 14) * 156;
        byte[] memory = State(frame).ImpactStates.ToArray();
        if (impact < 0 || impact > memory.Length - 156 || resources.ImpactAxis.IsDefault || resources.ImpactAxis.Length != 3)
        {
            throw new NotSupportedException("Native602390 requires its original contact-state record and constant alignment vector.");
        }
        int dx = BinaryPrimitives.ReadInt32LittleEndian(memory.AsSpan(impact + 8));
        int dy = BinaryPrimitives.ReadInt32LittleEndian(memory.AsSpan(impact + 12));
        int dz = BinaryPrimitives.ReadInt32LittleEndian(memory.AsSpan(impact + 16));
        NativeBattleModuleAxisAngle alignment = NativeBattleModuleAlignment.Between571480(resources.ImpactAxis[0], resources.ImpactAxis[1], resources.ImpactAxis[2], dx, dy, dz);
        NativeBattleActionMatrix oriented = new(NativeBattleModuleAlignment.Rotation5714F0(alignment.Angle, alignment.Axis), Read(memory, impact), Read(memory, impact + 2), Read(memory, impact + 4), 4096);
        NativeBattleActionMatrix parent = NativeBattlePoseMatrices.Compose56C2F0(view, oriented);
        short counter = Word(task, 12);
        uint flags = counter >= 2 ? 0xF0u : 0x30u;
        uint? far = counter >= 2 ? 0u : null;
        int? fade = counter >= 2 ? 682 * (counter - 2) : null;
        NativeBattleActionMatrix local = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CD50(new(0, 0, 0)), Word(task, 28), Word(task, 28), 4096);
        Draw(resources.Meshes[0].Address, NativeBattlePoseMatrices.Compose56C2F0(parent, local), new(flags, far, fade, null, null, null, 17, 2));
        int count = BinaryPrimitives.ReadInt32LittleEndian(memory.AsSpan(impact + 24));
        if (count is < 0 or > 16)
        {
            throw new InvalidDataException("Native602390 contact rays exceed their16-entry physical arrays.");
        }
        for (int index = 0; index < count; index++)
        {
            short angle = Read(memory, impact + 28 + index * 2);
            short radius = Read(memory, impact + 92 + index * 2);
            NativeBattleActionMatrix ray = NativeBattleGfMatrices.Euler56CD50(new(0, 0, angle));
            (int x, int y, int z) = NativeBattleGfMatrices.Rotate56C3D0(ray, new(0, radius, 0));
            short scale = Read(memory, impact + 60 + index * 2);
            ray = NativeBattlePoseMatrices.Scale56BEF0(ray, scale + (scale >> 1), scale, 4096) with { X = x, Y = y, Z = z };
            Draw(resources.Meshes[1].Address, NativeBattlePoseMatrices.Compose56C2F0(parent, ray), new(flags | 3u, far, fade, null, null, null, 17, 2));
            if ((inputs.GraphicsFlags & 0x201) == 0)
            {
                int velocityOffset = impact + 124 + index * 2;
                short speed = Read(memory, velocityOffset);
                Write(memory, impact + 92 + index * 2, radius + speed);
                Write(memory, velocityOffset, speed - (speed >> 4));
            }
        }
        frame = WriteState(frame, State(frame) with { ImpactStates = memory.ToImmutableArray() });
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return new(frame, pool, 0);
        }
        short expansion = Word(task, 30);
        task = task.WriteWord(28, unchecked((ushort)(task.ReadWord(28) + expansion))).WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1))).WriteWord(30, unchecked((ushort)(expansion - expansion / 6)));
        return new(frame, pool.WriteSlot(slot, task), task.ReadWord(12) >= 8 ? 2 : 0);

        void Draw(uint address, NativeBattleActionMatrix basis, NativeBattleModuleMeshContext parameters)
        {
            NativeBattleStaticMeshResource resource = resources.Meshes.Single(mesh => mesh.Address == address);
            NativeBattleGfGeometryBatch packets = NativeBattleModulePackets.EmitBatch572200(resource.Bytes, 0, default, basis, projection, parameters, colors);
            frame = inputs.Execute(frame, new NativeBattleRenzokuken159MeshDraw(packets));
        }
    }

    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
}
