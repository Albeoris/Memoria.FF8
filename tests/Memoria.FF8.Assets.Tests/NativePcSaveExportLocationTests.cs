using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed partial class NativePcSaveConverterTests
{
    [Theory]
    [InlineData(48)]
    [InlineData(50)]
    public void Field_export_preserves_current_vehicle_reload_auxiliaries_without_an_original_baseline(int entity)
    {
        NativeSavedWorldPose pose = new(12345, 23456, 77, 123);
        NativeSavedWorldState saved = NativeSavedWorldState.CreateNewGame();
        saved = entity == 48
            ? saved.WithGardenReturn(new(1, NativeSavedWorldGardenReturn.SaveRule, 17, 74, 0, pose, 321, 48, 255, 255, 1, 2, 5))
            : saved.WithRagnarokReturn(new(NativeSavedWorldRagnarokReturn.CurrentRule, 17, 74, 0, pose, 321, 2, 5) { AnimationVariant108 = 1 });
        NativePcSaveData data = NativePcSaveCodec.Decode(NativePcSaveConverter.Export(Defaults() with { SavedWorld = saved }));
        int world = NativePcSaveData.MainOffset + 4624;
        int position = world + (entity == 48 ? 36 : 24);
        Assert.Equal(12345u, BinaryPrimitives.ReadUInt32LittleEndian(data.Block.AsSpan(position)));
        Assert.Equal(23456u, BinaryPrimitives.ReadUInt32LittleEndian(data.Block.AsSpan(position + 4)));
        Assert.Equal((ushort)77, BinaryPrimitives.ReadUInt16LittleEndian(data.Block.AsSpan(position + 8)));
        Assert.Equal((ushort)123, BinaryPrimitives.ReadUInt16LittleEndian(data.Block.AsSpan(position + 10)));
        Assert.Equal((ushort)321, BinaryPrimitives.ReadUInt16LittleEndian(data.Block.AsSpan(world + 96)));
        Assert.Equal((byte)entity, data.Block[world + 105]);
        Assert.Equal((byte)1, data.Block[world + 108]);
        Assert.Equal((byte)2, data.Block[world + 110]);
    }

    [Fact]
    public void Unallocated_original_import_preserves_its_field_party_and_all_saved_poses()
    {
        NativePcSaveData original = NativePcSaveCodec.Decode(NativePcSaveConverter.Export(Defaults()));
        original.Block[NativePcSaveData.MainOffset + 3305] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(original.Block.AsSpan(NativePcSaveData.MainOffset + 3320), 314);
        GameState imported = NativePcSaveConverter.Import(NativePcSaveCodec.Encode(original), Defaults(), _ => "fixture", "en");
        Assert.Null(imported.EventState);
        NativePcSaveData exported = NativePcSaveCodec.Decode(NativePcSaveConverter.Export(imported));
        Assert.Equal(original.FieldParty, exported.FieldParty);
        Assert.Equal((short)314, BinaryPrimitives.ReadInt16LittleEndian(exported.Block.AsSpan(NativePcSaveData.MainOffset + 3320)));
    }

    [Fact]
    public void Field_export_requires_current_pose_even_when_original_bytes_contain_an_older_pose()
    {
        GameState defaults = Defaults();
        NativePcSaveData baseline = NativePcSaveCodec.Decode(NativePcSaveConverter.Export(defaults));
        GameState moved = defaults with { OriginalSave = baseline, FieldId = 257, FieldArrival = null };
        Assert.Throws<NotSupportedException>(() => NativePcSaveConverter.Export(moved));
    }

    [Fact]
    public void Field_export_does_not_replace_a_missing_location_with_a_new_game_field()
    {
        Assert.Throws<NotSupportedException>(() => NativePcSaveConverter.Export(Defaults() with { FieldArrival = null }));
        Assert.Throws<NotSupportedException>(() => NativePcSaveConverter.Export(Defaults() with { FieldId = 1 }));
    }

    [Fact]
    public void Export_writes_the_current_canonical_world_music_selection()
    {
        GameState state = Defaults() with { EventState = JsonSerializer.SerializeToElement(new { Scene = new { WorldMusicSelection = new { Music = 37 } } }) };
        NativePcSaveData data = NativePcSaveCodec.Decode(NativePcSaveConverter.Export(state));
        Assert.Equal((byte)37, data.Block[NativePcSaveData.EventBankOffset + 268]);
    }

    [Fact]
    public void Field_export_preserves_saved_car_pose_and_auxiliaries()
    {
        NativeSavedWorldPose pose = new(12345, 23456, 77, 123);
        NativeSavedWorldCarReturn receipt = new() { Version = 2, Rule = NativeSavedWorldCarReturn.SaveRule, SourceWorldFrame = 17, LastFieldId = 74, StoryWord = 0, Pose = pose, CameraYaw96 = 321, EntityType105 = 33, Channel106 = 255, Channel107 = 255, AnimationVariant108 = 1, MapDisplayMode110 = 2, MapLocationId = 5 };
        NativeSavedWorldState saved = NativeSavedWorldState.CreateNewGame().WithOrdinaryCarEntry().WithCarReturn(receipt);
        NativePcSaveData data = NativePcSaveCodec.Decode(NativePcSaveConverter.Export(Defaults() with { SavedWorld = saved }));
        int world = NativePcSaveData.MainOffset + 4624;
        Assert.Equal(12345u, BinaryPrimitives.ReadUInt32LittleEndian(data.Block.AsSpan(world + 48)));
        Assert.Equal((byte)255, data.Block[world + 98]);
        Assert.Equal((ushort)321, BinaryPrimitives.ReadUInt16LittleEndian(data.Block.AsSpan(world + 96)));
        Assert.Equal((byte)33, data.Block[world + 105]);
        Assert.Equal((byte)1, data.Block[world + 108]);
        Assert.Equal((byte)2, data.Block[world + 110]);
    }

    [Fact]
    public void Field_export_preserves_scripted_saved_foot_record_and_auxiliaries()
    {
        NativeSavedWorldState saved = NativeSavedWorldState.CreateNewGame();
        string hash = new('0', 64);
        NativeScriptedWorldFootRecord foot = new(1, NativeScriptedWorldFootRecord.AllocationRule, hash, hash, 0, 0, 2, new(0, 0, 0, 0));
        NativeScriptedWorldLoadReceipt load = new(1, NativeScriptedWorldLoadReceipt.LoadRule, 0, 0, foot, saved.Slot4, saved.Slot5, 0, 2, 0);
        NativeWorldPathCamera camera = new(new(0, 0, 0), new(0, 0, 1));
        ushort yaw = NativeSavedWorldScriptedReturn.CameraYaw(camera);
        NativeSavedWorldScriptedReturn receipt = new(1, NativeSavedWorldScriptedReturn.SaveRule, 30, 74, 1, new(0, 16, 0), new(74, 10, 20, 0, 0), load, camera, yaw, 16, 255, 255, 0, 2, 5, new(12345, 23456, 77, 0));
        receipt.Validate();
        saved = saved with { ScriptedReturn = receipt };
        saved.Validate();
        NativePcSaveData data = NativePcSaveCodec.Decode(NativePcSaveConverter.Export(Defaults() with { SavedWorld = saved }));
        int world = NativePcSaveData.MainOffset + 4624;
        Assert.Equal(12345u, BinaryPrimitives.ReadUInt32LittleEndian(data.Block.AsSpan(world)));
        Assert.Equal((ushort)yaw, BinaryPrimitives.ReadUInt16LittleEndian(data.Block.AsSpan(world + 96)));
        Assert.Equal((byte)16, data.Block[world + 105]);
        Assert.Equal((byte)0, data.Block[world + 108]);
        Assert.Equal((byte)2, data.Block[world + 110]);
    }

    [Fact]
    public void Export_refuses_uncertain_persistent_event_values_and_ignores_transient_cells()
    {
        NativeExamTimerState timer = NativeExamTimerState.FromReachedSet(2) with { Alternatives = [new NativeTimerAlternative(0, 0, 1), new NativeTimerAlternative(1, 1, 2)] };
        NativeScalarValue value = NativeScalarValue.FromTimer(new NativeTimerReadOrigin(1, 74, new string('A', 64), 0, 0, 1, 2, timer)).ToWidth(1);
        Assert.False(value.IsExact);
        NativeMemoryValue uncertain = new(1, value);
        NativeEventMemory persistent = new(NativeEventMemory.CurrentVersion, new Dictionary<int, NativeMemoryValue> { [300] = uncertain });
        Assert.Throws<NotSupportedException>(() => NativePcSaveConverter.Export(Defaults() with { EventMemory = persistent }));
        NativeEventMemory transient = new(NativeEventMemory.CurrentVersion, new Dictionary<int, NativeMemoryValue> { [1024] = uncertain });
        Assert.NotEmpty(NativePcSaveConverter.Export(Defaults() with { EventMemory = transient }));
    }

    [Fact]
    public void World_export_uses_current_party_when_imported_field_party_is_stale()
    {
        GameState state = Defaults();
        NativePcSaveData baseline = NativePcSaveCodec.Decode(NativePcSaveConverter.Export(state));
        state = state with { OriginalSave = baseline, Scene = SceneKind.WorldMap, CharacterVitals = state.CharacterVitals! with { CombatParty = [1, 255, 255] } };
        NativePcSaveData data = NativePcSaveCodec.Decode(NativePcSaveConverter.Export(state));
        Assert.Equal((byte)1, data.Block[NativePcSaveData.MainOffset + 3304]);
    }
}
