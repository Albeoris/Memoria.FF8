using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Logical actor-camera selection and interpolation phase; native table selectors derive from the current program and retained actors.</summary>
public sealed record GameplaySaveFieldActorScroll([property: JsonRequired] int ObjectId, [property: JsonRequired] int? PartySlot, [property: JsonRequired] int Layer, [property: JsonRequired] byte Mode, [property: JsonRequired] byte Status, [property: JsonRequired] short Duration, [property: JsonRequired] short Counter, [property: JsonRequired] short? X, [property: JsonRequired] short? Y, [property: JsonRequired] short? PreviousX, [property: JsonRequired] short? PreviousY, [property: JsonRequired] short? ProjectedTargetX, [property: JsonRequired] short? ProjectedTargetY)
{
    public void Validate()
    {
        if (ObjectId < 0 || PartySlot is < 0 or > 2 || Layer is < 0 or >= 8 || Mode > 2 || Status > 2 || Mode == 0 && Status > 1 || Mode != 0 && Duration < 0 || X.HasValue != Y.HasValue || PreviousX.HasValue != PreviousY.HasValue || ProjectedTargetX.HasValue != ProjectedTargetY.HasValue)
        {
            throw new InvalidDataException("Gameplay actor-camera continuation has an invalid logical selector or phase.");
        }
    }
}
