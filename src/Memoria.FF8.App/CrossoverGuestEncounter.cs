using System.Collections.Immutable;
using Memoria.FF7.Runtime;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record CrossoverGuestEncounterSnapshot(CrossoverBattleStockSnapshot Stock, NativeCrtRandomState? OriginCrtRandom, NativeCrtRandomState CrtRandom, long NextOrdinal, ImmutableArray<CrossoverGuestCommittedSpell>? CommittedSpells, bool LegacyBattleMaskContinuation, CrossoverGuestAudioState? Audio, CrossoverGuestItemInventory? Items);

/// <summary>Owns the suspended FFVIII state shared by all mapped combatants in one host encounter.</summary>
internal sealed class CrossoverGuestEncounter
{
    private readonly CrossoverBattleRoster roster;
    private readonly List<CrossoverGuestHostActions> guests = [];

    public NativeCharacterVitals Working { get; set; }
    public NativeGlobalRandomState GlobalRandom { get; set; }
    public NativeCrtRandomState? OriginCrtRandom { get; }
    public NativeCrtRandomState CrtRandom { get; set; }
    public long NextOrdinal { get; set; }
    public ImmutableArray<CrossoverGuestCommittedSpell> CommittedSpells { get; set; }
    public bool LegacyBattleMaskContinuation { get; }
    public CrossoverGuestAudioState Audio { get; set; }
    public CrossoverGuestItemInventory? Items { get; set; }

    public CrossoverGuestEncounter(CrossoverBattleRoster roster, NativeCharacterVitals origin, Ff7BattleSession host, NativeGlobalRandomState globalRandom, NativeCrtRandomState? crt, GameplaySaveEconomy? economy, CrossoverGuestEncounterSnapshot? saved)
    {
        this.roster = roster;
        CrossoverBattleParticipant[] participants = roster.Guests.ToArray();
        int[] slots = participants.Select(guest => guest.HostSlot).ToArray();
        GlobalRandom = globalRandom;
        OriginCrtRandom = crt;
        CrtRandom = saved?.CrtRandom ?? crt ?? NativeCrtRandomState.FromColdProcess();
        NextOrdinal = saved?.NextOrdinal ?? 1;
        Audio = saved?.Audio ?? CrossoverGuestAudioState.Create();
        Audio.Validate(roster.EncounterId, host.Tick);
        Items = economy is null ? null : saved?.Items?.Restore(economy, host.RequestId, slots, host.Tick) ?? CrossoverGuestItemInventory.Begin(economy);
        if (saved?.Items is not null && Items is null)
        {
            throw new InvalidDataException("The saved guest item stock has no suspended origin economy.");
        }

        Working = saved?.Stock.Restore(roster, origin) ?? origin.DeepCopy();
        string encounter = $"mixed:{roster.EncounterId:N}";
        LegacyBattleMaskContinuation = saved?.LegacyBattleMaskContinuation == true || NextOrdinal > 1 && Working.MagicSystem.EncounterId != encounter;
        Working = NextOrdinal == 1 ? MagicCastingService.BeginEncounter(Working, encounter) : Working with { MagicSystem = Working.MagicSystem with { EncounterId = encounter } };
        if (NextOrdinal < 1 || NextOrdinal - 1 > Working.MagicSystem.CommittedActionIds.Length || saved is not null && saved.OriginCrtRandom != crt)
        {
            throw new InvalidDataException("The guest action ordinals or CRT state disagree with the suspended origin.");
        }

        int[] rows = participants.Select(guest => (int)origin.FindPhysicalRow(checked((byte)guest.Combat.NativeId))).ToArray();
        CommittedSpells = saved?.CommittedSpells ?? (NextOrdinal == 1 ? [] : Enumerable.Range(1, checked((int)(NextOrdinal - 1))).Select(ordinal => new CrossoverGuestCommittedSpell(ordinal, 1, rows.Single())).ToImmutableArray());
        if (CommittedSpells.IsDefault || CommittedSpells.Length != NextOrdinal - 1 || CommittedSpells.Where((cost, index) => cost is null || cost.Ordinal != index + 1 || cost.Spell == 0 || cost.Spell >= Working.MagicSystem.SharedBattleMasks.Length || !rows.Contains(cost.OriginRow)).Any())
        {
            throw new InvalidDataException("The guest spell cost ledger contradicts its action ordinals or origin characters.");
        }
    }

    public void Register(CrossoverGuestHostActions guest) => guests.Add(guest);

    public void ReprojectKits(int caster)
    {
        foreach (CrossoverGuestHostActions guest in guests)
        {
            if (guest.ActorSlot == caster || Working.MagicSystem.SharedMagicStock)
            {
                guest.ReprojectWorkingKit();
            }
        }
    }

    public CrossoverGuestEncounterSnapshot Capture() => new(CrossoverBattleStockSnapshot.Capture(roster, Working), OriginCrtRandom, CrtRandom, NextOrdinal, CommittedSpells, LegacyBattleMaskContinuation, Audio, Items is null ? null : Items with { Slots = Items.Slots.ToArray() });
}
