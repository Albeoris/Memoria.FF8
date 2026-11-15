using System.Numerics;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7Session
{
    private Ff7NativeModelPresentation ReadNativeModelPresentation(int field, Ff7FieldProcess process, int slot)
    {
        Ff7NativeModelPresentation configured = executable.NativePresentation.Model(field, slot);
        Ff7NativePartyPresentationFilter? filter = executable.NativePresentation.PartyFilter(field);
        if (filter is null || slot > filter.LastSlotInclusive)
        {
            return configured;
        }
        byte[] mask = process.NativePartyPresentationMask ?? throw new InvalidDataException("Native party presentation requires its post-initialization mask publication.");
        if (mask.Length != filter.LastSlotInclusive + 1)
        {
            throw new InvalidDataException("Saved native party presentation differs from its configured slot range.");
        }
        return configured with { Mask = mask[slot] };
    }

    private void InitializeNativePartyPresentation(bool restoring = false)
    {
        Ff7NativePartyPresentationFilter? filter = executable.NativePresentation.PartyFilter(MapId);
        if (filter is null)
        {
            return;
        }
        if (Host.Process.NativePartyPresentationMask is { } saved)
        {
            if (saved.Length != filter.LastSlotInclusive + 1)
            {
                throw new InvalidDataException("Saved native party presentation differs from its configured slot range.");
            }
            return;
        }
        int[] scriptParty = Enumerable.Range(9, 3).Select(address => (int)Machine.Variables.ReadByte(Memoria.FF7.Events.FieldBank.Persistent1Byte, (byte)address)).ToArray();
        Host.PublishNativePartyPresentationMask(Ff7NativePartyPresentation.Create(filter.LastSlotInclusive, scriptParty, Machine.Capture().Entities, Host.Process.Actors));
        if (restoring && Host.Process.Tick > 0)
        {
            Console.Error.WriteLine("FFVII compatibility: legacy party presentation was reset from the saved script party and allocated models; native progress, RNG and script PCs are preserved.");
        }
    }

    /// <summary>Reads configuration by the original allocated model slot, independently of its resource descriptor index.</summary>
    public Ff7NativeModelPresentation GetModelPresentation(int slot) => (Host.NativeModelConfiguration ?? throw new NotSupportedException("Native model presentation requires its selected field configuration."))(slot);

    /// <summary>Returns the quantized native presentation endpoint. Interpolate copies of these endpoints, not logical actors.</summary>
    public Vector3 NativeVisualPosition(Ff7Actor actor) => Ff7NativeModelPosition.PresentationPoint(actor, GetModelPresentation(actor.Slot));

    public Ff7Actor PrepareActorPresentation(Ff7Actor actor) => Ff7NativeModelPosition.PrepareActorPresentation(actor, GetModelPresentation(actor.Slot));
}
