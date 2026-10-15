using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>The saved event BYTE204 and process BYTE1CFDCA0, assigned together by52CA90/523390.</summary>
public sealed record NativeDiscProcessState([property: JsonRequired] int Version, [property: JsonRequired] string Rule, [property: JsonRequired] byte SavedDisc204, [property: JsonRequired] byte CurrentDiscIndex)
{
    public const string CurrentRule = "ff8-en-52ca90-523390-disc-v1";

    public static NativeDiscProcessState CreateNewGame()
    {
        //52CA90(1) clears the248-byte event bank, writes BYTE204=1, then assigns BYTE204-1.
        return new NativeDiscProcessState(1, CurrentRule, 1, 0);
    }

    public NativeDiscProcessState Assign(byte disc)
    {
        Validate();
        return this with { SavedDisc204 = disc, CurrentDiscIndex = unchecked((byte)(disc - 1)) };
    }

    public void Validate()
    {
        if (Version != 1 || Rule != CurrentRule || CurrentDiscIndex != unchecked((byte)(SavedDisc204 - 1)))
        {
            throw new InvalidDataException("Native disc state requires the saved byte and its actual zero-based process assignment.");
        }
    }
}
