using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ExpandedBattleGuestProgressTests
{
    [Fact]
    public void PendingElevenAllyCapacityDoesNotCreateOrRecruitGuestsWhenNativeBattleReturns()
    {
        const ushort formation = 0;
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameSession initial = GameSession.CreateColdProcess();
        initial.StartNewGame();
        using (GameWorld bootstrap = new(initial, directory, sharedAssets: assets, expandedBattleAllyCount: 3))
        {
            bootstrap.EnsureField();
            initial.SetEventState(bootstrap.Events.Capture());
        }
        GameSession session = new(initial.State with
        {
            FieldId = 133,
            FieldName = "cavern-component",
            EventState = null,
            DepartingFieldState = initial.State.EventState,
            FieldLoadContext = null,
            FieldRequestPacket = null
        });
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));

        using GameWorld world = new(session, directory, sharedAssets: assets, expandedBattleAllyCount: 3);
        world.EnsureField();
        world.Apply(new(PlayerActionKind.Wait, Frames: 4));
        session.SetCharacterVitals(session.State.CharacterVitals!.Rest() with { CombatParty = [0, 1, 2] });
        BattleSession original = Start(world, session, formation);
        Assert.NotNull(original.Presentation);
        world.ConfigureGameplayPreferences(session.State.GameplayPreferences with { BattleAllyCount = 11 });
        Assert.Equal(11, session.State.PendingGameplayPreferences!.BattleAllyCount);
        Assert.Equal(8, session.State.CharacterVitals!.Records.Length);

        world.ApplyBattleInput(new(BattleInputKind.InstantVictory));
        world.ApplyBattleInput(new(BattleInputKind.Return));
        Assert.Null(session.State.PendingGameplayPreferences);
        Assert.Equal(11, session.State.GameplayPreferences.BattleAllyCount);
        Assert.Equal(8, session.State.CharacterVitals!.Records.Length);
        BattleSession next = Start(world, session, formation);
        Assert.NotNull(next.Presentation);
        Assert.Equal(3, next.Combatants.Count(actor => actor.IsPlayer));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GuestProgressSurvivesAThreeAllyBattleAndColdReload(bool sharedStock)
    {
        const int guestId = 10;
        const int fallenId = 9;
        const int guardianForce = 0;
        const ushort formation = 0;
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameSession initial = GameSession.CreateColdProcess();
        initial.StartNewGame();
        using (GameWorld bootstrap = new(initial, directory, sharedAssets: assets))
        {
            bootstrap.EnsureField();
            initial.SetEventState(bootstrap.Events.Capture());
        }
        GameSession session = new(initial.State with
        {
            FieldId = 133,
            FieldName = "cavern-component",
            EventState = null,
            DepartingFieldState = initial.State.EventState,
            FieldLoadContext = null,
            FieldRequestPacket = null
        });
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));

        BattleNativeAction quake;
        uint experience;
        GameState reduced;
        using (GameWorld world = new(session, directory, sharedAssets: assets))
        {
            world.EnsureField();
            world.Apply(new(PlayerActionKind.Wait, Frames: 4));
            quake = Assert.Single(world.BattleResources.NativeActions, action => action.Command == 2 && action.Name == "Quake");
            world.ConfigureGameplayPreferences(session.State.GameplayPreferences with { BattleAllyCount = 11, SharedMagicStock = sharedStock });
            NativeCharacterVitals vitals = session.State.CharacterVitals!.WithExtendedRoster(true).Rest().AcquireGuardianForce(guardianForce);
            Assert.Equal(11, vitals.Records.Length);
            Assert.True(vitals.ExtendedRosterEnabled);
            for (int row = 0; row < NativeCharacterVitals.NativeRecordCount; row++)
            {
                vitals.Records[row] = vitals.Records[row] with { MemberFlags = (ushort)(vitals.Records[row].MemberFlags | 1) };
            }
            int magicAbility = 20 + vitals.MenuDefinitions.CommandAbilityCommands.IndexOf((byte)2);
            int donor = Array.FindIndex(vitals.Records, record => record.Junction.GuardianForceAssignments.Contains(guardianForce));
            vitals = donor < 0 ? vitals.ToggleGuardianForceJunction(guestId, guardianForce) : vitals.ReassignGuardianForceJunction(guestId, guardianForce, donor);
            if (!vitals.JunctionMagicSlotAvailable(guestId, 0))
            {
                vitals = vitals.TeachGuardianForceAbility(guardianForce, 1);
            }
            vitals = vitals.SetJunctionAbility(guestId, 0, magicAbility, command: true);
            vitals = vitals.ChangeMagicStock(guestId, quake.ActionId, 5, MagicOperationReason.DrawPoint, out MagicStockMutation mutation);
            Assert.Equal(5, mutation.Accepted);
            vitals = vitals.SetJunctionMagic(guestId, 0, quake.ActionId);
            vitals.Records[fallenId] = vitals.Records[fallenId] with { HitPoints = new NativeHpWord(0, 0), Status = 1 };
            byte[] selectedRows = Enumerable.Range(0, NativeCharacterVitals.ExtendedRecordCount).Select(row => (byte)row).ToArray();
            session.SetCharacterVitals(vitals.WithBattleParty(selectedRows, NativeCharacterVitals.ExtendedRecordCount));
            experience = vitals.Records[guestId].Experience;

            BattleSession expanded = Start(world, session, formation);
            BattleCombatant guest = Assert.Single(expanded.Combatants, actor => actor.IsPlayer && actor.ModelId == guestId);
            BattleCombatant fallen = Assert.Single(expanded.Combatants, actor => actor.IsPlayer && actor.ModelId == fallenId);
            Assert.True(expanded.UsesExpandedRoster);
            Assert.Equal(11, expanded.Combatants.Count(actor => actor.IsPlayer));
            Assert.False(fallen.IsAlive);
            Assert.Equal(guestId, guest.NativeLimit!.CharacterId);
            Assert.Contains(guardianForce, guest.EquipmentIds);
            Assert.Equal(vitals.GetMagicMaximumHitPoints(guestId), guest.MaximumHp);
            Assert.Equal(5, expanded.GetAvailableMagicCasts(guest.Id, quake.ActionId));
            Assert.Equal(0, expanded.GetAvailableMagicCasts("party-0", quake.ActionId));
            if (sharedStock)
            {
                Assert.Equal(5, expanded.Combatants.Single(actor => actor.Id == "party-0").MagicStocks.Single(stock => stock.Id == quake.ActionId).Count);
            }

            world.ConfigureGameplayPreferences(session.State.GameplayPreferences with { BattleAllyCount = 3 });
            Assert.Equal(11, session.State.GameplayPreferences.BattleAllyCount);
            Assert.Equal(3, session.State.PendingGameplayPreferences!.BattleAllyCount);
            for (int turn = 0; turn < 32 && !expanded.CanChoosePlayerCommand(guest.Id); turn++)
            {
                world.ApplyBattleInput(new(BattleInputKind.AdvanceTurn));
                world.ApplyBattleInput(new(BattleInputKind.Tick, Frames: 30));
            }
            Assert.True(expanded.CanChoosePlayerCommand(guest.Id));
            Assert.Contains(BattleAbility.Magic, guest.Abilities);
            BattleCombatant target = Assert.Single(expanded.Combatants, actor => !actor.IsPlayer && actor.SceneSlot == 0 && actor.IsAlive);
            BattleActionDefinition action = Assert.Single(expanded.ActionDefinitions, candidate => candidate.Ability == BattleAbility.Magic && candidate.Id == quake.ActionId);
            Assert.True(BattleSession.IsValidSelectedActionTarget(guest, action, target));
            world.ApplyBattleInput(new(BattleInputKind.Action) { ActorId = guest.Id, TargetId = target.Id, Ability = BattleAbility.Magic, ActionId = quake.ActionId });
            for (int callback = 0; callback < 1200 && guest.IsBusy; callback++)
            {
                world.ApplyBattleInput(new(BattleInputKind.Tick));
            }
            Assert.False(guest.IsBusy);
            Assert.Equal(4, expanded.GetAvailableMagicCasts(guest.Id, quake.ActionId));
            BattleCombatant attacker = Assert.Single(expanded.Combatants, actor => actor.Id == "party-0");
            foreach (BattleCombatant survivor in expanded.Combatants.Where(actor => !actor.IsPlayer && actor.IsAlive).ToArray())
            {
                // Bound the cleanup turns while leaving each KO and reward record to the real Attack command.
                Assert.False(survivor.Untargetable);
                survivor.CurrentHp = 1;
                for (int attempt = 0; attempt < 3 && survivor.IsAlive; attempt++)
                {
                    attacker.Atb = BattleSession.AtbMaximum;
                    for (int callback = 0; callback < 1200 && !expanded.CanChoosePlayerCommand(attacker.Id); callback++)
                    {
                        world.ApplyBattleInput(new(BattleInputKind.Tick));
                    }
                    Assert.True(expanded.CanChoosePlayerCommand(attacker.Id));
                    world.ApplyBattleInput(new(BattleInputKind.Attack, ActorId: attacker.Id, TargetId: survivor.Id) { ForcePhysical = true });
                    for (int callback = 0; callback < 1200 && attacker.IsBusy; callback++)
                    {
                        world.ApplyBattleInput(new(BattleInputKind.Tick));
                    }
                    Assert.False(attacker.IsBusy);
                }
                Assert.False(survivor.IsAlive, $"Ordinary attacks did not defeat {survivor.Id} (model {survivor.ModelId}).");
            }
            for (int callback = 0; callback < 1200 && !expanded.CanReturnToField; callback++)
            {
                world.ApplyBattleInput(new(BattleInputKind.Tick));
            }
            Assert.Equal(BattleOutcome.Victory, expanded.Outcome);
            Assert.True(expanded.CanReturnToField);
            BattleRewardSummary reward = world.PreviewBattleRewards();
            Assert.True(reward.CharacterExperience[(byte)guestId] > 0);
            ushort abilityPoints = reward.GuardianForceAbilityPoints[guardianForce];
            Assert.True(abilityPoints > 0);
            experience += reward.CharacterExperience[(byte)guestId];
            world.ApplyBattleInput(new(BattleInputKind.Return));
            Assert.Equal(abilityPoints, world.LastBattleRewards!.GuardianForceAbilityPoints[guardianForce]);
            Assert.Null(world.Battle);
            Assert.Equal(3, session.State.GameplayPreferences.BattleAllyCount);
            Assert.Null(session.State.PendingGameplayPreferences);
            AssertGuestProgress(session.State.CharacterVitals!);

            BattleSession small = Start(world, session, formation);
            Assert.NotNull(small.Presentation);
            Assert.Equal(new[] { 0, 1, 2 }, small.Combatants.Where(actor => actor.IsPlayer).Select(actor => actor.ModelId));
            Assert.Equal(0, small.GetAvailableMagicCasts("party-0", quake.ActionId));
            world.ApplyBattleInput(new(BattleInputKind.InstantVictory));
            world.ApplyBattleInput(new(BattleInputKind.Return));
            Assert.Null(world.Battle);
            AssertGuestProgress(session.State.CharacterVitals!);
            reduced = session.PrepareSnapshot();
        }

        GameState restored = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(reduced))!;
        Assert.Equal(3, restored.GameplayPreferences.BattleAllyCount);
        AssertGuestProgress(restored.CharacterVitals!);
        GameplaySaveCharacters compact = GameplaySaveCharacters.Capture(restored.CharacterVitals!);
        AssertGuestProgress(compact.Restore(assets.ReadNewGameCharacterVitals()));

        GameSession resumed = new(restored);
        using GameWorld resumedWorld = new(resumed, directory, sharedAssets: assets);
        resumedWorld.EnsureField();
        resumedWorld.ConfigureGameplayPreferences(resumed.State.GameplayPreferences with { BattleAllyCount = 11 });
        BattleSession expandedAgain = Start(resumedWorld, resumed, formation);
        Assert.True(expandedAgain.UsesExpandedRoster);
        Assert.Equal(11, expandedAgain.Combatants.Count(actor => actor.IsPlayer));
        BattleCombatant resumedGuest = Assert.Single(expandedAgain.Combatants, actor => actor.IsPlayer && actor.ModelId == guestId);
        Assert.Contains(guardianForce, resumedGuest.EquipmentIds);
        Assert.Equal(guestId, resumedGuest.NativeLimit!.CharacterId);
        Assert.Equal(4, expandedAgain.GetAvailableMagicCasts(resumedGuest.Id, quake.ActionId));
        Assert.False(Assert.Single(expandedAgain.Combatants, actor => actor.IsPlayer && actor.ModelId == fallenId).IsAlive);

        void AssertGuestProgress(NativeCharacterVitals current)
        {
            NativeCharacterVital guest = Assert.Single(current.Records, record => record.CharacterId == guestId);
            NativeCharacterVital fallen = Assert.Single(current.Records, record => record.CharacterId == fallenId);
            Assert.Equal(experience, guest.Experience);
            Assert.Equal(4, current.GetMagicCount(guestId, quake.ActionId));
            Assert.Equal(quake.ActionId, guest.Junction.MagicJunctions[0]);
            Assert.Contains(guardianForce, guest.Junction.GuardianForceAssignments);
            Assert.Equal((ushort)0, fallen.HitPoints.Minimum);
            Assert.Equal(1, fallen.Status & 1);
            Assert.Equal(new byte[] { 0, 1, 2 }, current.CombatParty);
            Assert.True(current.ExtendedRosterEnabled);
            if (sharedStock)
            {
                Assert.Equal(guestId, MagicJunctionPolicy.GetOwner(current, quake.ActionId));
                Assert.Equal(4, current.GetMagicCount(0, quake.ActionId));
            }
        }
    }

    private static BattleSession Start(GameWorld world, GameSession session, ushort formation)
    {
        // Formation 0 supplies damageable ground targets through the ordinary random field host.
        string source = world.Events.Capture().GetProperty("ProgramHash").GetString()!;
        session.SetRandomEncounters(new(true, NativeRandomEncounterState.FromColdProcess(), new(SceneKind.Field, session.State.FieldId, world.Events.Frame, formation, source)));
        world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: 0));
        return Assert.IsType<BattleSession>(world.Battle);
    }
}
