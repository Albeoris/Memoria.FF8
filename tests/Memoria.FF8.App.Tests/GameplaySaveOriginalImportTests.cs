using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GameplaySaveOriginalImportTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Unallocated_native_import_retains_empty_slot_words_without_installing_physical_actors(bool historicalImageOnly)
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameState fixture = GameplaySaveSessionLoader.Restore(GameplaySaveFieldPartyResumeTests.ThreePartyDocument(assets), directory, sharedAssets: assets);
        NativePcSaveData original = NativePcSaveCodec.Decode(NativePcSaveConverter.Export(fixture, assets.ReadNewGameCharacterVitals(), originalPlaceId: 0));
        original.Block[NativePcSaveData.MainOffset + 3305] = 255;
        original.Block[NativePcSaveData.MainOffset + 3306] = 255;
        NativeFieldLocationPose[] dormant = [new(-1234, 2345, ushort.MaxValue, 241), new(-2100, -3100, ushort.MaxValue - 1, 242)];
        for (int slot = 1; slot < 3; slot++)
        {
            NativeFieldLocationPose pose = dormant[slot - 1];
            BinaryPrimitives.WriteInt16LittleEndian(original.Block.AsSpan(NativePcSaveData.MainOffset + 3318 + 2 * slot), pose.X);
            BinaryPrimitives.WriteInt16LittleEndian(original.Block.AsSpan(NativePcSaveData.MainOffset + 3324 + 2 * slot), pose.Y);
            BinaryPrimitives.WriteUInt16LittleEndian(original.Block.AsSpan(NativePcSaveData.MainOffset + 3330 + 2 * slot), pose.Triangle);
            original.Block[NativePcSaveData.MainOffset + 3336 + slot] = pose.Direction;
        }
        GameState imported = NativePcSaveConverter.Import(NativePcSaveCodec.Encode(original), assets);
        if (historicalImageOnly)
        {
            imported = imported with { GameplayFieldLocation = null };
        }
        GameSession session = new(imported);
        using GameWorld source = new(session, directory, sharedAssets: assets);
        source.EnsureField();
        GameplaySaveComponentDocument captured = source.CaptureGameplaySave();
        Assert.Equal(0, source.Events.Frame);
        Assert.Equal(NativeFieldInitializationStage.Allocated, source.Events.Scene.NativeFieldInitialization!.Stage);
        Assert.Null(session.State.NativeFieldLocation);
        Assert.True(captured.Progression!.FieldLocation!.IsNativeImport);
        Assert.Equal(dormant[0], captured.Progression.FieldLocation.PartyPositions[1]);
        Assert.Equal(dormant[1], captured.Progression.FieldLocation.PartyPositions[2]);
        Assert.Equal(new byte[] { 0, 255, 255 }, captured.Progression.SceneFacts!.FieldParty);
        using MemoryStream stored = new();
        GameplaySaveCodec.Write(stored, captured);
        stored.Position = 0;
        GameState restored = GameplaySaveSessionLoader.Restore(GameplaySaveCodec.Read(stored), directory, sharedAssets: assets);
        Assert.Null(restored.OriginalSave);
        Assert.Null(restored.NativeFieldLocation);
        Assert.True(restored.GameplayFieldLocation!.IsNativeImport);
        Assert.Equal(dormant[0], restored.GameplayFieldLocation.PartyPositions[1]);
        Assert.Equal(dormant[1], restored.GameplayFieldLocation.PartyPositions[2]);
        Assert.Equal(1, restored.EventState!.Value.GetProperty("Frame").GetInt64());
        JsonElement bindings = restored.EventState.Value.GetProperty("Scene").GetProperty("FieldPartyNativeActorIds");
        Assert.Equal(255, bindings[1].GetInt32());
        Assert.Equal(255, bindings[2].GetInt32());
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public void Original_field_transfer_captures_not_yet_run_normal_load_intent_and_preserves_source(int? battleMusic)
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameplaySaveComponentDocument document = GameplaySaveFieldPartyResumeTests.ThreePartyDocument(assets);
        GameState fixture = GameplaySaveSessionLoader.Restore(document, directory, sharedAssets: assets);
        int expectedMusic = battleMusic ?? 17;
        JsonObject snapshot = JsonNode.Parse(fixture.EventState!.Value.GetRawText())!.AsObject();
        snapshot["Scene"]!["NativeBattleMusic203"] = expectedMusic;
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
        Assert.Null(captured.Progression!.OriginalResidue);
        Assert.Equal(before.OriginalSave!.Names, captured.Progression.SceneFacts!.Names);
        Assert.Same(before, session.State);
        Assert.Equal(0, world.Events.Frame);
        Assert.Equal(before.FieldArrival!.X, captured.Resume.Field!.X);
        Assert.Equal(before.FieldArrival.Y, captured.Resume.Field.Y);
        for (int slot = 0; slot < 3; slot++)
        {
            NativeFieldLocationPose expected = before.OriginalSave!.ReadFieldPartyPosition(slot);
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
            Assert.Null(restored.OriginalSave);
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
