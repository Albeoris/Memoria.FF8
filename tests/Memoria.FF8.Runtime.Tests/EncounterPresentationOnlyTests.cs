namespace Memoria.FF8.Runtime.Tests;

public sealed class EncounterPresentationOnlyTests
{
    [Fact]
    public void Casting_control_publishes_no_hp_contact_feedback_or_damage_reaction()
    {
        BattleSession battle = Create();
        for (int frame = 0; frame < 10 && battle.NativeActionPackets.ActionCount == 0; frame++)
        {
            battle.Tick();
        }
        Assert.Equal((byte)1, battle.NativeActionPackets.ActionCount);
        NativeBattleActionHeaderSlot header = battle.NativeActionPackets.Headers[0];
        NativeBattleContactHalf contact = battle.NativeActionPackets.ReadContact(header.FirstContactIndex!.Value).Primary;
        Assert.Equal((byte)1, contact.Flags2);
        Assert.Equal((byte)0, contact.Sequence);
        Assert.Equal(0, contact.PresentedAmount);
        NativeBattleEffectsState effects = NativeBattleEffectsState.Clear();
        Assert.Same(effects, effects.PrependDamageText(contact.ActorIndex, contact.ContactFlags, contact.PresentedAmount));
        Assert.Equal(900, battle.Combatants.Single(actor => actor.Id == "enemy").CurrentHp);
        Assert.DoesNotContain(battle.Events, entry => entry.Kind is "Damage" or "Defeated");
        Assert.Equal((byte)0, battle.Capture().Combatants.Single(actor => actor.Id == "enemy").NativeParticipant!.PendingReaction!.Stage);
    }

    [Fact]
    public void Presentation_only_contract_rejects_a_damage_payload()
    {
        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
        {
            BattleSession battle = Create(power: 10);
            battle.Tick();
        });
        Assert.Contains("presentation-only", error.Message);
    }

    private static BattleSession Create(byte power = 0)
    {
        BattleNativeAction control = new(0, 9, -1, -1)
        {
            PresentationOnly = true,
            MagicId = 0,
            AttackType = 0,
            Power = power,
            HitCount = 1,
            TargetFlags = null,
            AttackFlags = 3,
            DamagePresentation = 0,
            Element = 0,
            ElementPercent = null,
            StatusAttack = 0,
            Statuses0 = 0,
            Statuses1 = 0,
            Hit = null,
            Critical = null,
            PowerModifier = null,
            LevelModifier = null,
            RawRecord = [],
            SupplementalRecord = []
        };
        NativeBattleAiDefinition ai = new([[4, 200, 12, 0, 0], [0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 9), 48).ToArray(), 0, []);
        return new(new BattleDefinition
        {
            Name = "Presentation control",
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            NativeActions = [control],
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true },
                new() { Id = "enemy", Name = "Enemy", MaximumHp = 1000, CurrentHp = 900, NativeAi = ai, NativeCombatantFlags = 0x11 }
            ]
        }, BattleAtbMode.Normal);
    }
}
