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
    [InlineData(33, 20, 10, false)]
    [InlineData(34, 20, 40, false)]
    [InlineData(34, 79, 100, false)]
    [InlineData(33, 100, 50, false)]
    [InlineData(34, 100, 100, false)]
    [InlineData(33, 1, 1, false)]
    [InlineData(33, 20, 20, true)]
    [InlineData(34, 20, 20, true)]
    public void InstalledLevelCommandsRebindOriginalEnemyStatisticsAndPublishNativeText(byte command, int previousLevel, int expectedLevel, bool immune)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSnapshot snapshot = Create(game, assets, command).Capture();
        NativeBattleEnemyTemplate template = assets.ReadEnemyTemplate(0, 20);
        NativeBattleEnemyLevel level = template.Levels[previousLevel];
        BattleCombatantDefinition enemy = snapshot.Definition.Combatants[2] with { ModelId = 0, Level = previousLevel, NativeCombatantFlags = immune ? (ushort)0x8000 : (ushort)0, MaximumHp = level.MaximumHp, CurrentHp = level.MaximumHp / 2, NativeTemplate = template, NativeAi = assets.ReadEnemyAi(0, 20) with { Scripts = [[0], [0], [0], [0], [0]] } };
        BattleDefinition definition = snapshot.Definition with { Combatants = [snapshot.Definition.Combatants[0], snapshot.Definition.Combatants[1], enemy] };
        BattleSession battle = new(definition, BattleAtbMode.Normal);
        QueueAndHit(battle, "enemy", command, assets.ReadNativeCommand(command).ActionId);

        BattleCombatant changed = battle.Combatants.Single(actor => actor.Id == "enemy");
        Assert.Equal(expectedLevel, changed.Level);
        Assert.Equal(template.Levels[expectedLevel].MaximumHp, changed.MaximumHp);
        NativeBattleActionPacketsState packets = battle.NativeActionPackets;
        NativeBattleActionHeaderSlot header = packets.ReadHeader(new BattleActionHeaderReference(0));
        NativeBattleContactHalf contact = packets.ReadContact(header.FirstContact).Primary;
        Assert.Equal(0, contact.PresentedAmount);
        Assert.Equal(new BattleEnemyActorModel(new("enemy")), contact.Recipient);
        NativeBattleEffectsState text = NativeBattleEffectsState.Clear(BattleContactTaskStorageContract.OriginalArena).PrependDamageText(contact.Recipient, contact.ContactFlags, contact.PresentedAmount, packets.Roster);
        if (immune)
        {
            Assert.Equal(4, contact.HitFlags & 4);
            Assert.Equal(new byte[] { 16, 0 }, text.Slots[text.Head!.Value].Glyphs.ToArray());
            Assert.DoesNotContain(battle.Events, entry => entry.Kind == "NativeText");
            Assert.Equal(level.MaximumHp / 2, changed.CurrentHp);
        }
        else
        {
            Assert.Equal(1, contact.Flags2 & 1);
            Assert.Null(text.Head);
            BattleEvent message = Assert.Single(battle.Events, entry => entry.Kind == "NativeText");
            Assert.Equal(new BattleLevelChange(previousLevel, expectedLevel), message.LevelChange);
            Assert.Equal(message.LevelChange!.Format(), message.Message);
            int expectedHp = command == 34 ? changed.MaximumHp - (level.MaximumHp - level.MaximumHp / 2) : Math.Min(level.MaximumHp / 2, changed.MaximumHp);
            Assert.Equal(expectedHp, changed.CurrentHp);
        }
        Assert.DoesNotContain(battle.Events, entry => entry.Kind == "Damage" && entry.Amount != 0);
        BattleSession restored = BattleSession.Restore(NativeBattleStorageSnapshots.Capture(battle.Capture()));
        for (int frame = 0; frame < 100 && (battle.ActiveAction is not null || battle.QueuedActions.Count != 0); frame++)
        {
            battle.Tick();
            restored.Tick();
        }
        Assert.Null(battle.ActiveAction);
        Assert.Empty(battle.QueuedActions);
        Assert.Equal(battle.Frame, restored.Frame);
        Assert.Equal(battle.Events, restored.Events);
        BattleCombatant restoredEnemy = restored.Combatants.Single(actor => actor.Id == changed.Id);
        Assert.Equal((changed.Level, changed.MaximumHp, changed.CurrentHp), (restoredEnemy.Level, restoredEnemy.MaximumHp, restoredEnemy.CurrentHp));
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
        BattleSession restored = BattleSession.Restore(battle.Capture());
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
    [InlineData(0x0D, 2400, false, null, null, false)]
    [InlineData(0x0D, 2400, true, null, null, false)]
    [InlineData(0x0D, 2399, false, 53, null, false)]
    [InlineData(0x01, 2400, false, 72, null, false)]
    [InlineData(0x0F, 2400, false, 72, null, false)]
    [InlineData(0x00, 2400, false, null, ChocoboWorldMode.Memoria, true)]
    [InlineData(0x01, 2400, false, null, ChocoboWorldMode.AngeloSearch, true)]
    [InlineData(0x00, 2400, false, 72, ChocoboWorldMode.NativeImport, true)]
    [InlineData(0x00, 2400, false, 72, ChocoboWorldMode.Memoria, false)]
    [InlineData(0x03, 2400, false, 72, ChocoboWorldMode.Memoria, true)]
    [InlineData(0x05, 2400, false, 72, ChocoboWorldMode.AngeloSearch, true)]
    [InlineData(0x0D, 2500, false, null, null, false, true)]
    [InlineData(0x0D, 2500, true, null, null, false, true)]
    [InlineData(0x0D, 2499, true, 53, null, false, true)]
    [InlineData(0x0F, 2500, false, 72, null, false, true)]
    [InlineData(0x00, 2500, false, null, ChocoboWorldMode.Memoria, true, true)]
    [InlineData(0x0D, 2500, false, null, null, false, true, true)]
    public void InstalledMiniMogSelectionRunsItsNativeCommandAndRestoresAcrossAdmission(byte flags, uint selectedGil, bool dream, int? failureMessage, ChocoboWorldMode? mode, bool amulet, bool expanded = false, bool cancel = false)
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
        BattleCombatantDefinition actor = new() { Id = "actor", Name = "Squall", ModelId = 0, IsPlayer = true, Level = 11, MaximumHp = 1000, CurrentHp = 1000, Speed = 1, InitialAtb = BattleSession.AtbMaximum, EquipmentIds = [0, 1], NativeCommands = [binding], NativeStatusResistances = new byte[40] };
        BattleCombatantDefinition ally = new() { Id = "ally", Name = "Ally", ModelId = 1, IsPlayer = true, Level = 24, MaximumHp = 1000, CurrentHp = 1000, Speed = 1, NativeStatusResistances = new byte[40] };
        BattleCombatantDefinition ko = new() { Id = "ko", Name = "KO", ModelId = 2, IsPlayer = true, Level = 38, MaximumHp = 1000, CurrentHp = 0, NativeStatus0 = 1, Speed = 1, EquipmentIds = [2], NativeStatusResistances = new byte[40] };
        BattleCombatantDefinition enemy = new() { Id = "enemy", Name = "Enemy", ModelId = 0, MaximumHp = 1000, CurrentHp = 1000, Speed = 1, NativeStatusResistances = new byte[40], InitialSuspended = expanded };
        BattleCombatantDefinition[] roster = expanded ? [actor, ally, ko, ally with { Id = "ally-3", ModelId = 3, Level = 28 }, enemy, .. Enumerable.Range(1, 5).Select(index => enemy with { Id = $"enemy-{index}" })] : [actor, ally, ko, enemy];
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
            ChocoboWorldMode = mode,
            ChocoboRewards = amulet ? new() { AcquiredUniqueRewardIds = ImmutableHashSet.Create("mogs_amulet") } : new(),
            NativeGil = gil,
            GuardianForces = [new(0, 500, 1000), new(1, 960, 1000), new(2, 500, 1000)],
            UsesExpandedRoster = expanded,
            NativeStatusRules = expanded ? assets.ReadNativeStatusRules(0) : null,
            Combatants = roster
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
        Assert.Equal(expanded ? choice.Id : 255, queued.ActionId);
        Assert.Equal(gil, battle.Capture().NativeGil);
        if (expanded)
        {
            Assert.Null(queued.TargetRequest.OriginalProjection);
        }

        BattleSession restored = BattleSession.Restore(battle.Capture());
        battle.Tick();
        restored.Tick();
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        if (expanded)
        {
            Assert.NotNull(battle.ActiveAction);
            Assert.Equal(gil, battle.Capture().NativeGil);
            if (cancel)
            {
                battle.InstantDefeat();
                Assert.Equal(gil, battle.Capture().NativeGil);
                Assert.DoesNotContain(battle.Events, entry => entry.Kind == "GuardianForceHealing");
                return;
            }
        }
        else
        {
            NativeBattleActionHeaderSlot header = battle.NativeActionPackets.ReadHeader(battle.ActiveAction!.NativeHeader!.Value);
            Assert.Equal(failureMessage is null ? (byte)38 : (byte)0, header.Command);
            Assert.Equal(failureMessage is null ? (byte)14 : (byte)10, header.Sequence);
            Assert.Equal(failureMessage is null ? (ushort)6 : (ushort)9, header.Action);
            Assert.Equal(failureMessage is null ? (ushort)96 : (ushort)0, header.Effect);
            Assert.Equal(failureMessage is null ? 0x495050u : 0x47EBD0u, header.Data!.ProducerAddress);
            Assert.Equal(failureMessage is null ? dance.Name : binding.Name, header.Data.FormattedText);
            if (failureMessage is not null)
            {
                Assert.Equal((byte)0, header.ReadContactCount());
            }
        }

        restored = BattleSession.Restore(battle.Capture());
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
            if (expanded)
            {
                Assert.Equal(roster.Where(member => member.IsPlayer && member.Id != "ko").Select(member => member.Id), battle.Events.Where(entry => entry.Kind == "EffectContact").Select(entry => entry.TargetId));
            }
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

    [Theory]
    [InlineData(23, "actor", 1000, 0)]
    [InlineData(24, "actor", 1000, 0)]
    [InlineData(25, "ally-3", 500, 0x7E)]
    [InlineData(26, "ally-3", 100, 0)]
    [InlineData(27, "ally-3", 0, 1)]
    [InlineData(30, "enemy-5", 1000, 0)]
    [InlineData(31, "enemy-5", 1000, 0)]
    [InlineData(32, "enemy-5", 1000, 0)]
    public void ExpandedEquippedCommandsPreserveEffectsAndTheirLogicalRecipients(byte command, string targetId, int allyHp, ushort allyStatus)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleDefinition original = Create(game, assets, command, allyHp, allyStatus).ReadDefinition();
        BattleCombatantDefinition caster = original.Combatants[0] with { CurrentHp = command == 32 ? 500 : 1000, NativeCommands = [.. original.Combatants[0].NativeCommands, assets.ReadNativeCommand(29)] };
        BattleCombatantDefinition ally = original.Combatants[1] with { Id = "ally-3", ModelId = 3 };
        BattleCombatantDefinition enemy = original.Combatants[2];
        BattleCombatantDefinition[] enemies = Enumerable.Range(0, 6).Select(index => enemy with { Id = $"enemy-{index}" }).ToArray();
        BattleSession battle = new(original with
        {
            UsesExpandedRoster = true,
            Combatants = [caster, ally with { Id = "ally-1", ModelId = 1, CurrentHp = 1000, NativeStatus0 = 0 }, ally with { Id = "ally-2", ModelId = 2, CurrentHp = 1000, NativeStatus0 = 0 }, ally, .. enemies]
        }, BattleAtbMode.Normal);

        BattleActionDefinition action = Assert.Single(battle.GetNativePlayerActionChoices(caster.Id, limits: false));
        Assert.Equal((command << 16) | assets.ReadNativeCommand(command).ActionId, action.Id);
        Assert.Empty(battle.GetNativePlayerActionChoices(caster.Id, limits: false, command: 29));
        Assert.Throws<NotSupportedException>(() => battle.QueueNativePlayerCommand(caster.Id, enemies[^1].Id, 29, assets.ReadNativeCommand(29).ActionId));
        battle.QueueAction(caster.Id, targetId, BattleAbility.NativeCommand, action.Id);
        BattleAttackCommand queued = Assert.Single(battle.QueuedActions);
        Assert.Equal(targetId, queued.TargetId);
        Assert.Null(queued.TargetRequest.OriginalProjection);
        Assert.Equal(command, queued.NativeQueuedCommand);
        BattleSession restored = BattleSession.Restore(battle.Capture());
        for (int frame = 0; frame <= BattleSession.AttackWindupFrames; frame++)
        {
            battle.Tick();
            restored.Tick();
        }
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        Assert.Single(battle.Events, entry => entry.Kind == "ActionComplete");
        Assert.Contains(battle.Events, entry => entry.Kind == "EffectContact" && entry.TargetId == targetId);
        BattleCombatant recipient = battle.Combatants.Single(actor => actor.Id == targetId);
        BattleCombatantSnapshot saved = battle.Capture().Combatants.Single(actor => actor.Id == targetId);
        switch (command)
        {
            case 23:
                Assert.Equal(0x80000u, saved.NativeStatus!.Battle & 0x80000);
                break;
            case 24:
                Assert.Equal(0x22u, saved.NativeStatus!.Battle & 0x22);
                Assert.Equal(0x20, saved.NativeStatus.Saved & 0x20);
                break;
            case 25:
                Assert.Equal(500, recipient.CurrentHp);
                Assert.Equal(0, recipient.PersistentStatus & 0x7F);
                break;
            case 26:
            case 27:
                Assert.Equal(1000, recipient.CurrentHp);
                Assert.True(recipient.IsAlive);
                break;
            case 30:
                Assert.Equal(0x400u, saved.NativeStatus!.Battle & 0x400);
                break;
            case 31:
                Assert.Equal(5000, recipient.MaximumHp - recipient.CurrentHp);
                BattleCombatant sacrificed = battle.Combatants.Single(actor => actor.Id == caster.Id);
                Assert.False(sacrificed.IsActive);
                Assert.Equal(0, sacrificed.CurrentHp);
                Assert.Equal((ushort)1, sacrificed.PersistentStatus);
                Assert.Equal((ushort)1, sacrificed.NativeDeathCount);
                BattleSession completed = BattleSession.Restore(battle.Capture());
                completed.Tick();
                Assert.Equal((ushort)1, completed.Combatants.Single(actor => actor.Id == caster.Id).NativeDeathCount);
                break;
            case 32:
                Assert.True(recipient.CurrentHp < recipient.MaximumHp);
                Assert.True(battle.Combatants.Single(actor => actor.Id == caster.Id).CurrentHp > 500);
                Assert.Single(battle.Events, entry => entry.Kind == "SecondaryEffectContact" && entry.TargetId == caster.Id);
                break;
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ExpandedInstalledCardCommitsAtContactAndRestoresWithoutReplay(bool succeeds)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleDefinition original = Create(game, assets, 29).ReadDefinition();
        BattleNativeEnemySpecial special = assets.ReadNativeEnemySpecial(4);
        Assert.InRange(special.CommonCard, (byte)0, (byte)109);
        BattleCombatantDefinition caster = original.Combatants[0];
        BattleCombatantDefinition ally = original.Combatants[1];
        BattleCombatantDefinition enemy = original.Combatants[2] with { ModelId = 4, MaximumHp = 1000, CurrentHp = succeeds ? 1 : 1000, NativeSpecial = special };
        NativeCardInventory cards = NativeCardInventory.CreateNewGame();
        BattleSession battle = new(original with
        {
            UsesExpandedRoster = true,
            NativeCards = cards,
            NativeCardNames = Enumerable.Range(0, 110).Select(id => (string?)$"Card {id}").ToArray(),
            InitialRandom = NativeBattleRandomState.ForecastPhase(0),
            Combatants = [caster, ally, ally with { Id = "ally-2", ModelId = 2 }, ally with { Id = "ally-3", ModelId = 3 },
                .. Enumerable.Range(0, 6).Select(index => enemy with { Id = $"enemy-{index}" })]
        }, BattleAtbMode.Normal);
        BattleActionDefinition action = Assert.Single(battle.GetNativePlayerActionChoices(caster.Id, limits: false));
        Assert.Throws<InvalidOperationException>(() => battle.QueueAction(caster.Id, ally.Id, BattleAbility.NativeCommand, action.Id));
        battle.QueueAction(caster.Id, "enemy-5", BattleAbility.NativeCommand, action.Id);
        battle.Tick();
        BattleSnapshot windup = battle.Capture();
        Assert.Null(windup.ActiveAction!.NativeSpecial);
        Assert.Equal(JsonSerializer.Serialize(cards), JsonSerializer.Serialize(windup.NativeCards));
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(windup))!);
        for (int tick = 0; tick <= BattleSession.AttackWindupFrames; tick++)
        {
            battle.Tick();
            restored.Tick();
        }
        BattleCombatant target = battle.Combatants.Single(actor => actor.Id == "enemy-5");
        Assert.Equal(succeeds, target.IsEjected);
        Assert.Equal(!succeeds, target.IsAlive);
        Assert.Equal(succeeds ? Math.Max(0, cards.AvailableCount(special.CommonCard)) + 1 : cards.AvailableCount(special.CommonCard), battle.Capture().NativeCards!.AvailableCount(special.CommonCard));
        Assert.Single(battle.Events, item => item.Kind == "ActionComplete");
        Assert.Single(battle.Events, item => item.Kind == "EffectContact" && item.TargetId == target.Id);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Fact]
    public void ExpandedInstalledDevourUsesTheAllocatedBandAndOriginalGrowthEffect()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleDefinition original = Create(game, assets, 7).ReadDefinition();
        const int enemyModel = 46;
        const int enemyLevel = 32;
        NativeBattleAiDefinition program = assets.ReadEnemyAi(enemyModel, enemyLevel);
        BattleNativeEnemySpecial special = assets.ReadNativeEnemySpecial(enemyModel);
        int effectIndex = special.DevourEffects[program.LevelBand];
        BattleNativeDevourEffect[] effects = assets.ReadNativeDevourEffects();
        BattleNativeDevourEffect effect = effects[effectIndex];
        Assert.Equal(30, effect.Effect);
        Assert.NotEqual(0, effect.StatFlags);

        BattleNativeCharacterGrowth growth = new(0, [0, 0, 0, 0, 0, 0]);
        BattleCombatantDefinition caster = original.Combatants[0] with { NativeGrowth = growth };
        BattleCombatantDefinition ally = original.Combatants[1];
        BattleCombatantDefinition enemy = original.Combatants[2] with
        {
            ModelId = enemyModel, Level = enemyLevel, MaximumHp = 1000, CurrentHp = 1, NativeSpecial = special, NativeAi = program
        };
        BattleSession battle = new(original with
        {
            UsesExpandedRoster = true,
            NativeDevourEffects = effects,
            InitialRandom = NativeBattleRandomState.ForecastPhase(0),
            Combatants = [caster, ally, ally with { Id = "ally-2", ModelId = 2 }, ally with { Id = "ally-3", ModelId = 3 },
                .. Enumerable.Range(0, 6).Select(index => enemy with { Id = $"enemy-{index}" })]
        }, BattleAtbMode.Normal);
        BattleActionDefinition action = Assert.Single(battle.GetNativePlayerActionChoices(caster.Id, limits: false));
        battle.QueueAction(caster.Id, "enemy-5", BattleAbility.NativeCommand, action.Id);
        battle.Tick();
        BattleSession restored = BattleSession.Restore(battle.Capture());
        for (int tick = 0; tick <= BattleSession.AttackWindupFrames; tick++)
        {
            battle.Tick();
            restored.Tick();
        }
        BattleCombatant target = battle.Combatants.Single(actor => actor.Id == "enemy-5");
        Assert.True(target.IsEjected);
        BattleNativeCharacterGrowth after = battle.Combatants.Single(actor => actor.Id == caster.Id).NativeGrowth!;
        Assert.Equal(effect.HitPointBonus, after.HitPointBonus);
        Assert.Equal(Enumerable.Range(0, 6).Select(index => (byte)((effect.StatFlags >> index) & 1)), after.StatBonuses);
        Assert.Single(battle.Events, entry => entry.Kind == "NativeText" && entry.Message == effect.Description);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
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
