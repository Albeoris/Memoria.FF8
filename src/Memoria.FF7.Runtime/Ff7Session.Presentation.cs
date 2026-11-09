using System.Numerics;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7Session
{
    /// <summary>Reads configuration by the original allocated model slot, independently of its resource descriptor index.</summary>
    public Ff7NativeModelPresentation GetModelPresentation(int slot) => (Host.NativeModelConfiguration ?? throw new NotSupportedException("Native model presentation requires its selected field configuration."))(slot);

    /// <summary>Returns the quantized native presentation endpoint. Interpolate copies of these endpoints, not logical actors.</summary>
    public Vector3 NativeVisualPosition(Ff7Actor actor) => Ff7NativeModelPosition.PresentationPoint(actor, GetModelPresentation(actor.Slot));

    public Ff7Actor PrepareActorPresentation(Ff7Actor actor) => Ff7NativeModelPosition.PrepareActorPresentation(actor, GetModelPresentation(actor.Slot));
}
