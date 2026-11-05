using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record GameplaySaveModelPartTransition([property: JsonRequired] short Counter, [property: JsonRequired] short StartX, [property: JsonRequired] short StartY, [property: JsonRequired] short StartZ, [property: JsonRequired] short Duration);

/// <summary>Script-selected translation relative to the selected part definition; completed interpolation bookkeeping is reconstructed.</summary>
public sealed record GameplaySaveModelPartOffset([property: JsonRequired] short TargetX, [property: JsonRequired] short TargetY, [property: JsonRequired] short TargetZ)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GameplaySaveModelPartTransition? Transition { get; init; }

    public void Validate()
    {
        if (Transition is { Duration: 0 })
        {
            throw new InvalidDataException("Gameplay model-part transition requires a nonzero native duration.");
        }
    }

    public static GameplaySaveModelPartOffset Capture(NativeModelPartTranslation source, NativeModelPartDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(definition);
        if (source.Mode is not (1 or 4) || source.Duration == 0)
        {
            throw new InvalidDataException("Gameplay model-part projection requires a supported logical translation.");
        }
        return new(unchecked((short)(source.TargetX - definition.TargetX)), unchecked((short)(source.TargetY - definition.TargetY)), unchecked((short)(source.TargetZ - definition.TargetZ)))
        {
            Transition = source.Mode == 1 ? new(source.Counter, unchecked((short)(source.StartX - definition.TargetX)), unchecked((short)(source.StartY - definition.TargetY)), unchecked((short)(source.StartZ - definition.TargetZ)), source.Duration) : null
        };
    }

    public NativeModelPartTranslation Restore(NativeModelPartDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        Validate();
        short targetX = unchecked((short)(definition.TargetX + TargetX));
        short targetY = unchecked((short)(definition.TargetY + TargetY));
        short targetZ = unchecked((short)(definition.TargetZ + TargetZ));
        return Transition is { } transition ? new(transition.Counter, unchecked((short)(definition.TargetX + transition.StartX)), unchecked((short)(definition.TargetY + transition.StartY)), unchecked((short)(definition.TargetZ + transition.StartZ)), targetX, targetY, targetZ, transition.Duration, 1) : new(2, targetX, targetY, targetZ, targetX, targetY, targetZ, 1, 4);
    }
}
