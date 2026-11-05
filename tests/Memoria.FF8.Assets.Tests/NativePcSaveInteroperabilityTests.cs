using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed partial class NativePcSaveConverterTests
{
    [Fact]
    public void Native_import_preserves_absent_party_field_poses_through_semantic_owners()
    {
        NativePcSaveData original = NativePcSaveCodec.Decode(NativePcSaveConverter.Export(Defaults()));
        for (int slot = 1; slot < 3; slot++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(original.Block.AsSpan(3782 + 2 * slot), (short)(short.MinValue + slot));
            BinaryPrimitives.WriteInt16LittleEndian(original.Block.AsSpan(3788 + 2 * slot), (short)(short.MaxValue - slot));
            BinaryPrimitives.WriteUInt16LittleEndian(original.Block.AsSpan(3794 + 2 * slot), (ushort)(ushort.MaxValue - slot));
            original.Block[3800 + slot] = (byte)(230 + slot);
        }
        GameState imported = NativePcSaveConverter.Import(NativePcSaveCodec.Encode(original), Defaults(), _ => "fixture", "en");
        Assert.Equal(new byte[] { 0, 255, 255 }, imported.GameplaySceneFacts!.FieldParty);
        GameplaySaveProgression progression = GameplaySaveCodec.Detach(GameplaySaveProgression.Capture(imported));
        Assert.Null(progression.OriginalResidue);
        Assert.True(progression.FieldLocation!.IsNativeImport);
        Assert.All(progression.FieldLocation!.PartyPositions, pose => Assert.NotNull(pose));
        GameState semantic = imported with { OriginalSave = null, FieldLoadContext = null, GameplayFieldLocation = progression.FieldLocation, GameplaySceneFacts = progression.SceneFacts, NativeSaveInteroperability = progression.NativeInteroperability };
        Assert.Null(semantic.NativeFieldLocation);
        NativePcSaveData exported = NativePcSaveCodec.Decode(NativePcSaveConverter.Export(semantic));
        Assert.Equal(original.Block.AsSpan(3782, 21).ToArray(), exported.Block.AsSpan(3782, 21).ToArray());
        Assert.Equal(new byte[] { 0, 255, 255 }, exported.FieldParty);
    }

    [Fact]
    public void Native_export_preserves_dormant_raw_world_angles_and_current_pose_overrides()
    {
        NativePcSaveData original = NativePcSaveCodec.Decode(NativePcSaveConverter.Export(Defaults()));
        original.Block[5193] = 48;
        for (int slot = 0; slot < 8; slot++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(original.Block.AsSpan(5098 + 12 * slot), (ushort)(0xF123 + slot));
        }
        BinaryPrimitives.WriteUInt16LittleEndian(original.Block.AsSpan(5184), 0xE456);
        GameState imported = NativePcSaveConverter.Import(NativePcSaveCodec.Encode(original), Defaults(), _ => "fixture", "en");
        NativePcSaveData exported = NativePcSaveCodec.Decode(NativePcSaveConverter.Export(imported with { OriginalSave = null }));
        for (int slot = 0; slot < 8; slot++)
        {
            Assert.Equal((ushort)(0xF123 + slot), BinaryPrimitives.ReadUInt16LittleEndian(exported.Block.AsSpan(5098 + 12 * slot)));
        }
        Assert.Equal((ushort)0xE456, BinaryPrimitives.ReadUInt16LittleEndian(exported.Block.AsSpan(5184)));
        NativeSavedWorldPose moved = imported.SavedWorld!.GardenReturn!.Pose3 with { Yaw = 789 };
        GameState updated = imported with { OriginalSave = null, SavedWorld = imported.SavedWorld with { GardenPose3 = moved, GardenReturn = imported.SavedWorld.GardenReturn with { Pose3 = moved, CameraYaw96 = 890 } } };
        exported = NativePcSaveCodec.Decode(NativePcSaveConverter.Export(updated));
        Assert.Equal((ushort)789, BinaryPrimitives.ReadUInt16LittleEndian(exported.Block.AsSpan(5134)));
        Assert.Equal((ushort)890, BinaryPrimitives.ReadUInt16LittleEndian(exported.Block.AsSpan(5184)));
    }

    [Fact]
    public void Native_export_recomputes_cold_process_lanes_then_overlays_current_logical_scene_values()
    {
        int bank = NativePcSaveData.EventBankOffset;
        int[] addresses = [87, 108, 109, 110, 111, 112, 113, 114, 115, 194, 195, 196, 199, 200, 201, 202, 240, 241];
        NativePcSaveData original = NativePcSaveCodec.Decode(NativePcSaveConverter.Export(Defaults()));
        foreach (int address in addresses)
        {
            original.Block[bank + address] = 73;
        }
        original.Block[bank + 213] = 27;
        original.Block[bank + 242] = 39;
        GameState imported = NativePcSaveConverter.Import(NativePcSaveCodec.Encode(original), Defaults(), _ => "fixture", "en");
        GameState semantic = imported with { OriginalSave = null };
        NativePcSaveData exported = NativePcSaveCodec.Decode(NativePcSaveConverter.Export(semantic));
        Assert.Equal(new byte[] { 0, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 0, 255, 0, 0 }, addresses.Select(address => exported.Block[bank + address]).ToArray());
        Assert.Equal((byte)27, exported.Block[bank + 213]);
        Assert.Equal((byte)39, exported.Block[bank + 242]);
        semantic = semantic with { EventState = JsonSerializer.SerializeToElement(new { Scene = new { FieldLoadReasonByte87 = 5, MusicChannel108 = new { ChannelId = 123, MusicId = 7 }, MusicChannel112 = new { ChannelId = -2, MusicId = 9 }, MusicProcess = new { Bank201 = 1, Bank202 = 0 }, PartyBindingMemory = new { First = 1, Second = 2, Third = 255 }, DrawPointEnabled = 1, DrawPointDisplayParameter = 2 } }) };
        exported = NativePcSaveCodec.Decode(NativePcSaveConverter.Export(semantic));
        Assert.Equal(new byte[] { 5, 123, 0, 0, 0, 254, 255, 255, 255, 1, 2, 255, 7, 9, 1, 0, 1, 2 }, addresses.Select(address => exported.Block[bank + address]).ToArray());
    }

    [Fact]
    public void Native_export_preserves_unknown_terrain_bits_from_explicit_baseline_and_rejects_missing_history()
    {
        NativePcSaveData original = NativePcSaveCodec.Decode(NativePcSaveConverter.Export(Defaults()));
        original.Block[5197] = 0xA5;
        GameState imported = NativePcSaveConverter.Import(NativePcSaveCodec.Encode(original), Defaults(), _ => "fixture", "en");
        GameState partial = imported with { OriginalSave = null, SavedWorld = imported.SavedWorld! with { TerrainFlags109 = new NativeWorldTerrainFlags109(0x03, 0x0F) } };
        NativePcSaveData exported = NativePcSaveCodec.Decode(NativePcSaveConverter.Export(partial));
        Assert.Equal((byte)0xA3, exported.Block[5197]);
        GameState missing = partial with { NativeSaveInteroperability = partial.NativeSaveInteroperability! with { Terrain109Baseline = null } };
        Assert.Throws<NotSupportedException>(() => NativePcSaveConverter.Export(missing));
        GameState exact = missing with { SavedWorld = missing.SavedWorld! with { TerrainFlags109 = NativeWorldTerrainFlags109.Exact(0x19) } };
        exported = NativePcSaveCodec.Decode(NativePcSaveConverter.Export(exact));
        Assert.Equal((byte)0x19, exported.Block[5197]);
    }

    [Fact]
    public void Native_interoperability_preserves_every_named_imported_storage_lane_without_original_image()
    {
        NativePcSaveData original = NativePcSaveCodec.Decode(NativePcSaveConverter.Export(Defaults()));
        (int Offset, int Length)[] lanes = [(4, 380), (394, 2), (457, 3), (460, 4), (3672, 4), (3764, 4), (3772, 4), (3805, 3), (5188, 4), (5199, 1), (5214, 2), (5338, 2), (5410, 2782), (5088, 10), (5100, 10), (5160, 10), (5172, 10)];
        foreach ((int offset, int length) in lanes)
        {
            for (int index = 0; index < length; index++)
            {
                original.Block[offset + index] = (byte)(1 + (offset + index) % 254);
            }
        }
        original.Block[5193] = 48;
        GameState imported = NativePcSaveConverter.Import(NativePcSaveCodec.Encode(original), Defaults(), _ => "fixture", "en");
        NativeSaveInteroperability retained = JsonSerializer.Deserialize<NativeSaveInteroperability>(JsonSerializer.Serialize(imported.NativeSaveInteroperability))!;
        Assert.NotNull(retained.Tail);
        Assert.Equal(2782, retained.Tail.Length);
        GameState semantic = imported with { OriginalSave = null, NativeSaveInteroperability = retained, Economy = imported.Economy! with { Gil = 1234567 } };
        NativePcSaveData exported = NativePcSaveCodec.Decode(NativePcSaveConverter.Export(semantic));
        foreach ((int offset, int length) in lanes)
        {
            Assert.Equal(original.Block.AsSpan(offset, length).ToArray(), exported.Block.AsSpan(offset, length).ToArray());
        }
        Assert.Equal(1234567u, BinaryPrimitives.ReadUInt32LittleEndian(exported.Block.AsSpan(3212)));
        GameState reimported = NativePcSaveConverter.Import(NativePcSaveCodec.Encode(exported), Defaults(), _ => "fixture", "en");
        Assert.Equal(retained.Tail, reimported.NativeSaveInteroperability!.Tail);
        Assert.Equal(retained.AlternateWorldPosition, reimported.NativeSaveInteroperability.AlternateWorldPosition);
        Assert.Equal(1234567u, reimported.Economy!.Gil);
    }

    [Fact]
    public void Native_interoperability_omits_canonical_header_and_zero_tail_and_bounds_named_storage()
    {
        NativePcSaveData original = NativePcSaveCodec.Decode(NativePcSaveConverter.Export(Defaults()));
        NativeSaveInteroperability retained = NativeSaveInteroperability.Capture(original);
        Assert.Null(retained.MemoryCardHeader);
        Assert.Null(retained.Tail);
        original.Block[5413] = 79;
        retained = NativeSaveInteroperability.Capture(original);
        Assert.Equal(new byte[] { 0, 0, 0, 79 }, retained.Tail);
        Assert.Throws<InvalidDataException>(() => (retained with { Tail = new byte[2783] }).Validate());
        Assert.Throws<InvalidDataException>(() => (retained with { MainReserved3672 = [1, 2, 3, 4, 5] }).Validate());
        Assert.Throws<InvalidDataException>(() => (retained with { Tail = [79, 0] }).Validate());
    }

    [Fact]
    public void Native_export_keeps_legacy_original_image_fallback_until_independent_owners_are_captured()
    {
        NativePcSaveData original = NativePcSaveCodec.Decode(NativePcSaveConverter.Export(Defaults()));
        original.Block[8191] = 219;
        GameState imported = NativePcSaveConverter.Import(NativePcSaveCodec.Encode(original), Defaults(), _ => "fixture", "en");
        NativePcSaveData exported = NativePcSaveCodec.Decode(NativePcSaveConverter.Export(imported with { NativeSaveInteroperability = null }));
        Assert.Equal((byte)219, exported.Block[8191]);
    }
}
