using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>SavedDWORD1CFEFB4, original534AA0; independent of battle48F020, event52D210 and menu4C2560.</summary>
public sealed record NativeGlobalRandomState([property: JsonRequired] uint State)
{
    public NativeGlobalRandomState Draw(out ushort result)
    {
        uint next = unchecked(State * 69069u + 1u);
        result = (ushort)(next >> 17);
        return this with { State = next };
    }

    public NativeGlobalRandomState Advance(ulong calls)
    {
        uint state = State;
        uint multiplier = 69069;
        uint increment = 1;
        while (calls != 0)
        {
            if ((calls & 1) != 0)
            {
                state = unchecked(state * multiplier + increment);
            }
            increment = unchecked(increment * (multiplier + 1));
            multiplier = unchecked(multiplier * multiplier);
            calls >>= 1;
        }
        return this with { State = state };
    }
}

public sealed record NativeGlobalRandomChange(NativeGlobalRandomState Before, NativeGlobalRandomState After, ulong Calls)
{
    public void Validate()
    {
        if (Before is null || After is null || Before.Advance(Calls) != After)
        {
            throw new InvalidDataException("Native global random receipt contradicts its534AA0 call count and before/after DWORDs.");
        }
    }
}
