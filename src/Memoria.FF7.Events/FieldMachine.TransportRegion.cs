using System.Security.Cryptography;
using System.Text;

namespace Memoria.FF7.Events;

public sealed record FieldTransportRegionProof(bool Complete, uint Cost, string? Region, FieldVariableRead[] Reads, string? Reason = null)
{
    public FieldTransitionCommand? Transition { get; init; }
}

public sealed partial class FieldMachine
{
    public FieldTransportRegionProof ProveTransportRegion(int entity, int script, uint gil, IReadOnlyList<FieldNavigationEntryPoint> possibleExternalEntries)
    {
        FieldTransportRegionProof Reject(string reason) => new(false, 0, null, [], reason);
        try
        {
            FieldScriptAnalysis body = AnalyzeScript(entity, script);
            FieldScriptEntryDependencies dependencies = AnalyzeEntryDependencies(entity, script, body);
            if (!body.Complete || !dependencies.Complete || body.HostReads.Any(read => read.Kind != FieldHostReadKind.Gil) || !body.HostReads.Any(read => read.Kind == FieldHostReadKind.Gil) || body.DestinationFields.Length != 1)
            {
                return Reject("The transport dependency/host/transition contract is incomplete.");
            }
            Dictionary<FieldVariableRead, bool> outputs = [];
            HashSet<FieldVariableRead> storage = [];
            List<FieldScriptInstructionSite> payments = [];
            foreach (FieldScriptInstructionSite site in body.InstructionSites)
            {
                int pc = site.ProgramCounter;
                if (program.Read(pc) == 0x3A)
                {
                    payments.Add(site);
                }
                if (program.Read(pc) != 0x3B)
                {
                    continue;
                }
                for (int word = 0; word < 2; word++)
                {
                    byte selectors = program.Read(pc + 1);
                    FieldBank bank = (FieldBank)(word == 0 ? selectors >> 4 : selectors & 15);
                    FieldVariableRead output = new(bank, program.Read(pc + 2 + word), true);
                    FieldVariableRead[] bytes = Variables.SpatialSourceBytes(output).ToArray();
                    if (bytes.Length != 2 || bytes.Any(value => value.Bank != FieldBank.MapByte) || outputs.TryGetValue(output, out bool high) && high != (word == 1))
                    {
                        return Reject("Gil output aliases have no exact local low/high word contract.");
                    }
                    if (!outputs.ContainsKey(output) && bytes.Any(storage.Contains))
                    {
                        return Reject("Gil low/high output words overlap.");
                    }
                    outputs[output] = word == 1;
                    storage.UnionWith(bytes);
                }
            }
            if (payments.Count != 1 || outputs.Count == 0 || dependencies.Reads.SelectMany(Variables.SpatialSourceBytes).Any(storage.Contains))
            {
                return Reject("A fixed payment or self-contained Gil producer is not proved.");
            }
            int paymentPc = payments[0].ProgramCounter;
            if (payments[0].Entity != entity)
            {
                return Reject("The payment continuation is not root-owned.");
            }
            FieldScriptAnalysis afterPayment = AnalyzeScriptAtProgramCounter(entity, paymentPc + 6);
            if (!afterPayment.Complete || afterPayment.InstructionSites.Any(site => program.Read(site.ProgramCounter) is 0x3A or 0x3B))
            {
                return Reject("A Gil producer or repeated payment is reachable after the debit.");
            }
            if (program.Read(paymentPc + 1) != 0)
            {
                return Reject("Payment is not literal.");
            }
            uint cost = (uint)(program.Read(paymentPc + 2) | program.Read(paymentPc + 3) << 8 | program.Read(paymentPc + 4) << 16 | program.Read(paymentPc + 5) << 24);
            if (cost == 0)
            {
                return Reject("Payment has no positive fixed cost.");
            }
            FieldVariables projected = new(Variables.CopyPersistent(), Variables.CopyMap());
            foreach (var output in outputs)
            {
                projected.WriteWord(output.Key.Bank, (byte)output.Key.Operand, unchecked((short)(output.Value ? gil >> 16 : gil)));
            }
            HashSet<FieldVariableRead> scratch = new(storage);
            List<string> guards = [];
            HashSet<FieldTransitionCommand> transitions = [];
            int choices = 0;
            foreach (FieldScriptInstructionSite site in body.InstructionSites)
            {
                int pc = site.ProgramCounter;
                byte opcode = program.Read(pc);
                if (opcode == 0x3B || opcode is >= 1 and <= 6)
                {
                    continue;
                }
                List<FieldVariableRead> reads = [];
                List<FieldVariableRead> writes = [];
                FieldVariables values = new(projected.CopyPersistent(), projected.CopyMap());
                values.ReadObserved = reads.Add;
                values.WriteObserved = writes.Add;
                FieldMachine detached = new(program, values, host: new InspectionHost()) { inspection = new() };
                for (int index = 0; index < entities.Length; index++)
                {
                    detached.entities[index].ModelBound = entities[index].ModelBound;
                    detached.entities[index].PartyCharacter = entities[index].PartyCharacter;
                }
                detached.entities[site.Entity].Pc = pc;
                detached.entities[site.Entity].Priority = 1;
                detached.Execute(site.Entity);
                if (writes.SelectMany(Variables.SpatialSourceBytes).Any(write => write.Bank != FieldBank.MapByte))
                {
                    return Reject("Transport utility does not discard persistent progress writes.");
                }
                scratch.UnionWith(writes.SelectMany(Variables.SpatialSourceBytes));
                foreach (FieldCommand command in detached.inspection!.Commands)
                {
                    if (command is FieldTransitionCommand transition)
                    {
                        transitions.Add(transition);
                    }
                    if (command is FieldDialogueCommand { FirstChoice: not null })
                    {
                        choices++;
                    }
                    if (command is not (FieldGilQuery or FieldGilCommand { Subtract: true } or FieldActorBind or FieldActorPlacement or FieldActorMove or FieldMovementAnimationCommand or FieldAnimation or FieldActorProperty or FieldWindowCommand or FieldDialogueCommand or FieldTransitionCommand or FieldControlCommand or FieldBackgroundCommand or FieldAudioCommand or FieldCameraCommand or FieldFadeCommand or FieldMusicCommand or FieldMusicChangeCommand or FieldActorTurn or FieldPartyProjection or FieldPartyCharacter or FieldPaletteCommand or FieldActorOffset or FieldPreloadCommand or FieldTriangleCommand or FieldLineCommand or FieldModelEffectCommand or FieldPartyJoin or FieldPartySplit or FieldActorFace or FieldActorFaceParty or FieldActorLadder or FieldActorJump))
                    {
                        return Reject($"Transport utility does not classify {command.GetType().Name}.");
                    }
                }
                if (writes.SelectMany(Variables.SpatialSourceBytes).Any(storage.Contains))
                {
                    return Reject("A Gil alias has another native producer.");
                }
                foreach (FieldWindowCommand window in detached.inspection!.Commands.OfType<FieldWindowCommand>().Where(command => command.Operation == FieldWindowOperation.Parameter && command.ParameterBank != FieldBank.Immediate))
                {
                    return Reject("A deferred window parameter may consume Gil scratch.");
                }
                FieldVariableRead[] consumers = reads.Where(read => Variables.SpatialSourceBytes(read).Any(storage.Contains)).ToArray();
                if (consumers.Length == 0)
                {
                    continue;
                }
                if (opcode is < 0x14 or > 0x19)
                {
                    return Reject("A semantic operation consumes Gil scratch.");
                }
                detached.entities[site.Entity].Pc = pc;
                FieldConditional conditional = detached.ReadConditional(site.Entity, opcode);
                FieldVariableRead? input = outputs.ContainsKey(conditional.LeftSource) && conditional.RightSource.Bank == FieldBank.Immediate ? conditional.LeftSource : outputs.ContainsKey(conditional.RightSource) && conditional.LeftSource.Bank == FieldBank.Immediate ? conditional.RightSource : null;
                if (input is null || consumers.Any(read => read != input))
                {
                    return Reject($"{site.Entity}:{pc:X4}: A predicate does not consume an exact Gil word against a literal ({conditional.LeftSource}/{conditional.RightSource}; {string.Join(';', consumers.Select(value => value.ToString()))}).");
                }
                guards.Add($"{site.Entity}:{pc}:{detached.Compare(conditional.Left, conditional.Right, conditional.Operation)}");
            }
            if (transitions.Count != 1 || choices != 1)
            {
                return Reject("Transport has multiple or unknown native endpoint/choice outcomes.");
            }
            if (guards.Count == 0)
            {
                return Reject("No typed Gil affordability predicate is represented.");
            }
            HashSet<(int Entity, int Pc)> roots = possibleExternalEntries.Select(entry => (entry.Entity, (int)program.Entry(entry.Entity, entry.Script))).ToHashSet();
            for (int owner = 0; owner < entities.Length; owner++)
            {
                roots.Add((owner, entities[owner].Pc));
                foreach (int saved in entities[owner].Saved.Where(pc => pc != 0))
                {
                    roots.Add((owner, saved));
                }
            }
            foreach (FieldHostRequest pending in pendingRequests.Values)
            {
                roots.Add((pending.Entity, pending.ProgramCounter));
            }
            foreach (var root in roots)
            {
                if (root.Entity == entity && entities[root.Entity].Priority == 1 && body.InstructionSites.Any(site => site.Entity == root.Entity && site.ProgramCounter == root.Pc) && pendingRequests.Values.Any(request => request.Entity == root.Entity && request.Priority == 1 && request.ProgramCounter == root.Pc && request.Command is FieldDialogueCommand { FirstChoice: not null }))
                {
                    continue;
                }
                FieldScriptAnalysis analysis = AnalyzeScriptCore(root.Entity, 0, root.Pc, 8192, null, 0);
                FieldScriptEntryDependencies incoming = AnalyzeEntryDependenciesCore(root.Entity, 0, root.Pc, analysis, 8192);
                if (root.Entity != entity || root.Pc != program.Entry(entity, script))
                {
                    foreach (FieldScriptInstructionSite site in analysis.InstructionSites)
                    {
                        if (program.Read(site.ProgramCounter) is >= 1 and <= 6)
                        {
                            continue;
                        }
                        FieldVariables values = new(Variables.CopyPersistent(), Variables.CopyMap());
                        bool aliases = false;
                        values.WriteObserved = write => aliases |= values.SpatialSourceBytes(write).Any(storage.Contains);
                        FieldMachine writer = new(program, values, host: new InspectionHost()) { inspection = new() };
                        for (int index = 0; index < entities.Length; index++)
                        {
                            writer.entities[index].ModelBound = entities[index].ModelBound;
                            writer.entities[index].PartyCharacter = entities[index].PartyCharacter;
                        }
                        writer.entities[site.Entity].Pc = site.ProgramCounter;
                        writer.entities[site.Entity].Priority = 1;
                        writer.Execute(site.Entity);
                        if (aliases)
                        {
                            return Reject("An external native continuation can overwrite Gil output storage.");
                        }
                    }
                }
                if (!incoming.Complete || incoming.Reads.SelectMany(Variables.SpatialSourceBytes).Any(scratch.Contains))
                {
                    return Reject($"{root.Entity}:{root.Pc:X4}: Transport scratch has an unknown or live future consumer ({string.Join(';', incoming.Reads.SelectMany(Variables.SpatialSourceBytes).Where(scratch.Contains))}).");
                }
            }
            string payload = $"transport-region:1:{program.Identity}:{entity}:{program.Entry(entity, script)}:{cost}:{string.Join(';', guards.Order(StringComparer.Ordinal))}:{ReadScriptAnalysisFingerprint(entity, script, dependencies.Reads)}";
            return new(true, cost, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))), dependencies.Reads) { Transition = transitions.Single() };
        }
        catch (Exception error) when (error is NotSupportedException or InvalidDataException or ArgumentException or IndexOutOfRangeException or FieldExecutionException)
        {
            return Reject(error.Message);
        }
    }
}
