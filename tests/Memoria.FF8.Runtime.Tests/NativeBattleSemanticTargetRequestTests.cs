using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSemanticTargetRequestTests
{
    [Theory]
    [InlineData(0x4022, false)]
    [InlineData(0x5080, true)]
    public void Original_single_target_transport_round_trips_typed_slots_and_reserved_positions(ushort word, bool hasReservedPositions)
    {
        BattleTargetRequest request = NativeBattleTargetRequestAdapter.Decode(word, BattleTargetEligibility.SavedStatus, BattleTargetReplacement.UseCurrentBattlePolicy, samplesOriginalSlots: true);
        var selection = Assert.IsType<NativeBattleTargetRequestAdapter.DeferredOperands>(request.Selection);

        Assert.True(request.AllowsKnockedOutAllies);
        if (hasReservedPositions)
        {
            Assert.Empty(selection.Slots);
            Assert.Equal(new[] { NativeBattleTargetRequestAdapter.ReservedPosition.Seven, NativeBattleTargetRequestAdapter.ReservedPosition.Twelve }, selection.ReservedPositions);
        }
        else
        {
            Assert.Equal(new[] { BattleRosterSlot.Ally(1), BattleRosterSlot.Enemy(2) }, selection.Slots);
            Assert.Empty(selection.ReservedPositions);
        }
        Assert.Equal(word, NativeBattleTargetRequestAdapter.ProjectOriginalWord(request, _ => null));
    }

    [Fact]
    public void Original_fallback_returns_the_full_shifted_word_at_original_word_width()
    {
        Assert.Equal(0x18, NativeBattleTargetRequestAdapter.ResolveObservedOriginalWord(0x30, 0x08, false));
        Assert.Equal(0x40, NativeBattleTargetRequestAdapter.ResolveObservedOriginalWord(0x1001, 0x40, false));
        Assert.Equal(0x30, NativeBattleTargetRequestAdapter.ResolveObservedOriginalWord(0x30, 0x08, true));
    }

    [Fact]
    public void Original_command_transport_preserves_authored_group_and_requires_a_new_binding_after_raw_write()
    {
        BattleTargetRequest request = NativeBattleTargetRequestAdapter.Decode(0x80F8, BattleTargetEligibility.SavedStatus, BattleTargetReplacement.UseCurrentBattlePolicy, samplesOriginalSlots: true);
        Assert.Equal(BattleTargetSide.Enemies, Assert.IsType<BattleTargetSelection.SideGroup>(request.Selection).Side);
        Assert.Equal((ushort)0x80F8, NativeBattleTargetRequestAdapter.ProjectOriginalWord(request, _ => null));
        BattleTargetRequest canonical = new(new BattleTargetSelection.Explicit([new BattleEnemyActorReference("added-target")]), BattleTargetEligibility.Living, false, BattleTargetReplacement.None);
        Assert.Null(NativeBattleTargetRequestAdapter.ProjectOriginalWord(canonical, _ => null));
        (NativeBattleCommandBanks banks, NativeBattleCommandLocation location) = NativeBattleCommandBanks.Initialize().Allocate(2);
        BattleAttackCommand command = new("source", "added-target", canonical);

        banks = banks.WriteProjectedCommand(location, 0, 0, 1, 0, 0, 0, [null]).BindCommand(location, 0, command);
        NativeBattleCommandBank bank = banks.Banks[2];
        Assert.Same(command, bank.LogicalCommands[2 * location.Slot]);
        Assert.Throws<NotSupportedException>(() => bank.Packets[2 * location.Slot].ReadWord(6));
        NativeBattleCommandBank overwritten = banks.WriteCommand(location, 0, 0, 1, 0, 0, 0, [8]).Banks[2];
        Assert.Null(overwritten.LogicalCommands[2 * location.Slot]);
        Assert.Equal(8, overwritten.Packets[2 * location.Slot].ReadWord(6));
    }

    [Fact]
    public void Installed_contact_producer_keeps_full_population_and_repeated_added_owner_contacts()
    {
        const int hitCount = 25;
        const int enemyCount = 11;
        const int initialHp = 9999;
        var casterDefinition = new BattleCombatantDefinition { Id = "caster", Name = "Caster", IsPlayer = true, MaximumHp = initialHp, CurrentHp = initialHp, Speed = 1 };
        BattleCombatantDefinition[] enemyDefinitions = Enumerable.Range(0, enemyCount).Select(index => new BattleCombatantDefinition
        {
            Id = $"target-{index}", Name = $"Target {index}", MaximumHp = initialHp, CurrentHp = initialHp, Speed = 1
        }).ToArray();
        BattleNativeAction native = new(8, 1, 3, 1)
        {
            MagicId = 0, AttackType = 27, Power = 1, Hit = 0, HitCount = hitCount, TargetFlags = 0, AttackFlags = 3,
            DamagePresentation = 0, Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0,
            Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = [], SupplementalRecord = []
        };
        BattleActionDefinition action = new(BattleAbility.EnemyAbility, 1, "Contact count", BattleActionEffect.Native, 1, false, true) { NativeAction = native };
        BattleSession seed = new(new() { Combatants = [casterDefinition, .. enemyDefinitions] }, BattleAtbMode.Normal);
        BattleSession battle = BattleSession.Restore(seed.Capture() with
        {
            NativeActionScratch = new() { Command = 8, Action = 1, EventLane = 2, DeferredEffectsE00 = false }
        });
        BattleCombatant caster = battle.Combatants.Single(actor => actor.Id == casterDefinition.Id);
        BattleCombatant target = battle.Combatants.Single(actor => actor.Id == enemyDefinitions[^1].Id);
        BattleTargetRequest request = new(new BattleTargetSelection.SideGroup(BattleTargetSide.Enemies), BattleTargetEligibility.Living, false, BattleTargetReplacement.None);
        BattleActionPresentation command = new(caster.Id, target.Id, BattleActionPhase.Windup, 0, request, BattleAbility.EnemyAbility, 1)
        {
            NativeAction = native, NativeQueuedCommand = 8
        };
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(BattleSession).GetMethod("BeginNativeReactionBatch", flags)!.Invoke(battle, null);
        object produced = typeof(BattleSession).GetMethod("ProduceNativeInstalledContacts", flags)!.Invoke(battle, [caster, target, command, action, null])!;

        int count = (int)produced.GetType().GetProperty("PublishedCount")!.GetValue(produced)!;
        var contacts = (ImmutableArray<NativeBattleContactPacket>)produced.GetType().GetProperty("Contacts")!.GetValue(produced)!;
        Assert.Equal(hitCount * enemyCount, count);
        Assert.Equal(count, contacts.Length);
        BattleModelOwner[] expected = enemyDefinitions.Select(enemy => (BattleModelOwner)new BattleEnemyActorModel(new(enemy.Id))).ToArray();
        Assert.Equal(Enumerable.Range(0, hitCount).SelectMany(_ => expected), contacts.Select(contact => contact.Primary.Recipient));
        Assert.All(battle.Combatants.Where(actor => !actor.IsPlayer), actor =>
        {
            Assert.Equal(initialHp - 100 * hitCount, actor.CurrentHp);
            Assert.Equal(0, actor.NativeRemainingHits);
        });
    }
}
