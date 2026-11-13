using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ExpandedBattleGuardianForceTests
{
    [Fact]
    public void EveryAdmittedOriginalGuardianActionPublishesOneContactPerLogicalTarget()
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        BattleActionDefinition[] catalog = assets.ActionDefinitions.Where(action => action.Ability == BattleAbility.GuardianForce).ToArray();
        Assert.Equal(16, catalog.Length);
        foreach (BattleActionDefinition action in catalog)
        {
            Assert.NotNull(RunGuardianForce(assets, action));
        }
    }

    [Fact]
    public void IncomingDamageUsesTheChargingGuardiansHpAndCancelsItsCast()
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        BattleActionDefinition action = Assert.Single(assets.ActionDefinitions, action => action.Ability == BattleAbility.GuardianForce && action.Id == 1);
        Assert.Equal(0, RunGuardianForce(assets, action, shieldInterruption: true));
    }

    internal static int? RunGuardianForce(BattleAssets assets, BattleActionDefinition action, bool shieldInterruption = false)
    {
        BattleNativeAction native = Assert.IsType<BattleNativeAction>(action.NativeAction);
        ushort guardianHp = shieldInterruption ? (ushort)1 : (ushort)300;
        BattleCombatantDefinition[] party = Enumerable.Range(0, 11).Select(row => new BattleCombatantDefinition
        {
            Id = $"party-{row}",
            Name = $"Ally {row}",
            IsPlayer = true,
            ModelId = row,
            CurrentHp = 4000,
            MaximumHp = 8000,
            NativeDeathCount = 0,
            NativeStatusResistances = new byte[40],
            Speed = 1
        }).ToArray();
        party[^1] = party[^1] with
        {
            Abilities = [BattleAbility.Attack, BattleAbility.GuardianForce],
            EquipmentIds = [action.Id],
            GuardianForceCompatibility = Enumerable.Repeat((ushort)1000, 16).ToArray(),
            InitialAtb = BattleSession.AtbMaximum
        };
        BattleCombatantDefinition[] enemies = Enumerable.Range(0, 11).Select(row => new BattleCombatantDefinition
        {
            Id = $"enemy-{row}",
            Name = $"Enemy {row}",
            CurrentHp = 4000,
            MaximumHp = 8000,
            NativeStatusResistances = new byte[40],
            Speed = 1
        }).ToArray();
        if (shieldInterruption)
        {
            party[0] = party[0] with { InitialAtb = BattleSession.AtbMaximum };
        }
        BattleSession battle = new(new()
        {
            UsesExpandedRoster = true,
            Combatants = [.. party, .. enemies],
            ActionDefinitions = [action],
            NativeActions = [native],
            GuardianForces = [new(action.Id, guardianHp, 300) { KillCount = 0, DeathCount = 0 }],
            NativeBattleSpeed = 0,
            NativeFieldBattleRules = 0,
            NativeStatusRules = assets.ReadNativeStatusRules(0)
        }, BattleAtbMode.Normal);
        if (!battle.SupportsExpandedAction(action))
        {
            return null;
        }
        BattleCombatant caster = battle.Combatants.Single(actor => actor.Id == party[^1].Id);
        BattleCombatant anchor = battle.Combatants.First(target => BattleSession.IsValidSelectedActionTarget(caster, action, target));
        battle.QueueAction(caster.Id, anchor.Id, BattleAbility.GuardianForce, action.Id);
        if (shieldInterruption)
        {
            battle.QueueAttack(party[0].Id, caster.Id);
        }
        for (int tick = 0; tick < 240 && caster.IsBusy; tick++)
        {
            battle.Tick();
        }
        Assert.False(caster.IsBusy, action.Name);
        BattleEvent[] contacts = battle.Events.Where(entry => entry.Kind == "EffectContact" && entry.ActorId == caster.Id && entry.Ability == BattleAbility.GuardianForce).ToArray();
        if (shieldInterruption)
        {
            Assert.Empty(contacts);
            Assert.Equal(party[^1].CurrentHp, caster.CurrentHp);
            Assert.Single(battle.Events, entry => entry.Kind == "GuardianForceCancelled" && entry.ActorId == caster.Id);
            BattleGuardianForceVital guardian = Assert.Single(battle.GuardianForces);
            Assert.Equal((ushort)0, guardian.HitPoints);
            Assert.Equal((ushort)1, guardian.DeathCount);
            return 0;
        }
        Assert.NotEmpty(contacts);
        Assert.Equal(contacts.Length, contacts.Select(contact => contact.TargetId).Distinct().Count());
        if (action.AllTargets)
        {
            Assert.Equal((action.Allies ? party : enemies).Select(actor => actor.Id), contacts.Select(contact => contact.TargetId));
        }
        else
        {
            Assert.Single(contacts);
        }
        return contacts.Length;
    }

    [Fact]
    public void InstalledShivaChargesAndHitsEveryLivingEnemyAfterStandaloneRestore()
    {
        const byte guardianId = 1;
        const byte casterRow = 10;
        const string casterId = "party-10";
        const ushort fieldId = 133;
        const ushort formation = 0;
        string directory = TestResources.GameDirectory;
        GameSession initial = GameSession.CreateColdProcess();
        initial.StartNewGame();
        using (GameWorld bootstrap = new(initial, directory))
        {
            bootstrap.EnsureField();
            initial.SetEventState(bootstrap.Events.Capture());
        }
        GameSession session = new(initial.State with
        {
            FieldId = fieldId,
            FieldName = "cavern-component",
            EventState = null,
            DepartingFieldState = initial.State.EventState,
            FieldLoadContext = null,
            FieldRequestPacket = null
        });
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        using GameWorld world = new(session, directory, expandedBattleEnemyCount: 11);
        world.EnsureField();
        world.Apply(new(PlayerActionKind.Wait, Frames: 4));
        session.SetCharacterVitals(initial.State.CharacterVitals! with { CombatParty = [0, 1, 2] });
        world.ConfigureGameplayPreferences(session.State.GameplayPreferences with { BattleAllyCount = 11 });
        NativeCharacterVitals vitals = session.State.CharacterVitals!.Rest();
        byte command = checked((byte)(20 + vitals.MenuDefinitions.CommandAbilityCommands.IndexOf((byte)3)));
        NativeCharacterVital[] records = vitals.Records.Select(record => record with
        {
            MemberFlags = (ushort)(record.MemberFlags | 1),
            HitPointBonus = 2400,
            Junction = record.Junction with { GuardianForces = (ushort)(record.Junction.GuardianForces & ~(1 << guardianId)) }
        }).ToArray();
        records[casterRow] = records[casterRow] with
        {
            Junction = records[casterRow].Junction with { GuardianForces = 1 << guardianId, Commands = command },
            GuardianForceCompatibility = Enumerable.Repeat((ushort)1000, 16).ToArray()
        };
        vitals = vitals with { Records = records };
        for (int row = 0; row < records.Length; row++)
        {
            ushort hp = vitals.GetMagicMaximumHitPoints(row);
            records[row] = records[row] with { HitPoints = new(hp, hp), Status = 0 };
        }
        NativeGuardianForceVital[] guardians = vitals.GuardianForces.ToArray();
        guardians[guardianId] = guardians[guardianId] with { Flags = 1 };
        guardians[guardianId] = guardians[guardianId] with { HitPoints = vitals.Definitions.GuardianForceMaximum(guardianId, guardians[guardianId]) };
        session.SetCharacterVitals(vitals with { GuardianForces = guardians });
        string source = world.Events.Capture().GetProperty("ProgramHash").GetString()!;
        session.SetRandomEncounters(new(true, NativeRandomEncounterState.FromColdProcess(), new(SceneKind.Field, fieldId, world.Events.Frame, formation, source)));
        world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: 0));
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        Assert.True(battle.UsesExpandedRoster);
        Assert.Equal(11, battle.Combatants.Count(actor => actor.IsPlayer));
        string[] enemyIds = battle.Combatants.Where(actor => !actor.IsPlayer).Select(actor => actor.Id).ToArray();
        Assert.Equal(11, enemyIds.Length);
        BattleCombatant caster = Assert.Single(battle.Combatants, actor => actor.Id == casterId);
        for (int turn = 0; turn < 32 && !caster.IsReady; turn++)
        {
            world.ApplyBattleInput(new(BattleInputKind.AdvanceTurn));
            world.ApplyBattleInput(new(BattleInputKind.Tick, Frames: 30));
        }
        Assert.True(caster.IsReady);

        BattleHudController hud = new(battle, (_, _) => { }, () => { }, () => { }, execute: input => world.ApplyBattleInput(input));
        hud.SelectParty(casterId);
        hud.Choose(hud.Commands.ToList().IndexOf(BattleAbility.GuardianForce));
        BattleActionDefinition summon = Assert.Single(hud.ActionChoices);
        Assert.Equal(guardianId, summon.Id);
        Assert.Equal("Diamond Dust", summon.Name);
        hud.Choose(0);
        Assert.True(hud.SelectingTargetGroup);
        Assert.Equal(enemyIds, hud.Targets.Select(actor => actor.Id));
        hud.Attack();
        BattleGuardianForceCast charge = Assert.Single(battle.GuardianForceCasts);
        Assert.Equal(casterId, charge.ActorId);
        Assert.True(charge.RemainingTicks > 0);

        string path = Path.Combine(Path.GetTempPath(), $"memoria-gf-charge-{Guid.NewGuid():N}.json");
        try
        {
            session.Save(path, compact: true);
            GameSession resumed = new(GameSession.Load(path));
            using GameWorld resumedWorld = new(resumed, directory);
            resumedWorld.EnsureField();
            BattleSession restored = Assert.IsType<BattleSession>(resumedWorld.Battle);
            Assert.Equal(charge, Assert.Single(restored.GuardianForceCasts));
            for (int turn = 0; turn < 32 && caster.IsBusy; turn++)
            {
                world.ApplyBattleInput(new(BattleInputKind.AdvanceTurn));
                resumedWorld.ApplyBattleInput(new(BattleInputKind.AdvanceTurn));
                world.ApplyBattleInput(new(BattleInputKind.Tick, Frames: 60));
                resumedWorld.ApplyBattleInput(new(BattleInputKind.Tick, Frames: 60));
            }
            Assert.False(caster.IsBusy);
            Assert.Empty(battle.GuardianForceCasts);
            BattleEvent[] contacts = battle.Events.Where(entry => entry.Kind == "EffectContact" && entry.ActorId == casterId && entry.Ability == BattleAbility.GuardianForce).ToArray();
            Assert.Equal(enemyIds, contacts.Select(entry => entry.TargetId));
            Assert.All(contacts, contact => Assert.True(contact.Amount > 0));
            Assert.Equal(battle.Events, restored.Events);
            Assert.Equal(battle.Combatants.Select(actor => actor.CurrentHp), restored.Combatants.Select(actor => actor.CurrentHp));
            Assert.Equal(battle.GuardianForces, restored.GuardianForces);
            Assert.Equal(caster.GuardianForceCompatibility, restored.Combatants.Single(actor => actor.Id == casterId).GuardianForceCompatibility);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
