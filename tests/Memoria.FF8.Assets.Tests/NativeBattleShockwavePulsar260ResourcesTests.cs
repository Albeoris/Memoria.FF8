using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleShockwavePulsar260ResourcesTests
{
    [Fact]
    public void Original_ACT_geometry_channels_dispatch_and_streamed_textures_have_complete_bounds()
    {
        using GameAssets game = Open();
        NativeBattleShockwavePulsar260Resources resources = game.ReadNativeShockwavePulsar260Resources();
        Assert.Same(resources, game.ReadNativeShockwavePulsar260Resources());
        NativeBattleShockwavePulsar260ResourcesReader.ValidatePayloads(resources);
        Assert.Equal(new[] { 2712, 4576, 4288, 43032, 10268 }, resources.Acts.Select(source => source.Data.Length));
        Assert.Equal(new[] { 428, 532 }, resources.PuffSprites.Select(source => source.Bytes.Length));
        Assert.Equal(new short[] { 360, 361 }, resources.Files.Select(file => file.FileId));
        Assert.Equal(new[] { "MA8DEF_0.TIM", "MA8DEF_1.TIM" }, resources.Files.Select(file => file.Name));
        Assert.All(resources.Files, file => Assert.Equal(33824, file.Data.Length));
        Assert.Equal(new[] { 6, 9, 10, 11 }, Enumerable.Range(0, 15).Where(index => resources.MainDispatch[index] == 0x31));
        Assert.Equal(280, resources.ParticleMesh.Bytes.Length);
        Assert.Equal(0x1B74, resources.CasterDat.Data.Length);
    }

    [Fact]
    public void Immutable_ingress_repeats_JSON_and_retains_source_identity()
    {
        using GameAssets game = Open();
        NativeBattleShockwavePulsar260Resources resources = game.ReadNativeShockwavePulsar260Resources();
        string json = JsonSerializer.Serialize(resources);
        NativeBattleShockwavePulsar260Resources restored = JsonSerializer.Deserialize<NativeBattleShockwavePulsar260Resources>(json)!;
        NativeBattleShockwavePulsar260ResourcesReader.ValidatePayloads(restored);
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        Assert.Equal(resources.Act(0x112015C).Data.ToArray(), restored.Act(0x112015C).Data.ToArray());
        Assert.Equal(resources.Puff(0x111CF4C).Bytes.ToArray(), restored.Puff(0x111CF4C).Bytes.ToArray());
        Assert.Throws<NotSupportedException>(() => restored.Act(0x111B3CC));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Malformed_ACT_vertex_dispatch_and_stream_boundaries_are_rejected(int selected)
    {
        using GameAssets game = Open();
        NativeBattleShockwavePulsar260Resources resources = game.ReadNativeShockwavePulsar260Resources();
        NativeBattleShockwavePulsar260Resources altered;
        if (selected == 0)
        {
            altered = resources with { Acts = resources.Acts.SetItem(0, resources.Acts[0] with { Data = resources.Acts[0].Data.RemoveAt(0) }) };
        }
        else if (selected == 1)
        {
            byte[] data = resources.Acts[0].Data.ToArray();
            int shape = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(8, 4));
            int face = shape + BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(shape, 4)) + 16;
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(face + 4, 2), 160);
            altered = resources with { Acts = resources.Acts.SetItem(0, resources.Acts[0] with { Data = data.ToImmutableArray() }) };
        }
        else if (selected == 2)
        {
            altered = resources with { MainDispatch = resources.MainDispatch.SetItem(0, 0x31) };
        }
        else
        {
            altered = resources with { Files = resources.Files.SetItem(1, resources.Files[1] with { Data = resources.Files[1].Data.RemoveAt(0) }) };
        }
        Assert.Throws<InvalidDataException>(() => NativeBattleShockwavePulsar260ResourcesReader.ValidatePayloads(altered));
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select original resources to validate Shockwave Pulsar ingress.");
        return new(directory!);
    }
}
