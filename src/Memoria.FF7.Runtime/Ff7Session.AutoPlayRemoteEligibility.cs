using Memoria.FF7.Events;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7Session
{
    private readonly Dictionary<ushort, FieldProgram> remoteEligibilityPrograms = [];
    private readonly Dictionary<(string Visit, string Event), (FieldNavigationEntryProof Proof, int[] Values)> remoteEligibilityProofs = [];

    internal FieldNavigationEntryProof ReadCurrentPersistentEntryProof(string visit, string eventId)
    {
        FieldNavigationEntryProof Unknown(string reason) => new(false, false, reason, []);
        string[] source = visit.Split(':');
        string[] target = eventId.Split(':');
        if (source.Length != 3 || source[0] != "ff7" || !ushort.TryParse(source[1], out ushort field) || !IsKnownAutoPlayDestination(field))
        {
            return Unknown("The retained source has no installed field allocation.");
        }
        int entity;
        int script;
        if (target is ["actor", _, "script", _] && int.TryParse(target[1], out entity) && int.TryParse(target[3], out script))
        {
        }
        else if (target is ["line", _, "entity", _, "script", _] && int.TryParse(target[3], out entity) && int.TryParse(target[5], out script))
        {
        }
        else
        {
            return Unknown("The retained target has no ordinary native script identity.");
        }
        if (!remoteEligibilityPrograms.TryGetValue(field, out FieldProgram? program))
        {
            PcFieldFile file = PcFieldFile.ReadCompressed(fields.ReadEntry(fields.Find(mapList[field]).Single()));
            program = FieldProgram.FromPcScripts(PcFieldScripts.Read(file.GetSection(PcFieldSectionKind.ScriptAndText).Span));
            remoteEligibilityPrograms.Add(field, program);
        }
        if (source[2] != program.Identity || entity < 0 || entity >= program.EntityCount || script is < 0 or > 31)
        {
            return Unknown("The retained script identity differs from the installed native program.");
        }
        byte[] persistent = World is { } world ? Enumerable.Range(0, 1280).Select(world.ReadPersistentByte).ToArray() : Machine.Variables.CopyPersistent();
        FieldVariables variables = new(persistent, new byte[256]);
        int[] Values(FieldNavigationEntryProof proof) => proof.Reads.Select(read => read.Word ? (int)variables.ReadWord(read.Bank, read.Operand) : variables.ReadByte(read.Bank, (byte)read.Operand)).ToArray();
        var key = (visit, eventId);
        if (remoteEligibilityProofs.TryGetValue(key, out var cached) && cached.Proof.Complete && cached.Proof.HasNoEffect && Values(cached.Proof).AsSpan().SequenceEqual(cached.Values))
        {
            return cached.Proof;
        }
        FieldMachine inspection = new(program, variables);
        FieldNavigationEntryProof result = inspection.ProvePersistentNavigationEntryHasNoEffect(entity, script);
        remoteEligibilityProofs[key] = (result, Values(result));
        return result;
    }

    internal bool HasCurrentPersistentNoEffectEntry(string visit, string eventId)
    {
        return IsStablePersistentNoEffectProof(ReadCurrentPersistentEntryProof(visit, eventId));
    }

    internal static bool IsStablePersistentNoEffectProof(FieldNavigationEntryProof proof)
    {
        bool WalkingCounter(FieldVariableRead read)
        {
            int address = unchecked((byte)read.Operand);
            int width = read.Word && read.Bank == FieldBank.Persistent3Word ? 2 : 1;
            return read.Bank is FieldBank.Persistent3Byte or FieldBank.Persistent3Word && address < 8 && address + width > 6;
        }
        return proof.Complete && proof.HasNoEffect && !proof.Reads.Any(read => Ff7AutoplayDriver.IsNativeClockAccess(read) || WalkingCounter(read) || read.Bank is FieldBank.MapByte or FieldBank.MapWord);
    }
}
