using System.Collections.Immutable;
using System.Security.Cryptography;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;

namespace Memoria.FF8;

internal sealed record AutoPlayVisitGuard(int Version, int FieldId, string ProgramHash, ImmutableArray<NativePersistentPredicateRead> Reads, string Fingerprint)
{
    internal const int CurrentVersion = 3;
}

internal sealed partial class GameWorld
{
    private byte[]? autoPlayConditionSource;
    private JsmProgram? autoPlayConditionProgram;
    private string? autoPlayConditionProgramHash;
    private int[] autoPlayInitializationConditionReads = [];
    private readonly Dictionary<(string Event, int Object), int[]> autoPlayConditionReads = [];

    internal EventPredicateState ReadAutoPlayConditions(string eventId, int objectId)
    {
        if (Field is null || !HasCurrentFieldAllocation)
        {
            return Events.ReadPredicateState([]);
        }
        EnsureAutoPlayConditionSource();
        return Events.ReadPredicateState(ReadAutoPlayConditionOperations(eventId, objectId));
    }

    internal AutoPlayVisitGuard? CaptureAutoPlayVisitGuard(IReadOnlyList<AutoPlayTarget> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);
        if (Field is null || !HasCurrentFieldAllocation)
        {
            return null;
        }
        EnsureAutoPlayConditionSource();
        IEnumerable<int> reads = autoPlayInitializationConditionReads.Concat(targets.SelectMany(target => ReadAutoPlayConditionOperations(target.CoverageId, target.ObjectId)));
        return CaptureAutoPlayVisitGuard(Events, autoPlayConditionProgramHash!, reads);
    }

    internal static AutoPlayVisitGuard? CaptureAutoPlayVisitGuard(EventEngine events, byte[] originalJsm, IReadOnlyList<AutoPlayTarget> targets)
    {
        JsmProgram program = JsmProgram.Read(originalJsm);
        IEnumerable<int> reads = DiscoverAutoPlayInitializationConditionReads(program).Concat(targets.SelectMany(target => DiscoverAutoPlayConditionReads(program, target.CoverageId, target.ObjectId)));
        return CaptureAutoPlayVisitGuard(events, Convert.ToHexString(SHA256.HashData(originalJsm)), reads);
    }

    private static AutoPlayVisitGuard? CaptureAutoPlayVisitGuard(EventEngine events, string programHash, IEnumerable<int> operations)
    {
        EventPredicateState represented = events.ReadPredicateState(operations.Distinct().ToArray(), includeParty: false);
        ImmutableArray<NativePersistentPredicateRead> reads = represented.Values.Select(value => new NativePersistentPredicateRead(value.Opcode, value.Address, value.Width)).Distinct().Where(read => events.ReadPersistentPredicateFingerprint([read]) is not null).OrderBy(read => read.Address).ThenBy(read => read.Width).ThenBy(read => read.Opcode, StringComparer.Ordinal).ToImmutableArray();
        string? fingerprint = events.ReadPersistentPredicateFingerprint(reads);
        return fingerprint is null ? null : new AutoPlayVisitGuard(AutoPlayVisitGuard.CurrentVersion, events.FieldId, programHash, reads, fingerprint);
    }

    private void EnsureAutoPlayConditionSource()
    {
        if (ReferenceEquals(autoPlayConditionSource, Field!.Jsm))
        {
            return;
        }
        autoPlayConditionSource = Field.Jsm;
        autoPlayConditionProgram = JsmProgram.Read(Field.Jsm);
        autoPlayConditionProgramHash = Convert.ToHexString(SHA256.HashData(Field.Jsm));
        autoPlayInitializationConditionReads = DiscoverAutoPlayInitializationConditionReads(autoPlayConditionProgram);
        autoPlayConditionReads.Clear();
    }

    private int[] ReadAutoPlayConditionOperations(string eventId, int objectId)
    {
        if (!autoPlayConditionReads.TryGetValue((eventId, objectId), out int[]? operations))
        {
            operations = DiscoverAutoPlayConditionReads(autoPlayConditionProgram!, eventId, objectId);
            autoPlayConditionReads[(eventId, objectId)] = operations;
        }
        return operations;
    }

    internal static int[] DiscoverAutoPlayConditionReads(JsmProgram program, string eventId, int objectId)
    {
        List<EventScript> roots = [];
        if ((uint)objectId < program.Objects.Count)
        {
            EventObject obj = program.Objects[objectId];
            string kind = eventId.Split(':').ElementAtOrDefault(1) ?? string.Empty;
            if (kind == "line")
            {
                roots.AddRange(obj.Scripts.Skip(3).Take(5));
            }
            else
            {
                int root = kind == "push" ? 3 : 2;
                if (root < obj.Scripts.Count)
                {
                    roots.Add(obj.Scripts[root]);
                }
            }
        }
        return DiscoverAutoPlayConditionReads(program, roots);
    }

    private static int[] DiscoverAutoPlayInitializationConditionReads(JsmProgram program)
    {
        return DiscoverAutoPlayConditionReads(program, program.Objects.Where(obj => obj.Scripts.Count != 0).Select(obj => obj.Scripts[0]));
    }

    private static int[] DiscoverAutoPlayConditionReads(JsmProgram program, IEnumerable<EventScript> roots)
    {
        return roots.SelectMany(root => AutoPlayScriptClosure(program, root)).Distinct().Where(index => program.Operations[index].Opcode is Jsm.Opcode.PSHM_B or Jsm.Opcode.PSHSM_B or Jsm.Opcode.PSHM_W or Jsm.Opcode.PSHSM_W or Jsm.Opcode.PSHM_L or Jsm.Opcode.PSHSM_L).Where(index => !IsAutoPlaySelfUpdateRead(program, index)).Order().ToArray();
    }

    private static bool IsAutoPlaySelfUpdateRead(JsmProgram program, int index)
    {
        if (index + 3 >= program.Operations.Count)
        {
            return false;
        }
        JsmOperation read = program.Operations[index];
        JsmOperation literal = program.Operations[index + 1];
        JsmOperation calculation = program.Operations[index + 2];
        JsmOperation store = program.Operations[index + 3];
        Jsm.Opcode? matchingStore = read.Opcode switch
        {
            Jsm.Opcode.PSHM_B or Jsm.Opcode.PSHSM_B => Jsm.Opcode.POPM_B,
            Jsm.Opcode.PSHM_W or Jsm.Opcode.PSHSM_W => Jsm.Opcode.POPM_W,
            Jsm.Opcode.PSHM_L or Jsm.Opcode.PSHSM_L => Jsm.Opcode.POPM_L,
            _ => null
        };
        if (literal.Opcode != Jsm.Opcode.PSHN_L || calculation.Opcode != Jsm.Opcode.CAL || calculation.Parameter != 0 || store.Opcode != matchingStore || store.Parameter != read.Parameter)
        {
            return false;
        }

        // This exact stack-balanced addition sends the read only to its own typed store.
        // Other reads of the same bytes remain dependencies, including in requested scripts.
        return program.Objects.Any(obj => obj.Scripts.Any(script => script.ContainsOperation(index) && script.ContainsOperation(index + 1) && script.ContainsOperation(index + 2) && script.ContainsOperation(index + 3)));
    }
}
