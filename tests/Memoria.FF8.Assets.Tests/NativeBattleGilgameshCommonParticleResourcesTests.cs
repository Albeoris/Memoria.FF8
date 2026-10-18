using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleGilgameshCommonParticleResourcesTests
{
    [Fact]
    public void Original_common_sprite_and_nested_mesh_preserve_complete_sources_and_JSON_next()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleGilgameshCommonParticleResources resources = game.ReadNativeGilgameshCommonParticleResources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeGilgameshCommonParticleResources());
        NativeExecutableMemory memory = new(game.ReadNativeExecutable());
        Assert.Equal(memory.Read(0xCC6DC8, 0x1AC), resources.Sprite.Bytes.ToArray());
        Assert.Equal(memory.Read(0xCCCB1C, 0x12E8), resources.NestedMesh.Bytes.ToArray());
        Assert.Equal(0xCC6DC8u, resources.Sprite.Address);
        Assert.Equal(0xCCCB1Cu, resources.NestedMesh.Address);
        Assert.Equal(168, BinaryPrimitives.ReadInt32LittleEndian(resources.NestedMesh.Bytes.AsSpan().Slice(4)));
        NativeBattleGilgameshCommonParticleResources restored = JsonSerializer.Deserialize<NativeBattleGilgameshCommonParticleResources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        NativeBattleActionMatrix view = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 16000, 4096);
        NativeBattleCameraProjection projection = new(null, 288, 160, 108);
        foreach (uint callback in new uint[] { 0x593770, 0x5939C0 })
        {
            NativeBattleModulePoolTask task = new(1, 0, null, callback, new byte[24].ToImmutableArray());
            NativeBattleGilgameshParticleStep first = NativeBattleGilgameshCommonParticles.Step(task, NativeBattleEffectArena.Clear(), new(1), resources, view, projection, 0);
            Assert.Equal(callback == 0x593770 ? 8 : 12, first.Packets.Length);
            NativeBattleGilgameshParticleStep expected = NativeBattleGilgameshCommonParticles.Step(first.UpdatedTask, first.Arena, first.Random, resources, view, projection, 0);
            NativeBattleGilgameshParticleStep actual = NativeBattleGilgameshCommonParticles.Step(JsonSerializer.Deserialize<NativeBattleModulePoolTask>(JsonSerializer.Serialize(first.UpdatedTask))!, JsonSerializer.Deserialize<NativeBattleEffectArena>(JsonSerializer.Serialize(first.Arena))!, first.Random, restored, view, projection, 0);
            Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
        }
        NativeBattleModulePoolTask nested = new NativeBattleModulePoolTask(1, 0, null, 0x593D20, new byte[24].ToImmutableArray()).WriteWord(12, 18).WriteWord(28, 4096).WriteWord(32, 4096).WriteWord(34, 186);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleGilgameshCommonParticles.Step593D20(nested, resources, view, projection, 0)), JsonSerializer.Serialize(NativeBattleGilgameshCommonParticles.Step593D20(nested, restored, view, projection, 0)));
        Assert.Throws<InvalidDataException>(() => (resources with { Sprite = resources.Sprite with { Bytes = resources.Sprite.Bytes.RemoveAt(0) } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { NestedMesh = resources.NestedMesh with { Bytes = resources.NestedMesh.Bytes.RemoveAt(0) } }).Validate());
    }
}
