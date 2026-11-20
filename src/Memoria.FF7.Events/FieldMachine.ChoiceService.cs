namespace Memoria.FF7.Events;

public sealed record FieldChoiceServiceProof(bool Complete, int[] ScratchMapBytes, int[] PresentationMapBytes, int[] PresentationEntities, string? Reason = null);

public sealed partial class FieldMachine
{
    public FieldChoiceServiceProof ProveChoiceService(int entity, int programCounter, IReadOnlyList<FieldNavigationEntryPoint> possibleExternalEntries, int instructionBudget = 8192)
    {
        FieldChoiceServiceProof Reject(string reason) => new(false, [], [], [], reason);
        try
        {
            (FieldScriptAnalysis Analysis, HashSet<FieldVariableRead> Writes, bool Presentation) Inspect(int owner, int pc, bool requests = true)
            {
                FieldScriptAnalysis analysis = AnalyzeScriptCore(owner, 0, pc, instructionBudget, null, 0, collectRandomReads: true);
                if (!analysis.Complete)
                {
                    throw new NotSupportedException(analysis.Reason);
                }
                HashSet<FieldVariableRead> writes = [];
                bool presentation = true;
                foreach (FieldScriptInstructionSite site in analysis.InstructionSites)
                {
                    int address = site.ProgramCounter;
                    int executing = site.Entity;
                    byte opcode = program.Read(address);
                    if (!requests && opcode is 0x30 or 0x31 or 0x32 or 0x99)
                    {
                        throw new NotSupportedException("Choice utility does not classify owned input or random dependencies.");
                    }
                    if (opcode is >= 1 and <= 6)
                    {
                        if (!requests)
                        {
                            throw new NotSupportedException("Choice utility does not certify owned child requests.");
                        }
                        continue;
                    }
                    FieldVariables variables = new(Variables.CopyPersistent(), Variables.CopyMap());
                    variables.WriteObserved = write => writes.UnionWith(variables.SpatialSourceBytes(write));
                    FieldMachine detached = new(program, variables, host: new InspectionHost()) { inspection = new() };
                    for (int index = 0; index < entities.Length; index++)
                    {
                        detached.entities[index].ModelBound = entities[index].ModelBound;
                        detached.entities[index].PartyCharacter = entities[index].PartyCharacter;
                    }
                    detached.entities[executing].Pc = address;
                    detached.entities[executing].Priority = 1;
                    detached.Input = Input;
                    detached.Execute(executing);
                    foreach (FieldCommand command in detached.inspection!.Commands)
                    {
                        bool ambient = command is FieldBackgroundCommand or FieldActorTurn or FieldRandomCommand or FieldActorFaceParty or FieldAnimation or FieldWindowCommand or FieldDialogueCommand { FirstChoice: null } or FieldControlCommand { Property: FieldControlKind.PlayerLocked or FieldControlKind.MenuDisabled };
                        presentation &= ambient;
                        if (!ambient && command is not (FieldGilQuery or FieldGilCommand or FieldPartyRecovery or FieldDialogueCommand or FieldWindowCommand or FieldFadeCommand or FieldMusicCommand or FieldMusicChangeCommand or FieldMusicPlayingQuery or FieldControlCommand { Property: FieldControlKind.PlayerLocked or FieldControlKind.MenuDisabled }))
                        {
                            throw new NotSupportedException($"Choice utility does not classify {command.GetType().Name}.");
                        }
                    }
                }
                if (writes.Any(write => write.Bank != FieldBank.MapByte))
                {
                    throw new NotSupportedException("Choice utility does not discard persistent writes.");
                }
                return (analysis, writes, presentation);
            }
            var body = Inspect(entity, programCounter, requests: false);
            HashSet<FieldVariableRead> scratch = body.Writes;
            HashSet<(int Entity, int Pc)> roots = [];
            for (int owner = 0; owner < entities.Length; owner++)
            {
                roots.Add((owner, entities[owner].Pc));
                foreach (int saved in entities[owner].Saved.Where(pc => pc != 0))
                {
                    roots.Add((owner, saved));
                }
            }
            foreach (FieldNavigationEntryPoint entry in possibleExternalEntries)
            {
                roots.Add((entry.Entity, program.Entry(entry.Entity, entry.Script)));
            }
            foreach (FieldHostRequest pending in pendingRequests.Values)
            {
                roots.Add((pending.Entity, pending.ProgramCounter));
            }
            HashSet<FieldVariableRead> presentationWrites = [];
            HashSet<int> presentationEntities = [];
            List<string> unclassified = [];
            List<(HashSet<FieldVariableRead> Reads, HashSet<FieldVariableRead> Writes, bool Presentation)> consumers = [];
            foreach (var root in roots)
            {
                FieldScriptAnalysis analysis = AnalyzeScriptCore(root.Entity, 0, root.Pc, instructionBudget, null, 0, collectRandomReads: true);
                if (!analysis.Complete)
                {
                    return Reject(analysis.Reason ?? "A choice scratch consumer is unsupported.");
                }
                FieldScriptEntryDependencies dependencies = AnalyzeEntryDependenciesCore(root.Entity, 0, root.Pc, analysis, instructionBudget);
                if (!dependencies.Complete)
                {
                    return Reject(dependencies.Reason ?? "A choice scratch dependency is unsupported.");
                }
                HashSet<FieldVariableRead> reads = dependencies.Reads.SelectMany(Variables.SpatialSourceBytes).ToHashSet();
                if (reads.Overlaps(scratch))
                {
                    return Reject($"{root.Entity}:{root.Pc:X4}: A future native consumer reads choice scratch before redefining it.");
                }
                try
                {
                    var effects = Inspect(root.Entity, root.Pc);
                    consumers.Add((reads, effects.Writes, effects.Presentation));
                    if (effects.Presentation)
                    {
                        presentationWrites.UnionWith(effects.Writes);
                    }
                }
                catch (NotSupportedException)
                {
                    consumers.Add((reads, [], false));
                }
            }
            HashSet<FieldVariableRead> observable = consumers.Where(item => !item.Presentation).SelectMany(item => item.Reads).Where(presentationWrites.Contains).ToHashSet();
            bool changed;
            do
            {
                changed = false;
                foreach (var consumer in consumers.Where(item => item.Presentation && item.Writes.Overlaps(observable)))
                {
                    int count = observable.Count;
                    observable.UnionWith(consumer.Reads.Where(presentationWrites.Contains));
                    changed |= count != observable.Count;
                }
            }
            while (changed);
            presentationWrites.ExceptWith(observable);
            for (int owner = 0; owner < entities.Length; owner++)
            {
                try
                {
                    var current = Inspect(owner, entities[owner].Pc);
                    if (current.Presentation && current.Writes.All(presentationWrites.Contains))
                    {
                        presentationEntities.Add(owner);
                    }
                    else if (!current.Presentation)
                    {
                        unclassified.Add($"{owner}:{entities[owner].Pc:X4}: The current phase contains a nonpresentation effect.");
                    }
                }
                catch (NotSupportedException error)
                {
                    unclassified.Add($"{owner}:{entities[owner].Pc:X4}: {error.Message}");
                }
            }
            return new(true, scratch.Select(write => (int)write.Operand).Order().ToArray(), presentationWrites.Select(write => (int)write.Operand).Order().ToArray(), presentationEntities.Order().ToArray(), unclassified.Count == 0 ? null : string.Join("; ", unclassified));
        }
        catch (Exception error) when (error is NotSupportedException or FieldExecutionException or InvalidDataException or ArgumentException or IndexOutOfRangeException)
        {
            return Reject(error.Message);
        }
    }
}
