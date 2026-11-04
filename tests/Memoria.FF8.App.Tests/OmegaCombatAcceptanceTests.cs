using System.Collections.Immutable;
using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class OmegaCombatAcceptanceTests
{
    [Fact]
    public void Direct_Omega_completes_its_attack_cycle_and_loses_to_an_invincible_party()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("MEMORIA_FF8_OMEGA_COMBAT_ACCEPTANCE") == "1", "Full Omega combat acceptance must be explicitly selected.");
        string directory = TestResources.GameDirectory;
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_BATTLE_VALIDATION_OUTPUT") ?? TestResources.ArtifactDirectory;
        Directory.CreateDirectory(output);
        using GameAssets assets = new(directory);
        NativeCharacterVitals vitals = PrepareParty(assets);
        GameSession session = GameWorld.CreateColdProcess(directory);
        session.StartNewGame();
        session.SetCharacterVitals(vitals);
        using GameWorld world = new(session, directory);
        BattleDefinition definition = world.CreateEncounterBattle(462);
        // An explicit diagnostic starting status; ordinary combat rules and enemy statistics are unchanged.
        definition = definition with { Combatants = definition.Combatants.Select(actor => actor.IsPlayer ? actor with { NativeStatus1 = actor.NativeStatus1 | 0x800 } : actor).ToArray() };
        NativeBattleMusicEntryResources musicResources = new NativeMusicCatalog(directory).ReadBattleEntryResources();
        NativeBattleMusicEntryPreparation music = NativeBattleMusicEntry.Prepare52CE30(462, definition.NativeFlags, new(1, null, null, null, 0, null), 5, -1, NativeGlobalSoundProcess.FromBufferlessColdProcess(), musicResources);
        definition = definition with { NativeMusicProcess = music.MusicProcess, NativeGlobalSoundProcess = music.SoundProcess };
        CombatCheckpoint? resumed = null;
        if (Environment.GetEnvironmentVariable("MEMORIA_FF8_BATTLE_VALIDATION_RESUME") is { } resume)
        {
            using GZipStream stream = new(File.OpenRead(resume), CompressionMode.Decompress);
            resumed = JsonSerializer.Deserialize<CombatCheckpoint>(stream) ?? throw new InvalidDataException("Empty combat checkpoint.");
        }
        BattleSession battle = resumed is null ? GameWorld.CreateLiveBattleSession(definition, BattleAtbMode.Normal) : BattleSession.Restore(resumed.Battle);
        battle.AttachNativeEffectResourceResolver(effect => assets.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
        BattleCombatant omega = Assert.Single(battle.Combatants, actor => !actor.IsPlayer && actor.IsActive);
        int initialEnemyHp = resumed?.InitialEnemyHp ?? omega.CurrentHp;
        Dictionary<string, int> initialPartyHp = battle.Combatants.Where(actor => actor.IsPlayer).ToDictionary(actor => actor.Id, actor => actor.CurrentHp);
        HashSet<int> dispatchedActions = resumed?.DispatchedActions.ToHashSet() ?? [];
        HashSet<int> queuedActions = resumed?.QueuedActions.ToHashSet() ?? [];
        int[] expectedActions = [2, 16, 124, 308, 309, 310, 311, 378];
        int completedEnemyActions = resumed?.CompletedEnemyActions ?? 0;
        int completedPartyActions = resumed?.CompletedPartyActions ?? 0;
        int issuedAttacks = resumed?.IssuedAttacks ?? 0;
        battle.DiagnosticEffectDispatched += dispatch =>
        {
            if (dispatch.ActorId == omega.Id && dispatch.ActionId is int action)
            {
                dispatchedActions.Add(action);
            }
        };
        battle.DiagnosticEventPublished += entry =>
        {
            if (entry.Kind == "Queued" && entry.ActorId == omega.Id && expectedActions.Contains(entry.Amount))
            {
                queuedActions.Add(entry.Amount);
            }
            if (entry.Kind == "ActionComplete")
            {
                if (entry.ActorId == omega.Id)
                {
                    completedEnemyActions++;
                }
                else
                {
                    completedPartyActions++;
                }
            }
        };
        BattleAudioPresentation audio = new(_ => { }, playModule: _ => { }, stopSounds: () => { });
        Stopwatch elapsed = Stopwatch.StartNew();
        long nextReport = 0;
        bool savedCycle = resumed is not null;
        bool cycleConfirmed = false;
        try
        {
            for (int callback = 0; callback < 500000 && battle.Outcome == BattleOutcome.Active; callback++)
            {
                if (!cycleConfirmed && expectedActions.All(queuedActions.Contains) && expectedActions.Where(action => action != 2).All(dispatchedActions.Contains) && completedEnemyActions >= expectedActions.Length && battle.ObserveDiagnosticDrainBlockers().Count == 0)
                {
                    cycleConfirmed = true;
                }
                if (cycleConfirmed && battle.NativeEntryReady && omega.IsAlive)
                {
                    if (!savedCycle && battle.ActiveAction is null)
                    {
                        SaveCheckpoint("omega-cycle.json.gz");
                        savedCycle = true;
                    }
                    foreach (BattleCombatant actor in battle.Combatants.Where(actor => actor.IsPlayer && actor.IsReady && !actor.IsBusy))
                    {
                        battle.QueueAttack(actor.Id, omega.Id);
                        issuedAttacks++;
                    }
                }
                battle.Tick();
                audio.Update(battle);
                foreach (BattleCombatant actor in battle.Combatants.Where(actor => actor.IsPlayer))
                {
                    Assert.Equal(initialPartyHp[actor.Id], actor.CurrentHp);
                }
                if (elapsed.ElapsedMilliseconds >= nextReport)
                {
                    Report("running");
                    nextReport = elapsed.ElapsedMilliseconds + 5000;
                }
                Assert.True(elapsed.Elapsed < TimeSpan.FromMinutes(15), "The bounded direct combat exceeded its elapsed-time budget.");
            }
            Assert.Equal(expectedActions, queuedActions.Order().ToArray());
            Assert.All(expectedActions.Where(action => action != 2), action => Assert.Contains(action, dispatchedActions));
            Assert.Equal(BattleOutcome.Victory, battle.Outcome);
            Assert.Equal(0, omega.CurrentHp);
            Assert.True(completedPartyActions > 0);
            Assert.DoesNotContain(battle.Events, entry => entry.Kind == "debug-victory");
            Assert.Contains(127, battle.NativeScriptState.UnlockedTutorials);
            Assert.Single(battle.EnemyDefeats);
            Assert.Empty(battle.ObserveDiagnosticDrainBlockers());
            SaveCheckpoint("omega-victory.json.gz");
            // This observed encounter uses an explicit diagnostic source, without claiming a field-script return.
            string formationHash = world.BattleResources.FormationSourceHash(462);
            BattleEncounterOccurrence occurrence = new(Guid.NewGuid().ToString("N"), 462, formationHash, SceneKind.Field, session.State.FieldId, 0, formationHash, null);
            session.SetPendingBattleEncounter(occurrence);
            BattleResult result = world.CaptureBattleOutcome(battle, occurrence);
            Assert.Equal(BattleRewardPolicy.NativeCombat, result.RewardPolicy);
            Assert.False(result.DebugVictory);
            world.ApplyBattleResult(occurrence, result);
            Assert.Equal(occurrence.OccurrenceId, Assert.Single(session.State.AppliedBattleResultIds));
            string committed = JsonSerializer.Serialize(session.State);
            Assert.Throws<InvalidOperationException>(() => world.ApplyBattleResult(occurrence, result));
            Assert.Equal(committed, JsonSerializer.Serialize(session.State));
            File.WriteAllText(Path.Combine(output, "omega-rewards.json"), JsonSerializer.Serialize(world.LastBattleRewards));
            Report("victory");
        }
        catch (Exception exception)
        {
            Report("failed", exception.Message);
            SaveCheckpoint("omega-boundary.json.gz");
            throw;
        }

        void SaveCheckpoint(string name)
        {
            using GZipStream stream = new(File.Create(Path.Combine(output, name)), CompressionLevel.Fastest);
            JsonSerializer.Serialize(stream, new CombatCheckpoint(battle.Capture(), initialEnemyHp, queuedActions.Order().ToArray(), dispatchedActions.Order().ToArray(), completedEnemyActions, completedPartyActions, issuedAttacks));
        }

        void Report(string status, string? failure = null)
        {
            File.WriteAllText(Path.Combine(output, "omega-progress.json"), JsonSerializer.Serialize(new
            {
                Status = status,
                Failure = failure,
                battle.Frame,
                battle.NativeHostCallbacks,
                Outcome = battle.Outcome.ToString(),
                InitialEnemyHp = initialEnemyHp,
                EnemyHp = omega.CurrentHp,
                DispatchedActions = dispatchedActions.Order().ToArray(),
                QueuedActions = queuedActions.Order().ToArray(),
                CompletedEnemyActions = completedEnemyActions,
                CompletedPartyActions = completedPartyActions,
                IssuedAttacks = issuedAttacks,
                ElapsedSeconds = elapsed.Elapsed.TotalSeconds,
                Blockers = battle.ObserveDiagnosticDrainBlockers()
            }));
        }
    }

    private sealed record CombatCheckpoint(BattleSnapshot Battle, int InitialEnemyHp, int[] QueuedActions, int[] DispatchedActions, int CompletedEnemyActions, int CompletedPartyActions, int IssuedAttacks);

    private static NativeCharacterVitals PrepareParty(GameAssets assets)
    {
        NativeCharacterVitals source = assets.ReadNewGameCharacterVitals();
        BattlePlayerStatReader reader = new(assets.Resources.Read(new ResourceKey("main/kernel.bin")));
        BattleNativePlayerStatRules rules = reader.ReadRules(source, 0);
        int spell = Enumerable.Range(1, rules.MagicRecords.Length - 1).OrderByDescending(id => rules.MagicRecords[id][24]).ThenBy(id => id).First();
        List<DiagnosticGuardianForcePreparation> guardians = [];
        foreach (int id in Enumerable.Range(0, source.GuardianForces.Length))
        {
            NativeCharacterVitals candidate = source.AcquireGuardianForce(id);
            if (candidate.GuardianForces[id].HasLearnedAbility(2))
            {
                guardians.Add(new(id, true));
            }
            else if (candidate.GuardianForceAbilities(id).SingleOrDefault(ability => ability.Ability == 2 && !ability.Learned) is { } ability)
            {
                guardians.Add(new(id, true) { LearningAbility = 2, AbilityPoints = checked((ushort)Math.Max(1, ability.RequiredPoints - ability.Progress)) });
            }
            if (guardians.Count == 3)
            {
                break;
            }
        }
        Assert.Equal(3, guardians.Count);
        NativeWeaponShopDefinitions weapons = assets.ReadNativeWeaponShopDefinitions();
        DiagnosticCharacterPreparation[] characters = Enumerable.Range(0, 3).Select(row => new DiagnosticCharacterPreparation(row, (byte)row, [new(spell, 100)], [guardians[row].Id])
        {
            Level = 100,
            Weapon = checked((byte)weapons.Recipes.Select((recipe, index) => (Recipe: recipe, Index: index)).Where(value => value.Recipe.Character == row).OrderByDescending(value => value.Recipe.KernelWords[8]).First().Index),
            MagicJunctions = [new(1, spell)]
        }).ToArray();
        DiagnosticBattlePreparation preparation = new(guardians.ToImmutableArray(), characters.ToImmutableArray(), false) { CombatParty = [0, 1, 2] };
        return GameWorld.PrepareDiagnosticCharacters(source, preparation, assets.ReadNativeBattleExperienceDefinition(), weapons).Vitals.Rest();
    }
}
