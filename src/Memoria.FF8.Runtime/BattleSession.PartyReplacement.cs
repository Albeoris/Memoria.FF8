using System.Collections.Immutable;
using static Memoria.FF8.Runtime.BattlePresentationProcess;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    public ImmutableArray<BattlePartyOccupancyChange> PartyOccupancyChanges { get; private set; } = [];
    private ImmutableArray<PartyReplacementDescriptor> pendingPartyReplacements = [];
    private bool HasPendingPartyReplacement => !pendingPartyReplacements.IsEmpty;

    private void EjectPartyOccupant(BattleCombatant actor)
    {
        if (!actor.IsPlayer || !actor.IsActive || actor.Slot is not { } slot || actor.MagicRecordId is not int record || magicVitals is not { } vitals)
        {
            throw new InvalidDataException("Native party ejection requires its allocated saved character row.");
        }
        BattlePresentationProcess process = Presentation ?? throw new InvalidOperationException("Native party ejection requires its current presentation process.");
        byte outgoing = checked((byte)record);
        byte? incoming = SelectPartyReplacement(vitals, outgoing);
        BattlePartyOccupancyChange change = new(slot, outgoing, incoming, (ushort)(NativeSavedStatusBits(actor) & 0xFFDF));
        NativeBattleModelSlot? original = actor.NativeActorIndex is int alias ? NativeBattleModelSlot.Decode(checked((byte)alias)) : null;
        PartyReplacementDescriptor? bind = incoming is byte next ? new(change, actor.Id, definition.PartyReplacementCandidates[next].Id, original, PartyReplacementStage.Bind) : null;
        PreparedHostedDispatch? dispatch = bind is null ? null : PreparePartyReplacement(process, [new HostedDispatchControl.PartyReplacement(bind)], []);

        actor.NativeStatus1 &= ~0x10000u;
        actor.NativeStatus0 = change.OutgoingStatus;
        RemoveExpandedQueuedReadyInputs(actor.Id);
        actor.Atb = 0;
        actor.IsBusy = false;
        actor.NativeCombatantFlags = 0;
        actor.BindRosterSlot(null);
        NativeCharacterVital[] records = vitals.Records.ToArray();
        ushort hp = checked((ushort)actor.HumanHitPoints);
        NativeCharacterVital saved = records[outgoing];
        BattleNativeCharacterGrowth growth = actor.NativeGrowth ?? saved.Growth;
        records[outgoing] = saved with
        {
            HitPoints = new(hp, hp),
            Status = change.OutgoingStatus,
            HitPointBonus = growth.HitPointBonus,
            StatBonuses = growth.StatBonuses.ToArray(),
            BonusSources = growth.BonusSources,
            KillCount = actor.NativeKillCount,
            DeathCount = actor.NativeDeathCount,
            GuardianForceCompatibility = actor.GuardianForceCompatibility?.ToArray()
        };
        magicVitals = vitals with { Records = records };
        PartyOccupancyChanges = PartyOccupancyChanges.Add(change);
        StopTurnAdvance();
        if (bind is not null && dispatch is not null)
        {
            pendingPartyReplacements = pendingPartyReplacements.Add(bind);
            SetNativeBattleClockActive(false);
            process.CommitDispatch(dispatch);
        }
        process.RefreshPartyRosterUi();
        Log("Ejected", null, actor.Id, 0, $"{actor.Name} leaves battle.");
    }

    private byte? SelectPartyReplacement(NativeCharacterVitals vitals, byte outgoing)
    {
        if (definition.EncounterId != 511)
        {
            return null;
        }
        byte? best = null;
        uint experience = 0;
        for (byte row = 0; row < NativeCharacterVitals.NativeRecordCount; row++)
        {
            NativeCharacterVital saved = vitals.Records[row];
            if (row == outgoing || vitals.CombatParty.Contains(row) || (saved.MemberFlags & 1) == 0 || (saved.Status & 1) != 0 || !definition.PartyReplacementCandidates.ContainsKey(row) ||
                allies.Any(actor => actor.Slot is not null && actor.MagicRecordId == row) || pendingPartyReplacements.Any(pending => pending.Change.IncomingRow == row))
            {
                continue;
            }
            if (best is null || saved.Experience < experience)
            {
                best = row;
                experience = saved.Experience;
            }
        }
        return best;
    }

    private static PreparedHostedDispatch PreparePartyReplacement(BattlePresentationProcess process, ImmutableArray<HostedDispatchControl> controls, ImmutableArray<HostedModelLoadAdmission> loads)
    {
        PreparedHostedDispatch dispatch = process.PrepareHostedDispatch([], controls: new(controls, []), allocations: loads);
        process.ValidatePreparedDispatch(dispatch);
        return dispatch;
    }

    private void ApplyPartyReplacement(PartyReplacementDescriptor descriptor)
    {
        int pendingIndex = pendingPartyReplacements.IndexOf(descriptor);
        if (pendingIndex < 0 || descriptor.Change.IncomingRow is not byte row || !definition.PartyReplacementCandidates.TryGetValue(row, out BattleCombatantDefinition? source))
        {
            throw new InvalidDataException("A party replacement descriptor lost its admitted outgoing and incoming rows.");
        }
        if (descriptor.Stage == PartyReplacementStage.Reveal)
        {
            pendingPartyReplacements = pendingPartyReplacements.RemoveAt(pendingIndex);
            if (!HasPendingPartyReplacement)
            {
                SetNativeBattleClockActive(true);
            }
            return;
        }

        BattlePresentationProcess process = Presentation ?? throw new InvalidOperationException("Party replacement requires its current presentation process.");
        BattleSnapshot before = CaptureState();
        BattleSession candidate = CreatePreparedHostedActionCandidate(before);
        BattleCombatant incoming = candidate.AddPartyReplacementActor(source, descriptor.Change.Slot, descriptor.OriginalSlot is not null);
        candidate.magicVitals = descriptor.Change.ApplySavedBinding(candidate.magicVitals!);
        candidate.InitializePartyReplacementActor(incoming, source, candidate.magicVitals.Records[row]);
        candidate.InitializePartyReplacementGauge(incoming);
        candidate.RefreshNativeKnownMagicStocks();
        candidate.expandedPartyPresentation = candidate.expandedPartyPresentation!.SetItem(incoming.Id, new(checked((ushort)incoming.CurrentHp), checked((ushort)incoming.MaximumHp), null, null) { DisplayedHitPointsQ12 = incoming.CurrentHp << 12 });
        candidate.expandedPartyReadyStatusMirrors = candidate.expandedPartyReadyStatusMirrors!.SetItem(incoming.Id, new(NativeSavedStatusBits(incoming), NativeBattleStatusBits(incoming)));
        PartyReplacementDescriptor reveal = descriptor with { Stage = PartyReplacementStage.Reveal };
        candidate.pendingPartyReplacements = candidate.pendingPartyReplacements.SetItem(pendingIndex, reveal);

        HostedDispatchControl.LoadParty load = new(new(new(incoming.Id)), new(new(descriptor.OutgoingActorId)), descriptor.Change.Slot, descriptor.OriginalSlot, (byte)((NativeSavedStatusBits(incoming) >> 8) & 1));
        HostedModelLoadAdmission admission = candidate.PrepareHostedModelLoadAdmission(process, load, []);
        PreparedHostedDispatch dispatch = PreparePartyReplacement(process, [load, new HostedDispatchControl.PartyReplacement(reveal)], [admission]);
        PreparedActionPublication publication = new(this, candidate, Restore(before));
        publication.Publish();
        process.CommitDispatch(dispatch);
        publication.PublishParticipantCallbackObservations();
        Log("PartyReplacement", incoming.Id, descriptor.OutgoingActorId, 0, $"{incoming.Name} enters battle.");
    }

    private BattleCombatant AddPartyReplacementActor(BattleCombatantDefinition source, BattleRosterSlot slot, bool bindNative)
    {
        if (slot.Side != BattleSide.Allies || !source.IsPlayer || source.MagicRecordId is null || allies.Any(actor => actor.Slot == slot))
        {
            throw new InvalidDataException("Party replacement requires an unallocated actor and a vacated ally slot.");
        }
        if (byId.TryGetValue(source.Id, out BattleCombatant? existing))
        {
            if (existing.Slot is not null || !existing.IsPlayer || existing.MagicRecordId != source.MagicRecordId || existing.ModelId != source.ModelId)
            {
                throw new InvalidDataException("Party replacement cannot reuse an occupied or different saved actor.");
            }
            existing.BindRosterSlot(slot, bindNative);
            return existing;
        }
        BattleCombatant actor = CreateCombatant(source);
        actor.BindRosterSlot(slot, bindNative);
        combatants.Add(actor);
        allies.Add(actor);
        byId.Add(actor.Id, actor);
        BindBattleMagicOwner(actor);
        return actor;
    }

    private void InitializePartyReplacementActor(BattleCombatant actor, BattleCombatantDefinition source, NativeCharacterVital saved)
    {
        BattleCombatant initial = CreateCombatant(source with
        {
            CurrentHp = saved.HitPoints.Minimum,
            NativeStatus0 = saved.Status,
            NativeRetainedStatusBits = 0,
            NativeGrowth = saved.Growth,
            NativeMagicInventory = saved.MagicInventory,
            NativeKillCount = saved.KillCount,
            NativeDeathCount = saved.DeathCount,
            GuardianForceCompatibility = saved.GuardianForceCompatibility
        });
        BindBattleMagicOwner(initial);
        initial.ReprojectNativePlayerStatistics();
        RestoreNativeParticipant(actor, CaptureNativeParticipant(initial));
        actor.GuardianFormVitals = null;
        actor.HumanHitPoints = initial.HumanHitPoints;
        actor.MutableGuardianForceCompatibility = initial.GuardianForceCompatibility?.ToArray();
        actor.RestoreMagicInventory(initial.NativeMagicInventory);
        actor.RestoreDrawnMagic(initial.DrawnMagicIds.ToArray());
        actor.RestoreDrawSlots(initial.NativeDrawSlots.ToArray());
        actor.ReprojectNativePlayerStatistics();
        RestoreNativeStatus(actor, CaptureNativeStatus(initial));
        actor.IsBusy = false;
        actor.LimitTurnCrisis = null;
    }

    private void InitializePartyReplacementGauge(BattleCombatant actor)
    {
        actor.Atb = 0;
        if ((NativeSavedStatusBits(actor) & 5) == 0)
        {
            actor.Atb = actor.SourceDefinition.NativeStartsWithFullGauge ? AtbMaximum : BattleInitialAtb.Calculate(actor.NativeAtbSpeed, checked((byte)NextRandomByte()));
        }
        if (NativeAiDrawInventory(actor).Any(stock => stock.SpellId != 0))
        {
            actor.NativeStatus1 |= 1u << 30;
        }
        RefreshNativePresentationStatus(actor);
    }

    private void RestorePartyOccupancy(BattleSnapshot snapshot)
    {
        PartyOccupancyChanges = snapshot.PartyOccupancyChanges;
        pendingPartyReplacements = snapshot.PendingPartyReplacements;
        foreach (BattlePartyOccupancyChange change in PartyOccupancyChanges)
        {
            BattleCombatant outgoing = allies.SingleOrDefault(actor => actor.Slot == change.Slot && actor.MagicRecordId == change.OutgoingRow)
                ?? throw new InvalidDataException("Saved party occupancy does not match the outgoing row and slot.");
            bool hadOriginalSlot = outgoing.NativeActorIndex is not null;
            outgoing.BindRosterSlot(null);
            if (change.IncomingRow is byte row && !pendingPartyReplacements.Any(pending => pending.Stage == PartyReplacementStage.Bind && pending.Change == change))
            {
                _ = AddPartyReplacementActor(definition.PartyReplacementCandidates[row], change.Slot, hadOriginalSlot);
            }
        }
    }
}
