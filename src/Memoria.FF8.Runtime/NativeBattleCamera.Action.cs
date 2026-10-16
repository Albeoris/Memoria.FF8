using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleCameraState
{
    /// <summary>BYTE2(1D97718), initialized to1 by5041E0, separate from the lowWORD task mask.</summary>
    [JsonRequired]
    public byte? Mode { get; init; }

    public NativeBattleCameraState SetActionSelector(ushort selector) => this with { Variables = Variables.SetItem(0, unchecked((short)selector)) };

    public NativeBattleCameraState SetSecondarySelector(ushort selector) => this with { Variables = Variables.SetItem(1, unchecked((short)selector)) };

    public NativeBattleCameraState CompleteAction()
    {
        byte mode = Mode ?? throw new NotSupportedException("Native50AED0 requires the original camera mode BYTE2(1D97718).");
        bool reset = false;
        if (mode != 3)
        {
            reset = Mask != 0 || (ForceCamera ?? throw new NotSupportedException("Native50AED0 requires the original1D97704 forced-camera bit."));
        }
        return this with { Mask = reset ? (ushort)0 : Mask, BlendProgress = reset ? (ushort)4096 : BlendProgress, ForceCamera = false };
    }
}
