using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class DiagnosticPetrifyRepeatTests
{
    [Fact]
    public void Original_Petrify_Stare_repeats_with_a_genuine_same_level_companion_and_original_random_party_selector()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? recipePath = Environment.GetEnvironmentVariable("MEMORIA_FF8_TERMINAL_HOLD_RECIPE");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory) && recipePath is not null && File.Exists(recipePath), "Select installed resources and the original Petrify Stare recipe.");
        JsonSerializerOptions json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, PropertyNameCaseInsensitive = true, WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
        DiagnosticBattleRequest original = JsonSerializer.Deserialize<DiagnosticBattleRequest>(File.ReadAllText(recipePath!), json)!;
        DiagnosticBattleDriverRecipe source = Assert.IsType<DiagnosticBattleDriverRecipe>(original.Driver);
        Assert.SkipUnless(source.ExpectedEffect == 120, "The selected recipe must be the original Petrify Stare case.");
        BattleDiagnosticEnemyAbility enemy = Assert.IsType<BattleDiagnosticEnemyAbility>(source.EnemyAbility);
        Assert.Equal(6, enemy.Model);
        Assert.Equal(1, enemy.LevelBand);
        Assert.Equal((byte)201, enemy.TargetSelector);
        using GameAssets assets = new(directory!);
        GameSession session = GameWorld.CreateColdProcess(directory!);
        session.StartNewGame();
        using GameWorld world = new(session, directory!);
        NativeCharacterVitals native = Assert.IsType<NativeCharacterVitals>(session.State.CharacterVitals);
        Assert.Equal((byte)0, native.Records[0].CharacterId);
        Assert.Equal((byte)3, native.Records[3].CharacterId);
        DiagnosticBattlePreparation originalFixture = Assert.IsType<DiagnosticBattlePreparation>(original.Fixture);
        DiagnosticCharacterPreparation squall = Assert.Single(originalFixture.Characters, character => character.Row == 0);
        Assert.Equal((byte)0, squall.CharacterId);
        Assert.Equal(24, squall.Level);
        Assert.NotNull(squall.HitPoints);
        Assert.True(originalFixture.Characters.All(character => character.Row == 0));
        DiagnosticBattlePreparation fixture = originalFixture with
        {
            CombatParty = [0, 3, 255],
            Characters = [squall with { HitPoints = null }, new(3, 3, [], []) { Level = 24 }]
        };
        DiagnosticPreparationResult leveled = GameWorld.PrepareDiagnosticCharacters(native, fixture, assets.ReadNativeBattleExperienceDefinition());
        ushort squallMaximumHitPoints = leveled.Vitals.GetMagicMaximumHitPoints(0);
        ushort companionHitPoints = leveled.Vitals.GetMagicMaximumHitPoints(3);
        Assert.True(squall.HitPoints.Value <= squallMaximumHitPoints, $"Original Squall HP{squall.HitPoints.Value} exceeds actual prepared row0 maximum{squallMaximumHitPoints}.");
        Assert.True(companionHitPoints > 0);
        fixture = fixture with { Characters = [squall, new(3, 3, [], []) { Level = 24, HitPoints = companionHitPoints }] };
        List<BattleDiagnosticEffectDispatch> focal = [];
        List<BattleEvent> completions = [];
        List<object> casts = [];
        List<object> events = [];
        List<BattleDiagnosticResolution> resolutions = [];
        BattleDiagnosticCombatRepair[] repairs = [new("party-0", squall.HitPoints.Value, RemoveStatuses0: 4), new("party-3", companionHitPoints, RemoveStatuses0: 4)];
        BattleDiagnosticRepairReceipt? repairReceipt = null;
        DiagnosticBattleRequest request = original with { Fixture = fixture, Driver = source with { TerminalHold = null, Repairs = repairs } };
        request.Validate();
        BattleSession battle = world.StartDiagnosticBattle(request);
        battle.DiagnosticResolution += resolutions.Add;
        BattleAudioPresentation audio = new(_ => { }, playModule: _ => { }, stopSounds: () => { });
        Guid lifecycle = battle.DiagnosticLifecycleId;
        battle.DiagnosticEffectDispatched += dispatch =>
        {
            if (dispatch.ActorId == source.Input.ActorId && dispatch.RequestedEffect == 120)
            {
                focal.Add(dispatch);
            }
        };
        battle.DiagnosticEventPublished += entry =>
        {
            if (events.Count < 4096)
            {
                events.Add(new { entry.Frame, entry.Kind, entry.ActorId, entry.TargetId, entry.Amount, entry.Message });
            }
            if (entry.Kind == "ActionComplete" && entry.ActorId == source.Input.ActorId)
            {
                completions.Add(entry);
            }
        };
        int ticks = 0;
        int petrifiedCasts = 0;
        int observingCast = 0;
        ushort? observedTargetMask = null;
        try
        {
            if (request.PrepareEntryBeforePresentation)
            {
                world.PrepareDiagnosticEntryBeforePresentation(request);
            }
            WaitUntil(() => battle.NativeEntryReady && battle.ObserveDiagnosticDrainBlockers().Count == 0 && battle.CanSubmitDiagnosticEnemyAbility(source.Input.ActorId!, enemy));
            BattleDiagnosticCombatant[] initialParty = battle.ObserveDiagnosticCombatants().Where(actor => actor.NativeIndex is >= 0 and < 3).ToArray();
            Assert.Equal(new[] { "party-0", "party-3" }, initialParty.Select(actor => actor.Id));
            Assert.All(battle.Combatants.Where(actor => actor.IsPlayer), actor => Assert.Equal(24, actor.Level));
            Assert.All(initialParty, actor => Assert.True(actor.CurrentHp > 0 && (actor.Statuses0 & 5) == 0));
            Assert.All(repairs, repair => Assert.Equal(repair.HitPoints, initialParty.Single(actor => actor.Id == repair.ActorId).CurrentHp));
            for (int cast = 1; cast <= 2; cast++)
            {
                WaitUntil(() => battle.CanSubmitDiagnosticEnemyAbility(source.Input.ActorId!, enemy) && battle.ObserveDiagnosticDrainBlockers().Count == 0);
                BattleDiagnosticCombatant[] before = battle.ObserveDiagnosticCombatants();
                observingCast = cast;
                observedTargetMask = null;
                battle.SubmitDiagnosticEnemyAbility(source.Input.ActorId!, enemy, 120);
                WaitUntil(() => focal.Count >= cast && completions.Count >= cast && battle.ActiveAction is null && battle.QueuedActions.Count == 0 && battle.ObserveDiagnosticDrainBlockers().Count == 0);
                ushort targetMask = observedTargetMask ?? throw new InvalidDataException("The focal native target registration was not observed.");
                Assert.True(targetMask is 1 or 2, "The original selector must resolve one of the two actual party allocations.");
                BattleDiagnosticCombatant[] after = battle.ObserveDiagnosticCombatants();
                BattleDiagnosticCombatant recipient = Assert.Single(after, actor => actor.NativeIndex is >= 0 and < 3 && (targetMask & (1 << actor.NativeIndex.Value)) != 0);
                casts.Add(new { cast, nativeTargetMask = targetMask, actualRecipient = recipient.Id, before, after, dispatch = focal[cast - 1], completion = completions[cast - 1], callback = battle.NativeHostCallbacks });
                // The original status roll may be resisted; the focal effect still executes.
                Assert.Equal(before.Single(actor => actor.Id == recipient.Id).Statuses0, recipient.Statuses0 & ~4);
                if ((recipient.Statuses0 & 4) != 0)
                {
                    petrifiedCasts++;
                }
                Assert.Contains(after, actor => actor.NativeIndex is >= 0 and < 3 && (actor.Statuses0 & 5) == 0);
                Assert.All(initialParty, initial => Assert.Equal(initial.CurrentHp, after.Single(actor => actor.Id == initial.Id).CurrentHp));
                Assert.Equal(BattleOutcome.Active, battle.Outcome);
                Assert.Null(battle.DiagnosticTerminalHold);
                Assert.True(battle.NativeGraphics!.NativeOutcome?.ResultByte6E7 is null or 0);
                if (cast == 1)
                {
                    repairReceipt = battle.ApplyDiagnosticCombatRepairs(repairs, battle.NativeHostCallbacks!.Value);
                    Assert.All(battle.ObserveDiagnosticCombatants().Where(actor => actor.NativeIndex is >= 0 and < 3), actor => Assert.Equal(0, actor.Statuses0 & 5));
                }
            }
            Assert.Equal(2, focal.Count);
            Assert.InRange(petrifiedCasts, 1, 2);
            Assert.All(focal, dispatch => Assert.Equal((ushort)120, dispatch.ExecutedEffect));
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
        }
        finally
        {
            string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_PETRIFY_REPEAT_EVIDENCE");
            if (!string.IsNullOrWhiteSpace(evidence))
            {
                Directory.CreateDirectory(evidence);
                File.WriteAllText(Path.Combine(evidence, "petrify-repeat-120.json"), JsonSerializer.Serialize(new { original, request, companionHitPoints, preparation = world.DiagnosticReceipt, casts, repairs, repairReceipt, focal, lifecycle, ticks, events, resolutions, final = battle.ObserveDiagnosticCombatants() }, json));
            }
        }

        void WaitUntil(Func<bool> condition)
        {
            while (!condition() && ticks < 4096)
            {
                battle.Tick();
                audio.Update(battle);
                if (observingCast > 0 && observedTargetMask is null && focal.Count >= observingCast && battle.NativeGraphics?.OriginalEffect is { RequestedEffect: 120, Parameters: { } parameters })
                {
                    observedTargetMask = parameters.TargetMask7A;
                }
                ticks++;
            }
            Assert.True(condition(), $"Petrify repeat did not reach its normal native boundary. Owners: {JsonSerializer.Serialize(battle.ObserveDiagnosticInvocationOwners(), json)}");
        }
    }
}
