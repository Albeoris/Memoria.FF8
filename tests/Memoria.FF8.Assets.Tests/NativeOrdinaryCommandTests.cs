using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeOrdinaryCommandTests
{
    [Fact]
    public void OrdinaryCommandSubmissionClearsDefendAndChargedWhileGfSubmissionPreservesThem()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSnapshot snapshot = Create(game, assets, 23).Capture();
        snapshot.Combatants[0] = snapshot.Combatants[0] with { NativeStatus = snapshot.Combatants[0].NativeStatus! with { Battle = 0x880000 } };
        BattleSession attack = BattleSession.Restore(snapshot);

        attack.QueueAttack("actor", "enemy");

        Assert.Equal(0u, attack.Capture().Combatants[0].NativeStatus!.Battle & 0x880000);
        BattleNativeAction gf = assets.NativeActions.Single(action => action.Command == 254 && action.ActionId == 64);
        snapshot = snapshot with { Definition = snapshot.Definition with { Combatants = [snapshot.Definition.Combatants[0] with { NativeCommands = ImmutableArray.Create(new BattleNativeCommandBinding(254, gf.Name!, gf.TargetFlags!.Value, 64)) }, .. snapshot.Definition.Combatants.Skip(1)] } };
        BattleSession summon = BattleSession.Restore(snapshot);

        summon.QueueNativePlayerCommand("actor", "enemy", 254, 64);

        Assert.Equal(0x880000u, summon.Capture().Combatants[0].NativeStatus!.Battle & 0x880000);
    }

    [Fact]
    public void InstalledAbsorbDamagesItsRecipientAndHealsItsCommandOwner()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSnapshot snapshot = Create(game, assets, 32).Capture();
        snapshot.Combatants[0] = snapshot.Combatants[0] with { CurrentHp = 500 };
        BattleSession battle = BattleSession.Restore(snapshot);
        QueueAndHit(battle, "enemy", 32, assets.ReadNativeCommand(32).ActionId);

        Assert.True(battle.Combatants[0].CurrentHp > 500);
        Assert.True(battle.Combatants.Single(actor => actor.Id == "enemy").CurrentHp < 1000000);
    }

    [Theory]
    [InlineData(33, 10)]
    [InlineData(34, 40)]
    public void InstalledLevelCommandsRebindOriginalEnemyStatisticsAndPublishNativeText(byte command, int expectedLevel)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSnapshot snapshot = Create(game, assets, command).Capture();
        NativeBattleEnemyTemplate template = assets.ReadEnemyTemplate(0, 20);
        NativeBattleEnemyLevel level = template.Levels[20];
        BattleCombatantDefinition enemy = snapshot.Definition.Combatants[2] with { ModelId = 0, Level = 20, MaximumHp = level.MaximumHp, CurrentHp = level.MaximumHp / 2, NativeTemplate = template, NativeAi = assets.ReadEnemyAi(0, 20) with { Scripts = [[0], [0], [0], [0], [0]] } };
        BattleDefinition definition = snapshot.Definition with { Combatants = [snapshot.Definition.Combatants[0], snapshot.Definition.Combatants[1], enemy] };
        BattleSession battle = new(definition, BattleAtbMode.Normal);
        QueueAndHit(battle, "enemy", command, assets.ReadNativeCommand(command).ActionId);

        BattleCombatant changed = battle.Combatants.Single(actor => actor.Id == "enemy");
        Assert.Equal(expectedLevel, changed.Level);
        Assert.Equal(template.Levels[expectedLevel].MaximumHp, changed.MaximumHp);
        Assert.Single(battle.Events, entry => entry.Kind == "NativeText");
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(BattleSession.Restore(battle.Capture()).Capture()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DarksidePaysItsOriginalFixedHpCostAfterTheAttackAndBeforePoison(bool poison)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = Create(game, assets, 28);
        if (poison)
        {
            BattleSnapshot saved = battle.Capture();
            battle = BattleSession.Restore(saved with { Combatants = [saved.Combatants[0] with { NativeStatus = saved.Combatants[0].NativeStatus! with { Saved = 2 } }, .. saved.Combatants.Skip(1)] });
        }
        QueueAndHit(battle, "enemy", 28, 0);
        Assert.Contains(battle.Events, entry => entry.Kind == "Damage" && entry.TargetId == "enemy");
        Assert.Equal(1000, battle.Combatants[0].CurrentHp);
        Assert.DoesNotContain(battle.Events, entry => entry.Message.StartsWith("Darkside cost:", StringComparison.Ordinal) || entry.Message.StartsWith("Poison:", StringComparison.Ordinal));
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
        for (int frame = 0; frame < 100 && (battle.ActiveAction is not null || battle.QueuedActions.Count != 0); frame++)
        {
            battle.Tick();
            restored.Tick();
        }
        Assert.Equal(100, Assert.Single(battle.Events, entry => entry.Kind == "Damage" && entry.Message.StartsWith("Darkside cost:", StringComparison.Ordinal)).Amount);
        if (poison)
        {
            BattleEvent cost = Assert.Single(battle.Events, entry => entry.Message.StartsWith("Darkside cost:", StringComparison.Ordinal));
            BattleEvent poisonHit = Assert.Single(battle.Events, entry => entry.Message.StartsWith("Poison:", StringComparison.Ordinal));
            Assert.InRange(poisonHit.Amount, 50, 90);
            Assert.True(battle.Events.ToList().IndexOf(cost) < battle.Events.ToList().IndexOf(poisonHit));
            Assert.Equal(900 - poisonHit.Amount, battle.Combatants[0].CurrentHp);
        }
        else
        {
            Assert.Equal(900, battle.Combatants[0].CurrentHp);
            Assert.DoesNotContain(battle.Events, entry => entry.Message.StartsWith("Poison:", StringComparison.Ordinal));
        }
        Assert.Equal((ushort)0, battle.Combatants[0].NativeDeathCount);
        Assert.Null(battle.ActiveAction);
        Assert.Empty(battle.QueuedActions);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Fact]
    public void KamikazeCommitsItsNativeSelfEjectionAndDeathCounterAndSurvivesRestore()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleNativeCommandBinding binding = assets.ReadNativeCommand(31);
        BattleSession battle = Create(game, assets, 31);
        QueueAndHit(battle, "enemy", 31, binding.ActionId);

        BattleCombatant actor = battle.Combatants[0];
        Assert.Equal(0, actor.CurrentHp);
        Assert.False(actor.IsActive);
        Assert.Equal((ushort)1, actor.PersistentStatus);
        Assert.Equal((ushort)1, actor.NativeDeathCount);
        Assert.True(battle.Combatants.Single(member => member.Id == "ally").IsAlive);
        BattleSnapshot snapshot = battle.Capture();
        Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(BattleSession.Restore(snapshot).Capture()));
    }

    [Theory]
    [InlineData(23, 0x80000)]
    [InlineData(24, 0x22)]
    [InlineData(30, 0x400)]
    public void InstalledStatusCommandsUseTheirActualAdditionalKernelRows(byte command, uint statuses)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = Create(game, assets, command);
        QueueAndHit(battle, command == 30 ? "enemy" : "actor", command, assets.ReadNativeCommand(command).ActionId);

        BattleCombatant affected = battle.Combatants.Single(actor => actor.Id == (command == 30 ? "enemy" : "actor"));
        Assert.Equal(statuses, battle.Capture().Combatants.Single(actor => actor.Id == affected.Id).NativeStatus!.Battle & statuses);
        if (command == 24)
        {
            Assert.NotEqual(0, battle.Capture().Combatants.Single(actor => actor.Id == affected.Id).NativeStatus!.Saved & 0x20);
        }
    }

    [Theory]
    [InlineData(0x0D, 2400, false, null)]
    [InlineData(0x0D, 2400, true, null)]
    [InlineData(0x0D, 2399, false, 53)]
    [InlineData(0x01, 2400, false, 72)]
    [InlineData(0x0F, 2400, false, 72)]
    public void InstalledMiniMogSelectionRunsItsNativeCommandAndRestoresAcrossAdmission(byte flags, uint selectedGil, bool dream, int? failureMessage)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleNativeCommandBinding binding = assets.ReadNativeCommand(38);
        BattleNativeAction dance = assets.NativeActions.Single(action => action.Command == 240 && action.ActionId == 6);
        Assert.Equal((byte)255, binding.ActionId);
        Assert.Equal(9, dance.KernelSection);
        Assert.Equal(6, dance.KernelIndex);
        Assert.Equal((ushort)96, dance.MagicId);
        NativeBattleGil gil = dream ? new(7777, selectedGil) : new(selectedGil, 7777);
        byte[] pocket = new byte[64];
        pocket[0] = flags;
        pocket[1] = 10;
        BattleSession battle = new(new BattleDefinition
        {
            NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory(),
            NativeCommandSequences = game.ReadNativeBattleCommandSequences(),
            NativeFieldBattleRules = 0,
            NativeActions = assets.NativeActions.ToArray(),
            NativeCommandNames = assets.ReadNativeCommandNames(),
            NativeGlobalMessages = assets.ReadNativeGlobalBattleMessages(),
            NativeMessageSpeed = 1,
            NativeSavedBattleFlags = dream ? (byte)1 : (byte)0,
            NativeSavedBattleFlagsKnownMask = 255,
            NativePocketStation = new(NativeInputConfiguration.OriginalInitHash, pocket.ToImmutableArray()),
            NativeGil = gil,
            GuardianForces = [new(0, 500, 1000), new(1, 960, 1000), new(2, 500, 1000)],
            Combatants =
            [
                new() { Id = "actor", Name = "Squall", ModelId = 0, IsPlayer = true, Level = 11, MaximumHp = 1000, CurrentHp = 1000, Speed = 1, InitialAtb = BattleSession.AtbMaximum, EquipmentIds = [0, 1], NativeCommands = [binding], NativeStatusResistances = new byte[40] },
                new() { Id = "ally", Name = "Ally", ModelId = 1, IsPlayer = true, Level = 24, MaximumHp = 1000, CurrentHp = 1000, Speed = 1, NativeStatusResistances = new byte[40] },
                new() { Id = "ko", Name = "KO", ModelId = 2, IsPlayer = true, Level = 38, MaximumHp = 1000, CurrentHp = 0, NativeStatus0 = 1, Speed = 1, EquipmentIds = [2], NativeStatusResistances = new byte[40] },
                new() { Id = "enemy", Name = "Enemy", ModelId = 0, MaximumHp = 1000, CurrentHp = 1000, Speed = 1, NativeStatusResistances = new byte[40] }
            ]
        }, BattleAtbMode.Normal);

        Assert.Empty(battle.GetNativePlayerActionChoices("ally", false));
        BattleActionDefinition choice = Assert.Single(battle.GetNativePlayerActionChoices("actor", false));
        Assert.Equal(binding.Name, choice.DisplayName ?? choice.Name);
        Assert.Equal((38 << 16) | 255, choice.Id);
        Assert.True(choice.Allies);
        Assert.True(choice.Available);
        Assert.Equal(dance.MagicId, choice.NativeAction!.MagicId);
        Assert.Equal(dance.AttackType, choice.NativeAction.AttackType);
        battle.QueueAction("actor", "actor", choice.Ability, choice.Id);
        BattleAttackCommand queued = Assert.Single(battle.QueuedActions);
        Assert.Equal((byte)38, queued.NativeQueuedCommand);
        Assert.Equal(255, queued.ActionId);
        Assert.Equal(gil, battle.Capture().NativeGil);

        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
        battle.Tick();
        restored.Tick();
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        NativeBattleActionHeaderSlot header = battle.NativeActionPackets.Headers[battle.ActiveAction!.NativeHeaderIndex!.Value];
        Assert.Equal(failureMessage is null ? (byte)38 : (byte)0, header.ReadByte(1));
        Assert.Equal(failureMessage is null ? (byte)14 : (byte)10, header.ReadByte(2));
        Assert.Equal(failureMessage is null ? (ushort)6 : (ushort)9, NativeBattleActionGraphics.Word(header, 4));
        Assert.Equal(failureMessage is null ? (ushort)96 : (ushort)0, NativeBattleActionGraphics.Word(header, 6));
        Assert.Equal(failureMessage is null ? 0x495050u : 0x47EBD0u, header.Data!.ProducerAddress);
        Assert.Equal(failureMessage is null ? dance.Name : binding.Name, header.Data.FormattedText);
        if (failureMessage is not null)
        {
            Assert.Equal((byte)0, header.ReadByte(16));
        }

        restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
        for (int frame = 0; frame < 100 && (battle.ActiveAction is not null || battle.QueuedActions.Count != 0); frame++)
        {
            battle.Tick();
            restored.Tick();
        }
        Assert.Null(battle.ActiveAction);
        Assert.Empty(battle.QueuedActions);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        if (failureMessage is int message)
        {
            Assert.Equal(gil, battle.Capture().NativeGil);
            Assert.Equal(assets.ReadNativeGlobalBattleMessages()[message], Assert.Single(battle.Events, entry => entry.Kind == "NativeText").Message);
            Assert.DoesNotContain(battle.Events, entry => entry.Kind == "GuardianForceHealing");
            Assert.Equal(new ushort[] { 500, 960, 500 }, battle.GuardianForces.Select(gf => gf.HitPoints));
        }
        else
        {
            Assert.Equal(dream ? new NativeBattleGil(7777, 0) : new NativeBattleGil(0, 7777), battle.Capture().NativeGil);
            Assert.Equal(assets.ReadNativeGlobalBattleMessages()[54], Assert.Single(battle.Events, entry => entry.Kind == "NativeText").Message);
            Assert.Equal(new[] { 100, 40 }, battle.Events.Where(entry => entry.Kind == "GuardianForceHealing").Select(entry => entry.Amount));
            Assert.Equal(new ushort[] { 600, 1000, 500 }, battle.GuardianForces.Select(gf => gf.HitPoints));
        }
    }

    [Theory]
    [InlineData(25, 500, 0x7E, 500)]
    [InlineData(26, 100, 0, 1000)]
    [InlineData(27, 0, 1, 1000)]
    public void InstalledTreatmentRecoverAndReviveApplyTheirActualCurativeEffects(byte command, int hp, ushort status, int expectedHp)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = Create(game, assets, command, hp, status);
        QueueAndHit(battle, "ally", command, assets.ReadNativeCommand(command).ActionId);

        BattleCombatant ally = battle.Combatants.Single(actor => actor.Id == "ally");
        Assert.Equal(expectedHp, ally.CurrentHp);
        Assert.Equal(0, ally.PersistentStatus & 0x7F);
        Assert.Equal((ushort)0, ally.NativeDeathCount);
    }

    private static void QueueAndHit(BattleSession battle, string target, byte command, ushort id)
    {
        battle.QueueNativePlayerCommand("actor", target, command, id);
        for (int frame = 0; frame <= BattleSession.AttackWindupFrames; frame++)
        {
            battle.Tick();
        }
    }

    private static BattleSession Create(GameAssets game, BattleAssets assets, byte command, int allyHp = 1000, ushort allyStatus = 0)
    {
        NativeCharacterVitals vitals = game.ReadNewGameCharacterVitals();
        return new(new BattleDefinition
        {
            NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory(),
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            NativeFieldBattleRules = 0,
            NativeActions = assets.NativeActions.ToArray(),
            NativeStatusRules = assets.ReadNativeStatusRules(0),
            NativeGlobalMessages = assets.ReadNativeGlobalBattleMessages(),
            NativeMessageSpeed = 1,
            Combatants =
            [
                new() { Id = "actor", Name = "Squall", ModelId = 0, WeaponId = vitals.Records[0].Weapon, IsPlayer = true, MaximumHp = 1000, CurrentHp = 1000, Speed = 1, Magic = 20, InitialAtb = BattleSession.AtbMaximum, NativeDeathCount = 0, NativeCommands = ImmutableArray.Create(assets.ReadNativeCommand(command)), NativeAttack = assets.ReadPlayerNativeAttackData(vitals, 0), NativeStatusResistances = new byte[40] },
                new() { Id = "ally", Name = "Ally", ModelId = 1, IsPlayer = true, MaximumHp = 1000, CurrentHp = allyHp, NativeStatus0 = allyStatus, Speed = 1, NativeDeathCount = 0, NativeStatusResistances = new byte[40] },
                new() { Id = "enemy", Name = "Enemy", ModelId = 1, MaximumHp = 1000000, CurrentHp = 1000000, Speed = 1, NativeStatusResistances = new byte[40] }
            ]
        }, BattleAtbMode.Normal);
    }
}
