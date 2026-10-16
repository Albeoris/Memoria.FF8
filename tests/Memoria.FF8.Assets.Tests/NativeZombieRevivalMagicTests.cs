using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeZombieRevivalMagicTests
{
    [Theory]
    [InlineData(5, "single")]
    [InlineData(6, "single")]
    [InlineData(5, "triple")]
    [InlineData(6, "triple")]
    [InlineData(5, "draw")]
    [InlineData(6, "draw")]
    public void InstalledRevivalSpellAdmitsLivingZombieToNativeOffensiveBranchThroughEveryPlayerMagicFlow(byte type, string flow)
    {
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        BattleActionDefinition spell = Assert.Single(assets.ActionDefinitions, action => action.Ability == BattleAbility.Magic && action.NativeAction?.AttackType == type);
        NativeCharacterVitals vitals = game.ReadNewGameCharacterVitals();
        NativeCharacterVital character = vitals.Records[0];
        BattleSession battle = new(new BattleDefinition
        {
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            NativeActions = assets.NativeActions.ToArray(),
            ActionDefinitions = assets.ActionDefinitions.ToArray(),
            NativeStatusRules = assets.ReadNativeStatusRules(0),
            NativeFieldBattleRules = 0,
            NativeKnownMagic = new(NativeInputConfiguration.OriginalInitHash, ImmutableArray.Create<uint>(0, 0)),
            Combatants =
            [
                new() { Id = "caster", Name = "Caster", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum, MaximumHp = 486, CurrentHp = 400, Magic = 100, Level = 10, NativeStatus1 = flow == "triple" ? 0x40000u : 0, NativeDeathCount = 0, Abilities = [BattleAbility.Magic, BattleAbility.Draw], MagicStocks = [new(spell.Id, 3)], NativePlayerStatRules = assets.ReadPlayerNativeStatRules(vitals, 0), NativeGrowth = new(character.HitPointBonus, character.StatBonuses.ToImmutableArray()), NativeStatusResistances = new byte[40] },
                new() { Id = "zombie", Name = "Zombie", IsPlayer = true, MaximumHp = 10000, CurrentHp = 10000, NativeStatus0 = 0x40, NativeDeathCount = 0, Speed = 1, NativeStatusResistances = new byte[40] },
                new() { Id = "source", Name = "Source", MaximumHp = 10000, CurrentHp = 10000, Level = 1, Speed = 1, NativeStatusResistances = new byte[40], NativeDrawSlots = [new(checked((byte)spell.Id), 0), new(0, 0), new(0, 0), new(0, 0)] }
            ]
        }, BattleAtbMode.TurnBased);
        if (flow == "draw")
        {
            battle.QueueAction("caster", "source", BattleAbility.Draw, spell.Id, drawCast: true, drawCastTargetId: "zombie");
        }
        else
        {
            battle.QueueAction("caster", "zombie", BattleAbility.Magic, spell.Id, magicTargets: flow == "triple" ? ["zombie", "zombie", "zombie"] : null);
        }
        battle.Tick();
        NativeBattleContactHalf zombieContact = battle.NativeActionPackets.ReadContact(0).Primary;
        Assert.Equal(0, zombieContact.Amount6);
        Assert.Equal(0, zombieContact.HitFlags3 & 1);
        Assert.Equal(1, zombieContact.SavedStatuses4 & 1);
        BattleSession restored = BattleSession.Restore(battle.Capture());
        for (int frame = 0; frame < 100 && (battle.ActiveAction is not null || battle.QueuedActions.Count != 0); frame++)
        {
            battle.Tick();
            restored.Tick();
        }
        Assert.Null(battle.ActiveAction);
        Assert.Equal(0, spell.NativeAction!.Power);
        int revivedHp = type == 6 ? 10000 : 1250;
        Assert.Equal(flow == "triple" ? revivedHp : 0, battle.Combatants[1].CurrentHp);
        BattleEvent[] contacts = battle.Events.Where(item => item.Kind == "Damage" && item.TargetId == "zombie").ToArray();
        Assert.Equal(10000, Assert.Single(contacts).Amount);
        if (flow == "triple")
        {
            Assert.Equal(revivedHp, Assert.Single(battle.Events, item => item.Kind == "Healing" && item.TargetId == "zombie").Amount);
            Assert.Single(battle.Events, item => item.Kind == "Miss" && item.Message.Contains("not KO", StringComparison.Ordinal));
        }
        else
        {
            Assert.DoesNotContain(battle.Events, item => item.Kind == "Healing" && item.TargetId == "zombie");
            Assert.DoesNotContain(battle.Events, item => item.Kind == "Miss" && item.Message.Contains("not KO", StringComparison.Ordinal));
        }
        Assert.Equal(flow == "draw" ? 3 : flow == "triple" ? 0 : 2, battle.Combatants[0].MagicStocks.Sum(stock => stock.Count));
        Assert.Equal(0, battle.Combatants[1].PersistentStatus & 0x40);
        Assert.Equal(flow == "triple", battle.Combatants[1].IsAlive);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }
}
