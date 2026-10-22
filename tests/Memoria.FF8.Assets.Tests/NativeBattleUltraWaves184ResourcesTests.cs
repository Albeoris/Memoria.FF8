using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleUltraWaves184ResourcesTests
{
    [Fact]
    public void Original_ring_camera_caster_and_literal_tables_have_complete_bounds()
    {
        using GameAssets game = Open();
        NativeBattleUltraWaves184Resources resources = game.ReadNativeUltraWaves184Resources();
        Assert.Same(resources, game.ReadNativeUltraWaves184Resources());
        NativeBattleUltraWaves184ResourcesReader.ValidatePayloads(resources);
        Assert.Equal(624, resources.Ring.Bytes.Length);
        Assert.Equal(0x164, resources.Camera.Data.Length);
        Assert.Equal(0x1B74, resources.CasterDat.Data.Length);
        Assert.Equal(35, resources.WarpColors.Length);
        Assert.Equal(16, resources.RingColors.Length);
        Assert.Equal(8, resources.EmitterVectors.Length);
        Assert.Equal(8, resources.StageDraw.Length);
        Assert.Equal(48, resources.CapturePackets.Length);
    }

    [Fact]
    public void JSON_resources_seed_capture_once_and_preserve_warm_unknown_process_bytes()
    {
        using GameAssets game = Open();
        NativeBattleUltraWaves184Resources resources = game.ReadNativeUltraWaves184Resources();
        string json = JsonSerializer.Serialize(resources);
        NativeBattleUltraWaves184Resources restored = JsonSerializer.Deserialize<NativeBattleUltraWaves184Resources>(json)!;
        NativeBattleUltraWaves184ResourcesReader.ValidatePayloads(restored);
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        NativeBattleUltraWaves184State state = NativeBattleUltraWaves184State.FromColdProcess().SeedColdSources(restored);
        Assert.Equal(resources.CapturePackets.ToArray(), state.CapturePackets.Read(0, 48));
        state = state with { CapturePackets = state.CapturePackets.Write(17, [0xAB]) };
        Assert.Equal(state, state.SeedColdSources(restored));
        NativeBattleUltraWaves184State unknown = NativeBattleUltraWaves184State.FromUnknown().SeedColdSources(restored);
        Assert.Throws<NotSupportedException>(() => unknown.CapturePackets.Read(0, 48));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Truncated_and_out_of_bounds_resource_inputs_are_rejected(int selected)
    {
        using GameAssets game = Open();
        NativeBattleUltraWaves184Resources resources = game.ReadNativeUltraWaves184Resources();
        NativeBattleUltraWaves184Resources altered;
        if (selected == 0)
        {
            altered = resources with { CasterDat = resources.CasterDat with { Data = resources.CasterDat.Data.RemoveAt(0) } };
        }
        else if (selected == 1)
        {
            altered = resources with { Camera = resources.Camera with { Data = resources.Camera.Data.RemoveAt(0) } };
        }
        else if (selected == 2)
        {
            byte[] bytes = resources.Ring.Bytes.ToArray();
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0xE4, 2), 50);
            altered = resources with { Ring = resources.Ring with { Bytes = bytes.ToImmutableArray() } };
        }
        else
        {
            altered = resources with { EmitterVectors = resources.EmitterVectors.RemoveAt(7) };
        }
        Assert.Throws<InvalidDataException>(() => NativeBattleUltraWaves184ResourcesReader.ValidatePayloads(altered));
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select original resources to validate Ultra Waves ingress.");
        return new(directory!);
    }
}
