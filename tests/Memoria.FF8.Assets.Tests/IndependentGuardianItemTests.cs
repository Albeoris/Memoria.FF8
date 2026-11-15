using System.Collections.Immutable;
using System.Text.Json;
using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class IndependentGuardianItemTests
{
    [Theory]
    [InlineData(30, 0, 97, 11)]
    [InlineData(30, 1, 98, 11)]
    [InlineData(30, 2, 99, 11)]
    [InlineData(30, 3, 100, 11)]
    [InlineData(32, 0, 338, 1)]
    public void ExpandedItemSelectsItsInstalledChildAndRetainsReservationAndLevel(int itemId, byte variant, ushort effect, int targetCount)
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        BattleSession battle = CreateExpanded(assets, itemId, 1, variant);
        BattleActionDefinition item = Assert.Single(battle.ActionDefinitions, action => action.Ability == BattleAbility.Item && action.Id == itemId);
        Assert.True(battle.SupportsExpandedAction(item));
        battle.QueueAction("party-10", "enemy-10", BattleAbility.Item, itemId);
        Assert.Equal(1, Assert.Single(battle.Items).Count);
        Assert.Null(Assert.Single(battle.QueuedActions).IndependentGuardian);
        BattleSession queued = RestoreJson(battle);
        battle.Tick();
        queued.Tick();
        BattleActionPresentation windup = Assert.IsType<BattleActionPresentation>(battle.ActiveAction);
        BattleIndependentGuardianInvocation invocation = Assert.IsType<BattleIndependentGuardianInvocation>(windup.IndependentGuardian);
        Assert.Equal((byte)10, invocation.Level);
        Assert.Equal((byte)244, invocation.Action!.Command);
        Assert.Equal(effect, invocation.Action.MagicId);
        Assert.Equal((byte?)4, windup.NativeQueuedCommand);
        Assert.Equal(itemId, windup.ActionId);
        Assert.Equal(0, windup.NativeTargetMask);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(queued.Capture()));
        BattleSession active = RestoreJson(battle);
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(battle.Capture() with { ActiveAction = windup with { IndependentGuardian = invocation with { Level = 11 } } }));
        for (int frame = 0; frame <= BattleSession.AttackWindupFrames; frame++)
        {
            battle.Tick();
            queued.Tick();
            active.Tick();
        }
        Assert.Null(battle.ActiveAction);
        Assert.Equal(1, Assert.Single(battle.Items).Count);
        BattleEvent[] contacts = battle.Events.Where(entry => entry.Kind == "EffectContact" && entry.ActorId == "party-10").ToArray();
        Assert.Equal(targetCount, contacts.Length);
        Assert.Contains(contacts, entry => entry.TargetId == "enemy-10");
        Assert.All(contacts, entry => Assert.Equal(invocation.Action.Name, entry.Message));
        Assert.Equal(targetCount, contacts.Select(entry => entry.TargetId).Distinct().Count());
        Assert.All(contacts, entry => Assert.True(entry.Amount > 0));
        if (itemId == 32)
        {
            Assert.Equal(1, Assert.Single(battle.Combatants, actor => actor.Id == "enemy-10").CurrentHp);
        }
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(queued.Capture()));
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(active.Capture()));
    }

    [Theory]
    [InlineData(0, 55)]
    [InlineData(3, 56)]
    public void ExpandedGysahlFailureUsesItsOriginalMessageAndConsumesOneItem(byte flags, int message)
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        BattleSession battle = CreateExpanded(assets, 30, flags, 0);
        battle.QueueAction("party-10", "enemy-10", BattleAbility.Item, 30);
        battle.Tick();
        Assert.Equal(message, battle.ActiveAction!.IndependentGuardian!.FailureMessage);
        Assert.Null(battle.ActiveAction.IndependentGuardian.Action);
        BattleSession restored = RestoreJson(battle);
        for (int frame = 0; frame <= BattleSession.AttackWindupFrames; frame++)
        {
            battle.Tick();
            restored.Tick();
        }
        Assert.Equal(1, Assert.Single(battle.Items).Count);
        string expected = (message == 56 ? "Kweh " : "") + assets.ReadNativeGlobalBattleMessages()[message];
        Assert.Equal(expected, Assert.Single(battle.Events, entry => entry.Kind == "NativeText").Message);
        Assert.DoesNotContain(battle.Events, entry => entry.Kind is "EffectContact" or "Damage");
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Fact]
    public void ExpandedPhoenixDamagesBeforeRevivingTheFullPartyAndDefersVictory()
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        BattleDefinition definition = CreateExpanded(assets, 31, 0, 0).ReadDefinition();
        BattleCombatantDefinition[] roster = definition.Combatants.Select(actor =>
        {
            if (actor.Id is "party-8" or "party-9")
            {
                return actor with { CurrentHp = 0, NativeStatus0 = 1 };
            }
            return actor.IsPlayer ? actor : actor with { CurrentHp = 1 };
        }).ToArray();
        BattleSession battle = new(definition with { Combatants = roster, NativeSavedBattleFlags = 2 }, BattleAtbMode.TurnBased);
        battle.QueueAction("party-10", "enemy-10", BattleAbility.Item, 31);
        Assert.Equal(1, Assert.Single(battle.Items).Count);
        BattleSession queued = RestoreJson(battle);
        battle.Tick();
        queued.Tick();
        Assert.Equal((ushort)140, battle.ActiveAction!.IndependentGuardian!.Action!.MagicId);
        Assert.Null(battle.NativeScriptState.SavedBattleFlagsAfter);
        BattleSession active = RestoreJson(battle);
        for (int frame = 0; frame <= BattleSession.AttackWindupFrames; frame++)
        {
            battle.Tick();
            queued.Tick();
            active.Tick();
        }

        Assert.Equal(BattleOutcome.Victory, battle.Outcome);
        Assert.Equal((byte?)2, battle.NativeScriptState.SavedBattleFlagsBefore);
        Assert.Equal((byte?)6, battle.NativeScriptState.SavedBattleFlagsAfter);
        Assert.Equal((byte)4, battle.NativeScriptState.SavedBattleFlagsSetMask);
        Assert.Equal(1, Assert.Single(battle.Items).Count);
        Assert.Equal(11, battle.Events.Count(entry => entry.Kind == "EffectContact" && entry.ActorId == "party-10"));
        Assert.Equal(11, battle.Events.Count(entry => entry.Kind == "PhoenixRevivalContact" && entry.ActorId == "party-10"));
        Assert.Equal(new[] { "party-8", "party-9" }, battle.Events.Where(entry => entry.Kind == "EffectHealing").Select(entry => entry.TargetId));
        Assert.All(battle.Combatants.Where(actor => actor.Id is "party-8" or "party-9"), actor =>
        {
            Assert.True(actor.IsAlive);
            Assert.Equal(actor.MaximumHp / 8, actor.CurrentHp);
        });
        BattleEvent[] events = battle.Events.ToArray();
        Assert.True(Array.FindLastIndex(events, entry => entry.Kind == "Defeated") < Array.FindIndex(events, entry => entry.Kind == "Healing"));
        Assert.True(Array.FindLastIndex(events, entry => entry.Kind == "PhoenixRevivalContact") < Array.FindIndex(events, entry => entry.Kind == "Outcome"));
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(queued.Capture()));
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(active.Capture()));
    }

    [Theory]
    [InlineData(30)]
    [InlineData(31)]
    [InlineData(32)]
    public void ExpandedGuardianItemRejectsMissingOrUnsupportedSourceBeforeReservingStock(int itemId)
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        BattleDefinition definition = CreateExpanded(assets, itemId, 1, 0).ReadDefinition();
        BattleSession battle = new(itemId switch
        {
            30 => definition with { NativePocketStation = definition.NativePocketStation! with { Raw = definition.NativePocketStation.Raw.SetItem(45, 4) } },
            31 => definition with { NativeSavedBattleFlagsKnownMask = 1 },
            _ => definition with { GuardianForces = [] }
        }, BattleAtbMode.TurnBased);
        Assert.Throws<NotSupportedException>(() => battle.QueueAction("party-10", "enemy-10", BattleAbility.Item, itemId));
        Assert.Equal(2, Assert.Single(battle.Items).Count);
        Assert.Empty(battle.QueuedActions);
    }

    [Theory]
    [InlineData(30, 1194)]
    [InlineData(31, 1247)]
    [InlineData(32, 9999)]
    public void SummonItemPreservesItsReservationAndRestoresTheResolvedNativeInvocation(int itemId, int expectedDamage)
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        BattleSession battle = Create(assets, itemId, 1);
        NativeBattleLastAction previousKoMemory = new(1, 2, 2, 12, 4, 1);
        if (itemId == 31)
        {
            BattleSnapshot before = battle.Capture();
            int index = before.Combatants.Single(actor => actor.Id == "ko").NativeParticipant!.ActorIndex;
            battle = BattleSession.Restore(before with { Combatants = before.Combatants.Select(actor => actor.Id == "ko" ? actor with { NativeParticipant = actor.NativeParticipant! with { LastAction = previousKoMemory } } : actor).ToArray(), NativeActionMemory = before.NativeActionMemory!.WithAction(index, previousKoMemory) });
        }
        battle.QueueAction("caster", "enemy", BattleAbility.Item, itemId);
        battle = BattleSession.Restore(battle.Capture());
        battle.Tick();
        Assert.Equal(1, Assert.Single(battle.Items).Count);
        BattleSnapshot windup = battle.Capture();
        BattleIndependentGuardianInvocation invocation = windup.ActiveAction!.IndependentGuardian!;
        Assert.Equal((byte)244, invocation.Action!.Command);
        Assert.Equal((byte)10, invocation.Level);
        NativeBattleActionHeaderSlot header = battle.NativeActionPackets.ReadHeader(new BattleActionHeaderReference(windup.ActiveAction.NativeHeaderIndex!.Value));
        Assert.Equal((byte)244, header.Command);
        Assert.Equal(invocation.Action.ActionId == 15 ? (byte)11 : (byte)14, header.Sequence);
        Assert.Equal((uint)0x495050, header.Data!.ProducerAddress);
        if (itemId == 30)
        {
            Assert.Equal((byte)4, battle.Capture().Combatants.Single(actor => actor.Id == "enemy").NativeParticipant!.LastAction!.Command);
        }
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
            Assert.Equal(previousKoMemory with { AttackType = 3 }, battle.Capture().Combatants[1].NativeParticipant!.LastAction);
        }
    }

    [Fact]
    public void GysahlGreensUsesItsOriginalFailureWhenPocketStationHasNoAvailableChocobo()
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        BattleSession battle = Create(assets, 30, 0);
        battle.QueueAction("caster", "enemy", BattleAbility.Item, 30);
        battle.Tick();
        NativeBattleActionHeaderSlot header = battle.NativeActionPackets.ReadHeader(new BattleActionHeaderReference(battle.ActiveAction!.NativeHeaderIndex!.Value));
        Assert.Equal((byte)0, header.Command);
        Assert.Equal((byte)10, header.Sequence);
        Assert.Equal((byte)9, header.ReadByte(4));
        Assert.Equal((byte)0, header.ReadContactCount());
        Assert.Equal((byte)1, battle.NativeActionPackets.ContactCount);
        Assert.Equal(0, battle.NativeActionPackets.ReadContact(header.FirstContactIndex!.Value).Primary.Amount6);
        Assert.Equal((uint)0x47EA30, header.Data!.ProducerAddress);
        Assert.Equal(30, header.Data.Argument);
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
        using BattleAssets assets = new(TestResources.GameDirectory);
        BattleSession missing = Create(assets, 30, 3);
        Assert.Throws<NotSupportedException>(() => missing.QueueAction("caster", "enemy", BattleAbility.Item, 30));
        Assert.Equal(2, Assert.Single(missing.Items).Count);
        byte[] name = new byte[12];
        FF8Encoding.CreateEuropean(FF8TextLanguage.EN).GetBytes("Kweh").CopyTo(name, 0);
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
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
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

    private static BattleSession CreateExpanded(BattleAssets assets, int item, byte flags, byte variant)
    {
        BattleDefinition definition = Create(assets, item, flags).ReadDefinition();
        byte[] name = new byte[12];
        FF8Encoding.CreateEuropean(FF8TextLanguage.EN).GetBytes("Kweh").CopyTo(name, 0);
        NativePocketStationState pocket = definition.NativePocketStation! with { ChocoboName = name.ToImmutableArray(), Raw = definition.NativePocketStation!.Raw.SetItem(45, variant) };
        BattleCombatantDefinition caster = definition.Combatants[0];
        BattleCombatantDefinition enemy = definition.Combatants[2];
        return new(definition with
        {
            UsesExpandedRoster = true,
            NativeItemInventory = Enumerable.Range(0, 198).Select(index => index == 0 ? new NativeItemStock(checked((byte)item), 2) : new NativeItemStock(0, 0)).ToArray(),
            NativePocketStation = pocket,
            NativeChocoboName = NativePocketStationText.ChocoboName(pocket),
            Combatants =
            [
                .. Enumerable.Range(0, 11).Select(index => caster with { Id = $"party-{index}", ModelId = index, InitialAtb = index == 10 ? BattleSession.AtbMaximum : 0 }),
                .. Enumerable.Range(0, 11).Select(index => enemy with { Id = $"enemy-{index}" })
            ]
        }, BattleAtbMode.TurnBased);
    }

    private static BattleSession RestoreJson(BattleSession battle)
    {
        return BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
    }
}
