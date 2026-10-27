using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleChoco99SecondaryResources(NativeBattleStaticMeshResource File480, NativeBattleStaticMeshResource File481);
public sealed record NativeBattleChoco99SecondaryStep(NativeBattleModulePoolTask Task, NativeBattleGfGeometryBatch Packets, bool ReadyToComplete);

public static class NativeBattleChoco99Secondary
{
    public static NativeBattleChoco99SecondaryStep Advance7211D0(NativeBattleModulePoolTask task, NativeBattleStaticMeshResource mesh, short contextMode40, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors) => Advance(task, mesh, contextMode40, view, projection, colors, false);

    public static NativeBattleChoco99SecondaryStep Advance721390(NativeBattleModulePoolTask task, NativeBattleStaticMeshResource mesh, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors) => Advance(task, mesh, 0, view, projection, colors, true);

    private static NativeBattleChoco99SecondaryStep Advance(NativeBattleModulePoolTask task, NativeBattleStaticMeshResource mesh, short contextMode40, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, bool timed)
    {
        if (task.PayloadLength != 0x58 - 12 || task.Callback != (timed ? 0x721390u : 0x7211D0u) || mesh.Address != (timed ? 0x19DE510u : 0x19DD2D0u) || mesh.Bytes.IsDefault || mesh.Bytes.Length != (timed ? 0xE70 : 0x1240))
        {
            throw new InvalidDataException("Native Choco secondary requires its actual task and current IO mesh owner.");
        }
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                task = task.WriteDword(0x4C, mesh.Address).WriteDword(0x30, 4096).WriteDword(0x34, timed ? 4096u : 8192u).WriteDword(0x38, 4096).WriteWord(0x1E, timed ? (ushort)0 : (ushort)5120).WriteByte(0x29, 1);
                if (timed)
                {
                    task = task.WriteWord(0x54, 16);
                }
                else
                {
                    task = task.WriteWord(0x20, 8192);
                }
                break;
            case 1:
                if (timed)
                {
                    task = task.WriteWord(0x54, unchecked((ushort)(task.ReadWord(0x54) - 1)));
                }
                if (timed ? unchecked((short)task.ReadWord(0x54)) <= 0 : contextMode40 >= 10)
                {
                    task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)).WriteByte(0x29, 2);
                }
                break;
            case 2:
                break;
            default:
                throw new NotSupportedException("Native Choco secondary phase leaves its physical caller.");
        }
        NativeBattleGfGeometryBatch packets = Draw721230(task, mesh, view, projection, colors);
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        return new(task, packets, (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0);
    }

    public static NativeBattleGfGeometryBatch Draw721230(NativeBattleModulePoolTask task, NativeBattleStaticMeshResource mesh, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        if ((task.ReadByte(0x26) & 4) != 0)
        {
            return new([], 0);
        }
        if (task.ReadDword(0x4C) != mesh.Address)
        {
            throw new InvalidDataException("Native Choco secondary mesh differs from its retained resource pointer.");
        }
        NativeBattleModuleMeshContext context = new(0xF0, task.ReadDword(0x40), unchecked((short)task.ReadWord(0x50)), null, null, null, 17, 2);
        return NativeBattleModulePackets.EmitBatch572200(mesh.Bytes, 0, default, ComposeMatrix(task, view), projection, context, colors);
    }

    public static NativeBattleActionMatrix ComposeMatrix(NativeBattleModulePoolTask task, NativeBattleActionMatrix view)
    {
        NativeBattleActionMatrix world = NativeBattleCuragaMatrices.Identity8DD770();
        world = NativeBattleCuragaMatrices.RotateY8DD8A0(world, unchecked((short)task.ReadWord(0x46)));
        world = NativeBattleCuragaMatrices.RotateX8DD7E0(world, unchecked((short)task.ReadWord(0x44)));
        world = NativeBattleCuragaMatrices.RotateZ8DD960(world, unchecked((short)task.ReadWord(0x48)));
        world = NativeBattlePoseMatrices.Scale56BEF0(world, unchecked((int)task.ReadDword(0x30)), unchecked((int)task.ReadDword(0x34)), unchecked((int)task.ReadDword(0x38)));
        world = world with { X = unchecked((short)task.ReadWord(0x1C)), Y = unchecked((short)task.ReadWord(0x1E)), Z = unchecked((short)task.ReadWord(0x20)) };
        return NativeBattlePoseMatrices.Compose56C2F0(view, world);
    }
}
