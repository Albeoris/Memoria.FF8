using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Search's ordinary mesh particle submission into the caller's common packet buffer.</summary>
public static class NativeBattleSearchMeshDrawing
{
    public static NativeBattleGfGeometryBatch Draw755FC0(NativeBattleModulePoolTask task, NativeBattleStaticMeshResource mesh, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        if (task.ReadDword(0x4C) != mesh.Address)
        {
            throw new InvalidDataException("Native Search mesh particle changed its retained resource owner.");
        }
        if (task.ReadDword(0x40) != 0 || task.ReadWord(0x50) != 0 || task.ReadWord(0x52) != 0)
        {
            throw new NotSupportedException("Native Search mesh particle requires its initialized zero far-color, fade and depth-bias owners.");
        }
        NativeBattleTextureWindow window = new(0, 0, 256, 256);
        return NativeBattleRestorationMeshPackets.EmitBatch88E240(mesh.Bytes, ComposeMatrix(task, view), projection, new(0xF0, 0, 0, 0, 0, 0, window, window, 17, 2), colors);
    }

    internal static NativeBattleActionMatrix ComposeMatrix(NativeBattleModulePoolTask task, NativeBattleActionMatrix view)
    {
        view.Validate();
        NativeBattleActionMatrix local = new(Enumerable.Range(0, 9).Select(index => view.Rotation[index % 3 * 3 + index / 3]).ToImmutableArray(), 0, 0, 0, 4096);
        local = NativeBattleCuragaMatrices.RotateZ8DD960(local, unchecked((short)task.ReadWord(0x48)));
        local = NativeBattlePoseMatrices.Scale56BEF0(local, unchecked((int)task.ReadDword(0x30)), unchecked((int)task.ReadDword(0x34)), unchecked((int)task.ReadDword(0x38))) with { X = unchecked((short)task.ReadWord(0x1C)), Y = unchecked((short)task.ReadWord(0x1E)), Z = unchecked((short)task.ReadWord(0x20)) };
        return NativeBattlePoseMatrices.Compose56C2F0(view, local);
    }
}
