namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleCameraState
{
    /// <summary>50A730 sets bit15 of the camera control DWORD and clears the separate task-mask WORD.</summary>
    public NativeBattleCameraState Apply50A730() => this with { ForceCamera = true, Mask = 0 };
}
