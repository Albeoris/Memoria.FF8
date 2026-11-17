using System.Reflection;
using System.Security.Cryptography;
using Memoria.FF8.Events;
using Memoria.FF8.Mods;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class HardcoreSaveContinuationTests
{
    [Fact]
    public void Configured_save_can_enable_hardcore_with_live_GF_publication_and_resume()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_HARDCORE_SAVE");
        Assert.SkipUnless(File.Exists(source), "Set MEMORIA_FF8_HARDCORE_SAVE to an external exploration save.");
        string directory = TestResources.GameDirectory;
        string temporary = Path.Combine(Path.GetTempPath(), "memoria-hardcore-continuation-" + Guid.NewGuid().ToString("N"));
        byte[] originalHash = SHA256.HashData(File.ReadAllBytes(source!));
        try
        {
            using FileStream original = File.OpenRead(source!);
            GameplaySaveComponentDocument document = GameplaySaveCodec.Read(original);
            using GameResourceHost resources = new(directory, watch: false, modsDirectory: Path.Combine(temporary, "Mods"));
            GameState state = GameplaySaveSessionLoader.Load(source!, directory, resources: resources.Resources, modelOverrides: resources, readGameplayPolicy: () => resources.GameplayPolicy, prepareCharacterDefinitions: initialized => GameplaySaveGuardianForceDefinitions.Select(initialized, resources));
            Assert.NotEqual(BattleDifficulty.Hardcore, state.GameplayPreferences.Difficulty);
            Assert.True(state.GameplayPreferences.SharedMagicStock);
            GameSession session = new(state);
            using RecreateGame game = new(new LaunchOptions { GameDirectory = directory, MuteAudio = true, AchievementsLocalOnly = true, SettingsDirectory = Path.Combine(temporary, "Settings") }, session);
            using GameWorld world = new(session, directory, resources: resources.Resources, modelOverrides: resources, readGameplayPolicy: () => resources.GameplayPolicy);
            world.EnsureField();
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            typeof(RecreateGame).GetField("resourceHost", flags)!.SetValue(game, resources);
            typeof(RecreateGame).GetField("world", flags)!.SetValue(game, world);
            MethodInfo prepare = typeof(RecreateGame).GetMethod("PrepareGuardianModPublication", flags)!;
            int publications = 0;
            resources.PrepareGuardianForcePublication = (mods, catalog) =>
            {
                publications++;
                return (ModPreparationResult)prepare.Invoke(game, [mods, catalog])!;
            };
            long frame = world.Events.Frame;
            Assert.True((bool)typeof(RecreateGame).GetMethod("ApplyHardcoreMenuAction", flags)!.Invoke(game, [new GameUi.MenuAction("config", "Difficulty", Value: "Hardcore")])!);
            Assert.True(publications > 0);
            Assert.Equal(BattleDifficulty.Hardcore, session.State.GameplayPreferences.Difficulty);
            Assert.Equal(frame, world.Events.Frame);
            Assert.Equal(state.EventRandom, session.State.EventRandom);
            Assert.Equal(state.AppliedBattleResultIds, session.State.AppliedBattleResultIds);
            Assert.Equal(state.Playtime, session.State.Playtime);
            string? configuredOutput = Environment.GetEnvironmentVariable("MEMORIA_FF8_HARDCORE_OUTPUT");
            string output = configuredOutput ?? Path.Combine(temporary, "activated.ff8save");
            Assert.False(File.Exists(output), "The continuation output must be a new file.");
            // A rules-only recovery preserves every original durable component, including dormant owners.
            GameplaySaveRules rules = document.Progression!.Rules;
            GameplaySaveComponentDocument activated = document with { Progression = document.Progression with { Rules = rules with { Preferences = (rules.Preferences with { Difficulty = BattleDifficulty.Hardcore }).NormalizeForDifficulty() } } };
            GameState saved = GameplaySaveSessionWriter.Write(output, activated, directory, resources: resources.Resources, modelOverrides: resources, readGameplayPolicy: () => resources.GameplayPolicy, prepareCharacterDefinitions: initialized => GameplaySaveGuardianForceDefinitions.Select(initialized, resources));
            Assert.Equal(BattleDifficulty.Hardcore, saved.GameplayPreferences.Difficulty);
            Assert.Equal(frame, saved.EventState!.Value.GetProperty("Frame").GetInt64());
            Assert.Throws<InvalidOperationException>(() => new GameSession(saved).SetGameplayPreferences(saved.GameplayPreferences with { Difficulty = BattleDifficulty.Normal }));
            GameSession restored = new(saved);
            using GameWorld resumed = new(restored, directory, resources: resources.Resources, modelOverrides: resources, readGameplayPolicy: () => resources.GameplayPolicy);
            resumed.EnsureField();
            Assert.True(resumed.Apply(new(PlayerActionKind.Wait, Frames: 1)).Accepted);
            Assert.Null(resumed.Events.LastFault);
            Assert.Equal(BattleDifficulty.Hardcore, restored.State.GameplayPreferences.Difficulty);
            Assert.Equal(originalHash, SHA256.HashData(File.ReadAllBytes(source!)));
            Console.WriteLine($"Hardcore continuation: field {saved.FieldId}, frame {frame}; resumed frame {resumed.Events.Frame}.");
        }
        finally
        {
            if (Directory.Exists(temporary))
            {
                Directory.Delete(temporary, recursive: true);
            }
        }
    }
}
