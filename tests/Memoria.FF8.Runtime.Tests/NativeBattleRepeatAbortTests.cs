using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRepeatAbortTests
{
    [Theory]
    [InlineData(false, 2)]
    [InlineData(false, 3)]
    [InlineData(true, 2)]
    [InlineData(true, 3)]
    public void Slots_excludes_the_aborted_repeat_after_killing_the_last_enemy(bool cards, int repetitions)
    {
        BattleSession battle = Create(cards);
        Queue(ref battle, cards, repetitions);
        BattleModelOwner enemy = battle.ReadPresentationModelOwner("enemy");
        NativeBattleContactPacket stale = new(new(enemy, 3, 8, 0, 1, 323, 0), null);
        BattleSnapshot snapshot = battle.Capture();
        NativeBattleActionPacketsState retained = snapshot.ReadFixturePresentation().NativeActionPackets!;
        retained = retained.AppendContacts([stale, stale]).ResetCounts();
        battle = BattleSession.Restore(snapshot.WithFixturePresentation(facts => facts with { NativeActionPackets = retained }));

        for (int frame = 0; frame < 300 && battle.Presentation!.NativeActionPackets.ActionCount < 2; frame++)
        {
            battle.Tick();
        }

        NativeBattleActionPacketsState packets = battle.Presentation!.NativeActionPackets;
        Assert.Equal(0, battle.Combatants.Single(actor => actor.Id == "enemy").CurrentHp);
        Assert.Equal(2, packets.ActionCount);
        Assert.Equal(1, packets.ContactCount);
        NativeBattleActionHeaderSlot first = packets.ReadHeader(new BattleActionHeaderReference(0));
        NativeBattleActionHeaderSlot second = packets.ReadHeader(new BattleActionHeaderReference(1));
        Assert.Equal(0, first.ReadRepeatModeMinusOne());
        Assert.Equal(1, first.ReadContactCount());
        Assert.Equal(new BattleContactCursor(0), first.FirstContact);
        Assert.Equal(1, second.ReadContactCount());
        Assert.Equal(new BattleContactCursor(1), second.FirstContact);
        Assert.Equal(stale, packets.ReadContact(new BattleContactOccurrence(1)));
        NativeBattleContactHalf primary = packets.ReadContact(new BattleContactOccurrence(0)).Primary;
        Assert.Equal(enemy, primary.Recipient);
        Assert.NotEqual(0, primary.SavedStatuses4 & 1);
    }

    private static void Queue(ref BattleSession battle, bool cards, int repetitions)
    {
        BattleNativeSlotsRoll roll = new(1, (byte)repetitions);
        if (cards)
        {
            BattleNativeSlotsDeal deal = battle.BeginNativeSlotsDeal("selphie");
            battle = BattleSession.Restore(battle.Capture() with { PendingSlotsDeal = deal with { Candidates = deal.Candidates.SetItem(0, roll) } });
            Assert.True(battle.SelectNativeSlotsCard(deal.Id, 0));
            Assert.True(battle.SubmitNativeSlotsCard(deal.Id));
        }
        else
        {
            battle = BattleSession.Restore(battle.Capture() with { NativeLimits = [new("selphie", 4) { Slots = roll }] });
            battle.QueueNativePlayerCommand("selphie", "selphie", 16, 1);
        }
        battle = BattleSession.Restore(NativeBattleTestSnapshots.Independent(battle.Capture()));
    }

    private static BattleSession Create(bool cards)
    {
        BattleNativeLimitDefinition rules = new(5, 16, 0, 0, new byte[32].ToImmutableArray())
        {
            SlotsSelectionBytes = new byte[80].ToImmutableArray(),
            SlotsSpellBytes = Enumerable.Repeat((byte)1, 16).ToImmutableArray()
        };
        BattleNativeAction spell = new(16, 1, 0, 0)
        {
            Name = "Spell", MagicId = 1, AttackType = 2, Power = 20, HitCount = 1, TargetFlags = 0x50, AttackFlags = 0, DamagePresentation = 0,
            Element = 0, ElementPercent = 0, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = 255, Critical = 0, PowerModifier = 0, LevelModifier = 0, RawRecord = [], SupplementalRecord = []
        };
        BattleSession battle = new(new BattleDefinition
        {
            SelphieCardSlots = cards, NativeGlobalRandom = new(12345), NativeLimitsDisabled = false,
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(), NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            NativeActions = [spell], NativeStatusRules = new(0, Enumerable.Repeat((byte)30, 14).ToImmutableArray()),
            Combatants =
            [
                new() { Id = "selphie", Name = "Selphie", IsPlayer = true, ModelId = 5, Level = 20, InitialAtb = BattleSession.AtbMaximum, NativeLimit = rules, MaximumHp = 1000, CurrentHp = 500, NativeStatus0 = 0, NativeStatusResistances = new byte[40], Speed = 1 },
                new() { Id = "enemy", Name = "Enemy", MaximumHp = 1, CurrentHp = 1, NativeStatusResistances = new byte[40], Speed = 1 }
            ]
        }, BattleAtbMode.TurnBased);
        return BattleSession.Restore(battle.Capture() with { NativeLimits = [new("selphie", 4)] });
    }
}
