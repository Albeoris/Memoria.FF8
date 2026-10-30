using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleRenzokuken160Controller
{
    public static NativeBattleRenzokuken160ChildStep Advance600780(NativeBattleRenzokuken160Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleRenzokuken160Inputs inputs, NativeBattleRenzokuken160Resources resources, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        int primitiveIndex = 0;
        short counter = Word(task, 12);
        NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Euler56CD50(new(0, Word(task, 24), 0));
        NativeBattleActionMatrix world = NativeBattlePoseMatrices.Scale56BEF0(rotation, 6144, Word(task, 32), 6144);
        NativeBattleGraphicsModel actor = inputs.ReadModel(State(frame).Actor);
        NativeBattleActionMatrix body = actor.GeometryWorldMatrix ?? throw new NotSupportedException("Native600780 requires its retained model matrix64.");
        NativeBattleVector offset = NativeBattleGfMatrices.RotateShort56C4F0(body, new(0, 0, -850));
        world = world with { X = unchecked(Word(task, 16) + offset.X), Y = unchecked(Word(task, 18) + offset.Y), Z = unchecked(Word(task, 20) + offset.Z) };
        NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.Compose56C2F0(view, world);
        NativeBattleModuleMeshContext context = counter >= 6 ? new(0xC0, 0, 682 * (counter - 6), null, null, null, 17, 2) : new(0, null, null, null, null, null, 17, 2);
        Draw(0xDAB970, matrix, context);
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return new(frame, pool, 0);
        }
        short speed = Word(task, 34);
        task = task.WriteWord(32, unchecked((ushort)(task.ReadWord(32) + speed))).WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1))).WriteWord(34, unchecked((ushort)(speed - (speed >> 2))));
        return new(frame, pool.WriteSlot(slot, task), task.ReadWord(12) >= 12 ? 2 : 0);

        void Draw(uint address, NativeBattleActionMatrix basis, NativeBattleModuleMeshContext parameters)
        {
            NativeBattleStaticMeshResource resource = resources.Meshes.Single(mesh => mesh.Address == address);
            NativeBattleGfGeometryBatch packets = NativeBattleModulePackets.EmitBatch572200(resource.Bytes, 0, default, basis, projection, parameters, colors, pilotIdentity: inputs.CapturePresentation ? NativeBattleRenzokukenVisualLifetimes.Task(frame.Effect, task, resource.Address, primitiveIndex++) : null);
            frame = inputs.Execute(frame, new NativeBattleRenzokuken160MeshDraw(packets));
        }
    }

    public static NativeBattleRenzokuken160ChildStep Advance6004A0(NativeBattleRenzokuken160Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleRenzokuken160Inputs inputs, NativeBattleRenzokuken160Resources resources, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        int primitiveIndex = 0;
        int impact = Word(task, 14) * 156;
        byte[] memory = State(frame).ImpactStates.ToArray();
        if (impact < 0 || impact > memory.Length - 156 || resources.ImpactAxis.IsDefault || resources.ImpactAxis.Length != 3)
        {
            throw new NotSupportedException("Native6004A0 requires its original contact-state record and constant alignment vector.");
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
            throw new InvalidDataException("Native6004A0 contact rays exceed their16-entry physical arrays.");
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
            NativeBattleGfGeometryBatch packets = NativeBattleModulePackets.EmitBatch572200(resource.Bytes, 0, default, basis, projection, parameters, colors, pilotIdentity: inputs.CapturePresentation ? NativeBattleRenzokukenVisualLifetimes.Task(frame.Effect, task, resource.Address, primitiveIndex++) : null);
            frame = inputs.Execute(frame, new NativeBattleRenzokuken160MeshDraw(packets));
        }
    }

    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
}
