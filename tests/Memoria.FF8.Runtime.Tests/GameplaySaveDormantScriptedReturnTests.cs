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
        string hash = new('0', 64);
        var foot = new NativeScriptedWorldFootRecord(1, NativeScriptedWorldFootRecord.AllocationRule, hash, hash, 4, 34, 205, new(0, 0, 0, 0));
        NativeScriptedWorldLoadReceipt load = NativeScriptedWorldLoadReceipt.Create(restored, null, foot);
        Assert.Equal(new NativeScriptedWorldLoadReceipt(1, NativeScriptedWorldLoadReceipt.LoadRule, 0, endpoint.Pose.Yaw, foot, source.Slot4, source.Slot5, 0, endpoint.MapDisplayMode!.Value, 0), load);
    }

    [Fact]
    public void Dormant_scripted_values_reject_packet_mismatch_or_native_loader_owner_collision()
    {
        GameplaySaveWorldReturn endpoint = Endpoint();
        Assert.Throws<InvalidDataException>(() => (endpoint with { EntityType = 21 }).Validate());
        Assert.Throws<InvalidDataException>(() => (endpoint with { ScriptedPacket = endpoint.ScriptedPacket! with { EntryIndex = 28 } }).Validate());
        Assert.Throws<InvalidDataException>(() => (endpoint with { Kind = GameplaySaveWorldReturnKind.Foot, FootMode = 0 }).Validate());
        NativeSavedWorldState native = NativeSavedWorldState.CreateNewGame() with { DormantScriptedReturn = endpoint };
        Assert.Throws<InvalidDataException>(() => native.Validate());
    }

    [Theory]
    [InlineData(GameplaySaveWorldReturnKind.Foot, 0)]
    [InlineData(GameplaySaveWorldReturnKind.Car, 33)]
    public void Scripted_departure_and_endpoint_use_the_same_cold_load_from_saved_transport(GameplaySaveWorldReturnKind kind, byte entity)
    {
        NativeSavedWorldState initialized = NativeSavedWorldState.CreateNewGame();
        NativeSavedWorldPose pose = new(100, -200, 3, 1234);
        GameplaySaveWorldReturn stored = new(kind, 159, 205, pose, 555, entity, 255, 255, 1, 2, 1)
        {
            FootMode = kind == GameplaySaveWorldReturnKind.Foot ? 0 : null,
            RetireSelectedVehicle = kind == GameplaySaveWorldReturnKind.Car ? false : null
        };
        GameplaySaveWorldData data = GameplaySaveWorldData.Capture(initialized) with { Return = stored };
        NativeSavedWorldState saved = GameplaySaveWorldResume.RestoreSavedWorld(data, initialized, 205);
        string hash = new('0', 64);
        var foot = new NativeScriptedWorldFootRecord(1, NativeScriptedWorldFootRecord.AllocationRule, hash, hash, 0, 205, 300, new(0, 0, 0, 0));

        NativeScriptedWorldLoadReceipt load = NativeScriptedWorldLoadReceipt.Create(saved, null, foot);
        Assert.Equal(new NativeScriptedWorldLoadReceipt(1, NativeScriptedWorldLoadReceipt.LoadRule, 0, pose.Yaw, foot, saved.Slot4, saved.Slot5, 0, 2, 0), load);
        var camera = new NativeWorldPathCamera(new(0, 0, 0), new(0, 0, 100));
        var endpoint = new NativeSavedWorldScriptedReturn(1, NativeSavedWorldScriptedReturn.SaveRule, 30, 104, 205, new(9, 22, 59), new(265, 0, 0, 0, 0), load, camera, NativeSavedWorldScriptedReturn.CameraYaw(camera), 22, 255, 255, 0, 2, 1, pose);
        Assert.Equal(endpoint, saved.WithScriptedReturn(endpoint).ScriptedReturn);
        Assert.Throws<InvalidDataException>(() => saved.WithScriptedReturn(endpoint with { Load = load with { RetainedCameraVelocity = 1 } }));
        Assert.Throws<NotSupportedException>(() => NativeScriptedWorldLoadReceipt.Create(initialized, null, foot));
    }

    internal static GameplaySaveWorldReturn Endpoint() => new(GameplaySaveWorldReturnKind.Scripted, 893, 100, new(111, -222, 3, 4), 63886, 22, 255, 255, 0, 0, 1) { ScriptedPacket = new(0, 22, 0), ScriptedFieldId = 893, ScriptedFieldPosition = new(10, 20, 0, 4) };
}
