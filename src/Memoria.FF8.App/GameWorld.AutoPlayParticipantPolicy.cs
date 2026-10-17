using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;

namespace Memoria.FF8;

internal sealed record AutoPlayParticipantPolicyGuard(int Version, int FieldId, string ProgramHash, ImmutableArray<NativePersistentPredicateRead> Reads, string Fingerprint, int[] SavedBindings, int[] RetainedActors, int[] CharacterIds);
internal sealed record AutoPlayParticipantPolicyDeferral(string TargetId, string SourceConditionSignature, AutoPlayParticipantPolicyGuard Guard, IReadOnlyList<AutoPlayChoiceSelection> Selections, NativePartyActionRequest Request, string? ChoiceKey = null, int FirstOption = 0, int OptionCount = 0);

internal sealed partial class GameWorld
{
    internal AutoPlayParticipantPolicyGuard ReadAutoPlayParticipantPolicyGuard(NativePartyActionRequest request, string eventId, IReadOnlyList<ScriptedInputMetadata> selectedInputs, AutoPlayVisitGuard sourceGuard)
    {
        if (Field is null || !HasCurrentFieldAllocation || Field.Id != request.FieldId || sourceGuard.Version != AutoPlayVisitGuard.CurrentVersion || sourceGuard.FieldId != Field.Id || sourceGuard.ProgramHash != request.ProgramHash || ReadAutoPlayFieldProgramHash(Field.Id) != request.ProgramHash)
        {
            throw new NotSupportedException("Participant-policy deferral requires the exact original source allocation and current portable guard domain.");
        }
        JsmProgram program = JsmProgram.Read(Field.Jsm);
        if ((uint)request.CallerObjectId >= program.Objects.Count)
        {
            throw new NotSupportedException("The reached party request has no original source object.");
        }
        EventObject obj = program.Objects[request.CallerObjectId];
        string kind = eventId.Split(':').ElementAtOrDefault(1) ?? string.Empty;
        IEnumerable<EventScript> roots = kind == "line" ? obj.Scripts.Skip(3).Take(5) : obj.Scripts.Skip(kind == "push" ? 3 : 2).Take(1);
        EventScript? script = roots.SingleOrDefault(candidate => candidate.Id == request.ScriptId && candidate.ContainsOperation(request.Operation));
        if (script is null)
        {
            throw new NotSupportedException("Participant-policy deferral currently requires a request reached directly in the admitted interaction callback.");
        }
        HashSet<int> prefix = [];
        Queue<int> pending = new([script.Start]);
        bool reached = false;
        while (pending.TryDequeue(out int index))
        {
            if (!prefix.Add(index))
            {
                continue;
            }
            if (!script.ContainsOperation(index))
            {
                throw new NotSupportedException("The source prefix leaves its original callback contract.");
            }
            if (index == request.Operation)
            {
                reached = true;
                continue;
            }
            JsmOperation operation = program.Operations[index];
            bool inputQuery = operation.Opcode is Jsm.Opcode.KEYSCAN or Jsm.Opcode.KEYSCAN2 or Jsm.Opcode.KEYON;
            bool knownInput = selectedInputs.Any(input => input.Queries.Any(query => query.ObjectId == request.CallerObjectId && query.ScriptId == request.ScriptId && query.Operation == index && query.Opcode == operation.Opcode.ToString()));
            bool knownResult = operation.Opcode == Jsm.Opcode.PSHI_L && operation.Parameter == 0 && index > script.Start && program.Operations[index - 1].Opcode is Jsm.Opcode.KEYSCAN or Jsm.Opcode.KEYSCAN2 or Jsm.Opcode.KEYON && !AutoPlaySourceHasInteriorEntry(program, script, index - 1, index) && selectedInputs.Any(input => input.Queries.Any(query => query.ObjectId == request.CallerObjectId && query.ScriptId == request.ScriptId && query.Operation == index - 1));
            if (inputQuery ? !knownInput : operation.Opcode == Jsm.Opcode.PSHI_L ? !knownResult : !IsAutoPlayUnavailableSourceOperation(operation.Opcode))
            {
                throw new NotSupportedException("The reached participant-action prefix contains an input or request without an exact portable policy guard.");
            }
            if (operation.Opcode is Jsm.Opcode.RET or Jsm.Opcode.HALT)
            {
                continue;
            }
            if (operation.Opcode is Jsm.Opcode.JMP or Jsm.Opcode.JPF)
            {
                pending.Enqueue(JsmProgram.JumpTarget(index, operation.Parameter));
            }
            if (operation.Opcode != Jsm.Opcode.JMP)
            {
                pending.Enqueue(index + 1);
            }
        }
        if (!reached)
        {
            throw new NotSupportedException("The native request is not reachable from the authenticated source callback.");
        }
        int[] reads = prefix.Where(index => program.Operations[index].Opcode is Jsm.Opcode.PSHM_B or Jsm.Opcode.PSHSM_B or Jsm.Opcode.PSHM_W or Jsm.Opcode.PSHSM_W or Jsm.Opcode.PSHM_L or Jsm.Opcode.PSHSM_L).ToArray();
        EventPredicateState represented = Events.ReadPredicateState(reads, includeParty: false);
        if (represented.DeferredOperations.Count != 0)
        {
            throw new NotSupportedException("The source prefix has unknown native memory dependencies; it cannot be suppressed by automation policy.");
        }
        ImmutableArray<NativePersistentPredicateRead> descriptors = sourceGuard.Reads.Concat(represented.Values.Select(value => new NativePersistentPredicateRead(value.Opcode, value.Address, value.Width))).Distinct().OrderBy(read => read.Address).ThenBy(read => read.Width).ThenBy(read => read.Opcode, StringComparer.Ordinal).ToImmutableArray();
        AutoPlayParticipantPolicyGuard guard = new(1, Field.Id, request.ProgramHash, descriptors, string.Empty, Enumerable.Range(0, 3).Select(slot => (int)Events.Scene.PartyBindingMemory!.At(slot)).ToArray(), Events.Scene.NativeActorObjectIds.ToArray(), Events.Scene.NativeActorObjectIds.Select(id => Events.Actors[id].CharacterId).ToArray());
        return guard with { Fingerprint = ReadAutoPlayParticipantPolicyFingerprint(guard) ?? throw new NotSupportedException("The source prefix contains nonportable native owners; participant-policy deferral cannot be cached.") };
    }

    internal string? ReadAutoPlayParticipantPolicyFingerprint(AutoPlayParticipantPolicyGuard guard)
    {
        if (guard.Version != 1)
        {
            throw new InvalidDataException("Unsupported participant-policy guard version.");
        }
        if (ReadAutoPlayFieldProgramHash(guard.FieldId) != guard.ProgramHash || Events.ReadPersistentPredicateFingerprint(guard.Reads) is not string persistent)
        {
            return null;
        }
        bool current = Field?.Id == guard.FieldId && HasCurrentFieldAllocation;
        int[] bindings = current ? Enumerable.Range(0, 3).Select(slot => (int)Events.Scene.PartyBindingMemory!.At(slot)).ToArray() : guard.SavedBindings;
        int[] retained = current ? Events.Scene.NativeActorObjectIds.ToArray() : guard.RetainedActors;
        int[] characters = current ? retained.Select(id => Events.Actors[id].CharacterId).ToArray() : guard.CharacterIds;
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { persistent, bindings, retained, characters })));
    }
}
