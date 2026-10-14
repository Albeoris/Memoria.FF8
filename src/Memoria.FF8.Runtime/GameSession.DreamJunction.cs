namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    private static void ValidateJunctionPartyBackupAliases(IReadOnlyDictionary<int, int> variables, IReadOnlyDictionary<int, NativeMemoryValue>? memory)
    {
        if (variables.Keys.Any(address => address < 194 && (long)address + 4 > 188) || memory?.Any(pair => pair.Key < 194 && (long)pair.Key + pair.Value.Width > 188) == true)
        {
            throw new InvalidDataException("Canonical JUNCTION backup bytes188..193 overlap a separate root memory alias; recreate authentic inputs.");
        }
    }
}
