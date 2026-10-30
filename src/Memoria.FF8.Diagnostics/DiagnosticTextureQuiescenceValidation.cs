using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects;

public static class DiagnosticTextureQuiescenceValidation
{
    public static void Validate(BattleDiagnosticTextureQuiescence observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (!observation.Complete || observation.Capacity != 16 || observation.Tasks.IsDefaultOrEmpty || observation.Tasks.Length > 16 || observation.PhysicalActiveCount != observation.Tasks.Length || observation.Head is not (>= 0 and < 16) || observation.Tail is not (>= 0 and < 16))
        {
            throw new InvalidDataException("Terminal texture observation requires its complete nonempty native auxiliary bank and linked owners.");
        }
        HashSet<int> visited = [];
        int? expected = observation.Head;
        foreach (BattleDiagnosticTextureTask row in observation.Tasks)
        {
            if (row is null || row.Slot is < 0 or >= 16 || row.Slot != expected || !visited.Add(row.Slot) || (row.Flags & 1) == 0 || row.Kind != NativeBattleAuxiliaryKind.TextureAnimation5057D0 || row.PayloadLength != 32 || (row.UnknownByteMask & 0x20F) != 0)
            {
                throw new InvalidDataException("Every reachable auxiliary row must be a known active terminal texture task; no foreign or orphan owner is admitted.");
            }
            if (row.ModelIndex is not (>= 0 and < 7) || row.ModelPointer != 0x1D972C0u + 156u * (uint)row.ModelIndex.Value || row.CapturedSequence is null || row.ProgramOffset is not >= 0 || row.ProgramLength is not > 0 || row.ProgramOffset >= row.ProgramLength || row.Deadline != 255 || row.ModelFlags0 is not ushort flags || (flags & 2) == 0 || row.ResourceFlags44 is not ushort resource || (resource & 0x40) != 0 || row.ModelSequence != row.CapturedSequence)
            {
                throw new InvalidDataException("Texture quiescence requires the actual FF programme byte and its unchanged loaded model sequence predicate.");
            }
            expected = row.Next;
        }
        if (expected is not null || observation.Tasks[^1].Slot != observation.Tail)
        {
            throw new InvalidDataException("Terminal texture observation does not close its complete actual native auxiliary chain.");
        }
    }
}