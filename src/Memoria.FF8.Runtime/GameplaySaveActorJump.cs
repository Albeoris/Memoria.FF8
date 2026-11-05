using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Consumed native ballistic operands and their held script owner. Current position and shared completion/elapsed words belong to the actor.</summary>
public sealed record GameplaySaveActorJump([property: JsonRequired] bool Active, [property: JsonRequired] long FiberSequence, [property: JsonRequired] int ScriptId, [property: JsonRequired] int Operation, [property: JsonRequired] GameplaySaveActorPosition Start, [property: JsonRequired] GameplaySaveActorPosition Target, [property: JsonRequired] short Duration, [property: JsonRequired] int Velocity, [property: JsonRequired] bool? AsyncFlag, [property: JsonRequired] bool? AnimationFlag, [property: JsonRequired] ushort LandingPlane)
{
    public void Validate()
    {
        if (FiberSequence < 0 || ScriptId < 0 || Operation < 0 || Start is null || Target is null || Duration <= 0)
        {
            throw new InvalidDataException("Gameplay native jump has invalid consumed operands or script identity.");
        }
    }
}
