using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Retained save data for Garden callbacks. Transfer admission reads the shared world input buffer.</summary>
public sealed record NativeGardenButtonHistory([property: JsonRequired] int Version, [property: JsonRequired] string Rule, [property: JsonRequired] uint? LastObservedButtons)
{
    public const string CurrentRule = "ff8-en-garden-input-54a7f0-observed-levels-v1";

    public static NativeGardenButtonHistory UnknownAtLoad() => new(1, CurrentRule, null);

    public void Validate()
    {
        if (Version != 1 || Rule != CurrentRule)
        {
            throw new InvalidDataException("Garden input requires its explicit retained button-history owner.");
        }
    }

    public NativeGardenButtonHistory Observe(uint buttons)
    {
        Validate();
        return this with { LastObservedButtons = buttons };
    }
}
