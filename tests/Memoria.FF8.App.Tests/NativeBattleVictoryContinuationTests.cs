using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleVictoryContinuationTests
{
    [Fact]
    public void Named_standalone_migrates_proved_ordinary_attack_owners_and_reaches_victory_through_ordinary_attacks()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_CAVERN_ATTACK_SNAPSHOT");
        Assert.SkipUnless(source is not null && File.Exists(source), "A named active standalone battle component is required.");
        string evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_PRODUCTION_EFFECT_EVIDENCE") ?? TestResources.ArtifactDirectory;
        Directory.CreateDirectory(evidence);
        string prefix = "native-victory-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff");
        byte[] originalBytes = File.ReadAllBytes(source!);
        JsonNode original = JsonNode.Parse(originalBytes)!;
        JsonNode migrated = original.DeepClone();
        JsonNode definition = migrated["Battle"]!["Definition"]!;
        JsonNode rules = definition["NativeGraphics"]!["Rules"]!;
        Assert.Null(definition["NativeVictoryFile"]);
        Assert.Null(rules["VictoryModelSections"]);
        // The declared ordinary-Attack component completed500870 and never loaded/invoked an original effect module.
        Assert.Equal(4, migrated["Battle"]!["NativeGraphics"]!["EntryLifecycle"]!.GetValue<int>());
        Assert.Null(migrated["Battle"]!["NativeGraphics"]!["OriginalEffect"]);
        foreach (JsonNode memory in new[] { migrated["Battle"]!["NativeEffectMemory"]!, migrated["NativeBattleMemory"]!["EffectMemory"]! })
        {
            Assert.Equal(0, memory["Active"]!.GetValue<int>());
            Assert.False(memory.AsObject().ContainsKey("ReturnedTaskPointer1D96AAC"));
            Assert.Null(memory["Entry21DFEC0"]);
            Assert.Null(memory["Entry21DFEC4"]);
            Assert.Null(memory["Entry21DFEC8"]);
            memory["ReturnedTaskPointer1D96AAC"] = 0;
        }
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        NativeBattleArchiveFile file = assets.ReadNativeBattleFile(766);
        NativeBattleGraphicsRules authenticated = game.ReadNativeBattleGraphicsRules();
        definition["NativeVictoryFile"] = JsonSerializer.SerializeToNode(file);
        rules["VictoryModelSections"] = JsonSerializer.SerializeToNode(authenticated.VictoryModelSections);
        JsonNode ui = migrated["Battle"]!["NativeGraphics"]!["UiServices"]!;
        Assert.Null(ui["Windows"]);
        // Explicitly execute the missing4A94D0 window reset; this is a declared migration, not reconstruction of prior callbacks.
        ui["Windows"] = JsonSerializer.SerializeToNode(NativeBattleUiWindows.Initialize4A94D0());
        JsonNode stripped = migrated.DeepClone();
        stripped["Battle"]!["Definition"]!.AsObject().Remove("NativeVictoryFile");
        stripped["Battle"]!["Definition"]!["NativeGraphics"]!["Rules"]!.AsObject().Remove("VictoryModelSections");
        stripped["Battle"]!["NativeEffectMemory"]!.AsObject().Remove("ReturnedTaskPointer1D96AAC");
        stripped["NativeBattleMemory"]!["EffectMemory"]!.AsObject().Remove("ReturnedTaskPointer1D96AAC");
        stripped["Battle"]!["NativeGraphics"]!["UiServices"]!.AsObject().Remove("Windows");
        Assert.True(JsonNode.DeepEquals(original, stripped));
        string migratedPath = Path.Combine(evidence, prefix + "-resources-migrated.json");
        File.WriteAllText(migratedPath, migrated.ToJsonString());
        NativeBattleProductionEffectAdmissionTests.Publish(evidence, prefix + "-migration", new
        {
            Source = Path.GetFullPath(source!),
            SourceSha256 = Convert.ToHexString(SHA256.HashData(originalBytes)),
            Migrated = migratedPath,
            MigratedSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(migratedPath))),
            ChangedPaths = new[] { "Battle.Definition.NativeVictoryFile", "Battle.Definition.NativeGraphics.Rules.VictoryModelSections", "Battle.NativeEffectMemory.ReturnedTaskPointer1D96AAC", "NativeBattleMemory.EffectMemory.ReturnedTaskPointer1D96AAC", "Battle.NativeGraphics.UiServices.Windows" },
            WindowReset = "Explicit4A94D0 reset and registration; all other UI bytes, flags, queue and callback counters retained. Unknown DWORD4/BYTE19 remain unknown.",
            TaskPointerProof = "Declared ordinary-Attack component: completed500870 clear, inactive effect allocator and no loaded/invoked original module entry; the active and retained physical mirrors therefore both retain NULL.",
            FileId = file.FileId,
            FileSha256 = Convert.ToHexString(SHA256.HashData(file.Bytes.AsSpan())),
            ExecutableSha256 = authenticated.ExecutableSha256
        });
        GameState state = GameSession.Load(migratedPath);
        GameSession session = new(state);
        BattleSession battle = BattleSession.Restore(state.Battle!);
        BattleSession independent = BattleSession.Restore(state.Battle!);
        battle.Tick();
        independent.Tick();
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(independent.Capture()));
        string? frozenHp = null;
        bool sawAdmission = false;
        bool sawVictoryTask = false;
        try
        {
            for (int callback = 0; callback < 2000 && battle.Outcome == BattleOutcome.Active; callback++)
            {
                if (battle.NativeGraphics?.NativeOutcome?.ResultByte6E7 != 4 && battle.ActiveAction is null && battle.QueuedActions.Count == 0)
                {
                    BattleCombatant? actor = battle.Combatants.FirstOrDefault(actor => actor.IsPlayer && actor.IsReady && !actor.IsBusy && actor.IsAlive);
                    BattleCombatant? target = battle.Combatants.FirstOrDefault(actor => !actor.IsPlayer && actor.IsActive && actor.IsAlive);
                    if (actor is not null && target is not null)
                    {
                        battle.QueueAttack(actor.Id, target.Id);
                    }
                }
                battle.Tick();
                sawVictoryTask |= battle.NativeGraphics!.Descriptors.Tasks.Any(task => (task.Flags & 1) != 0 && task.Kind == NativeBattleDescriptorTaskKind.Victory503040);
                if (battle.NativeGraphics.NativeOutcome?.ResultByte6E7 == 4)
                {
                    string hp = JsonSerializer.Serialize(battle.Combatants.Select(actor => new { actor.Id, actor.CurrentHp }));
                    if (!sawAdmission)
                    {
                        sawAdmission = true;
                        frozenHp = hp;
                        session.SetLiveBattle(battle);
                        NativeBattleProductionEffectAdmissionTests.Publish(evidence, prefix + "-admitted", session.State);
                        independent = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
                        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(independent.Capture()));
                    }
                    Assert.Equal(frozenHp, hp);
                }
            }
            Assert.True(sawAdmission);
            Assert.True(sawVictoryTask);
            Assert.Equal(BattleOutcome.Victory, battle.Outcome);
            Assert.Equal((byte?)0, battle.NativeGraphics!.NativeOutcome!.FadeDurationB0C);
            Assert.Single(battle.Events, item => item.Kind == "Outcome");
            session.SetLiveBattle(battle);
            NativeBattleProductionEffectAdmissionTests.Publish(evidence, prefix + "-after", session.State);
            string after = JsonSerializer.Serialize(battle.Capture());
            independent = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(after)!);
            Assert.Equal(after, JsonSerializer.Serialize(independent.Capture()));
            Assert.Equal(battle.NativeResult, independent.NativeResult);
        }
        catch (Exception exception)
        {
            session.SetLiveBattle(battle);
            NativeBattleProductionEffectAdmissionTests.Publish(evidence, prefix + "-failure-state", session.State);
            NativeBattleProductionEffectAdmissionTests.Publish(evidence, prefix + "-failure", new { Error = exception.ToString(), battle.Frame, Callback = battle.NativeGraphics?.Callback });
            throw;
        }
    }
}
