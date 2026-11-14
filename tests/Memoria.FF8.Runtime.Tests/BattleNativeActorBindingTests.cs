using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class BattleNativeActorBindingTests
{
    [Fact]
    public void NativeThreeByFourKeepsItsPhysicalRowsAndSnapshotBytes()
    {
        BattleCombatantDefinition[] enemies = Enumerable.Range(0, 4).Select(slot => new BattleCombatantDefinition { Id = $"enemy-{slot}", Name = $"Enemy {slot}", SceneSlot = slot }).ToArray();
        BattleSession battle = new(new BattleDefinition { Combatants = [.. Party(3), .. enemies] }, BattleAtbMode.Normal);
        BattleSnapshot saved = battle.Capture();
        Assert.Equal(Enumerable.Range(0, 7), battle.Combatants.Select(actor => actor.EncodeNativeSlot()));
        Assert.Equal(Enumerable.Range(0, 7), saved.Combatants.Select(actor => actor.NativeParticipant!.ActorIndex));
        Assert.Equal(Enumerable.Range(0, 7), Restore(saved).Combatants.Select(actor => actor.EncodeNativeSlot()));
    }

    [Theory]
    [InlineData(1, 152)]
    [InlineData(2, 204)]
    public void ExpandedLogicalPartyHolesNeverBecomePhysicalTargetsOrVariableBanks(int nativeCount, byte unownedVariable)
    {
        byte[] native = [0, nativeCount > 1 ? (byte)1 : byte.MaxValue, byte.MaxValue];
        NativeCharacterVitals frozen = MagicSystemFixture.Create() with { CombatParty = native };
        // The old snapshot has no optional extra selection metadata.
        BattleSession battle = CreateExpanded(frozen, [14, 96, 1, 0], [43, (byte)(nativeCount - 1), 7, 1, 6, 0]);
        Assert.Equal(new byte[] { 1, 0, 0, 0 }, battle.Capture().Combatants[0].NativeStatus!.Resistances![20..24]);
        Assert.Contains("unowned actor memory", Assert.Throws<NotSupportedException>(() => CreateExpanded(frozen, [14, unownedVariable, 1, 0])).Message);
        AssertBindings(battle, nativeCount);
        BattleSession restored = Restore(battle.Capture());
        AssertBindings(restored, nativeCount);

        battle.Tick();
        restored.Tick();
        Assert.Equal($"party-{nativeCount - 1}", battle.ActiveAction?.TargetId);
        Assert.Equal(JsonSerializer.Serialize(battle.ActiveAction), JsonSerializer.Serialize(restored.ActiveAction));

        BattleSession absent = CreateExpanded(frozen, [0], [43, (byte)nativeCount, 7, 1, 6, 0]);
        absent.Tick();
        Assert.Null(absent.ActiveAction);
        Assert.DoesNotContain(absent.QueuedActions, command => command.TargetId == $"party-{nativeCount}");
    }

    [Fact]
    public void ReplacedOriginalEnemyReleasesItsNativeAddressAcrossJsonRestore()
    {
        NativeCharacterVitals frozen = MagicSystemFixture.Create() with { CombatParty = [0, 255, 255] };
        BattleSession battle = CreateExpanded(frozen, [52, 6, 59, 6, 4, 52, 6, 0]);
        Verify(battle);
        Verify(Restore(battle.Capture()));

        static void Verify(BattleSession session)
        {
            BattleCombatant retired = Assert.Single(session.Combatants, actor => actor.Id == "native-allocation-1");
            Assert.False(retired.IsActive);
            Assert.Null(retired.Slot);
            Assert.Null(retired.NativeActorIndex);
            Assert.Equal(-1, retired.EncodeNativeSlot());
            Assert.Equal(4, Assert.Single(session.Combatants, actor => actor.Id == "native-allocation-2").EncodeNativeSlot());
            Assert.Equal(5, Assert.Single(session.Combatants, actor => actor.Id == "native-allocation-3").EncodeNativeSlot());
        }
    }

    private static void AssertBindings(BattleSession battle, int nativeCount)
    {
        BattleCombatant[] allies = battle.Combatants.Where(actor => actor.IsPlayer).ToArray();
        for (int row = 0; row < allies.Length; row++)
        {
            BattleCombatant actor = allies[row];
            Assert.Equal(BattleRosterSlot.Ally(row), actor.Slot);
            if (row < nativeCount)
            {
                Assert.Equal(row, actor.EncodeNativeSlot());
            }
            else
            {
                Assert.Null(actor.NativeActorIndex);
                Assert.Throws<NotSupportedException>(() => actor.EncodeNativeSlot());
            }
        }
        Assert.Equal(3, Assert.Single(battle.Combatants, actor => actor.Id == "enemy").EncodeNativeSlot());
        Assert.All(battle.Capture().Combatants.Take(allies.Length), actor => Assert.Equal(-1, actor.NativeParticipant!.ActorIndex));
    }

    private static BattleCombatantDefinition[] Party(int count)
    {
        return Enumerable.Range(0, count).Select(row => new BattleCombatantDefinition
        {
            Id = $"party-{row}", Name = $"Party {row}", IsPlayer = true, ModelId = row,
            NativeStatusResistances = Enumerable.Repeat(checked((byte)(100 + row)), 40).ToArray()
        }).ToArray();
    }

    private static BattleSession CreateExpanded(NativeCharacterVitals frozen, byte[] init, byte[]? turn = null)
    {
        NativeBattleEnemyLevel level = new(100, 10, 10, 10, 10, 10, new int[8], [], [], 0);
        NativeBattleEnemyTemplate template = new(5, Enumerable.Repeat(level, 101).ToArray()) { NativeStatusAllocation = new(0, 0, Enumerable.Repeat((byte)100, 40).ToImmutableArray()) };
        NativeBattleAiAbility[] abilities = Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray();
        BattleCombatantDefinition enemy = new() { Id = "enemy", Name = "Enemy", SceneSlot = 0, Level = 5, NativeTemplate = template, InitialAtb = turn is null ? 0 : BattleSession.AtbMaximum, NativeAi = new([init, turn ?? [0], [0], [0], [0]], abilities, 0, []) };
        BattleCombatantDefinition reserve = enemy with { Id = "reserve", SceneSlot = 6, Level = 0, InitialActive = false, InitialAtb = 0, NativeAi = new([[0], [0], [0], [0], [0]], abilities, 0, []) };
        BattleNativeAction action = new(8, 1, 3, 0)
        {
            Name = "Test attack", MagicId = 0, AttackType = 1, Power = 10, HitCount = 1, TargetFlags = null, AttackFlags = 0, DamagePresentation = 4,
            Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = 255, Critical = 0, PowerModifier = null,
            LevelModifier = null, RawRecord = [], SupplementalRecord = []
        };
        BattleCombatantDefinition[] party = Party(4).Select((actor, row) => actor with { MagicRecordId = row }).ToArray();
        return new(new BattleDefinition
        {
            UsesExpandedRoster = true, UsesOriginalEnemySlots = true, MagicVitals = frozen,
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(), Combatants = [.. party, enemy, reserve], NativeActions = [action]
        }, BattleAtbMode.Normal);
    }

    private static BattleSession Restore(BattleSnapshot snapshot)
    {
        return BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(snapshot))!);
    }
}
