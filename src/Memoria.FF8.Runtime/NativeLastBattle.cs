using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeLastBattle([property: JsonRequired] string OccurrenceId, [property: JsonRequired] ushort Formation, [property: JsonRequired] byte Result)
{
    internal void Validate(string[] appliedResults)
    {
        if (Formation >= 1024 || Result is < 1 or > 5 || !appliedResults.Contains(OccurrenceId, StringComparer.Ordinal))
        {
            throw new InvalidDataException("The last native battle must have a committed formation and result.");
        }
    }
}
