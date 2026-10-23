using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRetainedModelFieldsTests
{
    [Fact]
    public void Loaded_and_unloaded_rotation_XYZW_and_full_normal_color_survive_capture_JSON_and_clear()
    {
        NativeBattleRetainedMemory retained = NativeBattleRetainedMemory.FromColdProcess();
        ImmutableArray<NativeBattleGraphicsModel> models = NativeBattleGraphicsModels.ClearBank(retainedTransformRows: retained.ModelTransformRows).ToImmutableArray();
        NativeBattleModelTransform transform = new(new(1, 2, 3), new(-4, 5, -6), 7, 8, 9) { Scale148 = new(10, 11, 12), Scale154 = 13 };
        models = models.SetItem(0, models[0] with { Flags0 = 3, Rotation12 = -21, Rotation14 = 22, Rotation16 = -23, RotationPadding18 = 0xABCD, Transform = transform, Color44 = 0x87654321, GeometryBounds52 = new(new(-101, -202, -303), new(404, 505, 606)) });
        models = models.SetItem(6, models[6] with { Flags0 = 0, Rotation12 = 31, Rotation14 = -32, Rotation16 = 33, RotationPadding18 = 0xFEDC, Transform = transform with { Position28 = new(101, -102, 103), Vertical34 = -104 }, Color44 = 0xA5123456, GeometryBounds52 = new(new(-707, -808, -909), new(1010, 1111, 1212)) });
        BattleSnapshot snapshot = Snapshot(retained) with { NativeGraphics = new(models, ["party", null, null, "enemy", null, null, null], 0, 0) };
        NativeBattleRetainedMemory captured = NativeBattleRetainedMemory.Capture(snapshot, retained.ColdExecutableSha256);
        NativeBattleRetainedMemory restored = JsonSerializer.Deserialize<NativeBattleRetainedMemory>(JsonSerializer.Serialize(captured))!;
        NativeBattleGraphicsModel[] next = NativeBattleGraphicsModels.ClearBank(retainedTransformRows: restored.ModelTransformRows);
        foreach (int index in new[] { 0, 6 })
        {
            Assert.Equal(NativeBattleModelRetainedFields.Capture(models[index]), NativeBattleModelRetainedFields.Capture(next[index]));
            Assert.Equal((ushort)0, next[index].Flags0);
        }
        Assert.Equal(0xA5123456u, next[6].Color44);
        Assert.Equal((ushort)0xFEDC, next[6].RotationPadding18);
    }

    [Fact]
    public void Missing_previous_fields_stay_unknown_when_captured_and_cleared()
    {
        NativeBattleRetainedMemory retained = NativeBattleRetainedMemory.FromColdProcess() with { ModelTransformRows = null };
        ImmutableArray<NativeBattleGraphicsModel> models = NativeBattleGraphicsModels.ClearBank().ToImmutableArray();
        NativeBattleRetainedMemory captured = NativeBattleRetainedMemory.Capture(Snapshot(retained) with { NativeGraphics = new(models, ["party", null, null, "enemy", null, null, null], 0, 0) }, null);
        NativeBattleRetainedMemory restored = JsonSerializer.Deserialize<NativeBattleRetainedMemory>(JsonSerializer.Serialize(captured))!;
        Assert.All(NativeBattleGraphicsModels.ClearBank(retainedTransformRows: restored.ModelTransformRows), model =>
        {
            Assert.Null(model.Rotation12);
            Assert.Null(model.Rotation14);
            Assert.Null(model.Rotation16);
            Assert.Null(model.RotationPadding18);
            Assert.Null(model.Transform);
            Assert.Null(model.Color44);
            Assert.Null(model.GeometryBounds52);
        });
    }

    [Fact]
    public void Native_load_resets_rotation_XYZ_but_retains_the_adjacent_padding_WORD()
    {
        NativeBattleGraphicsModel model = NativeBattleGraphicsModels.ClearBank(retainedTransformRows: NativeBattleRetainedMemory.FromColdProcess().ModelTransformRows)[3] with { Rotation12 = 91, Rotation14 = 92, Rotation16 = 93, RotationPadding18 = 0xBEEF, GeometryBounds52 = new(new(-100, -200, -300), new(400, 500, 600)) };
        NativeGlobalRandomState random = new(1);
        NativeBattleGraphicsModel loaded = NativeBattleGraphicsModels.CompleteLoad(model, 77, 0, ref random, 2);
        Assert.Equal((short)0, loaded.Rotation12);
        Assert.Equal((short)2048, loaded.Rotation14);
        Assert.Equal((short)0, loaded.Rotation16);
        Assert.Equal((ushort)0xBEEF, loaded.RotationPadding18);
        Assert.Equal(model.GeometryBounds52, loaded.GeometryBounds52);
    }

    [Fact]
    public void Loader_codec_preserves_signed_WORDs_and_the_complete_color_DWORD()
    {
        byte[] row = new byte[156];
        BinaryPrimitives.WriteInt16LittleEndian(row.AsSpan(12), -123);
        BinaryPrimitives.WriteUInt16LittleEndian(row.AsSpan(18), 0xCAFE);
        BinaryPrimitives.WriteInt16LittleEndian(row.AsSpan(30), -321);
        BinaryPrimitives.WriteInt16LittleEndian(row.AsSpan(34), -987);
        BinaryPrimitives.WriteUInt32LittleEndian(row.AsSpan(44), 0xDE123456);
        BinaryPrimitives.WriteInt16LittleEndian(row.AsSpan(148), -4096);
        short[] bounds = [-1234, short.MinValue, -3456, 4567, short.MaxValue, 6789];
        for (int index = 0; index < bounds.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(row.AsSpan(52 + index * 2), bounds[index]);
        }
        NativeBattleModelRetainedFields decoded = NativeBattleModelRetainedFields.FromLoaderBytes(row);
        Assert.Equal((short)-123, decoded.Rotation12);
        Assert.Equal((ushort)0xCAFE, decoded.RotationPadding18);
        Assert.Equal((short)-321, decoded.Transform!.Position28.Y);
        Assert.Equal((short)-987, decoded.Transform.Vertical34);
        Assert.Equal(0xDE123456u, decoded.Color44);
        Assert.Equal((short)-4096, decoded.Transform.Scale148.X);
        Assert.Equal(new NativeBattleEffectLocalBounds(new(-1234, short.MinValue, -3456), new(4567, short.MaxValue, 6789)), decoded.GeometryBounds52);
    }

    [Fact]
    public void Cold_loader_bounds_are_zero_while_legacy_rows_and_unknown_models_remain_unknown()
    {
        NativeBattleEffectLocalBounds zero = new(new(0, 0, 0), new(0, 0, 0));
        NativeBattleRetainedMemory cold = NativeBattleRetainedMemory.FromColdProcess();
        NativeBattleGraphicsModel[] models = NativeBattleGraphicsModels.ClearBank(retainedTransformRows: cold.ModelTransformRows);
        Assert.All(models, model => Assert.Equal(zero, model.GeometryBounds52));
        NativeBattleModelRetainedFields unknown = NativeBattleModelRetainedFields.Capture(new(4, 0));
        Assert.Null(unknown.GeometryBounds52);
        string json = JsonSerializer.Serialize(NativeBattleModelRetainedFields.FromColdProcess());
        System.Text.Json.Nodes.JsonObject legacy = System.Text.Json.Nodes.JsonNode.Parse(json)!.AsObject();
        Assert.True(legacy.Remove(nameof(NativeBattleModelRetainedFields.GeometryBounds52)));
        NativeBattleModelRetainedFields imported = JsonSerializer.Deserialize<NativeBattleModelRetainedFields>(legacy.ToJsonString())!;
        Assert.Null(imported.GeometryBounds52);
        Assert.Null(imported.Restore502340(new(4, 0)).GeometryBounds52);
    }

    private static BattleSnapshot Snapshot(NativeBattleRetainedMemory retained) => new BattleSession(new BattleDefinition
    {
        NativeRetainedMemory = retained,
        Combatants = [new() { Id = "party", IsPlayer = true, Name = "Party", MaximumHp = 1000, CurrentHp = 1000 }, new() { Id = "enemy", Name = "Enemy", MaximumHp = 1000, CurrentHp = 1000 }]
    }, BattleAtbMode.Normal).Capture();
}
