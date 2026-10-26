using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AppDiagnosticEjectRepeatTests
{
    [Fact]
    public void Original_Gesper_Degenerator_repeats_by_ejecting_two_genuine_party_owners_without_repair()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? recipePath = Environment.GetEnvironmentVariable("MEMORIA_FF8_EJECT_REPEAT_RECIPE");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory) && recipePath is not null && File.Exists(recipePath), "Select installed resources and the original Gesper Degenerator recipe.");
        JsonSerializerOptions json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, PropertyNameCaseInsensitive = true, WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
        byte[] originalBytes = File.ReadAllBytes(recipePath!);
        string originalHash = Convert.ToHexString(SHA256.HashData(originalBytes));
        DiagnosticBattleRequest original = JsonSerializer.Deserialize<DiagnosticBattleRequest>(originalBytes, json)!;
        DiagnosticBattleDriverRecipe source = Assert.IsType<DiagnosticBattleDriverRecipe>(original.Driver);
        BattleDiagnosticEnemyAbility enemy = Assert.IsType<BattleDiagnosticEnemyAbility>(source.EnemyAbility);
        Assert.Equal(226, original.Encounter);
        Assert.Equal((ushort)174, source.ExpectedEffect);
        Assert.Equal((byte)8, source.ExpectedNativeCommand);
        Assert.Equal(72, source.ExpectedNativeAction);
        Assert.Equal(new BattleDiagnosticEnemyAbility(22, 0, 1, 1, 8, 72, 13, 201), enemy);
        Assert.Equal("enemy-0", source.Input.ActorId);
        Assert.Equal("party-0", source.Input.TargetId);
        using GameAssets assets = new(directory!);
        GameSession session = GameWorld.CreateColdProcess(directory!);
        session.StartNewGame();
        using GameWorld world = new(session, directory!);
        NativeCharacterVitals native = Assert.IsType<NativeCharacterVitals>(session.State.CharacterVitals);
        Assert.Equal((byte)0, native.Records[0].CharacterId);
        Assert.Equal((byte)1, native.Records[1].CharacterId);
        Assert.Equal((byte)3, native.Records[3].CharacterId);
        DiagnosticBattlePreparation originalFixture = Assert.IsType<DiagnosticBattlePreparation>(original.Fixture);
        DiagnosticCharacterPreparation squall = Assert.Single(originalFixture.Characters);
        Assert.Equal(0, squall.Row);
        Assert.Equal((ushort)1213, squall.HitPoints);
        Assert.Equal(24, squall.Level);
        DiagnosticBattlePreparation fixture = originalFixture with
        {
            CombatParty = [0, 1, 3],
            Characters = [squall with { HitPoints = null, Level = 24 }, new(1, 1, [], []) { Level = 24 }, new(3, 3, [], []) { Level = 24 }]
        };
        DiagnosticPreparationResult leveled = GameWorld.PrepareDiagnosticCharacters(native, fixture, assets.ReadNativeBattleExperienceDefinition());
        ushort row1Maximum = leveled.Vitals.GetMagicMaximumHitPoints(1);
        ushort row3Maximum = leveled.Vitals.GetMagicMaximumHitPoints(3);
        Assert.True(leveled.Vitals.GetMagicMaximumHitPoints(0) >= 1213);
        Assert.True(row1Maximum > 0 && row3Maximum > 0);
        fixture = fixture with { Characters = [squall, new(1, 1, [], []) { Level = 24, HitPoints = row1Maximum }, new(3, 3, [], []) { Level = 24, HitPoints = row3Maximum }] };
        DiagnosticBattleRequest request = original with { Fixture = fixture, Driver = source with { Repairs = [], TerminalHold = null } };
        request.Validate();
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_EJECT_REPEAT_EVIDENCE");
        if (!string.IsNullOrWhiteSpace(evidence))
        {
            Assert.False(Directory.Exists(evidence) || File.Exists(evidence), "Eject repeat evidence must use a new location.");
            Directory.CreateDirectory(evidence);
        }
        BattleSession battle = world.StartDiagnosticBattle(request);
        Guid lifecycle = battle.DiagnosticLifecycleId;
        BattleCombatant caster = Assert.Single(battle.Combatants, actor => actor.Id == "enemy-0");
        List<BattleDiagnosticEffectDispatch> focal = [];
        List<BattleEvent> completions = [];
        List<BattleDiagnosticResolution> resolutions = [];
        List<object> casts = [];
        HashSet<string> removed = new(StringComparer.Ordinal);
        battle.DiagnosticResolution += resolutions.Add;
        battle.DiagnosticEffectDispatched += dispatch =>
        {
            if (dispatch.ActorId == "enemy-0" && dispatch.RequestedEffect == 174)
            {
                focal.Add(dispatch);
            }
        };
        battle.DiagnosticEventPublished += entry =>
        {
            if (entry.Kind == "ActionComplete" && entry.ActorId == "enemy-0")
            {
                completions.Add(entry);
            }
        };
        BattleAudioPresentation audio = new(_ => { }, playModule: _ => { }, stopSounds: () => { });
        int ticks = 0;
        bool completed = false;
        try
        {
            if (request.PrepareEntryBeforePresentation)
            {
                world.PrepareDiagnosticEntryBeforePresentation(request);
            }
            WaitUntil(() => battle.NativeEntryReady && battle.ObserveDiagnosticDrainBlockers().Count == 0 && battle.CanSubmitDiagnosticEnemyAbility("enemy-0", enemy));
            BattleCombatant[] party = battle.Combatants.Where(actor => actor.IsPlayer).ToArray();
            Assert.Equal(new[] { "party-0", "party-1", "party-3" }, party.Select(actor => actor.Id));
            Dictionary<string, BattleDiagnosticCombatant> initialStates = battle.ObserveDiagnosticCombatants().ToDictionary(actor => actor.Id);
            Assert.Equal(new int?[] { 0, 1, 2 }, party.Select(actor => initialStates[actor.Id].NativeIndex));
            Assert.All(party, actor => Assert.True(actor.IsActive && actor.CurrentHp > 0 && actor.Level == 24));
            Assert.Equal(1213, party[0].CurrentHp);
            Assert.Equal(row1Maximum, party[1].CurrentHp);
            Assert.Equal(row3Maximum, party[2].CurrentHp);
            Assert.Equal(22, caster.ModelId);
            Assert.Equal(0, caster.SceneSlot);
            Assert.Equal(3, initialStates[caster.Id].NativeIndex);
            int casterHitPoints = caster.CurrentHp;
            int casterLevel = caster.Level;
            for (int cast = 1; cast <= 2; cast++)
            {
                WaitUntil(() => battle.CanSubmitDiagnosticEnemyAbility("enemy-0", enemy) && battle.ObserveDiagnosticDrainBlockers().Count == 0);
                BattleDiagnosticCombatant[] before = battle.ObserveDiagnosticCombatants();
                Dictionary<string, BattleDiagnosticCombatant> beforeStates = before.ToDictionary(actor => actor.Id);
                string[] activeBefore = party.Where(actor => actor.IsActive).Select(actor => actor.Id).ToArray();
                battle.SubmitDiagnosticEnemyAbility("enemy-0", enemy, 174);
                BattleDiagnosticEnemySubmission submission = Assert.IsType<BattleDiagnosticEnemySubmission>(battle.LastDiagnosticEnemySubmission);
                Assert.Equal(lifecycle, submission.LifecycleId);
                Assert.Equal("enemy-0", submission.ActorId);
                Assert.Equal(3, submission.ActorIndex);
                Assert.Equal(enemy, submission.Selection);
                ushort mask = Assert.IsType<ushort>(submission.TargetMask);
                Assert.True(mask is 1 or 2 or 4);
                BattleCombatant victim = Assert.Single(party, actor => beforeStates[actor.Id].NativeIndex is int index && index is >= 0 and < 3 && (mask & (1 << index)) != 0);
                Assert.Contains(victim.Id, activeBefore);
                WaitUntil(() => focal.Count >= cast && completions.Count >= cast && battle.ActiveAction is null && battle.QueuedActions.Count == 0 && battle.ObserveDiagnosticDrainBlockers().Count == 0);
                BattleDiagnosticEffectDispatch dispatch = focal[cast - 1];
                BattleDiagnosticCombatant[] after = battle.ObserveDiagnosticCombatants();
                Dictionary<string, BattleDiagnosticCombatant> afterStates = after.ToDictionary(actor => actor.Id);
                Assert.Equal((byte)8, dispatch.NativeCommand);
                Assert.Equal(72, dispatch.ActionId);
                Assert.Equal((ushort)174, dispatch.ExecutedEffect);
                Assert.Equal(submission.TargetMask, dispatch.NativeTargetMask);
                Assert.False(victim.IsActive);
                Assert.Equal(0, victim.CurrentHp);
                Assert.Equal(1, afterStates[victim.Id].Statuses0 & 1);
                Assert.True(removed.Add(victim.Id), "The second cast must eject another preexisting owner.");
                Assert.Equal(3 - cast, party.Count(actor => actor.IsActive));
                Assert.All(party.Where(actor => actor.IsActive), actor => Assert.True(actor.CurrentHp > 0 && (afterStates[actor.Id].Statuses0 & 5) == 0));
                Assert.Same(caster, Assert.Single(battle.Combatants, actor => actor.Id == "enemy-0"));
                Assert.True(caster.IsActive);
                Assert.Equal(casterHitPoints, caster.CurrentHp);
                Assert.Equal(casterLevel, caster.Level);
                Assert.Equal(22, caster.ModelId);
                Assert.Equal(0, caster.SceneSlot);
                Assert.Equal(3, afterStates[caster.Id].NativeIndex);
                Assert.Equal(BattleOutcome.Active, battle.Outcome);
                Assert.Null(battle.DiagnosticTerminalHold);
                Assert.True(battle.NativeGraphics!.NativeOutcome?.ResultByte6E7 is null or 0);
                casts.Add(new { cast, submission, dispatch, completion = completions[cast - 1], victim = victim.Id, activeBefore, activeAfter = party.Where(actor => actor.IsActive).Select(actor => actor.Id).ToArray(), before, after });
            }
            Assert.Equal(2, focal.Count);
            Assert.Equal(2, removed.Count);
            Assert.Equal(lifecycle, battle.DiagnosticLifecycleId);
            Assert.Equal(0, battle.DiagnosticRestoreCount);
            Assert.Equal(0, battle.DiagnosticResetCount);
            BattleSnapshot snapshot = JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!;
            BattleSession restored = BattleSession.Restore(snapshot);
            restored.AttachNativeEffectResourceResolver(effect => assets.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
            long callback = restored.NativeHostCallbacks!.Value;
            restored.Tick();
            Assert.True(restored.NativeHostCallbacks > callback);
            Assert.Equal(BattleOutcome.Active, restored.Outcome);
            Assert.Equal(JsonSerializer.Serialize(battle.ObserveDiagnosticCombatants()), JsonSerializer.Serialize(restored.ObserveDiagnosticCombatants()));
            Assert.Equal(1, restored.Combatants.Count(actor => actor.IsPlayer && actor.IsActive));
            Assert.Equal(originalHash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(recipePath!))));
            completed = true;
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(evidence))
            {
                WriteNew(Path.Combine(evidence, "eject-repeat-174.json"), JsonSerializer.Serialize(new { originalRecipe = Path.GetFullPath(recipePath!), originalSha256 = originalHash, completed, request, row1Maximum, row3Maximum, lifecycle, ticks, preparation = world.DiagnosticReceipt, casts, resolutions, final = battle.ObserveDiagnosticCombatants() }, json));
                if (completed)
                {
                    WriteNew(Path.Combine(evidence, "eject-repeat-174.recipe.json"), JsonSerializer.Serialize(request, json));
                }
            }
        }

        void WaitUntil(Func<bool> condition)
        {
            while (!condition() && ticks < 4096)
            {
                battle.Tick();
                audio.Update(battle);
                ticks++;
            }
            Assert.True(condition(), $"Eject repeat did not reach its native boundary: {JsonSerializer.Serialize(battle.ObserveDiagnosticInvocationOwners(), json)}");
        }
    }

    private static void WriteNew(string path, string value)
    {
        using FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        using StreamWriter writer = new(stream);
        writer.Write(value);
    }
}
