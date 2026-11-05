using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GameplaySaveWorldDialogueTests
{
    [Fact]
    public void Ordinary_world_fuel_message_cold_load_retains_text_and_movement_close_owner_without_replaying_consumption()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        using WorldAssets worldAssets = new(directory);
        GameplaySaveComponentDocument initial = GameplaySaveSessionLoaderTests.Document(assets, true);
        initial = initial with { Resume = initial.Resume with { World = initial.Resume.World! with { X = 24576, Z = -29406, BodyYaw = 1024, MapDisplayMode = 0 } } };
        GameSession sourceSession = new(GameplaySaveSessionLoader.Restore(initial, directory, sharedAssets: assets));
        using GameWorld source = new(sourceSession, directory, sharedAssets: assets);
        source.EnsureField();
        NativeWorldMessage binding = worldAssets.ReadNativeFuelMessage(3);
        NativeWorldTextState text = NativeWorldTextState.Create(ImmutableArray.CreateRange(worldAssets.ReadFuelMessageBytes(3)), worldAssets.ReadWorldMessageSpeed(2));
        NativeWorldEventUi ui = NativeWorldEventUi.Create().Show(binding with { Text = text }).AdvanceText(9);
        sourceSession.Restore(sourceSession.State with { World = sourceSession.State.World! with { EventUi = ui }, WorldProcess = sourceSession.WorldProcess! with { FuelMessageMovementCallbacks2045C78 = 3 } });
        Assert.Equal(0, sourceSession.State.World!.Frame);
        Assert.NotNull(source.CurrentDialogue);
        GameState prepared = sourceSession.PrepareSnapshot();
        string sourceBefore = JsonSerializer.Serialize(prepared);
        string firstPath = SavePath();
        string secondPath = SavePath();
        try
        {
            source.WriteGameplaySave(firstPath);
            Assert.Equal(sourceBefore, JsonSerializer.Serialize(sourceSession.PrepareSnapshot()));
            byte[] original = File.ReadAllBytes(firstPath);
            using (FileStream input = File.OpenRead(firstPath))
            {
                GameplaySaveComponentDocument document = GameplaySaveCodec.Read(input);
                Assert.NotNull(document.Progression!.WorldUi);
                string serialized = JsonSerializer.Serialize(document.Progression.WorldUi);
                Assert.DoesNotContain("Bytes", serialized, StringComparison.Ordinal);
                Assert.DoesNotContain("NativeFootRuntime", serialized, StringComparison.Ordinal);
                GameplaySaveWorldMessage message = Assert.Single(document.Progression.WorldUi.Windows);
                Assert.Equal(binding.SourceSha256, message.SourceSha256);
                GameplaySaveWorldMessage invalidMessage = message with { SourceSha256 = new string('A', 64) };
                GameplaySaveComponentDocument malformed = document with { Progression = document.Progression with { WorldUi = document.Progression.WorldUi with { Windows = [invalidMessage] } } };
                Assert.Throws<InvalidDataException>(() => GameplaySaveSessionLoader.Restore(malformed, directory, sharedAssets: assets));
            }
            GameSession firstSession = new(PlayerStateLoader.Load(firstPath, directory, sharedAssets: assets));
            using GameWorld first = new(firstSession, directory, sharedAssets: assets);
            first.EnsureField();
            Assert.Equal(source.CurrentDialogue, first.CurrentDialogue);
            Assert.Equal(3, firstSession.WorldProcess!.FuelMessageMovementCallbacks2045C78);
            Assert.Equal(JsonSerializer.Serialize(ui), JsonSerializer.Serialize(firstSession.State.World!.EventUi));
            Assert.Equal(prepared.Economy!.Gil, firstSession.Economy!.Gil);
            Assert.Equal(prepared.Economy.Items, firstSession.Economy.Items);
            first.WriteGameplaySave(secondPath);
            GameSession secondSession = new(PlayerStateLoader.Load(secondPath, directory, sharedAssets: assets));
            using GameWorld second = new(secondSession, directory, sharedAssets: assets);
            second.EnsureField();
            Assert.Equal(first.CurrentDialogue, second.CurrentDialogue);
            Assert.Equal(JsonSerializer.Serialize(firstSession.State.World!.EventUi), JsonSerializer.Serialize(secondSession.State.World!.EventUi));
            long beforeIdle = secondSession.State.World!.Frame;
            for (int callback = 0; callback < 30; callback++)
            {
                Assert.True(source.Apply(new(PlayerActionKind.Wait)).Accepted);
                Assert.True(second.Apply(new(PlayerActionKind.Wait)).Accepted);
            }
            Assert.Equal(beforeIdle + 30, secondSession.State.World!.Frame);
            Assert.NotNull(second.CurrentDialogue);
            Assert.Equal(3, secondSession.WorldProcess!.FuelMessageMovementCallbacks2045C78);
            WorldState beforeMovement = secondSession.State.World!;
            for (int callback = 0; callback < 40 && second.CurrentDialogue is not null; callback++)
            {
                PlayerAction move = new(PlayerActionKind.Move, Horizontal: 1);
                Assert.True(source.Apply(move).Accepted);
                Assert.True(second.Apply(move).Accepted);
                Assert.Equal(sourceSession.WorldProcess!.FuelMessageMovementCallbacks2045C78, secondSession.WorldProcess!.FuelMessageMovementCallbacks2045C78);
            }
            WorldState afterMovement = secondSession.State.World!;
            Assert.True(afterMovement.X != beforeMovement.X || afterMovement.Z != beforeMovement.Z, JsonSerializer.Serialize(new { Source = new { sourceSession.State.World!.Frame, sourceSession.State.World.X, sourceSession.State.World.Z, sourceSession.State.World.GroundType }, Restored = new { afterMovement.Frame, afterMovement.X, afterMovement.Z, afterMovement.GroundType, afterMovement.EventUi!.ScriptMode }, beforeMovement.X, beforeMovement.Z, Controls = secondSession.State.WorldControls }));
            Assert.Equal(0, secondSession.WorldProcess.FuelMessageMovementCallbacks2045C78);
            Assert.Null(second.CurrentDialogue);
            Assert.DoesNotContain(secondSession.State.World!.EventUi!.Windows, message => message.WindowId == 1);
            Assert.Equal(prepared.Economy.Items, secondSession.Economy!.Items);
            Assert.Equal(original, File.ReadAllBytes(firstPath));
        }
        finally
        {
            File.Delete(firstPath);
            File.Delete(secondPath);
        }
    }

    private static string SavePath() => Path.Combine(Path.GetTempPath(), "Memoria.FF8-world-dialogue-" + Guid.NewGuid().ToString("N"));
}
