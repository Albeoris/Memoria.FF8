using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleGilgameshParticleResourcesTests
{
    [Fact]
    public void Original_four_projected_sprites_preserve_complete_kinds_frames_and_extents()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleGilgameshParticleResources resources = game.ReadNativeGilgameshParticleResources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeGilgameshParticleResources());
        Assert.Equal(new uint[] { 0xCC6730, 0xCC6954, 0xCC6F74, 0xCC6C0C }, new[] { resources.SourceA.Address, resources.SourceB.Address, resources.Sparks.Address, resources.Streak.Address });
        Assert.Equal(new[] { 0x224, 0x210, 0x1AC, 0x28 }, new[] { resources.SourceA.Bytes.Length, resources.SourceB.Bytes.Length, resources.Sparks.Bytes.Length, resources.Streak.Bytes.Length });
        Assert.Equal(new byte[] { 9, 9, 6, 6 }, new[] { resources.SourceA.Kind, resources.SourceB.Kind, resources.Sparks.Kind, resources.Streak.Kind });
        foreach (NativeBattleModuleResource resource in new[] { resources.SourceA, resources.SourceB, resources.Sparks, resources.Streak })
        {
            int frames = BinaryPrimitives.ReadUInt16LittleEndian(resource.Bytes.AsSpan().Slice(8));
            Assert.Equal(resource == resources.Streak ? 1 : 16, frames);
            Assert.True(resource.ProjectedFrame571C80(frames - 1).NextOffset < 0);
        }
        Assert.Throws<NotSupportedException>(() => NativeBattleGilgameshParticleResourcesReader.Read(new byte[128]));
    }

    [Theory]
    [InlineData(0x5915C0u, 0)]
    [InlineData(0x5915C0u, 1)]
    [InlineData(0x591850u, 0)]
    [InlineData(0x591850u, 1)]
    [InlineData(0x591B80u, 0)]
    public void Original_particle_spawning_and_all_reached_frames_complete_with_identical_json_next(uint callback, ushort source)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleGilgameshParticleResources resources = game.ReadNativeGilgameshParticleResources();
        NativeBattleGilgameshParticleResources restoredResources = JsonSerializer.Deserialize<NativeBattleGilgameshParticleResources>(JsonSerializer.Serialize(resources))!;
        NativeBattleModulePoolTask task = new NativeBattleModulePoolTask(1, 0, null, callback, new byte[24].ToImmutableArray()).WriteWord(14, source).WriteWord(22, 1);
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        NativeCrtRandomState random = new(1);
        NativeBattleActionMatrix view = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 30000, 4096);
        NativeBattleCameraProjection projection = new(null, 288, 160, 108);
        NativeBattleGilgameshParticleStep first = NativeBattleGilgameshParticles.Step(task, arena, random, resources, view, projection, 0);
        NativeBattleGilgameshParticleStep restored = JsonSerializer.Deserialize<NativeBattleGilgameshParticleStep>(JsonSerializer.Serialize(first))!;
        Assert.NotEmpty(first.Packets);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleGilgameshParticles.Step(first.UpdatedTask, first.Arena, first.Random, resources, view, projection, 0)), JsonSerializer.Serialize(NativeBattleGilgameshParticles.Step(restored.UpdatedTask, restored.Arena, restored.Random, restoredResources, view, projection, 0)));
        NativeBattleGilgameshParticleStep current = first;
        for (int frame = 1; frame < 24 && current.Result == 0; frame++)
        {
            current = NativeBattleGilgameshParticles.Step(current.UpdatedTask, current.Arena, current.Random, resources, view, projection, 0);
        }
        Assert.Equal(2, current.Result);
        Assert.Equal(0, current.Survivors);
    }
}

