using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class OriginalSaveBankIntegrationTests
{
    [Fact]
    public void Menu_copy_initializes_original_field_continuation_and_exports_back_without_changing_sources()
    {
        string? fixture = Environment.GetEnvironmentVariable("MEMORIA_FF8_PC_SAVE_FIXTURE");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(fixture) && File.Exists(fixture), "Set MEMORIA_FF8_PC_SAVE_FIXTURE to an original PC save at a supported continuation.");
        string gameDirectory = TestResources.GameDirectory;
        string directory = Path.Combine(Path.GetTempPath(), "Memoria.FF8-save-bank-integration-" + Guid.NewGuid().ToString("N"));
        string originals = Directory.CreateDirectory(Path.Combine(directory, "originals")).FullName;
        byte[] originalSource = File.ReadAllBytes(fixture!);
        try
        {
            using GameAssets assets = new(gameDirectory);
            PlayerSaveStore store = new(Path.Combine(directory, "checkpoint.json"), Path.Combine(directory, "memoria"), originalDirectory: originals, loadOriginal: path => OriginalPlayerSaveLoader.Load(path, gameDirectory, assets));
            File.WriteAllBytes(store.GetPath(101), originalSource);
            PlayerSaveMenu menu = new(store);
            menu.Open(PlayerSaveMenuMode.Load);
            Assert.True(menu.SelectBank(1));
            menu.SelectSlot(101);
            menu.Update(Card(), UnexpectedAction, UnexpectedAction);
            Assert.Equal(101, menu.TransferSource);
            Assert.Equal(0, menu.Bank);
            menu.SelectSlot(100);
            menu.Update(Confirm(), UnexpectedAction, UnexpectedAction, Transfer);
            Assert.False(menu.IsTransferring);
            Assert.True(File.Exists(store.GetPath(100)), menu.Message);
            byte[] memoriaSource = File.ReadAllBytes(store.GetPath(100));
            GameState imported = store.Load(100);
            Assert.NotNull(imported.OriginalSave);
            if (imported.Scene == SceneKind.Field)
            {
                Assert.NotNull(imported.EventState);
            }

            Assert.True(menu.SelectBank(0));
            menu.SelectSlot(100);
            menu.Update(Card(), UnexpectedAction, UnexpectedAction);
            Assert.Equal(100, menu.TransferSource);
            Assert.True(menu.SelectBank(2));
            menu.SelectSlot(160);
            menu.Update(Confirm(), UnexpectedAction, UnexpectedAction, Transfer);
            Assert.False(menu.IsTransferring);
            Assert.True(File.Exists(store.GetPath(160)), menu.Message);
            GameState exported = store.Load(160);
            Assert.Equal(imported.Scene, exported.Scene);
            Assert.Equal(imported.FieldId, exported.FieldId);
            Assert.Equal(imported.Playtime!.Seconds, exported.Playtime!.Seconds);
            Assert.Equal(imported.Economy!.Gil, exported.Economy!.Gil);
            Assert.Equal(imported.CharacterVitals!.CombatParty, exported.CharacterVitals!.CombatParty);

            GameSession resumed = new(imported);
            using GameWorld continuation = new(resumed, gameDirectory, sharedAssets: assets);
            continuation.EnsureField();
            long frame = imported.Scene == SceneKind.WorldMap ? resumed.State.World!.Frame : continuation.Events.Frame;
            PlayerActionResult result = continuation.Apply(new PlayerAction(PlayerActionKind.Wait));
            Assert.True(result.Accepted, result.Detail);
            Assert.Equal(1, result.FramesAdvanced);
            Assert.Equal(frame + 1, imported.Scene == SceneKind.WorldMap ? resumed.State.World!.Frame : continuation.Events.Frame);
            Assert.Null(continuation.Events.LastFault);
            Assert.Equal(originalSource, File.ReadAllBytes(store.GetPath(101)));
            Assert.Equal(memoriaSource, File.ReadAllBytes(store.GetPath(100)));
            Assert.Equal(originalSource, File.ReadAllBytes(fixture!));

            void Transfer(int source, int destination)
            {
                store.Transfer(source, destination, candidate =>
                {
                    GameSession validation = new(candidate);
                    using MovieClock clock = new(validation, gameDirectory);
                    using GameWorld world = new(validation, gameDirectory, clock, sharedAssets: assets);
                    world.EnsureField();
                });
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Memoria_fixture_exports_native_progress_and_continues_without_modifying_source()
    {
        string? fixture = Environment.GetEnvironmentVariable("MEMORIA_FF8_MEMORIA_SAVE_FIXTURE");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(fixture) && File.Exists(fixture), "Set MEMORIA_FF8_MEMORIA_SAVE_FIXTURE to test a Memoria save conversion.");
        string gameDirectory = TestResources.GameDirectory;
        string directory = Path.Combine(Path.GetTempPath(), "Memoria.FF8-memoria-export-" + Guid.NewGuid().ToString("N"));
        byte[] originalSource = File.ReadAllBytes(fixture!);
        try
        {
            GameState source = GameSession.Load(fixture!);
            using GameAssets assets = new(gameDirectory);
            NativeCharacterVitals target = assets.ReadNewGameCharacterVitals();
            string originals = Path.Combine(directory, "originals");
            Directory.CreateDirectory(originals);
            PlayerSaveStore store = new(Path.Combine(directory, "checkpoint.json"), Path.Combine(directory, "memoria"), originalDirectory: originals, loadOriginal: path => OriginalPlayerSaveLoader.Load(path, gameDirectory, assets));
            store.Save(1, new GameSession(source));
            store.Transfer(1, 101, targetSource: target, allowGuardianForceLoss: true);
            GameState imported = store.Load(101);
            Assert.Equal(source.Scene, imported.Scene);
            Assert.Equal(source.Economy!.Gil, imported.Economy!.Gil);
            Assert.Equal(source.Playtime!.Seconds, imported.Playtime!.Seconds);
            Assert.Equal(source.CharacterVitals!.CombatParty, imported.CharacterVitals!.CombatParty);
            Assert.False(imported.CharacterVitals.MagicSystem.RequiresMemoriaFormat);
            Assert.Equal(BattleDifficulty.Normal, imported.GameplayPreferences.Difficulty);
            GameSession resumed = new(imported);
            using MovieClock clock = new(resumed, gameDirectory);
            using GameWorld world = new(resumed, gameDirectory, clock, sharedAssets: assets);
            world.EnsureField();
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 1)).Accepted);
            Assert.Null(world.Events.LastFault);
            Assert.Equal(originalSource, File.ReadAllBytes(fixture!));
            string? proofDirectory = Environment.GetEnvironmentVariable("MEMORIA_FF8_EXPORT_PROOF_DIRECTORY");
            if (!string.IsNullOrWhiteSpace(proofDirectory))
            {
                Directory.CreateDirectory(proofDirectory);
                File.Copy(store.GetPath(101), Path.Combine(proofDirectory, "exported.ff8"), overwrite: false);
            }
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static void UnexpectedAction(int slot)
    {
        Assert.Fail("Copy must use the selected saved snapshot.");
    }

    private static InputFrame Card()
    {
        return new InputFrame(0, 0, false, false, false, false, CardChallenge: true);
    }

    private static InputFrame Confirm()
    {
        return new InputFrame(0, 0, true, false, false, false);
    }
}
