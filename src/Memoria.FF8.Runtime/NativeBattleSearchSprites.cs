using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Search's flags-zero projected sprite adapter, preserving its rounded view/transposed-view basis and signed depth bias.</summary>
public static class NativeBattleSearchSprites
{
    public static NativeBattleGfGeometryBatch Draw7582A0(NativeBattleModulePoolTask task, ImmutableArray<byte> resource, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, short cameraRoll)
    {
        if ((task.ReadByte(0x26) & 4) != 0)
        {
            return new([], 0);
        }
        return NativeBattleCuragaProjectedSprites.EmitBatch8DD260(resource, task.ReadWord(0x50), Matrix(task, view), projection, cameraRoll, 17, 2, unchecked((short)task.ReadWord(0x56)));
    }

    internal static NativeBattleActionMatrix Matrix(NativeBattleModulePoolTask task, NativeBattleActionMatrix view)
    {
        view.Validate();
        NativeBattleActionMatrix transposed = new([view.Rotation[0], view.Rotation[3], view.Rotation[6], view.Rotation[1], view.Rotation[4], view.Rotation[7], view.Rotation[2], view.Rotation[5], view.Rotation[8]], 0, 0, 0, 4096);
        NativeBattleActionMatrix basis = NativeBattlePoseMatrices.MultiplyGteRotation45E470(view, transposed);
        (int x, int y, int z) = NativeBattleGfMatrices.RotateLong56C600(view, unchecked((short)task.ReadWord(0x1C)), unchecked((short)task.ReadWord(0x1E)), unchecked((short)task.ReadWord(0x20)));
        return basis with { X = unchecked(x + view.X), Y = unchecked(y + view.Y), Z = unchecked(z + view.Z) };
    }
}
