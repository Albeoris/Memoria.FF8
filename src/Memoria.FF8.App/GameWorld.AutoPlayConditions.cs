using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    private byte[]? autoPlayConditionSource;
    private JsmProgram? autoPlayConditionProgram;
    private readonly Dictionary<(string Event, int Object), int[]> autoPlayConditionReads = [];

    internal EventPredicateState ReadAutoPlayConditions(string eventId, int objectId)
    {
        if (Field is null || !HasCurrentFieldAllocation)
        {
            return Events.ReadPredicateState([]);
        }
        if (!ReferenceEquals(autoPlayConditionSource, Field.Jsm))
        {
            autoPlayConditionSource = Field.Jsm;
            autoPlayConditionProgram = JsmProgram.Read(Field.Jsm);
            autoPlayConditionReads.Clear();
        }
        if (!autoPlayConditionReads.TryGetValue((eventId, objectId), out int[]? operations))
        {
            operations = DiscoverAutoPlayConditionReads(autoPlayConditionProgram!, eventId, objectId);
            autoPlayConditionReads[(eventId, objectId)] = operations;
        }
        return Events.ReadPredicateState(operations);
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
