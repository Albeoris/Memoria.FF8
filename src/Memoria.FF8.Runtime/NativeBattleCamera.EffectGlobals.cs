namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleCameraState
{
    /// <summary>Effect callbacks write Eye/Target, scratch focalWORD1D8E038 and rollWORD1D977A2 before normal504060/5033E0 publication.</summary>
    public NativeBattleCameraState WriteEffectGlobals(NativeBattleCameraPose pose)
    {
        ArgumentNullException.ThrowIfNull(pose);
        Validate();
        return this with { CurrentPose = pose, FocalLength = pose.FocalLength, Roll = pose.Roll };
    }
}
