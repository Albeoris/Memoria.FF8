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
        BattlePartyOccupancyChange change = new(slot, outgoing, incoming);
        PartyReplacementDescriptor? bind = incoming is byte next ? new(change, actor.Id, definition.PartyReplacementCandidates[next].Id, PartyReplacementStage.Bind) : null;
        PreparedHostedDispatch? dispatch = bind is null ? null : PreparePartyReplacement(process, [new HostedDispatchControl.PartyReplacement(bind)], []);

        actor.NativeStatus1 &= ~0x10000u;
        actor.NativeStatus0 &= 0xFFDF;
        RemoveExpandedQueuedReadyInputs(actor.Id);
        actor.Atb = 0;
        actor.IsBusy = false;
        actor.NativeCombatantFlags = 0;
        actor.BindRosterSlot(null);
        NativeCharacterVital[] records = vitals.Records.ToArray();
        ushort hp = checked((ushort)actor.HumanHitPoints);
        records[outgoing] = records[outgoing] with { HitPoints = new(hp, hp), Status = NativeSavedStatusBits(actor) };
        magicVitals = vitals with { Records = records };
        PartyOccupancyChanges = PartyOccupancyChanges.Add(change);
        RefreshNativePacketBindings();
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
            if (row == outgoing || (saved.MemberFlags & 1) == 0 || (saved.Status & 1) != 0 || !definition.PartyReplacementCandidates.ContainsKey(row) ||
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
        BattleCombatant incoming = candidate.AddPartyReplacementActor(source, descriptor.Change.Slot);
        candidate.RefreshNativePacketBindings();
        candidate.magicVitals = descriptor.Change.ApplySavedBinding(candidate.magicVitals!);
        candidate.InitializePartyReplacementGauge(incoming);
        candidate.RefreshNativeKnownMagicStocks();
        candidate.expandedPartyPresentation = candidate.expandedPartyPresentation!.SetItem(incoming.Id, new(checked((ushort)incoming.CurrentHp), checked((ushort)incoming.MaximumHp), null, null) { DisplayedHitPointsQ12 = incoming.CurrentHp << 12 });
        candidate.expandedPartyReadyStatusMirrors = candidate.expandedPartyReadyStatusMirrors!.SetItem(incoming.Id, new(NativeSavedStatusBits(incoming), NativeBattleStatusBits(incoming)));
        PartyReplacementDescriptor reveal = descriptor with { Stage = PartyReplacementStage.Reveal };
        candidate.pendingPartyReplacements = candidate.pendingPartyReplacements.SetItem(pendingIndex, reveal);

        NativeBattleModelSlot? original = incoming.NativeActorIndex is int alias ? NativeBattleModelSlot.Decode(checked((byte)alias)) : null;
        HostedDispatchControl.LoadParty load = new(new(new(incoming.Id)), new(new(descriptor.OutgoingActorId)), descriptor.Change.Slot, original, (byte)((NativeSavedStatusBits(incoming) >> 8) & 1));
        HostedModelLoadAdmission admission = candidate.PrepareHostedModelLoadAdmission(process, load, []);
        PreparedHostedDispatch dispatch = PreparePartyReplacement(process, [load, new HostedDispatchControl.PartyReplacement(reveal)], [admission]);
        PreparedActionPublication publication = new(this, candidate, Restore(before));
        publication.Publish();
        process.CommitDispatch(dispatch);
        Log("PartyReplacement", incoming.Id, descriptor.OutgoingActorId, 0, $"{incoming.Name} enters battle.");
    }

    private BattleCombatant AddPartyReplacementActor(BattleCombatantDefinition source, BattleRosterSlot slot)
    {
        if (slot.Side != BattleSide.Allies || !source.IsPlayer || source.MagicRecordId is null || allies.Any(actor => actor.Slot == slot) || byId.ContainsKey(source.Id))
        {
            throw new InvalidDataException("Party replacement requires an unallocated actor and a vacated ally slot.");
        }
        BattleCombatant actor = CreateCombatant(source);
        actor.BindRosterSlot(slot);
        combatants.Add(actor);
        allies.Add(actor);
        byId.Add(actor.Id, actor);
        BindBattleMagicOwner(actor);
        return actor;
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
            outgoing.BindRosterSlot(null);
            if (change.IncomingRow is byte row && !pendingPartyReplacements.Any(pending => pending.Stage == PartyReplacementStage.Bind && pending.Change == change))
            {
                _ = AddPartyReplacementActor(definition.PartyReplacementCandidates[row], change.Slot);
            }
        }
        if (!PartyOccupancyChanges.IsEmpty)
        {
            RefreshNativePacketBindings();
        }
    }
}
