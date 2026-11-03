using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record BattleEncounterPolicyState(string GenerationIdentity, ImmutableDictionary<string, EncounterActionCursor> Cursors, EncounterPolicyDecision? PendingSummon = null, bool CleanedUp = false, bool TerminalStarted = false, bool TerminalReached = false);

public sealed partial class BattleSession
{
    private BattleEncounterPolicyState? encounterPolicies;

    public BattleEncounterPolicyState? EncounterPolicies => encounterPolicies;

    private string? ResolveEncounterRole(string role)
    {
        if (role == "original")
        {
            return definition.EncounterMod?.OriginalActorId;
        }
        if (role.StartsWith("scene-", StringComparison.Ordinal))
        {
            return combatants.SingleOrDefault(actor => !actor.IsPlayer && actor.SceneSlot?.ToString(System.Globalization.CultureInfo.InvariantCulture) == role[6..] && !nativeAllocations.Any(allocation => allocation.Id == actor.Id))?.Id;
        }
        return nativeAllocations.SingleOrDefault(allocation => allocation.EncounterRole == role)?.Id ?? (byId.ContainsKey(role) ? role : null);
    }

    private string EncounterRoleOf(string actorId)
    {
        return definition.EncounterMod?.OriginalActorId == actorId ? "original" : nativeAllocations.SingleOrDefault(allocation => allocation.Id == actorId)?.EncounterRole ?? actorId;
    }

    private string? EncounterPolicyOf(BattleCombatant actor)
    {
        if (definition.EncounterMod is not { } mod)
        {
            return null;
        }
        string role = EncounterRoleOf(actor.Id);
        return mod.NormalActionPolicies.GetValueOrDefault(role) ?? mod.Templates.GetValueOrDefault(role)?.NormalActionPolicy;
    }

    private bool HasEncounterPolicy(BattleCombatant actor)
    {
        return EncounterPolicyOf(actor) is not null;
    }

    private ImmutableArray<EncounterPolicyActor> EncounterPolicyCandidates()
    {
        return combatants.Where(actor => actor.NativeIndex is >= 0 and < 7).Select(actor => new EncounterPolicyActor(actor.Id, EncounterRoleOf(actor.Id), actor.IsPlayer, actor.IsAlive, actor.IsActive, (NativeBattleStatusBits(actor) & 0x80) != 0, !actor.Untargetable)).ToImmutableArray();
    }

    private int NextEncounterRandomIndex(int count)
    {
        if (count is < 1 or > 256)
        {
            throw new InvalidDataException("Encounter selection requires one to 256 candidates.");
        }
        int limit = 256 - 256 % count;
        int draw;
        do
        {
            draw = NextRandomByte();
        }
        while (draw >= limit);
        return draw % count;
    }

    private bool TryRunEncounterPolicy(BattleCombatant actor, int callback)
    {
        if (callback != 1 || definition.EncounterMod is not { } mod || EncounterPolicyOf(actor) is not string policyId)
        {
            return false;
        }
        encounterPolicies ??= new(mod.GenerationIdentity, ImmutableDictionary<string, EncounterActionCursor>.Empty);
        if (encounterPolicies.CleanedUp || !actor.IsActive || !actor.IsAlive)
        {
            return true;
        }
        if ((NativeSavedStatusBits(actor) & 0x20) != 0)
        {
            TryRunNativeEnemyBerserk(actor, callback);
            return true;
        }
        if (TryRunEncounterNativeTerminal(actor, callback))
        {
            return true;
        }
        if ((NativeSavedStatusBits(actor) & 0x35) != 0 || (NativeBattleStatusBits(actor) & 9) != 0)
        {
            return true;
        }
        EncounterActionCursor cursor = encounterPolicies.Cursors.GetValueOrDefault(actor.Id) ?? new();
        EncounterPolicyDecision? decision = EncounterPolicyRuntime.Select(mod, policyId, cursor, actor.Id, EncounterPolicyCandidates(), scanKnowledge, NextEncounterRandomIndex);
        if (decision is null)
        {
            return true;
        }
        if (decision.Kind == EncounterCompiledActionKind.SummonGroup)
        {
            if (encounterPolicies.PendingSummon is not null || encounterAllocation is not null)
            {
                throw new InvalidDataException("A summon policy attempted to repeat an already admitted encounter allocation.");
            }
            if (Enumerable.Range(3, 4).Count(index => !combatants.Any(candidate => candidate.NativeIndex == index && candidate.IsActive)) < decision.Spawns.Length)
            {
                throw new InvalidDataException("The authored summon exceeds the available simultaneous enemy capacity.");
            }
            if (decision.Spawns.Any(spawn => mod.Templates[spawn.Template].InheritFrom != EncounterRoleOf(actor.Id)))
            {
                throw new InvalidDataException("An atomic summon batch requires every template to inherit from its acting source allocation.");
            }
            BattleNativeAction control = NativeReturnDamageAction() with { ActionId = 9, Name = "Summon" };
            BattleAttackCommand command = new(actor.Id, actor.Id, BattleAbility.EnemyAbility, 9) { NativeAiCallbackStage = 1, NativeQueuedCommand = 0, NativeSequence = mod.CastingSequence, NativeAction = control, NativeTargetMask = checked((ushort)(1 << actor.NativeIndex)) };
            QueueEncounterNativeEvents(actor, decision.OwnTurn);
            EnqueueNativePacket(actor, command, "Summon");
            encounterPolicies = encounterPolicies with { PendingSummon = decision, Cursors = encounterPolicies.Cursors.SetItem(actor.Id, cursor.Admit()) };
        }
        else
        {
            QueueEncounterNativeEvents(actor, decision.OwnTurn);
            QueueEncounterSpell(actor, decision, mod);
            encounterPolicies = encounterPolicies with { Cursors = encounterPolicies.Cursors.SetItem(actor.Id, cursor.Admit()) };
        }
        Log("EncounterPolicy", actor.Id, decision.TargetIds.FirstOrDefault(), 0, $"{policyId}/{decision.RuleId}: own turn {decision.OwnTurn}.");
        return true;
    }

    private void QueueEncounterSpell(BattleCombatant actor, EncounterPolicyDecision decision, EncounterCompiledDefinition mod)
    {
        EncounterCompiledSpell spell = decision.Spell ?? throw new InvalidDataException("An admitted cast lacks its compiled native spell.");
        ushort Mask(EncounterPolicyDecision cast)
        {
            ushort mask = 0;
            foreach (string id in cast.TargetIds)
            {
                mask |= checked((ushort)(1 << GetActor(id).NativeIndex));
            }
            if (cast.TargetIds.Length > 1 || cast.OverrideNativeTargeting && mod.Policies[cast.PolicyId].Select(cast.OwnTurn).Action.Target is EncounterCompiledTargetKind.AllOpponents or EncounterCompiledTargetKind.GroupAllLiving)
            {
                mask |= 0x8000;
            }
            return mask;
        }
        ushort firstMask = Mask(decision);
        ImmutableArray<ushort> repeats = [];
        if (decision.NativeDouble && (NativeBattleStatusBits(actor) & 0x20000) != 0)
        {
            EncounterCompiledRule rule = mod.Policies[decision.PolicyId].Select(decision.OwnTurn);
            EncounterCompiledRule pinned = rule with { Action = rule.Action with { Spells = [spell] } };
            EncounterPolicyDecision? second = EncounterPolicyRuntime.Resolve(mod, decision.PolicyId, pinned, decision.OwnTurn, actor.Id, EncounterPolicyCandidates(), scanKnowledge, NextEncounterRandomIndex);
            repeats = second is null ? [firstMask] : [firstMask, Mask(second)];
        }
        BattleAttackCommand command = new(actor.Id, decision.TargetIds[0], BattleAbility.Magic, spell.NativeAction.ActionId)
        {
            NativeAiCallbackStage = 1,
            NativeQueuedCommand = 2,
            NativeSequence = mod.CastingSequence,
            NativeAction = spell.NativeAction,
            NativeTargetMask = firstMask,
            NativeMagicTargets = repeats
        };
        NativeBattleActionProductionContext? previous = nativeProductionContext;
        int firstHeader = nativeActionPackets.ActionCount;
        if (repeats.Length > 1)
        {
            nativeProductionContext = new(0, 1, false, 0x47DE70);
        }
        try
        {
            EnqueueNativePacket(actor, command, spell.CanonicalName);
            if (repeats.Length > 1)
            {
                nativeActionPackets = nativeActionPackets.WritePlayerRepeatMode(firstHeader, checked((byte)nativeActionPackets.Headers.Skip(firstHeader).Take(nativeActionPackets.ActionCount - firstHeader).Count(header => header.ReadByte(1) == 2)));
            }
        }
        finally
        {
            nativeProductionContext = previous;
        }
    }

    private void CompleteEncounterPolicyPresentation(BattleActionPresentation action)
    {
        if (encounterPolicies?.PendingSummon is not { } summon || definition.EncounterMod is not { } mod || action.ActorId != mod.OriginalActorId || action.NativeQueuedCommand != 0 || action.ActionId != 9 || encounterAllocation is not null)
        {
            return;
        }
        BeginEncounterAllocation(summon.PolicyId + "/" + summon.RuleId, action.ActorId, summon.Spawns.Select(spawn => spawn.Template).ToArray());
    }

    private void RestoreEncounterPolicies(BattleEncounterPolicyState? state)
    {
        if (state is not null && (definition.EncounterMod is not { } mod || state.GenerationIdentity != mod.GenerationIdentity || state.Cursors is null || state.Cursors.Any(pair => !byId.ContainsKey(pair.Key) || pair.Value is null)))
        {
            throw new InvalidDataException("Encounter policy snapshot requires its captured definition generation and allocation-owned cursors.");
        }
        encounterPolicies = state;
        ValidateEncounterNativeTerminalWitness(state);
    }

    private NativeBattleGraphicsState AdvanceEncounterPolicySpawn(NativeBattleGraphicsState graphics)
    {
        if (encounterPolicies?.PendingSummon is not { } summon || definition.EncounterMod is not { } mod || encounterAllocation is not { } batch)
        {
            return graphics;
        }
        if (batch.Phase == BattleEncounterAllocationPhase.Ready)
        {
            NativeGraphics = graphics;
            List<EncounterArrivalTrack> tracks = [];
            NativeBattleModelTransform original = graphics.Models[GetActor(batch.SourceActorId).NativeIndex].Transform ?? throw new InvalidDataException("Encounter arrival requires the source actor's native placement.");
            for (int index = 0; index < batch.ActorIds.Length; index++)
            {
                EncounterCompiledSpawn spawn = summon.Spawns[index];
                EncounterCompiledTemplate template = mod.Templates[spawn.Template];
                EncounterCompiledPresentation presentation = mod.Presentations[template.Arrival];
                EncounterCompiledPlacement placement = mod.Placements[spawn.Anchor];
                int physical = GetActor(batch.ActorIds[index]).NativeIndex;
                NativeBattleVector Offset(NativeBattleVector value)
                {
                    return new(checked((short)(value.X + (int)MathF.Round(placement.X))), checked((short)(value.Y + (int)MathF.Round(placement.Y))), checked((short)(value.Z + (int)MathF.Round(placement.Z))));
                }
                NativeBattleGraphicsModel model = graphics.Models[physical];
                NativeBattleModelTransform transform = model.Transform ?? throw new InvalidDataException("Encounter arrival requires the copy's initialized native transform.");
                graphics = graphics with { Models = graphics.Models.SetItem(physical, model with { Transform = transform with { Initial20 = Offset(original.Initial20), Position28 = Offset(original.Position28) } }) };
                tracks.Add(new(batch.ActorIds[index], byte.Parse(presentation.Clip, System.Globalization.CultureInfo.InvariantCulture), checked((byte)presentation.StartFrame), checked((byte)presentation.EndFrame), graphics.Callback, OpacityFrom: (float)presentation.OpacityFrom, OpacityTo: (float)presentation.OpacityTo));
            }
            NativeGraphics = graphics;
            StartEncounterArrival(tracks);
            BeginEncounterAllocationArrival();
            return NativeGraphics!;
        }
        if (batch.Phase == BattleEncounterAllocationPhase.Arriving && batch.ActorIds.All(EncounterArrivalComplete))
        {
            NativeGraphics = graphics;
            CompleteEncounterAllocationArrival();
            encounterPolicies = encounterPolicies with { PendingSummon = null };
            return NativeGraphics!;
        }
        return graphics;
    }

    private void CleanupEncounterPoliciesAtTerminal()
    {
        if (definition.EncounterMod is not { } mod || encounterPolicies is not { CleanedUp: false } state)
        {
            return;
        }
        BattleCombatant original = GetActor(mod.OriginalActorId);
        if (!state.TerminalStarted && !state.TerminalReached && Outcome == BattleOutcome.Active && !nativeScriptState.ScriptedExitRequested && original.IsActive && !(original.CurrentHp == 0 && original.AiDeathHandled))
        {
            return;
        }
        HashSet<string> retiring = nativeAllocations.Where(allocation => allocation.EncounterBatchId is not null).Select(allocation => allocation.Id).ToHashSet(StringComparer.Ordinal);
        foreach (string id in retiring)
        {
            BattleCombatant actor = GetActor(id);
            if (actor.NativeIndex is >= 0 and < 7)
            {
                RemoveNativeQueuedActorCommands(actor);
                nativeCommandBanks = nativeCommandBanks.RemoveActorCallbacks(checked((byte)actor.NativeIndex), actor.Id);
            }
            nativeCommandBanks = nativeCommandBanks.RemoveAllocationCallbacks(actor.Id);
            ReleaseNativeParticipant(actor);
            RequestEncounterAllocationRetirement(actor);
            actor.IsBusy = false;
            actor.Atb = 0;
            if (NativeGraphics is { } graphics && actor.NativeIndex is >= 0 and < 7)
            {
                NativeBattleGraphicsModel model = graphics.Models[actor.NativeIndex];
                NativeGraphics = graphics with { Models = graphics.Models.SetItem(actor.NativeIndex, model with { Flags0 = (ushort)(model.Flags0 | 4) }) };
            }
        }
        BattleAttackCommand[] retained = actions.Where(command => !retiring.Contains(command.ActorId)).ToArray();
        actions.Clear();
        foreach (BattleAttackCommand command in retained)
        {
            actions.Enqueue(command);
        }
        encounterPolicies = state with { CleanedUp = true, PendingSummon = null };
        if (encounterAllocation is not null && NativeScriptAllocationPending && nativeBattleClock is not null)
        {
            CompleteNativeAllocationGate();
        }
    }
}
