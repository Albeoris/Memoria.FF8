using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

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

    public NativeGardenTransferAdmission Evaluate(NativeGardenTransferInput input)
    {
        Validate();
        if (LastObservedButtons is uint previous)
        {
            return NativeGardenTransferState.EvaluateInput(input with { PreviousButtons = previous });
        }
        if ((input.CurrentButtons & 0x42) != 0)
        {
            throw new NotSupportedException("Garden action/view edge requires the preceding actual button level; release both inputs for an ordinary callback first.");
        }
        // Both relevant current bits are zero, so every possible previous
        // DWORD gives the same absent edges. This does not assert a prior0.
        return NativeGardenTransferState.EvaluateInput(input with { PreviousButtons = input.CurrentButtons });
    }

    public NativeGardenButtonHistory Observe(uint buttons)
    {
        Validate();
        return this with { LastObservedButtons = buttons };
    }
}
