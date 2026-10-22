using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleAcid317ResourcesTests
{
    [Fact]
    public void Original_ACT_sprites_distance_table_and_binding_are_complete()
    {
        using GameAssets game = Open();
        NativeBattleAcid317Resources resources = game.ReadNativeAcid317Resources();
        Assert.Same(resources, game.ReadNativeAcid317Resources());
        NativeBattleAcid317ResourcesReader.ValidatePayloads(resources);
        Assert.Equal(0x1178, resources.TargetAct.Data.Length);
        Assert.Equal(new[] { 0x160, 0x1AC, 0xF8 }, new[] { resources.SpriteA.Bytes.Length, resources.SpriteB.Bytes.Length, resources.SpriteC.Bytes.Length });
        Assert.Equal(new ushort[] { 13, 16, 9 }, new[] { resources.SpriteA, resources.SpriteB, resources.SpriteC }.Select(sprite => BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8))));
        Assert.Equal(0x328, resources.Camera.Data.Length);
        Assert.Equal(0x55784u, resources.Sound);
        Assert.Equal(16384, resources.DistanceTable.Length);
        Assert.Equal((byte)0, resources.DistanceTable[0]);
        Assert.Equal((byte)179, resources.DistanceTable[^1]);
        Assert.Equal(new short[] { 1292, 0, 547, 9 }, Enumerable.Range(0, 4).Select(index => BinaryPrimitives.ReadInt16LittleEndian(resources.MouthAnchor8.AsSpan().Slice(index * 2))));
        Assert.Equal(game.ReadNativeGatlingGun305Resources().CasterDat.Data.ToArray(), resources.CasterDat.Data.ToArray());
    }

    [Fact]
    public void Source_addresses_and_complete_programme_survive_JSON()
    {
        using GameAssets game = Open();
        NativeBattleAcid317Resources resources = game.ReadNativeAcid317Resources();
        string json = JsonSerializer.Serialize(resources);
        NativeBattleAcid317Resources restored = JsonSerializer.Deserialize<NativeBattleAcid317Resources>(json)!;
        NativeBattleAcid317ResourcesReader.ValidatePayloads(restored);
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        Assert.Equal(restored.SpriteC, restored.ReadSprite(0x10016A8));
        Assert.Throws<NotSupportedException>(() => restored.ReadSprite(0x10016AC));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Malformed_extents_and_shape_indices_are_rejected(int selected)
    {
        using GameAssets game = Open();
        NativeBattleAcid317Resources resources = game.ReadNativeAcid317Resources();
        NativeBattleAcid317Resources altered;
        if (selected == 0)
        {
            altered = resources with { TargetAct = resources.TargetAct with { Data = resources.TargetAct.Data.RemoveAt(resources.TargetAct.Data.Length - 1) } };
        }
        else if (selected == 1)
        {
            altered = resources with { DistanceTable = resources.DistanceTable.RemoveAt(0) };
        }
        else if (selected == 2)
        {
            altered = resources with { MouthAnchor8 = resources.MouthAnchor8.RemoveAt(0) };
        }
        else
        {
            byte[] bytes = resources.TargetAct.Data.ToArray();
            int shape = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(8));
            int faces = shape + BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(shape));
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(faces + 8 * 4 + 4), 128);
            altered = resources with { TargetAct = resources.TargetAct with { Data = bytes.ToImmutableArray() } };
        }
        Assert.Throws<InvalidDataException>(() => NativeBattleAcid317ResourcesReader.ValidatePayloads(altered));
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select original resources to validate Acid ingress.");
        return new(directory!);
    }
}
