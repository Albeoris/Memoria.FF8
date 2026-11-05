using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Consumed mode4 route and logical transition timing. Current body placement and shared phase words remain actor-owned.</summary>
public sealed record GameplaySaveActorLadder([property: JsonRequired] long FiberSequence, [property: JsonRequired] int ScriptId, [property: JsonRequired] int Operation, [property: JsonRequired] bool Up, [property: JsonRequired] GameplaySaveActorPosition Entry, [property: JsonRequired] GameplaySaveActorPosition Approach, [property: JsonRequired] GameplaySaveActorPosition ClimbingEnd, [property: JsonRequired] GameplaySaveActorPosition Exit, [property: JsonRequired] short Duration, [property: JsonRequired] bool AutomaticCompletion, [property: JsonRequired] byte StartSelector, [property: JsonRequired] short StartDuration, [property: JsonRequired] byte EndSelector, [property: JsonRequired] short EndDuration)
{
    public void Validate()
    {
        if (FiberSequence < 0 || ScriptId < 0 || Operation < 0 || Entry is null || Approach is null || ClimbingEnd is null || Exit is null || Duration < -1 || StartDuration is < -1 or > 4093 || EndDuration is < -1 or > 4093 || StartSelector == EndSelector && StartDuration != EndDuration)
        {
            throw new InvalidDataException("Gameplay native mode4 ladder has invalid route, timing or script identity.");
        }
    }
}
