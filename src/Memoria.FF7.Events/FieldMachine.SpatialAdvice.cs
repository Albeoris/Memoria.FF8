namespace Memoria.FF7.Events;

public sealed partial class FieldMachine
{
    private int[]? ReadSpatialChildDestinationAdvice(int sourceEntity, int targetEntity, int targetScript, int requestMode, int requestPriority, IReadOnlySet<FieldVariableRead> sampledBytes, FieldSpatialAdviceContext context, out string? reason, ISet<FieldVariableRead> dependencies, int instructionBudget = 8192)
    {
        reason = null;
        // A completed synchronous request suspends its producer; started/nowait requests do not own that interval.
        if (requestMode != 3 || sourceEntity == targetEntity || entities[targetEntity].Priority <= requestPriority || sampledBytes.Count == 0)
        {
            reason = "The sampled child has no exclusive synchronous ownership interval.";
            return null;
        }
        int entry = program.Entry(targetEntity, targetScript);
        if (context.FixedPlayerEntity is not null && (program.Read(entry) != 0x33 || program.Read(entry + 1) == 0))
        {
            reason = "A scoped LINE publication horizon requires the native child to lock free player input before yielding.";
            return null;
        }
        HashSet<(int Entity, int Pc)> roots = [(targetEntity, program.Entry(targetEntity, targetScript))];
        for (int owner = 0; owner < entities.Length; owner++)
        {
            // These captured continuations cannot run before the selected child returns. Other producers may still request them.
            if (owner == sourceEntity || owner == targetEntity)
            {
                continue;
            }
            roots.Add((owner, entities[owner].Pc));
            foreach (int saved in entities[owner].Saved.Where(pc => pc != 0))
            {
                roots.Add((owner, saved));
            }
            if (entities[owner].Priority < 7 && entities[owner].Saved[7] == 0)
            {
                roots.Add((owner, 0));
            }
        }
        foreach (FieldHostRequest pending in pendingRequests.Values)
        {
            roots.Add((pending.Entity, pending.ProgramCounter));
        }
        foreach (FieldNavigationEntryPoint possible in context.PossibleEntries)
        {
            roots.Add((possible.Entity, program.Entry(possible.Entity, possible.Script)));
        }
        FieldBank[] persistentBanks = [FieldBank.Persistent0Byte, FieldBank.Persistent1Byte, FieldBank.Persistent2Byte, FieldBank.Persistent3Byte, FieldBank.Persistent4Byte];
        HashSet<FieldVariableRead> liveFacts = persistentBanks.SelectMany(bank => Enumerable.Range(0, 256).Select(address => new FieldVariableRead(bank, (ushort)address, false))).Concat(sampledBytes).ToHashSet();
        NavigationByteDomains closure = new(Variables, context.ExternalWrites, Compare, liveFacts);
        HashSet<FieldVariableRead> writes = [];
        int instructions = 0;
        bool allocationMutable = false;
        while (true)
        {
            int revision = closure.Revision;
            NavigationInvariantInspection producers = new(closure, context.PossibleEntries.ToHashSet()) { RestrictPartyRecipientsToCurrentAllocation = true, MayChangePartyAllocation = allocationMutable };
            foreach (var root in roots)
            {
                int remaining = instructionBudget - instructions - producers.Instructions;
                if (remaining < 1)
                {
                    reason = "The bounded sampled producer closure exhausted its instruction budget.";
                    return null;
                }
                int commandStart = producers.Commands.Count;
                FieldScriptAnalysis producer = AnalyzeScriptCore(root.Entity, 0, root.Pc, remaining, null, 0, producers);
                dependencies.UnionWith(producers.AssumedReads.Where(closure.IsSingleton).Where(read => read.Bank is not (FieldBank.MapByte or FieldBank.MapWord)));
                if (!producer.Complete)
                {
                    reason = $"Producer {root.Entity}:{root.Pc:X4}: {producer.Reason}";
                    return null;
                }
                if (context.FixedPlayerEntity is not null && producers.Commands.Any(publication => InvalidatesLockedLineHorizon(publication.Command)))
                {
                    var changed = producers.Commands.First(publication => InvalidatesLockedLineHorizon(publication.Command));
                    reason = $"Producer {root.Entity}:{root.Pc:X4} can invalidate the locked LINE publication horizon through {changed.Entity}/{changed.Command.GetType().Name}.";
                    return null;
                }
                bool selectedEntry = root.Entity == targetEntity && root.Pc == entry;
                if (context.FixedPlayerEntity is int player && !selectedEntry && producers.Commands.Skip(commandStart).Any(publication => publication.Entity == player && InvalidatesPreLockPlayerGeometry(publication.Command)))
                {
                    reason = $"Producer {root.Entity}:{root.Pc:X4} can relocate or resize the sampled player before the selected child acquires its input lock.";
                    return null;
                }
                foreach (FieldVariableRead write in producers.Writes)
                {
                    closure.Join(write, null);
                }
            }
            instructions += producers.Instructions;
            writes.UnionWith(producers.Writes);
            bool allocationChanged = allocationMutable != producers.MayChangePartyAllocation;
            allocationMutable = producers.MayChangePartyAllocation;
            if (closure.Revision == revision && !allocationChanged)
            {
                break;
            }
        }
        HashSet<FieldVariableRead> known = sampledBytes.Except(writes).Except(context.ExternalWrites.SelectMany(Variables.SpatialSourceBytes)).ToHashSet();
        if (known.Count == 0 || instructions >= instructionBudget)
        {
            reason = "Native producers can clobber every sampled child-entry byte.";
            return null;
        }
        NavigationInvariantInspection selected = new(closure, context.PossibleEntries.ToHashSet()) { RestrictPartyRecipientsToCurrentAllocation = true, MayChangePartyAllocation = allocationMutable };
        FieldScriptAnalysis advice = AnalyzeScriptCore(targetEntity, targetScript, program.Entry(targetEntity, targetScript), instructionBudget - instructions, null, 0, selected);
        dependencies.UnionWith(selected.AssumedReads.Where(closure.IsSingleton).Where(read => read.Bank is not (FieldBank.MapByte or FieldBank.MapWord)));
        reason = advice.Reason;
        return advice.Complete ? advice.DestinationFields : null;
    }

    private static bool InvalidatesLockedLineHorizon(FieldCommand command)
    {
        return command is FieldLineCommand or FieldPartyJoin or FieldPartySplit or FieldPartyProjection or FieldMenuCommand or FieldBattleCommand or FieldMinigameCommand or FieldActorBind or FieldPartyCharacter or FieldActorProperty { Property: FieldActorPropertyKind.PlayerCharacter } or FieldControlCommand { Property: FieldControlKind.PlayerLocked, Value: 0 };
    }

    private static bool InvalidatesPreLockPlayerGeometry(FieldCommand command)
    {
        return command is FieldActorPlacement or FieldActorMove or FieldActorLadder or FieldActorJump or FieldActorProperty { Property: FieldActorPropertyKind.CollisionRadius };
    }
}
