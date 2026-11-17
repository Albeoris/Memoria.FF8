using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private ImmutableDictionary<string, NativeBattlePartyPresentedVital>? expandedPartyPresentation;

    private void InitializeExpandedPartyPresentation()
    {
        if (expandedPartyPresentation is not null)
        {
            throw new InvalidOperationException("Expanded party presentation requires an expanded roster and one initialization.");
        }
        BattleModelRoster roster = ReadBattlePresentationRoster();
        ImmutableDictionary<string, NativeBattlePartyPresentedVital>.Builder rows = ImmutableDictionary.CreateBuilder<string, NativeBattlePartyPresentedVital>(StringComparer.Ordinal);
        ImmutableDictionary<string, NativeBattleStatusMirror>.Builder statuses = ImmutableDictionary.CreateBuilder<string, NativeBattleStatusMirror>(StringComparer.Ordinal);
        foreach (BattlePartyModelOwner owner in roster.Party)
        {
            BattleCombatant actor = GetActor(RequireCanonicalGraphicsActorId(owner));
            rows.Add(actor.Id, new(checked((ushort)actor.CurrentHp), checked((ushort)actor.MaximumHp), null, null) { DisplayedHitPointsQ12 = actor.CurrentHp << 12 });
            statuses.Add(actor.Id, new(NativeSavedStatusBits(actor), NativeBattleStatusBits(actor)));
        }
        ImmutableDictionary<string, NativeBattlePartyPresentedVital> vitals = rows.ToImmutable();
        ImmutableDictionary<string, NativeBattleStatusMirror> mirrors = statuses.ToImmutable();
        expandedPartyPresentation = vitals;
        expandedPartyReadyStatusMirrors = mirrors;
    }

    private BattleCombatant? PublishExpandedPartyPresentedContact(BattleModelRoster roster, NativeBattleContactHalf half)
    {
        ImmutableDictionary<string, NativeBattlePartyPresentedVital> state = expandedPartyPresentation ?? throw new InvalidOperationException("Expanded contact presentation requires the current party cache.");
        BattleModelOwner owner = half.Recipient;
        if (!roster.Contains(owner) || !byId.TryGetValue(RequireCanonicalGraphicsActorId(owner), out BattleCombatant? actor) ||
            RequirePresentationModelOwner(roster, actor) != owner)
        {
            throw new InvalidDataException("Expanded contact presentation requires the current bound actor.");
        }
        if (owner.Side != BattleSide.Allies)
        {
            return null;
        }
        NativeBattlePartyPresentedVital row = RequireExpandedPartyPresentedRow(state, actor.Id);
        if (actor.GuardianFormVitals is not null && (half.HitFlags & 0x20) != 0)
        {
            row = row with { GuardianForceHitPoints = checked((ushort)actor.CurrentHp), GuardianForceMaximumHitPoints = checked((ushort)actor.MaximumHp), DisplayedGuardianForceHitPointsQ12 = actor.CurrentHp << 12 };
        }
        else
        {
            row = row.Apply(half);
        }
        expandedPartyPresentation = state.SetItem(actor.Id, row);
        return actor;
    }

    private void AdvanceExpandedPresentedVitals4B0500()
    {
        ImmutableDictionary<string, NativeBattlePartyPresentedVital> state = expandedPartyPresentation ?? throw new InvalidOperationException("Expanded party HUD requires its initialized cache.");
        ImmutableDictionary<string, NativeBattlePartyPresentedVital>.Builder rows = state.ToBuilder();
        foreach ((string actorId, NativeBattlePartyPresentedVital row) in state)
        {
            rows[actorId] = row.AdvanceHud4B0500(guardianForceCasts.ContainsKey(actorId));
        }
        expandedPartyPresentation = rows.ToImmutable();
    }

    private static NativeBattlePartyPresentedVital RequireExpandedPartyPresentedRow(ImmutableDictionary<string, NativeBattlePartyPresentedVital> state, string actorId)
    {
        return state.TryGetValue(actorId, out NativeBattlePartyPresentedVital? row) ? row : throw new InvalidDataException("Expanded party presentation lacks the allocated actor's cache row.");
    }
}
