using System.Security.Cryptography;
using System.Text;
using Albeoris.Games.FF8.Jsm;

namespace Memoria.FF8.Events;

public sealed record ScriptedInputQuery(int ObjectId, int ScriptId, int Operation, string Opcode, int? Mask);
public sealed record ScriptedInputMetadata(string Fingerprint, IReadOnlyList<int> Masks, IReadOnlyList<ScriptedInputQuery> Queries);

public sealed partial class EventEngine
{
    /// <summary>Read-only descriptions of original key queries in active scripts. Masks are raw physical inputs for FieldButtons.</summary>
    public ScriptedInputMetadata? DiscoverScriptedInput()
    {
        if (program is null || transitionRequested)
        {
            return null;
        }
        Fiber[] active = fibers.Where(fiber => !fiber.Complete && fiber.WaitReason != WaitReason.NativeHalt).OrderBy(fiber => fiber.ObjectId).ThenBy(fiber => fiber.Script.Id).ThenBy(fiber => fiber.Position).ToArray();
        List<ScriptedInputQuery> queries = [];
        HashSet<int> masks = [];
        HashSet<EventScript> queryOwners = [];
        foreach (Fiber fiber in active)
        {
            foreach (int index in fiber.Script.ReachableOperations.Order())
            {
                JsmOperation operation = program.Operations[index];
                if (operation.Opcode is not (Jsm.Opcode.KEYSCAN or Jsm.Opcode.KEYSCAN2 or Jsm.Opcode.KEYON))
                {
                    continue;
                }
                int? mask = index > 0 && fiber.Script.ContainsOperation(index - 1) && program.Operations[index - 1].Opcode == Jsm.Opcode.PSHN_L ? unchecked((ushort)program.Operations[index - 1].Parameter) : null;
                if (mask is null && fiber.Position == index && fiber.Stack.Count > 0 && fiber.Stack.PeekValue().IsExact)
                {
                    mask = unchecked((ushort)fiber.Stack.Peek());
                }
                queries.Add(new ScriptedInputQuery(fiber.ObjectId, fiber.Script.Id, index, operation.Opcode.ToString(), mask));
                queryOwners.Add(fiber.Script);
                // These native queries test whether ANY mask bit is down, so one physical bit is sufficient.
                int alternatives = mask ?? ushort.MaxValue;
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
        if (queries.Count == 0)
        {
            return null;
        }
        StringBuilder identity = new();
        identity.Append(FieldId).Append(':').Append(programHash);
        foreach (ScriptedInputQuery query in queries.DistinctBy(query => (query.ObjectId, query.ScriptId, query.Operation)).OrderBy(query => query.ObjectId).ThenBy(query => query.ScriptId).ThenBy(query => query.Operation))
        {
            identity.Append('|').Append(query.ObjectId).Append(':').Append(query.ScriptId).Append(':').Append(query.Operation).Append(':').Append(query.Opcode);
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
        return new ScriptedInputMetadata(fingerprint, masks.Order().ToArray(), queries);
    }
}
