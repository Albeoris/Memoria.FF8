using System.Collections.Immutable;
using System.Text.Json;
using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class IndependentGuardianItemTests
{
    [Theory]
    [InlineData(30, 1194)]
    [InlineData(31, 1247)]
    [InlineData(32, 9999)]
    public void SummonItemPreservesItsReservationAndRestoresTheResolvedNativeInvocation(int itemId, int expectedDamage)
    {
        using BattleAssets assets = new(@"C:\Steam\steamapps\common\FINAL FANTASY VIII");
        BattleSession battle = Create(assets, itemId, 1);
        battle.QueueAction("caster", "enemy", BattleAbility.Item, itemId);
        battle = BattleSession.Restore(battle.Capture());
        battle.Tick();
        Assert.Equal(1, Assert.Single(battle.Items).Count);
        BattleSnapshot windup = battle.Capture();
        BattleIndependentGuardianInvocation invocation = windup.ActiveAction!.IndependentGuardian!;
        Assert.Equal((byte)244, invocation.Action!.Command);
        Assert.Equal((byte)10, invocation.Level);
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(windup with { ActiveAction = windup.ActiveAction with { IndependentGuardian = invocation with { Level = 11 } } }));
        BattleSession restored = BattleSession.Restore(windup);
        while (battle.Frame <= BattleSession.AttackWindupFrames)
        {
            battle.Tick();
            restored.Tick();
        }
        Assert.Equal(expectedDamage, Assert.Single(battle.Events, item => item.Kind == "Damage").Amount);
        Assert.Equal(1, Assert.Single(battle.Items).Count);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        if (itemId == 31)
        {
            Assert.Equal(100, battle.Combatants[1].CurrentHp);
            Assert.Equal((byte)4, battle.NativeScriptState.SavedBattleFlagsAfter);
            Assert.Equal((byte)0, battle.Capture().Combatants[1].NativeParticipant!.LastAction!.Command);
        }
    }

    [Fact]
    public void GysahlGreensUsesItsOriginalFailureWhenPocketStationHasNoAvailableChocobo()
    {
        using BattleAssets assets = new(@"C:\Steam\steamapps\common\FINAL FANTASY VIII");
        BattleSession battle = Create(assets, 30, 0);
        battle.QueueAction("caster", "enemy", BattleAbility.Item, 30);
        for (int frame = 0; frame <= BattleSession.AttackWindupFrames; frame++)
        {
            battle.Tick();
        }
        Assert.DoesNotContain(battle.Events, item => item.Kind == "Damage");
        Assert.Single(battle.Events, item => item.Kind == "NativeText");
        Assert.Equal(1, Assert.Single(battle.Items).Count);
    }

    [Fact]
    public void AwayChocoboPrefixesItsSavedEncodedNameAndMissingNameFailsBeforeReservation()
    {
        using BattleAssets assets = new(@"C:\Steam\steamapps\common\FINAL FANTASY VIII");
        BattleSession missing = Create(assets, 30, 3);
        Assert.Throws<NotSupportedException>(() => missing.QueueAction("caster", "enemy", BattleAbility.Item, 30));
        Assert.Equal(2, Assert.Single(missing.Items).Count);
        byte[] name = new byte[12];
        FF8Encoding.CreateEuropean().GetBytes("Kweh").CopyTo(name, 0);
        BattleDefinition definition = missing.Capture().Definition;
        NativePocketStationState pocket = definition.NativePocketStation! with { ChocoboName = name.ToImmutableArray() };
        BattleSession battle = new(definition with { NativePocketStation = pocket, NativeChocoboName = NativePocketStationText.ChocoboName(pocket) }, BattleAtbMode.Normal);
        battle.QueueAction("caster", "enemy", BattleAbility.Item, 30);
        battle.Tick();
        battle = BattleSession.Restore(battle.Capture());
        for (int frame = 0; frame < BattleSession.AttackWindupFrames; frame++)
        {
            battle.Tick();
        }
        Assert.Equal("Kweh " + assets.ReadNativeGlobalBattleMessages()[56], Assert.Single(battle.Events, entry => entry.Kind == "NativeText").Message);
        Assert.Equal(1, Assert.Single(battle.Items).Count);
    }

    private static BattleSession Create(BattleAssets assets, int item, byte flags)
    {
        byte[] pocket = new byte[64];
        pocket[0] = flags;
        pocket[1] = 10;
        return new(new BattleDefinition
        {
            NativeFieldBattleRules = 0,
            NativeActions = assets.NativeActions.ToArray(),
            ActionDefinitions = assets.ActionDefinitions.Select(action => action.Ability == BattleAbility.GuardianForce ? action with { Level = 10 } : action).ToArray(),
            GuardianForces = [new(0, 1000, 1000)],
            NativePocketStation = new(NativeInputConfiguration.OriginalInitHash, pocket.ToImmutableArray()),
            NativeSavedBattleFlags = 0,
            NativeSavedBattleFlagsKnownMask = 255,
            NativeMessageSpeed = 0,
            NativeGlobalMessages = assets.ReadNativeGlobalBattleMessages(),
            Items = [new(item, 2)],
            Combatants =
            [
                new() { Id = "caster", Name = "Caster", ModelId = 0, IsPlayer = true, MaximumHp = 1000, CurrentHp = 1000, InitialAtb = BattleSession.AtbMaximum, Abilities = [BattleAbility.Item], NativeStatusResistances = new byte[40] },
                new() { Id = "ko", Name = "KO", ModelId = 1, IsPlayer = true, MaximumHp = 800, CurrentHp = 0, NativeStatus0 = 1, NativeStatusResistances = new byte[40] },
                new() { Id = "enemy", Name = "Enemy", ModelId = 2, MaximumHp = 10000, CurrentHp = 10000, Spirit = 5, Speed = 1, NativeStatusResistances = new byte[40] }
            ]
        }, BattleAtbMode.Normal);
    }
}
