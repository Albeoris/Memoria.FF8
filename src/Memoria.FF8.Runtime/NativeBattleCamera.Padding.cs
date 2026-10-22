using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleCameraState
{
    public short? EyePaddingF6 { get; init; }
    public short? TargetPaddingFE { get; init; }
    public short? CapturedEyePadding806 { get; init; }
    public short? CapturedTargetPadding80E { get; init; }
    public ImmutableArray<short?> TrackEyePadding { get; init; } = [null, null];
    public ImmutableArray<short?> TrackTargetPadding { get; init; } = [null, null];

    /// <summary>503300 copies both eight-byte vectors, including their separately retained fourth WORDs.</summary>
    public NativeBattleCameraState Capture503300()
    {
        return this with
        {
            CapturedPose = CurrentPose is { } pose ? pose with { FocalLength = FocalLength, Roll = Roll } : null,
            PoseCaptureCount = checked(PoseCaptureCount + 1),
            CapturedEyePadding806 = EyePaddingF6,
            CapturedTargetPadding80E = TargetPaddingFE
        };
    }
}
