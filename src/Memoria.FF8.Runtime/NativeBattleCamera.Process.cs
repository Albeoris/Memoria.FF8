using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleCameraState
{
    /// <summary>Authenticated PE.data eye/target globals and zero-filled camera/GTE/screen-offset scratch before any native callback.</summary>
    public static NativeBattleCameraState FromColdProcess()
    {
        NativeBattleCameraPose pose = new(new(10000, -3000, 5000), new(0, -500, 0), 0, 0);
        NativeBattleActionMatrix view = new(Enumerable.Repeat((short)0, 9).ToImmutableArray(), 0, 0, 0, 4096);
        return Initialize() with { CurrentPose = pose, FocalLength = 0, Projection = new(view, 0, 0, 0), ProjectionOffsetsCleared = true };
    }

    /// <summary>5041E0 resets the two task slots and control globals, retaining the camera eye/target/focal and published projection registers.</summary>
    public static NativeBattleCameraState Initialize5041E0(NativeBattleCameraState? retained)
    {
        retained?.Validate();
        return Initialize() with { CurrentPose = retained?.CurrentPose is { } pose ? pose with { Roll = 0 } : null, FocalLength = retained?.FocalLength, Projection = retained?.Projection, ProjectionOffsetsCleared = retained?.ProjectionOffsetsCleared ?? false };
    }

    /// <summary>500400 calls56CD00(216) twice. It does not write screen offsets or the prior camera matrix.</summary>
    public NativeBattleCameraState Initialize500400() => this with { Projection = Projection is { } projection ? projection with { FocalLength = 216 } : new(null, 216, null, null) };
}
