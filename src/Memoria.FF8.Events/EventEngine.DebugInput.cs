using System.Security.Cryptography;
using System.Text;
using Albeoris.Games.FF8.Jsm;

namespace Memoria.FF8.Events;

public sealed record ScriptedInputQuery(int ObjectId, int ScriptId, int Operation, string Opcode, int? Mask);
public sealed record ScriptedInputMetadata(string Fingerprint, IReadOnlyList<int> Masks, IReadOnlyList<ScriptedInputQuery> Queries)
{
    public bool ObservedQueriesProven { get; init; }
}

public sealed partial class EventEngine
{
    /// <summary>Read-only descriptions of original key queries in active scripts. Masks are raw physical inputs for FieldButtons.</summary>
    public ScriptedInputMetadata? DiscoverScriptedInput(int? objectId = null)
    {
        return DiscoverScriptedInputCore(objectId, null);
    }

    /// <summary>Describes only key queries actually reached by a neutral native callback, using their sampled operands.</summary>
    public ScriptedInputMetadata? DiscoverObservedScriptedInput(IReadOnlyList<ScriptedInputQuery> observedQueries, int? objectId = null)
    {
        ArgumentNullException.ThrowIfNull(observedQueries);
        return DiscoverScriptedInputCore(objectId, observedQueries);
    }

    private ScriptedInputMetadata? DiscoverScriptedInputCore(int? objectId, IReadOnlyList<ScriptedInputQuery>? observedQueries)
    {
        if (program is null || transitionRequested)
        {
            return null;
        }
        Fiber[] active = fibers.Where(fiber => !fiber.Complete && fiber.WaitReason != WaitReason.NativeHalt && (objectId is null || fiber.ObjectId == objectId.Value)).OrderBy(fiber => fiber.ObjectId).ThenBy(fiber => fiber.Script.Id).ThenBy(fiber => fiber.Position).ToArray();
        List<(int ObjectId, EventScript Script, Fiber? Fiber)> owners = [];
        if (observedQueries is null)
        {
            owners.AddRange(active.Select(fiber => (fiber.ObjectId, fiber.Script, (Fiber?)fiber)));
        }
        else
        {
            foreach ((int observedObject, int scriptId) in observedQueries.Where(query => objectId is null || query.ObjectId == objectId.Value).Select(query => (query.ObjectId, query.ScriptId)).Distinct().OrderBy(owner => owner.ObjectId).ThenBy(owner => owner.ScriptId))
            {
                EventScript script = program.Objects.SingleOrDefault(owner => owner.Index == observedObject)?.Scripts.SingleOrDefault(script => script.Id == scriptId) ?? throw new NotSupportedException("The observed native key query has no declared script owner in the current field program.");
                owners.Add((observedObject, script, null));
            }
        }
        List<ScriptedInputQuery> queries = [];
        HashSet<int> masks = [];
        HashSet<EventScript> queryOwners = [];
        foreach ((int ownerId, EventScript script, Fiber? fiber) in owners)
        {
            foreach (int index in script.ReachableOperations.Order())
            {
                JsmOperation operation = program.Operations[index];
                if (operation.Opcode is not (Jsm.Opcode.KEYSCAN or Jsm.Opcode.KEYSCAN2 or Jsm.Opcode.KEYON))
                {
                    continue;
                }
                ScriptedInputQuery[]? observed = observedQueries?.Where(query => query.ObjectId == ownerId && query.ScriptId == script.Id && query.Operation == index && query.Opcode == operation.Opcode.ToString()).Distinct().ToArray();
                if (observed is { Length: 0 })
                {
                    continue;
                }
                int? mask = index > 0 && script.ContainsOperation(index - 1) && program.Operations[index - 1].Opcode == Jsm.Opcode.PSHN_L ? unchecked((ushort)program.Operations[index - 1].Parameter) : null;
                if (mask is null && fiber is not null && fiber.Position == index && fiber.Stack.Count > 0 && fiber.Stack.PeekValue().IsExact)
                {
                    mask = unchecked((ushort)fiber.Stack.Peek());
                }
                queryOwners.Add(script);
                int?[] operands = observed is null ? [mask] : observed.Select(query => (int?)(query.Mask ?? throw new NotSupportedException("An observed native input query requires its actual sampled operand."))).Distinct().ToArray();
                foreach (int? operand in operands)
                {
                    queries.Add(new ScriptedInputQuery(ownerId, script.Id, index, operation.Opcode.ToString(), operand));
                    // These native queries test whether ANY mask bit is down, so one physical bit is sufficient.
                    int alternatives = operand ?? ushort.MaxValue;
                    for (int bit = 0; bit < 16; bit++)
                    {
                        int mapped = 1 << bit;
                        if ((alternatives & mapped) == 0)
                        {
                            continue;
                        }
                        int raw = operation.Opcode == Jsm.Opcode.KEYSCAN2 ? mapped : (services.InputConfiguration ?? throw new NotSupportedException("Scripted mapped input discovery requires the retained native configuration.")).RawForMappedPulse((ushort)mapped);
                        masks.Add(raw);
                    }
                }
            }
        }
        if (queries.Count == 0)
        {
            return null;
        }
        StringBuilder identity = new();
        identity.Append(FieldId).Append(':').Append(programHash);
        foreach (ScriptedInputQuery query in queries.Distinct().OrderBy(query => query.ObjectId).ThenBy(query => query.ScriptId).ThenBy(query => query.Operation).ThenBy(query => query.Mask))
        {
            identity.Append('|').Append(query.ObjectId).Append(':').Append(query.ScriptId).Append(':').Append(query.Operation).Append(':').Append(query.Opcode);
            if (observedQueries is not null)
            {
                identity.Append("/sampled:").Append(query.Mask);
            }
        }
        if (observedQueries is not null)
        {
            identity.Append("|physical:").AppendJoin(',', masks.Order());
        }
        foreach (DialogueState dialogue in dialogues.Values.OrderBy(dialogue => dialogue.Channel))
        {
            identity.Append("|display:").Append(dialogue.Channel).Append(':').Append(dialogue.MessageId).Append(':').Append(dialogue.X).Append(':').Append(dialogue.Y).Append(':').Append(dialogue.FirstChoice).Append(':').Append(dialogue.LastChoice).Append(':').Append(dialogue.InitialChoice).Append(':').Append(dialogue.CanConfirm);
            if (Scene.DialogueWindows.TryGetValue(dialogue.Channel, out DialogueWindow? window))
            {
                identity.Append(':').Append(window.X).Append(':').Append(window.Y).Append(':').Append(window.Width).Append(':').Append(window.Height);
            }
        }
        // Retain all typed values read by the key-query owners. Scheduler phase and unrelated callbacks do not identify a screen.
        foreach (JsmOperation operation in queryOwners.SelectMany(script => script.ReachableOperations).Distinct().Select(index => program.Operations[index]).Where(operation => operation.Opcode is Jsm.Opcode.PSHM_B or Jsm.Opcode.PSHSM_B or Jsm.Opcode.PSHM_W or Jsm.Opcode.PSHSM_W or Jsm.Opcode.PSHM_L or Jsm.Opcode.PSHSM_L).DistinctBy(operation => (operation.Opcode, operation.Parameter)).OrderBy(operation => operation.Parameter).ThenBy(operation => operation.Opcode))
        {
            identity.Append('|').Append(operation.Parameter).Append('/').Append(NativeMemoryWidth(operation.Opcode)).Append('/').Append(operation.Opcode).Append('=');
            try
            {
                identity.AppendJoin(',', ReadEventMemory(operation).Values);
            }
            catch (Exception exception) when (exception is NotSupportedException or InvalidDataException)
            {
                identity.Append('?');
            }
        }
        string fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity.ToString())));
        return new ScriptedInputMetadata(fingerprint, masks.Order().ToArray(), queries) { ObservedQueriesProven = observedQueries is not null };
    }
}
