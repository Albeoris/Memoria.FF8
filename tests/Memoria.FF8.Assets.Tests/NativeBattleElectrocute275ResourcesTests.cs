using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleElectrocute275ResourcesTests
{
    [Fact]
    public void Original_ACT_sprites_nested_paths_and_lookup_tables_are_complete()
    {
        using GameAssets game = Open();
        NativeBattleElectrocute275Resources resources = game.ReadNativeElectrocute275Resources();
        Assert.Same(resources, game.ReadNativeElectrocute275Resources());
        NativeBattleElectrocute275ResourcesReader.ValidatePayloads(resources);
        Assert.Equal(0xA10, resources.Act.Data.Length);
        Assert.Equal(new[] { 0x154, 0xDC }, new[] { resources.SpriteA.Bytes.Length, resources.SpriteB.Bytes.Length });
        Assert.Equal(0x1E8, resources.Camera.Data.Length);
        Assert.Equal(0x61AE8u, resources.Sound);
        Assert.Equal(0xC8DCFAu, resources.ActorColor);
        Assert.Equal(0x1000u, resources.InitialFlashThreshold);
        Assert.Equal(new byte[] { 25, 26, 11, 12, 8, 10 }, resources.ColorIndices.ToArray());
        Assert.Equal(4096, resources.WaveTable.Length);
        Assert.Equal((short)4096, resources.WaveTable[1024]);
        Assert.Equal(16384, resources.DistanceTable.Length);
        Assert.Equal((short)16, resources.ReadPathWord(NativeBattleElectrocute275Profile.BonePathsAddress));
        Assert.Equal(game.ReadNativeGatlingGun305Resources().CasterDat.Data.ToArray(), resources.CasterDat.Data.ToArray());
    }

    [Fact]
    public void Resources_and_once_seeded_mutable_threshold_survive_JSON()
    {
        using GameAssets game = Open();
        NativeBattleElectrocute275Resources resources = game.ReadNativeElectrocute275Resources();
        string json = JsonSerializer.Serialize(resources);
        NativeBattleElectrocute275Resources restored = JsonSerializer.Deserialize<NativeBattleElectrocute275Resources>(json)!;
        NativeBattleElectrocute275ResourcesReader.ValidatePayloads(restored);
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        NativeBattleElectrocute275State state = NativeBattleElectrocute275State.FromColdProcess().SeedColdSources(restored);
        Assert.Equal(0x1000u, state.ReadFlashThreshold());
        Assert.False(state.ColdSourcesPending);
        state = state with { FlashThreshold = 0x5000 };
        Assert.Equal(state, state.SeedColdSources(restored));
        NativeBattleElectrocute275State unknown = NativeBattleElectrocute275State.FromUnknown().SeedColdSources(restored);
        Assert.Throws<NotSupportedException>(() => unknown.ReadFlashThreshold());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Malformed_ACT_selected_vertices_and_nested_paths_are_rejected(int selected)
    {
        using GameAssets game = Open();
        NativeBattleElectrocute275Resources resources = game.ReadNativeElectrocute275Resources();
        NativeBattleElectrocute275Resources altered;
        if (selected == 0)
        {
            altered = resources with { Act = resources.Act with { Data = resources.Act.Data.RemoveAt(resources.Act.Data.Length - 1) } };
        }
        else if (selected == 1)
        {
            altered = resources with { ColorIndices = resources.ColorIndices.RemoveAt(5) };
        }
        else if (selected == 2)
        {
            byte[] bytes = resources.Act.Data.ToArray();
            int shape = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(8));
            int faces = shape + BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(shape));
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(faces + 4 * 4 + 4), 102);
            altered = resources with { Act = resources.Act with { Data = bytes.ToImmutableArray() } };
        }
        else
        {
            byte[] bytes = resources.BonePaths.ToArray();
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(66 * 2), -17);
            altered = resources with { BonePaths = bytes.ToImmutableArray() };
        }
        Assert.Throws<InvalidDataException>(() => NativeBattleElectrocute275ResourcesReader.ValidatePayloads(altered));
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select original resources to validate Electrocute ingress.");
        return new(directory!);
    }
}
