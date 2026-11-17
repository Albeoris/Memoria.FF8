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
            return enemies.LastOrDefault(actor => actor.IsActive && actor.SceneSlot?.ToString(System.Globalization.CultureInfo.InvariantCulture) == role[6..] &&
                !nativeAllocations.Any(allocation => allocation.Id == actor.Id && allocation.EncounterBatchId is not null))?.Id;
        }
        return nativeAllocations.LastOrDefault(allocation => allocation.EncounterRole == role && GetActor(allocation.Id).IsActive)?.Id ?? (byId.ContainsKey(role) ? role : null);
    }

    private string EncounterRoleOf(string actorId)
    {
        if (definition.EncounterMod?.OriginalActorId == actorId)
        {
            return "original";
        }
        return nativeAllocations.SingleOrDefault(allocation => allocation.Id == actorId)?.EncounterRole ??
            (byId.GetValueOrDefault(actorId) is { IsPlayer: false, SceneSlot: int scene } ? $"scene-{scene}" : actorId);
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
        return combatants.Where(actor => actor.Slot is not null).Select(actor => new EncounterPolicyActor(actor.Id, EncounterRoleOf(actor.Id), actor.IsPlayer, actor.IsAlive, actor.IsActive, (NativeBattleStatusBits(actor) & 0x80) != 0, !actor.Untargetable, NativeBattleStatusBits(actor), NativeSavedStatusBits(actor))).ToImmutableArray();
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
            if (encounterPolicies.PendingSummon is not null || encounterAllocation is { Phase: not BattleEncounterAllocationPhase.Complete })
            {
                throw new InvalidDataException("A summon policy attempted to repeat an already admitted encounter allocation.");
            }
            if (ReadFreeEncounterRosterSlots(decision.Spawns.Length).Length != decision.Spawns.Length)
            {
                throw new InvalidDataException("The authored summon exceeds the available simultaneous enemy capacity.");
            }
            foreach (EncounterCompiledSpawn spawn in decision.Spawns)
            {
                _ = ReadEncounterCopySource(actor, spawn.Template);
            }
            BattleNativeAction control = CreateEncounterSummonControl();
            BattleAttackCommand command = new(actor.Id, actor.Id, SelectBattleTarget(actor), BattleAbility.EnemyAbility, 9) { NativeAiCallbackStage = 1, NativeQueuedCommand = 0, NativeSequence = mod.CastingSequence, NativeAction = control };
            QueueEncounterNativeEvents(actor, decision.OwnTurn);
            BattleEncounterPolicyState admitted = encounterPolicies with { PendingSummon = decision, Cursors = encounterPolicies.Cursors.SetItem(actor.Id, cursor.Admit()) };
            encounterPolicies = admitted;
            ProduceExpandedAiCallbackCommand(command);
        }
        else
        {
            QueueEncounterNativeEvents(actor, decision.OwnTurn);
            ImmutableArray<string> targets = QueueEncounterSpell(actor, decision, mod);
            encounterPolicies = encounterPolicies with { Cursors = encounterPolicies.Cursors.SetItem(actor.Id, cursor.Admit(targets)) };
        }
        Log("EncounterPolicy", actor.Id, decision.TargetIds.FirstOrDefault(), 0, $"{policyId}/{decision.RuleId}: own turn {decision.OwnTurn}.");
        return true;
    }

    private uint EncounterMulticastStatusBits(BattleCombatant actor)
    {
        uint statuses = NativeBattleStatusBits(actor);
        if (definition.EncounterMod is { } mod && mod.Templates.TryGetValue(EncounterRoleOf(actor.Id), out EncounterCompiledTemplate? template) && ResolveEncounterRole(template.InheritFrom) is string sourceId)
        {
            BattleCombatant source = GetActor(sourceId);
            if (source.IsActive && source.IsAlive)
            {
                statuses |= NativeBattleStatusBits(source) & 0x60000;
            }
        }
        return statuses;
    }

    private ImmutableArray<string> QueueEncounterSpell(BattleCombatant actor, EncounterPolicyDecision decision, EncounterCompiledDefinition mod)
    {
        EncounterCompiledSpell spell = decision.Spell ?? throw new InvalidDataException("An admitted cast lacks its compiled native spell.");
        BattleTargetSelection SelectTargets(IEnumerable<string> ids)
        {
            return new BattleTargetSelection.Explicit(ids.Select(id => TargetActorReference(GetActor(id))));
        }
        BattleTargetRequest Request(BattleTargetSelection selection)
        {
            return new(selection, BattleTargetEligibility.Living, false, BattleTargetReplacement.UseCurrentBattlePolicy);
        }

        BattleTargetSelection firstSelection = SelectTargets(decision.TargetIds);
        ImmutableArray<BattleTargetSelection> repeats = [];
        ImmutableArray<string> targets = decision.TargetIds;
        uint statuses = EncounterMulticastStatusBits(actor);
        bool mirror = mod.Templates.ContainsKey(EncounterRoleOf(actor.Id));
        int castCount = !decision.NativeDouble ? 1 : (statuses & 0x40000) != 0 ? 3 : (statuses & 0x20000) != 0 ? 2 : 1;
        if (mirror && decision.TargetIds.Length > 1 && !spell.NativeAllTargets)
        {
            List<string> available = decision.TargetIds.ToList();
            List<BattleTargetSelection> selections = [];
            List<string> selected = [];
            for (int cast = 0; cast < castCount; cast++)
            {
                if (available.Count == 0)
                {
                    available.AddRange(decision.TargetIds);
                }
                string[] preferred = available.Where(id => (NativeBattleStatusBits(GetActor(id)) & spell.NativeAction.Statuses1) != spell.NativeAction.Statuses1 || (NativeSavedStatusBits(GetActor(id)) & spell.NativeAction.Statuses0) != spell.NativeAction.Statuses0).ToArray();
                if (preferred.Length == 0)
                {
                    preferred = available.ToArray();
                }
                ImmutableArray<string> previousTargets = encounterPolicies?.Cursors.GetValueOrDefault(actor.Id)?.LastTargetIds ?? [];
                string[] fresh = preferred.Where(id => !previousTargets.Contains(id, StringComparer.Ordinal)).ToArray();
                if (fresh.Length != 0)
                {
                    preferred = fresh;
                }
                string id = preferred[NextEncounterRandomIndex(preferred.Length)];
                available.Remove(id);
                selected.Add(id);
                selections.Add(SelectTargets([id]));
            }
            targets = selected.Distinct(StringComparer.Ordinal).ToImmutableArray();
            firstSelection = selections[0];
            repeats = selections.Count > 1 ? selections.ToImmutableArray() : [];
        }
        else if (castCount > 1)
        {
            EncounterCompiledRule rule = mod.Policies[decision.PolicyId].Select(decision.OwnTurn);
            EncounterCompiledAction pinnedAction = rule.Action with { Spells = [spell] };
            if (decision.Delivery == EncounterOffensiveDelivery.Dispel)
            {
                pinnedAction = pinnedAction with { Target = EncounterCompiledTargetKind.RandomReflectedOpponent, AvoidReflect = false };
            }
            EncounterCompiledRule pinned = rule with { Action = pinnedAction };
            ImmutableArray<BattleTargetSelection>.Builder selections = ImmutableArray.CreateBuilder<BattleTargetSelection>(castCount);
            selections.Add(firstSelection);
            for (int cast = 1; cast < castCount; cast++)
            {
                EncounterPolicyDecision? next = EncounterPolicyRuntime.Resolve(mod, decision.PolicyId, pinned, decision.OwnTurn, actor.Id, EncounterPolicyCandidates(), scanKnowledge, NextEncounterRandomIndex, targets, distinctTargets: true);
                if (next is null)
                {
                    break;
                }
                selections.Add(SelectTargets(next.TargetIds));
                targets = targets.Concat(next.TargetIds).Distinct(StringComparer.Ordinal).ToImmutableArray();
            }
            repeats = selections.Count == 1 ? [] : selections.ToImmutable();
        }
        BattleAttackCommand command = new(actor.Id, targets[0], Request(firstSelection), BattleAbility.Magic, spell.NativeAction.ActionId)
        {
            NativeAiCallbackStage = 1,
            NativeQueuedCommand = 2,
            NativeSequence = mod.CastingSequence,
            NativeAction = spell.NativeAction,
            MagicCasts = repeats.Select(selection => new BattleMagicCastInvocation(Request(selection), selection,
                selection is BattleTargetSelection.Explicit { Actors: [var first, ..] } ? first : TargetActorReference(GetActor(targets[0])))).ToImmutableArray()
        };
        ProduceExpandedAiCallbackCommand(command);
        return targets;
    }

    private void CompleteEncounterPolicyPresentation(BattleActionPresentation action)
    {
        if (encounterPolicies?.PendingSummon is not { } summon || EncounterPolicyOf(GetActor(action.ActorId)) != summon.PolicyId ||
            action.NativeQueuedCommand != 0 || action.ActionId != 9 || encounterAllocation is { Phase: not BattleEncounterAllocationPhase.Complete })
        {
            return;
        }
        BeginEncounterAllocation(summon.PolicyId + "/" + summon.RuleId, action.ActorId, summon.Spawns.Select(spawn => spawn.Template).ToArray());
    }

    private static BattleNativeAction CreateEncounterSummonControl()
    {
        return NativeReturnDamageAction() with { ActionId = 9, Name = "Summon", PresentationOnly = true };
    }

    private BattleNativeAction ValidateEncounterSummonControl(BattleCombatant actor, string targetId, BattleAbility ability, int actionId, BattleTargetRequest request)
    {
        if (definition.EncounterMod is not { } mod || EncounterPolicyOf(actor) is not string policy || !mod.Policies[policy].Rules.Any(rule => rule.Action.Kind == EncounterCompiledActionKind.SummonGroup) || actor.Id != targetId || ability != BattleAbility.EnemyAbility || actionId != 9 || request.Selection is not BattleTargetSelection.Explicit selection || selection.Actors.Length != 1 || selection.Actors[0] != TargetActorReference(actor))
        {
            throw new InvalidDataException("Saved encounter presentation control requires its declared SummonGroup policy and exact self target.");
        }
        return CreateEncounterSummonControl();
    }

    private void ValidateEncounterSummonState(BattleEncounterPolicyState? state)
    {
        void Validate(string actorId, byte? command, byte? sequence, byte? callback)
        {
            if (definition.EncounterMod is not { } mod || state?.PendingSummon is not { } pending || EncounterPolicyOf(GetActor(actorId)) != pending.PolicyId || !state.Cursors.TryGetValue(actorId, out EncounterActionCursor? cursor) || cursor.CommittedTurns != pending.OwnTurn || pending.OwnTurn == 0 || command != 0 || sequence != mod.CastingSequence || callback != 1)
            {
                throw new InvalidDataException("Saved encounter Summon control lacks its admitted policy cursor and casting command.");
            }
            EncounterCompiledRule rule = mod.Policies[pending.PolicyId].Select(pending.OwnTurn);
            EncounterPolicyDecision expected = new(pending.PolicyId, rule.Id, pending.OwnTurn, EncounterCompiledActionKind.SummonGroup, null, [], false, true, rule.Action.Spawns);
            if (rule.Action.Kind != EncounterCompiledActionKind.SummonGroup || !System.Text.Json.JsonElement.DeepEquals(System.Text.Json.JsonSerializer.SerializeToElement(expected), System.Text.Json.JsonSerializer.SerializeToElement(pending)))
            {
                throw new InvalidDataException("Saved encounter Summon decision differs from its declared policy rule.");
            }
        }
        foreach (BattleAttackCommand command in QueuedActions.Where(command => command.NativeAction?.PresentationOnly == true))
        {
            Validate(command.ActorId, command.NativeQueuedCommand, command.NativeSequence, command.NativeAiCallbackStage);
        }
        if (ActiveAction is { NativeAction.PresentationOnly: true } action)
        {
            Validate(action.ActorId, action.NativeQueuedCommand, action.NativeSequence, action.NativeAiCallbackStage);
        }
    }

    private void RestoreEncounterPolicies(BattleEncounterPolicyState? state)
    {
        if (state is not null && (definition.EncounterMod is not { } mod || state.GenerationIdentity != mod.GenerationIdentity || state.Cursors is null || state.Cursors.Any(pair => !byId.ContainsKey(pair.Key) || pair.Value is null || pair.Value.LastTargetIds.IsDefault || pair.Value.LastTargetIds.Any(id => string.IsNullOrWhiteSpace(id) || !byId.ContainsKey(id)) || pair.Value.LastTargetIds.Distinct(StringComparer.Ordinal).Count() != pair.Value.LastTargetIds.Length)))
        {
            throw new InvalidDataException("Encounter policy snapshot requires its captured definition generation and allocation-owned cursors.");
        }
        encounterPolicies = state;
        ValidateEncounterSummonState(state);
        ValidateEncounterNativeTerminalWitness(state);
    }

    private void AdvanceHostedEncounterPolicySpawn(BattlePresentationProcess process)
    {
        if (encounterPolicies?.PendingSummon is not { } summon || definition.EncounterMod is not { } mod || encounterAllocation is not { } batch)
        {
            return;
        }
        if (batch.Phase == BattleEncounterAllocationPhase.Ready)
        {
            List<EncounterArrivalTrack> tracks = [];
            List<NativeBattleVector> offsets = [];
            BattleModelOwner anchor = CreatePresentationModelOwner(GetActor(batch.SourceActorId));
            for (int index = 0; index < batch.ActorIds.Length; index++)
            {
                EncounterCompiledSpawn spawn = summon.Spawns[index];
                EncounterCompiledTemplate template = mod.Templates[spawn.Template];
                EncounterCompiledPresentation arrival = mod.Presentations[template.Arrival];
                EncounterCompiledPlacement placement = mod.Placements[spawn.Anchor];
                offsets.Add(new(checked((short)MathF.Round(placement.X)), checked((short)MathF.Round(placement.Y)), checked((short)MathF.Round(placement.Z))));
                tracks.Add(new(batch.ActorIds[index], byte.Parse(arrival.Clip, System.Globalization.CultureInfo.InvariantCulture), checked((byte)arrival.StartFrame), checked((byte)arrival.EndFrame), process.Graphics.Callback, OpacityFrom: (float)arrival.OpacityFrom, OpacityTo: (float)arrival.OpacityTo));
            }
            process.StartEncounterArrival(tracks, anchor, offsets);
            BeginEncounterAllocationArrival(process);
        }
        if (batch.Phase == BattleEncounterAllocationPhase.Arriving && batch.ActorIds.All(process.EncounterArrivalComplete))
        {
            CompleteEncounterAllocationArrival();
            encounterPolicies = encounterPolicies with { PendingSummon = null };
        }
        encounterArrivals = process.CaptureEncounterArrivals();
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
            if (actor.IsActive)
            {
                ReleaseNativeParticipant(actor);
            }
            RequestEncounterAllocationRetirement(actor);
            actor.IsBusy = false;
            actor.Atb = 0;
        }
        RemoveEncounterQueuedActions(retiring);
        encounterPolicies = state with { CleanedUp = true, PendingSummon = null };
        if (encounterAllocation is not null && NativeScriptAllocationPending && nativeBattleClock is not null)
        {
            CompleteNativeAllocationGate();
        }
    }
}
