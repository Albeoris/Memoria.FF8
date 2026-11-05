using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Consumed native turn operands and the remaining inclusive sampling phase. Completed turn history is not retained.</summary>
public sealed record GameplaySaveActorTurn([property: JsonRequired] byte Kind, [property: JsonRequired] byte Start, [property: JsonRequired] short Target, [property: JsonRequired] byte Duration, [property: JsonRequired] ushort Remaining)
{
    public void Validate()
    {
        if (Kind is not (1 or 2 or 3) || (Kind == 3 ? Duration == 0 ? Remaining != 0 : Remaining < 1 || Remaining > Duration : Remaining < 1 || Remaining > Duration + 1))
        {
            throw new InvalidDataException("Gameplay native body turn has an invalid interpolation kind or remaining phase.");
        }
    }
}
