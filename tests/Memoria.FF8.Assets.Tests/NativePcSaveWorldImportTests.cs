using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed partial class NativePcSaveConverterTests
{
    [Theory]
    [InlineData(32, 74)]
    [InlineData(33, 73)]
    [InlineData(35, 82)]
    public void Original_car_import_retains_its_transport_pose_and_raw_angles(byte entity, byte shape)
    {
        NativePcSaveData data = NativePcSaveCodec.Decode(NativePcSaveConverter.Export(Defaults()));
        int world = NativePcSaveData.MainOffset + 4624;
        BinaryPrimitives.WriteUInt16LittleEndian(data.Block.AsSpan(NativePcSaveData.MainOffset + 3312), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(data.Block.AsSpan(NativePcSaveData.EventBankOffset + 84), 74);
        BinaryPrimitives.WriteUInt32LittleEndian(data.Block.AsSpan(world + 48), 12345);
        BinaryPrimitives.WriteUInt32LittleEndian(data.Block.AsSpan(world + 52), 23456);
        BinaryPrimitives.WriteUInt16LittleEndian(data.Block.AsSpan(world + 56), 77);
        BinaryPrimitives.WriteUInt16LittleEndian(data.Block.AsSpan(world + 58), 0xF456);
        BinaryPrimitives.WriteUInt16LittleEndian(data.Block.AsSpan(world + 96), 0xF9C5);
        data.Block[world + 98] = shape;
        data.Block[world + 104] = 6;
        data.Block[world + 105] = entity;
        data.Block[world + 108] = 1;
        data.Block[world + 109] = 0xA5;
        data.Block[world + 110] = 2;
        GameState imported = NativePcSaveConverter.Import(NativePcSaveCodec.Encode(data), Defaults(), _ => "fixture", "en");
        NativeSavedWorldCarReturn car = Assert.IsType<NativeSavedWorldCarReturn>(imported.SavedWorld!.CarReturn);
        Assert.Equal(NativeSavedWorldCarReturn.OriginalSaveRule, car.Rule);
        Assert.Equal(entity, car.EntityType105);
        Assert.Equal(new NativeSavedWorldPose(12345, 23456, 77, 0x456), car.Pose);
        Assert.Equal((ushort)0x9C5, car.CameraYaw96);
        Assert.Equal((byte)1, car.AnimationVariant108);
        Assert.Equal((byte)2, car.MapDisplayMode110);
        Assert.Equal(NativeWorldTerrainFlags109.Exact(0xA5), imported.SavedWorld.TerrainFlags109);
        Assert.Equal((byte)6, imported.SavedWorld.EntryMode104);
        Assert.Equal((ushort)0xF456, imported.SavedWorld.OriginalAngleWords!.BodyWords[4]);
        Assert.Equal((ushort)0xF9C5, imported.SavedWorld.OriginalAngleWords.CameraWord);
        Assert.Equal(new WorldTransition(62, entity, -1), imported.WorldArrival);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(16)]
    [InlineData(34)]
    [InlineData(40)]
    [InlineData(49)]
    [InlineData(128)]
    [InlineData(132)]
    public void Original_import_refuses_an_unsupported_transport_instead_of_substituting_foot(byte entity)
    {
        NativePcSaveData data = NativePcSaveCodec.Decode(NativePcSaveConverter.Export(Defaults()));
        BinaryPrimitives.WriteUInt16LittleEndian(data.Block.AsSpan(NativePcSaveData.MainOffset + 3312), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(data.Block.AsSpan(NativePcSaveData.EventBankOffset + 84), 74);
        data.Block[NativePcSaveData.MainOffset + 4624 + 105] = entity;
        Assert.Throws<NotSupportedException>(() => NativePcSaveConverter.Import(NativePcSaveCodec.Encode(data), Defaults(), _ => "fixture", "en"));
    }

    [Fact]
    public void Field_import_preserves_a_dormant_unsupported_native_transport_without_claiming_foot()
    {
        NativePcSaveData data = NativePcSaveCodec.Decode(NativePcSaveConverter.Export(Defaults()));
        int world = NativePcSaveData.MainOffset + 4624;
        BinaryPrimitives.WriteUInt32LittleEndian(data.Block.AsSpan(world + 48), 12345);
        for (int slot = 0; slot < 8; slot++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(data.Block.AsSpan(world + slot * 12 + 10), (ushort)(0xF100 + slot));
        }
        BinaryPrimitives.WriteUInt16LittleEndian(data.Block.AsSpan(world + 96), 0xF9C5);
        data.Block[world + 105] = 34;
        data.Block[world + 108] = 1;
        data.Block[world + 110] = 2;
        GameState imported = NativePcSaveConverter.Import(NativePcSaveCodec.Encode(data), Defaults(), _ => "fixture", "en");
        Assert.Equal(SceneKind.Field, imported.Scene);
        Assert.Null(imported.SavedWorld!.FootReturn);
        Assert.Null(imported.SavedWorld.CarReturn);
        NativeSavedWorldUnsupportedEntry entry = Assert.IsType<NativeSavedWorldUnsupportedEntry>(imported.SavedWorld.UnsupportedEntry);
        Assert.Equal((byte)34, entry.EntityType105);
        Assert.Equal(12345, entry.Poses[4].X);
        Assert.Equal((ushort)0xF104, entry.Poses[4].Yaw);
        Assert.Equal((ushort)0xF9C5, entry.CameraWord96);
        Assert.Equal((byte)1, entry.AnimationVariant108);
        Assert.Equal((byte)2, entry.MapDisplayMode110);
        NativePcSaveData exported = NativePcSaveCodec.Decode(NativePcSaveConverter.Export(imported));
        Assert.Equal(data.Block.AsSpan(world, 128).ToArray(), exported.Block.AsSpan(world, 128).ToArray());
    }
}
