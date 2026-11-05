using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Consumed native mode3 traversal and its independent renderer phase; current pose and shared elapsed/completion words belong to the actor.</summary>
public sealed record GameplaySaveActorLadderMode3([property: JsonRequired] long FiberSequence, [property: JsonRequired] int ScriptId, [property: JsonRequired] int Operation, [property: JsonRequired] bool Up, [property: JsonRequired] GameplaySaveActorPosition Entry, [property: JsonRequired] GameplaySaveActorPosition Target, [property: JsonRequired] short Duration, [property: JsonRequired] byte InitializationSpeed, [property: JsonRequired] ushort RendererFrame)
{
    public void Validate()
    {
        if (FiberSequence < 0 || ScriptId < 0 || Operation < 0 || Entry is null || Target is null || Duration < 0 || InitializationSpeed == 0)
        {
            throw new InvalidDataException("Gameplay native mode3 ladder has invalid traversal operands or script identity.");
        }
    }
}
