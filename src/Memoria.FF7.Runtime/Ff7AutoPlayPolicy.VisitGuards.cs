using System.Security.Cryptography;
using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed record Ff7AutoPlayVisitGuard(FieldVariableRead[] Reads, FieldHostRead[] HostReads, string Fingerprint);

public sealed partial class Ff7AutoPlayPolicy
{
    private void ObserveVisitGuard(Ff7AutoPlayObservation observation)
    {
        State.VisitGuards[observation.VisitId] = CaptureVisitGuard(observation.PersistentReads, observation.GlobalHostReads, session.Machine.Variables, session.Progress, session.Host.Process);
    }

    private bool NeedsVisitRefresh(AutoPlayVisit visit)
    {
        return NeedsVisitRefresh(State, visit, session.Machine.Variables, session.Progress, session.Host.Process);
    }

    internal static bool NeedsVisitRefresh(Ff7AutoPlayState state, AutoPlayVisit visit, FieldVariables variables, Ff7Progress progress, Ff7FieldProcess process)
    {
        AutoPlayNavigationVisit? navigation = state.Coverage.NavigationVisits.SingleOrDefault(item => item.Key == visit.Key);
        if (navigation is null)
        {
            return false;
        }
        if (!state.VisitGuards.TryGetValue(navigation.VisitKey, out Ff7AutoPlayVisitGuard? guard))
        {
            return true;
        }
        Ff7AutoPlayVisitGuard current = CaptureVisitGuard(guard.Reads, guard.HostReads, variables, progress, process);
        return current.Fingerprint != guard.Fingerprint;
    }

    internal static Ff7AutoPlayVisitGuard CaptureVisitGuard(IReadOnlyList<FieldVariableRead> reads, IReadOnlyList<FieldHostRead> hostReads, FieldVariables variables, Ff7Progress progress, Ff7FieldProcess process)
    {
        FieldVariableRead[] persistent = reads.Where(read => read.Bank is FieldBank.Persistent0Byte or FieldBank.Persistent0Word or FieldBank.Persistent1Byte or FieldBank.Persistent1Word or FieldBank.Persistent2Byte or FieldBank.Persistent2Word or FieldBank.Persistent3Byte or FieldBank.Persistent3Word or FieldBank.Persistent4Byte or FieldBank.Persistent4Word).Distinct().OrderBy(read => read.Bank).ThenBy(read => read.Operand).ThenBy(read => read.Word).ToArray();
        FieldHostRead[] global = hostReads.Where(read => read.Kind is FieldHostReadKind.Gil or FieldHostReadKind.ItemQuantity or FieldHostReadKind.PartyAvailable or FieldHostReadKind.MateriaCapacity).Distinct().OrderBy(read => read.Kind).ThenBy(read => read.Parameter).ThenBy(read => read.Selector).ToArray();
        Ff7AutoPlayHostConditions external = Ff7AutoPlayDiscovery.ReadHostConditions(global, progress, process);
        if (!external.Complete)
        {
            throw new NotSupportedException(external.Reason ?? "The saved visit's global dependencies cannot be read exactly.");
        }
        byte[] facts = JsonSerializer.SerializeToUtf8Bytes(new
        {
            Reads = persistent.Select(read => new { Read = read, Value = read.Word ? variables.ReadWord(read.Bank, read.Operand) : variables.ReadByte(read.Bank, checked((byte)read.Operand)) }).ToArray(),
            Host = external.Fingerprint
        });
        return new(persistent, global, Convert.ToHexString(SHA256.HashData(facts)));
    }
}
