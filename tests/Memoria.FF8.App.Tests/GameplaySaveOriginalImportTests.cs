using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GameplaySaveOriginalImportTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public void Original_field_transfer_captures_not_yet_run_normal_load_intent_and_preserves_source(int? battleMusic)
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameplaySaveComponentDocument document = GameplaySaveFieldPartyResumeTests.ThreePartyDocument(assets);
        GameState fixture = GameplaySaveSessionLoader.Restore(document, directory, sharedAssets: assets);
        fixture.OriginalSave!.Block[NativePcSaveData.EventBankOffset + 203] = 17;
        int expectedMusic = battleMusic ?? 17;
        JsonObject snapshot = JsonNode.Parse(fixture.EventState!.Value.GetRawText())!.AsObject();
        snapshot["Scene"]!["NativeBattleMusic203"] = battleMusic;
        fixture = fixture with { EventState = JsonSerializer.SerializeToElement(snapshot) };
        byte[] image = NativePcSaveConverter.Export(fixture, assets.ReadNewGameCharacterVitals(), originalPlaceId: 0);
        GameSession session = new(NativePcSaveConverter.Import(image, assets));
        Assert.Equal((byte)expectedMusic, session.State.OriginalSave!.Block[NativePcSaveData.EventBankOffset + 203]);
        using GameWorld world = new(session, directory, sharedAssets: assets);
        world.EnsureField();
        Assert.Equal(0, world.Events.Frame);
        Assert.Equal(NativeFieldInitializationStage.Allocated, world.Events.Scene.NativeFieldInitialization!.Stage);
        GameState before = session.State;
        GameplaySaveComponentDocument captured = world.CaptureGameplaySave();
        Assert.Same(before, session.State);
        Assert.Equal(0, world.Events.Frame);
        Assert.Equal(before.FieldArrival!.X, captured.Resume.Field!.X);
        Assert.Equal(before.FieldArrival.Y, captured.Resume.Field.Y);
        for (int slot = 0; slot < 3; slot++)
        {
            NativeFieldLocationPose? expected = captured.Progression!.SceneFacts!.FieldParty[slot] == 255 ? null : before.OriginalSave!.ReadFieldPartyPosition(slot);
            Assert.Equal(expected, captured.Progression.FieldLocation!.PartyPositions[slot]);
        }
        string folder = Path.Combine(Path.GetTempPath(), "Memoria.FF8-original-gameplay-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string source = Path.Combine(folder, "original.ff8");
        string destination = Path.Combine(folder, "imported.sav");
        try
        {
            File.WriteAllBytes(source, image);
            Assert.Equal(0, SaveTransferCommand.Run([source, "--output", destination, "--game-dir", directory, "--mods-dir", Path.Combine(folder, "mods")], import: true));
            using (FileStream stored = File.OpenRead(destination))
            {
                Assert.True(GameplaySaveCodec.HasSignature(stored));
            }
            GameState restored = PlayerStateLoader.Load(destination, directory, sharedAssets: assets);
            Assert.Equal(before.FieldId, restored.FieldId);
            Assert.Equal(before.Economy!.Gil, restored.Economy!.Gil);
            Assert.Equal(before.LimitProgress!.AngeloFlags, restored.LimitProgress!.AngeloFlags);
            Assert.Equal(image, File.ReadAllBytes(source));
            Assert.Equal(1, restored.EventState!.Value.GetProperty("Frame").GetInt64());
            Assert.Equal(0, world.Events.Frame);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
