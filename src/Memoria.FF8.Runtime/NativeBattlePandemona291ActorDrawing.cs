namespace Memoria.FF8.Runtime;

public static class NativeBattlePandemona291ActorDrawing
{
    public static NativeBattleActionMatrix Prepare68D1D0(NativeBattleActionMatrix world, NativeBattleActionMatrix view, NativeBattleVector angles, NativeBattleVector pivot, NativeBattleVector scale)
    {
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(angles) with { X = pivot.X, Y = pivot.Y, Z = pivot.Z };
        NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(world, local);
        composed = NativeBattlePoseMatrices.Scale56BEF0(composed, scale.X, scale.Y, scale.Z);
        NativeBattleActionMatrix camera = NativeBattleGfMatrices.Compose56C2F0(view, composed);
        (int x, int y, int z) = NativeBattleGfMatrices.Rotate56C3D0(camera, pivot);
        return camera with { X = unchecked(camera.X - x), Y = unchecked(camera.Y - y), Z = unchecked(camera.Z - z) };
    }
}
