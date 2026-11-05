using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class GameplaySaveDormantScriptedReturnTests
{
    [Fact]
    public void Field_side_scripted_endpoint_preserves_full_word_yaw_packet_and_parked_values_without_loader_history()
    {
        GameplaySaveWorldReturn endpoint = Endpoint();
        NativeSavedWorldState source = NativeSavedWorldState.CreateNewGame() with
        {
            Origin = new(NativeSavedWorldState.CurrentProofVersion, NativeSavedWorldOriginKind.GameplaySave, NativeSavedWorldState.GameplaySaveInitializationRule),
            GardenPose3 = new(700, 800, 3, 40),
            FuelDistance112 = 321,
            DormantScriptedReturn = endpoint
        };
        source.Validate();
        GameplaySaveWorldData saved = GameplaySaveCodec.Detach(GameplaySaveWorldData.Capture(source));
        saved.Validate();
        NativeSavedWorldState restored = GameplaySaveWorldResume.RestoreSavedWorld(saved, NativeSavedWorldState.CreateNewGame(), 100);
        Assert.Null(restored.ScriptedReturn);
        Assert.Equal(endpoint, restored.DormantScriptedReturn);
        Assert.Equal((ushort)63886, restored.DormantScriptedReturn!.CameraYaw);
        Assert.Equal(source.GardenPose3, restored.GardenPose3);
        Assert.Equal(source.Slot4, restored.Slot4);
        Assert.Equal(source.Slot5, restored.Slot5);
        Assert.Equal(source.FuelDistance112, restored.FuelDistance112);
        Assert.Equal((byte)0, restored.ReadMapDisplayModeForFreshFootLoader());
        Assert.Equal(JsonSerializer.Serialize(saved), JsonSerializer.Serialize(GameplaySaveWorldData.Capture(restored)));
    }

    [Fact]
    public void Dormant_scripted_values_reject_packet_mismatch_or_native_loader_owner_collision()
    {
        GameplaySaveWorldReturn endpoint = Endpoint();
        Assert.Throws<InvalidDataException>(() => (endpoint with { EntityType = 21 }).Validate());
        Assert.Throws<InvalidDataException>(() => (endpoint with { ScriptedPacket = endpoint.ScriptedPacket! with { EntryIndex = 14 } }).Validate());
        Assert.Throws<InvalidDataException>(() => (endpoint with { Kind = GameplaySaveWorldReturnKind.Foot, FootMode = 0 }).Validate());
        NativeSavedWorldState native = NativeSavedWorldState.CreateNewGame() with { DormantScriptedReturn = endpoint };
        Assert.Throws<InvalidDataException>(() => native.Validate());
    }

    internal static GameplaySaveWorldReturn Endpoint() => new(GameplaySaveWorldReturnKind.Scripted, 893, 100, new(111, -222, 3, 4), 63886, 22, 255, 255, 0, 0, 1) { ScriptedPacket = new(0, 22, 0), ScriptedFieldId = 893, ScriptedFieldPosition = new(10, 20, 0, 4) };
}
