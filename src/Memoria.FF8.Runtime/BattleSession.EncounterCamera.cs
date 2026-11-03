namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private double EncounterCameraElevation => NativeGraphics?.OriginalEffect is null ? definition.EncounterMod?.Camera?.ElevationDegrees ?? 0 : 0;

    private NativeBattleCameraPose? EncounterPresentationCamera => NativeCamera?.CurrentPose is { } pose ? EncounterCameraPresentation.Elevate(pose, EncounterCameraElevation) : null;
}
