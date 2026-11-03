using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime;

public enum EncounterCompiledActionKind
{
    Cast,
    CastFromPool,
    SummonGroup
}

public enum EncounterCompiledTargetKind
{
    Self,
    RandomOpponent,
    RandomAlly,
    AllOpponents,
    NativeOpponentTargeting,
    GroupAllLiving,
    RandomReflectedOpponent,
    ScanKnownWeakestElseRandom
}

public sealed record EncounterCompiledSpell(string CanonicalName, BattleNativeAction NativeAction, int ElementIndex, bool Reflectable, bool NativeAllTargets);
public sealed record EncounterCompiledSpawn(string Template, string Anchor);
public sealed record EncounterCompiledAction(EncounterCompiledActionKind Kind, ImmutableArray<EncounterCompiledSpell> Spells, EncounterCompiledTargetKind Target, string? Group, bool NativeDouble, bool AvoidReflect, EncounterCompiledSpell? Dispel, ImmutableArray<EncounterCompiledSpawn> Spawns);
public sealed record EncounterCompiledRule(string Id, ulong MinimumOwnTurn, ulong MaximumOwnTurn, int Priority, EncounterCompiledAction Action);
public sealed record EncounterCompiledPolicy(string Id, ImmutableArray<EncounterCompiledRule> Rules)
{
    public EncounterCompiledRule Select(ulong ownTurn)
    {
        if (ownTurn == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ownTurn));
        }
        return Rules.Where(rule => ownTurn >= rule.MinimumOwnTurn && ownTurn <= rule.MaximumOwnTurn).OrderByDescending(rule => rule.Priority).ThenBy(rule => rule.Id, StringComparer.Ordinal).FirstOrDefault() ?? throw new InvalidDataException($"Encounter policy '{Id}' has no normal-action rule for own turn {ownTurn}.");
    }
}

public sealed record EncounterCompiledTemplate(string Id, string InheritFrom, string Group, string NormalActionPolicy, string Arrival);
public sealed record EncounterCompiledPresentation(string Id, string Clip, int StartFrame, int EndFrame, double OpacityFrom, double OpacityTo);
public sealed record EncounterCompiledDamageRoute(string Id, string SourceActor, ImmutableArray<string> Recipients);
public sealed record EncounterCompiledEventGate(string Actor, string Event, ulong MinimumOwnTurn);
public sealed record EncounterCompiledPlacement(string Id, float X, float Y, float Z);

/// <summary>Snapshot-safe prepared data captured by a battle. No JSON tokens, mutable source documents or delegates are retained.</summary>
public sealed record EncounterCompiledDefinition(
    string Encounter,
    string SourceProfile,
    string GenerationIdentity,
    string OriginalActorId,
    byte CastingSequence,
    ImmutableDictionary<string, EncounterCompiledPolicy> Policies,
    ImmutableDictionary<string, string> NormalActionPolicies,
    ImmutableDictionary<string, EncounterCompiledTemplate> Templates,
    ImmutableDictionary<string, ImmutableArray<string>> Groups,
    ImmutableDictionary<string, EncounterCompiledPresentation> Presentations,
    ImmutableDictionary<string, EncounterCompiledDamageRoute> DamageRoutes,
    ImmutableDictionary<string, EncounterCompiledEventGate> EventGates,
    ImmutableDictionary<string, EncounterCompiledPlacement> Placements,
    ImmutableDictionary<string, double> ActorProperties);

/// <summary>Only observable candidate state is passed to policy selection; concealed defense values are absent.</summary>
public sealed record EncounterPolicyActor(string AllocationId, string RoleId, bool IsPlayer, bool Living, bool Active, bool Reflect, bool Targetable = true);
public sealed record EncounterPolicyDecision(string PolicyId, string RuleId, ulong OwnTurn, EncounterCompiledActionKind Kind, EncounterCompiledSpell? Spell, ImmutableArray<string> TargetIds, bool NativeDouble, bool OverrideNativeTargeting, ImmutableArray<EncounterCompiledSpawn> Spawns, EncounterOffensiveDelivery Delivery = EncounterOffensiveDelivery.Direct);

public static class EncounterPolicyRuntime
{
    // Canonical spell identities index the selected kernel spell table, independently of localized display names.
    private static readonly ImmutableDictionary<string, ushort> MagicIds = new Dictionary<string, ushort>(StringComparer.Ordinal)
    {
        ["Fire"] = 1,
        ["Fira"] = 2,
        ["Firaga"] = 3,
        ["Blizzard"] = 4,
        ["Blizzara"] = 5,
        ["Blizzaga"] = 6,
        ["Thunder"] = 7,
        ["Thundara"] = 8,
        ["Thundaga"] = 9,
        ["Water"] = 10,
        ["Aero"] = 11,
        ["Bio"] = 12,
        ["Quake"] = 17,
        ["Tornado"] = 18,
        ["Cure"] = 21,
        ["Cura"] = 22,
        ["Curaga"] = 23,
        ["Regen"] = 26,
        ["Dispel"] = 28,
        ["Protect"] = 29,
        ["Shell"] = 30,
        ["Reflect"] = 31,
        ["Double"] = 33,
        ["Haste"] = 35,
        ["Slow"] = 36,
        ["Stop"] = 37,
        ["Blind"] = 38,
        ["Confuse"] = 39,
        ["Silence"] = 41,
        ["Drain"] = 44,
        ["Scan"] = 50
    }.ToImmutableDictionary(StringComparer.Ordinal);

    public static EncounterCompiledDefinition Compile(EncounterPatchComposition composition, BattleDefinition definition, EncounterBindingResult binding)
    {
        ArgumentNullException.ThrowIfNull(composition);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(binding);
        EncounterPatchDocument.Require(binding.Alias == composition.Encounter && definition.Combatants.Count(actor => actor.Id == binding.OriginalActorId && !actor.IsPlayer) == 1, "Encounter compilation requires a matching verified original-actor binding.");
        ImmutableDictionary<string, EncounterCompiledPolicy>.Builder policies = ImmutableDictionary.CreateBuilder<string, EncounterCompiledPolicy>(StringComparer.Ordinal);
        foreach ((string id, EncounterPatchPolicy policy) in composition.Policies)
        {
            string document = DocumentOf(id);
            ImmutableArray<EncounterCompiledRule>.Builder rules = ImmutableArray.CreateBuilder<EncounterCompiledRule>(policy.Rules.Length);
            foreach (EncounterPatchRule rule in policy.Rules)
            {
                try
                {
                    EncounterCompiledAction action = CompileAction(document, rule.Action);
                    ulong minimum = (ulong)(rule.When.OwnTurn ?? rule.When.OwnTurnAtLeast ?? 1);
                    ulong maximum = rule.When.OwnTurn is int exact ? (ulong)exact : rule.When.OwnTurnAtMost is int end ? (ulong)end : ulong.MaxValue;
                    rules.Add(new(rule.Id, minimum, maximum, rule.Priority ?? 0, action));
                }
                catch (InvalidDataException error)
                {
                    throw new InvalidDataException($"Encounter '{composition.Encounter}', policy '{id}', rule '{rule.Id}': {error.Message}", error);
                }
            }
            policies.Add(id, new(id, rules.MoveToImmutable()));
        }
        ImmutableDictionary<string, EncounterCompiledTemplate> templates = composition.ActorTemplates.ToImmutableDictionary(pair => pair.Key, pair => new EncounterCompiledTemplate(pair.Key, Role(DocumentOf(pair.Key), pair.Value.InheritFrom!), Scope(DocumentOf(pair.Key), pair.Value.Group!), Scope(DocumentOf(pair.Key), pair.Value.NormalActionPolicy!), Scope(DocumentOf(pair.Key), pair.Value.Arrival!)), StringComparer.Ordinal);
        ImmutableDictionary<string, ImmutableArray<string>> groups = composition.Groups.ToImmutableDictionary(pair => pair.Key, pair => pair.Value.Concat(templates.Values.Where(template => template.Group == pair.Key).Select(template => template.Id)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToImmutableArray(), StringComparer.Ordinal);
        return new(composition.Encounter, composition.SourceProfile, composition.GenerationIdentity, binding.OriginalActorId, binding.CastingSequence, policies.ToImmutable(), composition.NormalActionPolicies, templates, groups,
            composition.Presentations.ToImmutableDictionary(pair => pair.Key, pair => new EncounterCompiledPresentation(pair.Key, pair.Value.Clip!, pair.Value.StartFrame!.Value, pair.Value.EndFrame!.Value, pair.Value.Opacity!.From, pair.Value.Opacity.To), StringComparer.Ordinal),
            composition.DamageRoutes.ToImmutableDictionary(pair => pair.Key, pair => new EncounterCompiledDamageRoute(pair.Key, Role(DocumentOf(pair.Key), pair.Value.SourceActor!), pair.Value.Recipients.Select(recipient => Role(DocumentOf(pair.Key), recipient)).ToImmutableArray()), StringComparer.Ordinal),
            composition.EventGates.ToImmutableDictionary(pair => pair.Key, pair => new EncounterCompiledEventGate(Role(DocumentOf(pair.Key), pair.Value.Actor!), pair.Value.Event!, (ulong)pair.Value.OwnTurnAtLeast!.Value), StringComparer.Ordinal),
            composition.Placements.ToImmutableDictionary(pair => pair.Key, pair => new EncounterCompiledPlacement(pair.Key, pair.Value.X, pair.Value.Y, pair.Value.Z), StringComparer.Ordinal), composition.ActorProperties);

        EncounterCompiledAction CompileAction(string document, EncounterPatchAction source)
        {
            if (source.Kind == "SummonGroup")
            {
                return new(EncounterCompiledActionKind.SummonGroup, [], EncounterCompiledTargetKind.Self, null, false, false, null, source.Actors.Select(spawn => new EncounterCompiledSpawn(Scope(document, spawn.Template), Scope(document, spawn.Anchor))).ToImmutableArray());
            }
            ImmutableArray<EncounterCompiledSpell> spells = source.Kind == "Cast" ? [CompileSpell(source.Spell!)] : source.Spells.Select(CompileSpell).OrderBy(spell => spell.CanonicalName, StringComparer.Ordinal).ToImmutableArray();
            EncounterCompiledTargetKind target;
            string? group = null;
            if (source.TargetPolicy == "scanKnownWeakestElseRandom")
            {
                target = EncounterCompiledTargetKind.ScanKnownWeakestElseRandom;
                EncounterPatchDocument.Require(spells.All(spell => spell.ElementIndex >= 0), "Scan-aware selection requires single-element native spells.");
            }
            else if (source.Target.ValueKind == JsonValueKind.Object)
            {
                target = EncounterCompiledTargetKind.GroupAllLiving;
                group = Scope(document, source.Target.GetProperty("group").GetString()!);
                EncounterPatchDocument.Require(composition.Groups.ContainsKey(group), "Missing target group: " + group);
            }
            else
            {
                target = source.Target.GetString() switch
                {
                    "self" => EncounterCompiledTargetKind.Self,
                    "randomOpponent" => EncounterCompiledTargetKind.RandomOpponent,
                    "randomAlly" => EncounterCompiledTargetKind.RandomAlly,
                    "allOpponents" => EncounterCompiledTargetKind.AllOpponents,
                    "nativeOpponentTargeting" => EncounterCompiledTargetKind.NativeOpponentTargeting,
                    "randomReflectedOpponent" => EncounterCompiledTargetKind.RandomReflectedOpponent,
                    _ => throw new InvalidDataException("Unsupported compiled target selector.")
                };
            }
            bool nativeDouble = source.Casts.ValueKind == JsonValueKind.String && source.Casts.GetString() == "nativeDouble";
            bool avoidReflect = source.ReflectionPolicy is not null;
            EncounterCompiledSpell? dispel = avoidReflect ? CompileSpell("Dispel") : null;
            return new(source.Kind == "Cast" ? EncounterCompiledActionKind.Cast : EncounterCompiledActionKind.CastFromPool, spells, target, group, nativeDouble, avoidReflect, dispel, []);
        }

        EncounterCompiledSpell CompileSpell(string name)
        {
            EncounterPatchDocument.Require(MagicIds.TryGetValue(name, out ushort id), "Unknown canonical spell: " + name);
            BattleNativeAction[] matches = definition.NativeActions.Where(action => action.Command == 2 && action.ActionId == id && action.CrisisLevel is null).ToArray();
            EncounterPatchDocument.Require(matches.Length == 1, $"Canonical spell '{name}' requires one selected native magic action 2/{id}; observed {matches.Length}. Prepare the selected kernel action table.");
            BattleNativeAction native = matches[0];
            EncounterPatchDocument.Require(native.KernelSection == 1 && native.KernelIndex == id && native.TargetFlags is not null && !native.RawRecord.IsDefaultOrEmpty, $"Canonical spell '{name}' has an incompatible selected native spell record 2/{id}.");
            int element = native.Element != 0 && (native.Element & (native.Element - 1)) == 0 ? System.Numerics.BitOperations.TrailingZeroCount((uint)native.Element) : -1;
            bool reflectable = (native.AttackFlags & 0x10) != 0 && native.AttackType is 2 or 3 or 5 or 6 or 8 or 11 or 15 or 20 or 21 or 22 or 26 or 33;
            byte flags = native.TargetFlags.Value;
            bool all = (flags & 0x30) != 0x10 && (flags & 2) == 0;
            return new(name, native, element, reflectable, all);
        }

        string Role(string document, string id) => id == "original" || definition.Combatants.Any(actor => actor.Id == id) ? id : Scope(document, id);
    }

    public static EncounterPolicyDecision? Select(EncounterCompiledDefinition definition, string policyId, EncounterActionCursor cursor, string casterId, ImmutableArray<EncounterPolicyActor> actors, BattleScanKnowledge knowledge, Func<int, int> nextIndex)
    {
        if (!definition.Policies.TryGetValue(policyId, out EncounterCompiledPolicy? policy))
        {
            throw new InvalidDataException("Missing compiled encounter policy: " + policyId);
        }
        return Resolve(definition, policyId, policy.Select(cursor.NextOwnTurn), cursor.NextOwnTurn, casterId, actors, knowledge, nextIndex);
    }

    /// <summary>Re-resolves targets for an accepted compound cast without advancing its main-action cursor.</summary>
    public static EncounterPolicyDecision? Resolve(EncounterCompiledDefinition definition, string policyId, EncounterCompiledRule rule, ulong ownTurn, string casterId, ImmutableArray<EncounterPolicyActor> actors, BattleScanKnowledge knowledge, Func<int, int> nextIndex)
    {
        ArgumentNullException.ThrowIfNull(nextIndex);
        EncounterPatchDocument.Require(!actors.IsDefault && actors.All(actor => actor is not null && !string.IsNullOrWhiteSpace(actor.AllocationId)) && actors.Select(actor => actor.AllocationId).Distinct(StringComparer.Ordinal).Count() == actors.Length, "Policy candidates require distinct allocation identities.");
        EncounterPolicyActor caster = actors.SingleOrDefault(actor => actor.AllocationId == casterId) ?? throw new InvalidDataException("Policy caster has no live allocation binding: " + casterId);
        if (!caster.Living || !caster.Active)
        {
            return null;
        }
        EncounterCompiledAction action = rule.Action;
        if (action.Kind == EncounterCompiledActionKind.SummonGroup)
        {
            return new(policyId, rule.Id, ownTurn, action.Kind, null, [], false, true, action.Spawns);
        }
        EncounterPolicyActor[] eligible = actors.Where(actor => actor.Active && actor.Living && actor.Targetable).OrderBy(actor => actor.AllocationId, StringComparer.Ordinal).ToArray();
        EncounterPolicyActor[] opponents = eligible.Where(actor => actor.IsPlayer != caster.IsPlayer).ToArray();
        EncounterPolicyActor[] allies = eligible.Where(actor => actor.IsPlayer == caster.IsPlayer).ToArray();
        EncounterCompiledSpell spell;
        ImmutableArray<string> targets;
        EncounterOffensiveDelivery delivery = EncounterOffensiveDelivery.Direct;
        if (action.Target == EncounterCompiledTargetKind.ScanKnownWeakestElseRandom)
        {
            EncounterOffensiveChoice? choice = EncounterActionSelection.Select(action.Spells.Select(candidate => new EncounterSpellChoice(candidate.CanonicalName, candidate.ElementIndex, candidate.Reflectable)).ToImmutableArray(), opponents.Select(actor => new EncounterTargetChoice(actor.AllocationId, actor.Reflect)), allies.Select(actor => new EncounterTargetChoice(actor.AllocationId, actor.Reflect)), knowledge, caster.IsPlayer ? BattleObserverSide.Party : BattleObserverSide.Enemy, action.AvoidReflect, nextIndex);
            if (choice is null)
            {
                return null;
            }
            delivery = choice.Delivery;
            spell = delivery == EncounterOffensiveDelivery.Dispel ? action.Dispel ?? throw new InvalidDataException("Compiled Reflect fallback lacks Dispel.") : action.Spells.Single(candidate => candidate.CanonicalName == choice.Spell);
            targets = [choice.TargetId];
        }
        else
        {
            if (opponents.Length == 0 && action.Target is EncounterCompiledTargetKind.RandomOpponent or EncounterCompiledTargetKind.AllOpponents or EncounterCompiledTargetKind.NativeOpponentTargeting or EncounterCompiledTargetKind.RandomReflectedOpponent)
            {
                return null;
            }
            spell = action.Spells.Length == 1 ? action.Spells[0] : Choose(action.Spells);
            targets = action.Target switch
            {
                EncounterCompiledTargetKind.Self => [casterId],
                EncounterCompiledTargetKind.RandomOpponent => One(opponents),
                EncounterCompiledTargetKind.RandomAlly => One(allies),
                EncounterCompiledTargetKind.AllOpponents => opponents.Select(actor => actor.AllocationId).ToImmutableArray(),
                EncounterCompiledTargetKind.NativeOpponentTargeting => spell.NativeAllTargets ? opponents.Select(actor => actor.AllocationId).ToImmutableArray() : One(opponents),
                EncounterCompiledTargetKind.RandomReflectedOpponent => One(opponents.Where(actor => actor.Reflect).ToArray()),
                EncounterCompiledTargetKind.GroupAllLiving => GroupTargets(),
                _ => throw new InvalidDataException("Unsupported compiled target selection.")
            };
            if (targets.IsEmpty)
            {
                return null;
            }
        }
        return new(policyId, rule.Id, ownTurn, action.Kind, spell, targets, action.NativeDouble, action.Target != EncounterCompiledTargetKind.NativeOpponentTargeting, [], delivery);

        ImmutableArray<string> GroupTargets()
        {
            if (action.Group is null || !definition.Groups.TryGetValue(action.Group, out ImmutableArray<string> roles))
            {
                throw new InvalidDataException("Compiled action lacks its named group binding.");
            }
            return allies.Where(actor => roles.Contains(actor.RoleId, StringComparer.Ordinal)).Select(actor => actor.AllocationId).ToImmutableArray();
        }

        ImmutableArray<string> One(EncounterPolicyActor[] candidates)
        {
            return candidates.Length == 0 ? [] : [Choose(candidates).AllocationId];
        }

        T Choose<T>(IReadOnlyList<T> candidates)
        {
            int index = nextIndex(candidates.Count);
            EncounterPatchDocument.Require((uint)index < candidates.Count, "Battle RNG returned an index outside the policy candidate set.");
            return candidates[index];
        }
    }

    private static string DocumentOf(string scopedId) => scopedId[..scopedId.LastIndexOf('/')];
    private static string Scope(string document, string id) => document + "/" + id;
}
