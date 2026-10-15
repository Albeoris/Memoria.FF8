namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    private static void ValidateJunctionPartyBackupAliases(NativeEventMemory memory)
    {
        if (memory.Cells.Any(pair => pair.Key < 194 && (long)pair.Key + pair.Value.Width > 188))
        {
            throw new InvalidDataException("Canonical JUNCTION backup bytes188..193 overlap a separate root memory alias; recreate authentic inputs.");
        }
    }
}
