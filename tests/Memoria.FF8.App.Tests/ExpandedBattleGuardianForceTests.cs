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

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(7)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(13)]
    public void InstalledGuardianSummonsFinishTheirNativeModulesOnAnExpandedRoster(int guardianId)
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        using GameAssets game = new(TestResources.GameDirectory);
        BattleActionDefinition action = Assert.Single(assets.ActionDefinitions, candidate => candidate.Ability == BattleAbility.GuardianForce && candidate.Id == guardianId);
        Assert.NotNull(RunGuardianForce(assets, action with { NativeGuardianBoostAvailable = false }, presentationGame: game));
    }

    [Fact]
    public void IncomingDamageUsesTheChargingGuardiansHpAndCancelsItsCast()
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        BattleActionDefinition action = Assert.Single(assets.ActionDefinitions, action => action.Ability == BattleAbility.GuardianForce && action.Id == 1);
        Assert.Equal(0, RunGuardianForce(assets, action, shieldInterruption: true));
    }

    internal static int? RunGuardianForce(BattleAssets assets, BattleActionDefinition action, bool shieldInterruption = false, GameAssets? presentationGame = null)
    {
        BattleNativeAction native = Assert.IsType<BattleNativeAction>(action.NativeAction);
        ushort guardianHp = shieldInterruption ? (ushort)1 : (ushort)300;
        BattleCombatantDefinition[] party = Enumerable.Range(0, 11).Select(row => new BattleCombatantDefinition
        {
            Id = $"party-{row}",
            Name = $"Ally {row}",
            IsPlayer = true,
            ModelId = row == 10 && presentationGame is not null ? 0 : row,
            WeaponId = row == 10 && presentationGame is not null ? 0 : null,
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
            ModelId = 2,
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
            NativeBattleUiRandom = presentationGame is null ? null : NativePlaytimeState.FromColdProcess().CreateBattleUiRandom(),
            NativeGlobalRandom = presentationGame is null ? null : new(0),
            NativeGlobalSoundProcess = presentationGame is null ? null : NativeGlobalSoundProcess.FromBufferlessColdProcess(),
            NativeCommonEffectTexture = presentationGame?.ReadNativeCommonEffectTexture(),
            NativeStatusRules = assets.ReadNativeStatusRules(0)
        }, BattleAtbMode.Normal);
        if (presentationGame is not null)
        {
            GameWorld.AttachExpandedEffects(battle, presentationGame, assets);
        }
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
        int tickLimit = presentationGame is null ? 240 : 1200;
        bool sawModule = false;
        for (int tick = 0; tick < tickLimit && caster.IsBusy; tick++)
        {
            battle.Tick();
            sawModule |= battle.ExpandedEffect?.Graphics.OriginalEffect?.RequestedEffect == native.MagicId;
        }
        Assert.False(caster.IsBusy, action.Name);
        Assert.Equal(presentationGame is not null, sawModule);
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
            IEnumerable<string> expected = (action.Allies ? party : enemies).Select(actor => actor.Id);
            IEnumerable<string?> actual = contacts.Select(contact => contact.TargetId);
            if (presentationGame is not null)
            {
                expected = expected.Order(StringComparer.Ordinal);
                actual = actual.Order(StringComparer.Ordinal);
            }
            Assert.Equal(expected, actual);
        }
        else
        {
            Assert.Single(contacts);
        }
        return contacts.Length;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InstalledGuardianChargesAndUsesItsCommandsAfterStandaloneRestore(bool transform)
    {
        byte guardianId = transform ? (byte)2 : (byte)1;
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
        world.ConfigureGameplayPreferences(session.State.GameplayPreferences with { BattleAllyCount = 11, GuardianFormsEnabled = transform });
        NativeCharacterVitals vitals = session.State.CharacterVitals!.WithExtendedRoster(true).Rest();
        byte command = checked((byte)(20 + vitals.MenuDefinitions.CommandAbilityCommands.IndexOf((byte)3)));
        NativeCharacterVital[] records = vitals.Records.Select(record => record with
        {
            MemberFlags = (ushort)(record.MemberFlags | 1),
            HitPointBonus = 2400,
            Junction = record.Junction with { GuardianForces = (ushort)(record.Junction.GuardianForces & ~(1 << guardianId)) }
        }).ToArray();
        records[casterRow] = records[casterRow] with
        {
            Junction = records[casterRow].Junction with { GuardianForces = (ushort)(1 << guardianId), Commands = command },
            GuardianForceCompatibility = Enumerable.Repeat((ushort)1000, 16).ToArray(),
            StatBonuses = [0, 0, 0, 0, 240, 0]
        };
        vitals = vitals with { Records = records };
        for (int row = 0; row < records.Length; row++)
        {
            ushort hp = vitals.GetMagicMaximumHitPoints(row);
            records[row] = records[row] with { HitPoints = new(hp, hp), Status = 0 };
        }
        NativeGuardianForceVital[] guardians = vitals.GuardianForces.ToArray();
        int boostBit = Assert.Single(Enumerable.Range(19, 9), index => (vitals.Definitions.GuardianForceModifiers[index - 19].Flags & 1) != 0);
        NativeGuardianForceVital selectedGuardian = guardians[guardianId];
        guardians[guardianId] = selectedGuardian with { Flags = 1, LearnedBank = selectedGuardian.LearnedBank with { High = selectedGuardian.LearnedBank.High | (1UL << boostBit) } };
        guardians[guardianId] = guardians[guardianId] with { HitPoints = vitals.Definitions.GuardianForceMaximum(guardianId, guardians[guardianId]) };
        byte[] selectedRows = Enumerable.Range(0, NativeCharacterVitals.ExtendedRecordCount).Select(row => (byte)row).ToArray();
        session.SetCharacterVitals((vitals with { GuardianForces = guardians }).WithBattleParty(selectedRows, NativeCharacterVitals.ExtendedRecordCount));
        string source = world.Events.Capture().GetProperty("ProgramHash").GetString()!;
        session.SetRandomEncounters(new(true, NativeRandomEncounterState.FromColdProcess(), new(SceneKind.Field, fieldId, world.Events.Frame, formation, source)));
        world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: 0));
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        Assert.True(battle.UsesExpandedRoster);
        Assert.Equal(11, battle.Combatants.Count(actor => actor.IsPlayer));
        string[] enemyIds = battle.Combatants.Where(actor => !actor.IsPlayer).Select(actor => actor.Id).ToArray();
        string[] expectedSummonRecipients = enemyIds;
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
        Assert.Equal(transform ? "Hell Fire" : "Diamond Dust", summon.Name);
        if (transform)
        {
            hud.Navigate(MenuInput.Right);
            Assert.True(hud.SelectingGuardianFormAction);
            hud.Choose(0);
        }
        else
        {
            hud.Choose(0);
            Assert.True(hud.SelectingTargetGroup);
            Assert.Equal(enemyIds, hud.Targets.Select(actor => actor.Id));
            hud.Attack();
        }
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
            bool sawEntry = false;
            bool pressedBoost = false;
            ushort boostPercent = 0;
            bool sawOrdinaryModule = false;
            HashSet<NativeBattleCameraPose> entryCameras = [];
            BattleScenePresentation presentation = new(world.BattleResources, battle.ReadDefinition(), true);
            bool renderedEntry = false;
            for (int turn = 0; turn < 32 && caster.IsBusy; turn++)
            {
                world.ApplyBattleInput(new(BattleInputKind.AdvanceTurn));
                resumedWorld.ApplyBattleInput(new(BattleInputKind.AdvanceTurn));
                for (int frame = 0; frame < 60 && caster.IsBusy; frame++)
                {
                    world.ApplyBattleInput(new(BattleInputKind.Tick));
                    resumedWorld.ApplyBattleInput(new(BattleInputKind.Tick));
                    sawOrdinaryModule |= !transform && battle.ExpandedEffect?.Graphics.OriginalEffect?.RequestedEffect == summon.NativeAction!.MagicId;
                    if (!transform && battle.NativeGuardianBoost is { Phase: 4, Window: 1 } && !pressedBoost)
                    {
                        Assert.Equal((ushort)0, battle.NativeGuardianBoost.Percent);
                        foreach ((BattleSession current, GameWorld host) in new[] { (battle, world), (restored, resumedWorld) })
                        {
                            current.AdvanceComponentGuardianBoostPresentation(TimeSpan.Zero, 0x100);
                            host.ApplyBattleInput(new(BattleInputKind.GuardianBoost));
                        }
                        pressedBoost = true;
                    }
                    boostPercent = Math.Max(boostPercent, battle.NativeGuardianBoost?.Percent ?? 0);
                    if (transform && battle.ExpandedEffect is { } entrance)
                    {
                        sawEntry = true;
                        if (entrance.CinematicCamera is { FocalLength: > 0 } camera)
                        {
                            entryCameras.Add(camera);
                        }
                        Assert.Equal((byte)77, entrance.Source.ModelId4);
                        Assert.True(battle.IsExpandedEffectReplacingModel(casterId));
                        Assert.DoesNotContain(battle.Events, entry => entry.Kind == "EffectContact" && entry.ActorId == casterId);
                        if (!renderedEntry && entrance.Overlay is { LastCommands.IsEmpty: false })
                        {
                            Assert.NotEmpty(presentation.Render(battle, 640, 448));
                            renderedEntry = true;
                        }
                    }
                }
            }
            Assert.False(caster.IsBusy);
            Assert.Empty(battle.GuardianForceCasts);
            if (transform)
            {
                Assert.True(sawEntry);
                Assert.True(entryCameras.Count > 1);
                Assert.True(renderedEntry);
                Assert.Equal(BattleGuardianFormPhase.Active, battle.GetGuardianForm(casterId)!.Phase);
                Assert.Equal(battle.GetGuardianForm(casterId), restored.GetGuardianForm(casterId));
                Assert.NotEmpty(presentation.Render(battle, 640, 448));
                Assert.NotNull(presentation.GetActorBounds(casterId, new(0, 0, 640, 448)));
                _ = presentation.CollectSounds(battle);
                const int fireId = 1;
                int fireBefore = caster.MagicStocks.FirstOrDefault(stock => stock.Id == fireId)?.Count ?? 0;
                foreach (BattleGuardianFormAction action in new[] { BattleGuardianFormAction.Attack, BattleGuardianFormAction.Fire, BattleGuardianFormAction.HellFire })
                {
                    for (int turn = 0; turn < 32 && !caster.IsReady; turn++)
                    {
                        world.ApplyBattleInput(new(BattleInputKind.AdvanceTurn));
                        resumedWorld.ApplyBattleInput(new(BattleInputKind.AdvanceTurn));
                        world.ApplyBattleInput(new(BattleInputKind.Tick, Frames: 30));
                        resumedWorld.ApplyBattleInput(new(BattleInputKind.Tick, Frames: 30));
                    }
                    Assert.True(caster.IsReady, $"Could not select {action} at frame {battle.Frame}: {battle.GetGuardianForm(casterId)}.");
                    string[] recipients = battle.Combatants.Where(actor => !actor.IsPlayer && actor.IsAlive && actor.IsActive && !actor.Untargetable).Select(actor => actor.Id).ToArray();
                    Assert.True(recipients.Length > NativeBattleSlots.Count);
                    world.ApplyBattleInput(new(BattleInputKind.GuardianFormAction, ActorId: casterId, TargetId: recipients[^1], ActionId: (int)action));
                    resumedWorld.ApplyBattleInput(new(BattleInputKind.GuardianFormAction, ActorId: casterId, TargetId: recipients[^1], ActionId: (int)action));
                    if (action == BattleGuardianFormAction.HellFire)
                    {
                        expectedSummonRecipients = recipients;
                        break;
                    }
                    bool sawFire = false;
                    for (int frame = 0; frame < 300 && caster.IsBusy; frame++)
                    {
                        world.ApplyBattleInput(new(BattleInputKind.Tick));
                        if (frame == 5)
                        {
                            Assert.NotEmpty(presentation.Render(battle, 640, 448));
                        }
                        resumedWorld.ApplyBattleInput(new(BattleInputKind.Tick));
                        if (action == BattleGuardianFormAction.Fire && battle.ExpandedEffect is { } fire)
                        {
                            Assert.Equal((byte)77, fire.Source.ModelId4);
                            Assert.Equal(battle.GetGuardianFormActionDefinition(casterId, action).NativeAction!.MagicId, fire.Graphics.OriginalEffect!.RequestedEffect);
                            sawFire = true;
                        }
                    }
                    Assert.False(caster.IsBusy);
                    Assert.Equal(action == BattleGuardianFormAction.Fire, sawFire);
                    Assert.Equal(fireBefore, caster.MagicStocks.FirstOrDefault(stock => stock.Id == fireId)?.Count ?? 0);
                }
                bool sawFinisher = false;
                bool renderedFinisher = false;
                bool skipped = false;
                for (int frame = 0; frame < 900 && battle.GetGuardianForm(casterId) is not null; frame++)
                {
                    world.ApplyBattleInput(new(BattleInputKind.Tick));
                    resumedWorld.ApplyBattleInput(new(BattleInputKind.Tick));
                    if (battle.ExpandedEffect is { } finisher)
                    {
                        sawFinisher = true;
                        Assert.Equal((byte)77, finisher.Source.ModelId4);
                        Assert.Equal(201, finisher.Graphics.OriginalEffect!.RequestedEffect);
                        if (!renderedFinisher && finisher.Overlay is { LastCommands.IsEmpty: false })
                        {
                            Assert.NotEmpty(presentation.Render(battle, 640, 448));
                            renderedFinisher = true;
                        }
                        if (!skipped && renderedFinisher && battle.CanSkipGuardianFormFinisher)
                        {
                            // Compare the naturally advanced and skipped paths at the same callback boundary below.
                            skipped = restored.SkipGuardianFormFinisher();
                        }
                    }
                }
                Assert.True(sawFinisher);
                Assert.True(renderedFinisher);
                Assert.True(skipped);
                Assert.Null(battle.GetGuardianForm(casterId));
                Assert.Null(restored.GetGuardianForm(casterId));
            }
            BattleEvent[] contacts = battle.Events.Where(entry => entry.Kind == "EffectContact" && entry.ActorId == casterId && entry.Ability == BattleAbility.GuardianForce).ToArray();
            Assert.Equal(expectedSummonRecipients, contacts.Select(entry => entry.TargetId));
            Assert.All(contacts, contact => Assert.True(contact.Amount > 0));
            if (transform)
            {
                Assert.Equal(contacts, restored.Events.Where(entry => entry.Kind == "EffectContact" && entry.ActorId == casterId && entry.Ability == BattleAbility.GuardianForce).ToArray());
            }
            else
            {
                Assert.True(pressedBoost);
                Assert.True(sawOrdinaryModule);
                Assert.Equal((ushort)76, boostPercent);
                Assert.Equal(battle.Events, restored.Events);
            }
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
