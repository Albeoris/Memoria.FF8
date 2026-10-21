using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleAquaResourcesTests
{
    [Theory]
    [InlineData(55, 308, 0x64224u)]
    [InlineData(72, 416, 0x5577Eu)]
    public void Original_profiles_keep_complete_camera_mesh_vertex_sprite_and_caster_sources(ushort effect, int cameraLength, uint sound)
    {
        using GameAssets game = OpenGame();
        NativeBattleAquaResources resources = game.ReadNativeAquaResources(effect);
        resources.Validate();
        NativeBattleAquaProfile profile = NativeBattleAquaProfile.ForEffect(effect);
        Assert.Equal(effect, resources.Effect);
        Assert.Equal($"FF8_EN:{profile.AnimationAddress:X}", resources.Animation.Identity);
        Assert.Equal(cameraLength, resources.Animation.Data.Length);
        Assert.Equal(2, BinaryPrimitives.ReadUInt16LittleEndian(resources.Animation.Data.AsSpan()));
        Assert.Equal(cameraLength, BinaryPrimitives.ReadUInt16LittleEndian(resources.Animation.Data.AsSpan().Slice(6)));
        Assert.Equal(sound, resources.Sound);
        Assert.Equal(2504, resources.WaveVertices.Length);
        Assert.Equal(10600, resources.WaveMesh.Length);
        Assert.Equal(312u, Dword(resources.WaveMesh.AsSpan(), 4));
        Assert.Equal(2504u, Dword(resources.WaveMesh.AsSpan(), 0));
        int cursor = 2504;
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < 8; group++)
        {
            int count = checked((int)Dword(resources.WaveMesh.AsSpan(), cursor));
            Assert.Equal(group == 3 ? 48 : group == 7 ? 192 : 0, count);
            cursor += 4 + count * strides[group];
        }
        Assert.Equal(resources.WaveMesh.Length, cursor);
        Assert.Equal(6, resources.Sprites.Count);
        foreach (uint canonical in new uint[] { 0x15A38DC, 0x15A3A3C, 0x15A3B9C, 0x15A3CFC, 0x15A3E5C, 0x15A4008 })
        {
            uint address = profile.SpriteAddress(canonical);
            var sprite = resources.Sprites[address];
            int frames = canonical == 0x15A3E5C ? 16 : 13;
            Assert.Equal(frames, BinaryPrimitives.ReadUInt16LittleEndian(sprite.AsSpan().Slice(8)));
            NativeBattleModuleResource decoded = new(0, address, 0, 0, sprite);
            for (int phase = 0; phase < frames; phase++)
            {
                _ = decoded.ProjectedFrame571C80(phase);
            }
        }
        if (effect == 72)
        {
            var dat = resources.CasterDat72!.Value;
            Assert.Equal(7028, dat.Length);
            Assert.Equal(3u, Dword(dat.AsSpan(), 0));
            int first = NativeBattleDatDirectory.Section(dat.AsSpan(), 0);
            int geometry = NativeBattleDatDirectory.Section(dat.AsSpan(), 1);
            int animation = NativeBattleDatDirectory.Section(dat.AsSpan(), 2);
            int end = NativeBattleDatDirectory.Section(dat.AsSpan(), 3);
            Assert.InRange(first, 16, geometry);
            Assert.InRange(geometry, first, animation);
            Assert.InRange(animation, geometry, end);
            Assert.Equal(dat.Length, end);
        }
        else
        {
            Assert.Null(resources.CasterDat72);
        }
    }

    [Fact]
    public void Cold_loader_owners_and_independent_JSON_keep_mutable_mesh_and_shared_bytes_separate()
    {
        using GameAssets game = OpenGame();
        NativeBattleAquaResources first = game.ReadNativeAquaResources(55);
        NativeBattleAquaResources second = game.ReadNativeAquaResources(72);
        Assert.Equal(first.WaveMesh.ToArray(), second.WaveMesh.ToArray());
        Assert.Equal(first.WaveVertices.ToArray(), second.WaveVertices.ToArray());
        NativeBattleEffectMemory cold = Assert.IsType<NativeBattleEffectMemory>(game.ReadColdProcessNativeBattleMemory().EffectMemory);
        NativeBattleAquaState enemy = Assert.IsType<NativeBattleAquaState>(cold.Aqua55);
        NativeBattleAquaState blue = Assert.IsType<NativeBattleAquaState>(cold.Aqua72);
        Assert.Equal(new byte[84], enemy.Shared84.ToArray());
        Assert.Equal(new byte[84], blue.Shared84.ToArray());
        Assert.Equal(first.WaveMesh.ToArray(), enemy.WaveMesh!.Value.ToArray());
        Assert.Equal(second.WaveMesh.ToArray(), blue.WaveMesh!.Value.ToArray());
        NativeBattleAquaState changed = enemy.WithWord(0x44, 2) with { WaveMesh = enemy.WaveMesh.Value.SetItem(12, unchecked((byte)(enemy.WaveMesh.Value[12] ^ 0x55))) };
        Assert.Equal((short)0, blue.Word(0x44));
        Assert.Equal(second.WaveMesh.ToArray(), blue.WaveMesh.Value.ToArray());
        Assert.Equal(first.WaveMesh.ToArray(), enemy.WaveMesh.Value.ToArray());
        Assert.NotEqual(enemy.WaveMesh.Value[12], changed.WaveMesh!.Value[12]);
        NativeBattleAquaState restored = JsonSerializer.Deserialize<NativeBattleAquaState>(JsonSerializer.Serialize(changed))!;
        Assert.Equal(JsonSerializer.Serialize(changed), JsonSerializer.Serialize(restored));
        Assert.Equal((ushort)55, restored.Effect);
        Assert.Equal((short)2, restored.Word(0x44));
        Assert.Equal(first.Sprites[0x15A38DC].ToArray(), second.Sprites[0x1589C30].ToArray());
    }

    private static uint Dword(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset));

    private static GameAssets OpenGame()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources to check Aqua module ownership.");
        return new(directory!);
    }
}
