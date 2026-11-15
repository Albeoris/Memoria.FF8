using System.Security.Cryptography;
using System.Text;

namespace Memoria.FF7.Events;

public sealed record FieldSpatialSample(int Id, FieldActorPosition Position);
public sealed record FieldSpatialDemand(int Entity, int ProgramCounter, int TargetEntity, int TargetScript, int SampleId, int[] DestinationFields, bool ChangesStory, string Fingerprint, FieldVariableRead[] Reads, FieldHostRead[] HostReads);
public sealed record FieldSpatialObservation(FieldSpatialDemand[] Demands, FieldVariableRead[] Reads, string Fingerprint);

public sealed partial class FieldVariables
{
    internal IEnumerable<FieldVariableRead> SpatialSourceBytes(FieldVariableRead read)
    {
        if (read.Bank == FieldBank.Immediate)
        {
            yield break;
        }
        var (storage, index, word) = Locate(read.Bank, (byte)read.Operand);
        int width = read.Word && word ? 2 : 1;
        for (int offset = 0; offset < width; offset++)
        {
            int address = index + offset;
            FieldBank bank = ReferenceEquals(storage, map) ? FieldBank.MapByte : (address / 256) switch
            {
                0 => FieldBank.Persistent0Byte,
                1 => FieldBank.Persistent1Byte,
                2 => FieldBank.Persistent2Byte,
                3 => FieldBank.Persistent3Byte,
                4 => FieldBank.Persistent4Byte,
                _ => throw new InvalidDataException("Spatial inspection read exceeds variable storage.")
            };
            yield return new(bank, checked((ushort)(ReferenceEquals(storage, map) ? address : address % 256)), false);
        }
    }
}

public sealed partial class FieldMachine
{
    private sealed class SpatialInspectionQueryException : Exception
    {
    }

    /// <summary>Projects current autonomous spatial predicates onto supplied physical samples without dispatching scripts or hosts.</summary>
    public FieldSpatialObservation ReadCurrentSpatialDemands(int controlledEntity, IReadOnlyList<FieldSpatialSample> samples, int instructionBudget = 128)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if ((uint)controlledEntity >= entities.Length || instructionBudget is < 1 or > 4096 || samples.Select(sample => sample.Id).Distinct().Count() != samples.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(controlledEntity));
        }
        if (!Initialized || !entities[controlledEntity].ModelBound)
        {
            return new([], [], program.Identity);
        }
        FieldVmSnapshot source = Capture() with { CycleCursor = null };
        List<FieldSpatialDemand> demands = [];
        HashSet<FieldVariableRead> observationReads = [];
        for (int entity = 0; entity < entities.Length; entity++)
        {
            if (source.Entities[entity].Priority != 7)
            {
                continue;
            }
            if (!Project(entity, null))
            {
                continue;
            }
            foreach (FieldSpatialSample sample in samples)
            {
                Project(entity, sample);
            }
        }
        FieldVariableRead[] observed = observationReads.OrderBy(read => read.Bank).ThenBy(read => read.Operand).ToArray();
        string observationFacts = $"{ReadScriptAnalysisAllocationFingerprint()}|{string.Join(';', source.Entities.Select(entity => $"{entity.Priority}:{entity.ProgramCounter}"))}|{ReadScriptAnalysisFingerprint(controlledEntity, 0, observed)}";
        return new(demands.ToArray(), observed, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(observationFacts))));

        bool Project(int entity, FieldSpatialSample? sample)
        {
            FieldMachine detached = Restore(program, source, new InspectionHost());
            HashSet<FieldVariableRead> dependencies = [];
            HashSet<FieldVariableRead> written = [];
            HashSet<FieldVariableRead> instructionReads = [];
            HashSet<FieldVariableRead> instructionWrites = [];
            bool sampled = false;
            detached.inspection = new()
            {
                StopAtHost = true,
                ActorReply = query =>
                {
                    if (query.Entity != controlledEntity || query.Query is not (FieldActorQueryKind.Position or FieldActorQueryKind.Coordinates or FieldActorQueryKind.Triangle))
                    {
                        throw new InputInspectionBoundaryException();
                    }
                    if (sample is null)
                    {
                        throw new SpatialInspectionQueryException();
                    }
                    sampled = true;
                    int value = query.Query == FieldActorQueryKind.Triangle ? sample.Position.Triangle : (ushort)sample.Position.X | (sample.Position.Y << 16);
                    return new(true, value, sample.Position);
                }
            };
            detached.Variables.ReadObserved = read => instructionReads.UnionWith(detached.Variables.SpatialSourceBytes(read));
            detached.Variables.WriteObserved = write => instructionWrites.UnionWith(detached.Variables.SpatialSourceBytes(write));
            HashSet<int> visited = [];
            for (int step = 0; step < instructionBudget; step++)
            {
                Entity owner = detached.entities[entity];
                int pc = owner.Pc;
                if (!visited.Add(pc))
                {
                    break;
                }
                instructionReads.Clear();
                instructionWrites.Clear();
                try
                {
                    byte opcode = program.Read(pc);
                    if (opcode is >= 1 and <= 6)
                    {
                        if (sampled)
                        {
                            AddDemand(pc, opcode);
                        }
                        break;
                    }
                    if (opcode == 0x24 || opcode == 0x5F)
                    {
                        owner.Pc = unchecked((ushort)(pc + (opcode == 0x24 ? 3 : 1)));
                        owner.Wait = 0;
                        continue;
                    }
                    if (!IsSpatialInspectionOpcode(opcode))
                    {
                        break;
                    }
                    detached.Execute(entity);
                    // Stores own their result; only preceding source reads can define their dependencies.
                    dependencies.UnionWith(instructionReads.Where(read => !written.Contains(read) && !instructionWrites.Contains(read)));
                    written.UnionWith(instructionWrites);
                }
                catch (Exception error) when (error is InputInspectionBoundaryException or FieldExecutionException or InvalidDataException or NotSupportedException or ArgumentException or IndexOutOfRangeException)
                {
                    break;
                }
                catch (SpatialInspectionQueryException)
                {
                    dependencies.UnionWith(instructionReads.Where(read => !written.Contains(read)));
                    observationReads.UnionWith(dependencies);
                    return true;
                }
            }
            observationReads.UnionWith(dependencies);
            return false;

            void AddDemand(int pc, byte opcode)
            {
                int target = program.Read(pc + 1);
                if (opcode >= 4)
                {
                    if (target >= 3)
                    {
                        return;
                    }
                    int character = detached.ReadParty()[target];
                    target = character == 255 ? -1 : Array.FindIndex(detached.entities, actor => actor.PartyCharacter == character);
                }
                if ((uint)target >= detached.entities.Length)
                {
                    return;
                }
                int script = program.Read(pc + 2) & 31;
                FieldScriptAnalysis child = detached.AnalyzeScript(target, script);
                if (!child.Complete || child.DestinationFields.Length == 0)
                {
                    return;
                }
                dependencies.UnionWith(instructionReads.Where(read => !written.Contains(read)));
                foreach (FieldVariableRead read in child.Reads)
                {
                    dependencies.UnionWith(detached.Variables.SpatialSourceBytes(read).Where(value => !written.Contains(value)));
                }
                FieldVariableRead[] reads = dependencies.OrderBy(read => read.Bank).ThenBy(read => read.Operand).ToArray();
                string facts = $"{ReadScriptAnalysisAllocationFingerprint()}|{entity}:{pc}:{target}:{script}|{ReadScriptAnalysisFingerprint(target, script, reads)}";
                string fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(facts)));
                demands.Add(new(entity, pc, target, script, sample!.Id, child.DestinationFields, child.ChangesStory, fingerprint, reads, child.HostReads));
            }
        }
    }

    private static bool IsSpatialInspectionOpcode(byte opcode)
    {
        return opcode is >= 0x10 and <= 0x19 or 0x75 or 0x76 or 0x77 or 0x78 or >= 0x80 and <= 0x89 or >= 0x8D and <= 0x98 or 0xB8 or 0xB9 or 0xC1;
    }
}
