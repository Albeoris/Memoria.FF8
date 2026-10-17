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
        return roots.SelectMany(root => AutoPlayScriptClosure(program, root)).Distinct().Where(index => program.Operations[index].Opcode is Jsm.Opcode.PSHM_B or Jsm.Opcode.PSHSM_B or Jsm.Opcode.PSHM_W or Jsm.Opcode.PSHSM_W or Jsm.Opcode.PSHM_L or Jsm.Opcode.PSHSM_L).Order().ToArray();
    }
}
