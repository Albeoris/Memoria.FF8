using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleGilgameshSwordParticleResourcesTests
{
    [Fact]
    public void Original_sword_particles_use_complete_sources_current_sword_bones_and_JSON_next()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleGilgameshSwordParticleResources resources = game.ReadNativeGilgameshSwordParticleResources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeGilgameshSwordParticleResources());
        NativeExecutableMemory memory = new(game.ReadNativeExecutable());
        Assert.Equal(memory.Read(0xCC8E64, 0xF8), resources.Mesh.Bytes.ToArray());
        Assert.Equal(memory.Read(0xCC6C34, 0x194), resources.Sprite.Bytes.ToArray());
        Assert.Equal(new int[] { 0, 3, 1, 2 }, resources.SwordOrder.ToArray());
        byte[] order = memory.Read(0xCD0480, 16);
        Assert.Equal(Enumerable.Range(0, 4).Select(index => BinaryPrimitives.ReadInt32LittleEndian(order.AsSpan(index * 4))), resources.SwordOrder);
        NativeBattleGilgameshModelResources models = game.ReadNativeGilgameshModelResources();
        NativeBattleGilgameshSwordParticleResources restoredResources = RoundTrip(resources);
        restoredResources.Validate();
        NativeBattleActionMatrix modelWorld = NativeBattleGfMatrices.Euler56CE30(new(0, 0, 0));
        NativeBattleVector ReadBone(int sword, byte selector, short fraction)
        {
            Assert.Equal((byte)1, selector);
            Assert.Equal((short)4096, fraction);
            NativeBattleEffectOwnedModelResource model = models.StaticModels[sword];
            return NativeBattlePoseMatrices.ReadEffectOwnedBone(model.PoseSource, model.PoseSource.Sample(3, 1, 0), modelWorld, selector, fraction);
        }
        NativeBattleActionMatrix view = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 16000, 4096);
        NativeBattleCameraProjection projection = new(null, 288, 160, 108);
        foreach (uint callback in new uint[] { 0x5948A0, 0x594CA0 })
        {
            NativeBattleModulePoolTask task = new NativeBattleModulePoolTask(1, 0, null, callback, new byte[24].ToImmutableArray()).WriteWord(28, 1).WriteWord(30, 0);
            NativeBattleGilgameshSwordParticleStep first = NativeBattleGilgameshSwordParticles.Step(task, NativeBattleEffectArena.Clear(), new(1), resources, view, projection, 0, ReadBone);
            Assert.Equal(callback == 0x5948A0 ? 6 : 4, first.Survivors);
            NativeBattleGilgameshSwordParticleStep restoredFirst = NativeBattleSnapshotJson.Deserialize<NativeBattleGilgameshSwordParticleStep>(NativeBattleSnapshotJson.Serialize(first))!;
            NativeBattleGilgameshSwordParticleStep expected = NativeBattleGilgameshSwordParticles.Step(first.UpdatedTask, first.Arena, first.Random, resources, view, projection, 0, ReadBone);
            NativeBattleGilgameshSwordParticleStep actual = NativeBattleGilgameshSwordParticles.Step(restoredFirst.UpdatedTask, restoredFirst.Arena, restoredFirst.Random, restoredResources, view, projection, 0, ReadBone);
            Assert.Equal(NativeBattleSnapshotJson.Serialize(expected), NativeBattleSnapshotJson.Serialize(actual));
        }
        Assert.Throws<InvalidDataException>(() => (resources with { SwordOrder = [0, 1, 2, 3] }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Mesh = resources.Mesh with { Bytes = resources.Mesh.Bytes.RemoveAt(0) } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Sprite = resources.Sprite with { Bytes = resources.Sprite.Bytes.RemoveAt(0) } }).Validate());
    }

    [Fact]
    public void Complete_effect_aggregate_preserves_required_resource_owners_without_snapshot_hydration()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleGilgameshEffectResources resources = game.ReadNativeGilgameshEffectResources();
        Assert.Same(game.ReadNativeGilgameshSwordParticleResources(), resources.SwordParticles);
        string json = NativeBattleSnapshotJson.Serialize(resources);
        Assert.Equal(json, NativeBattleSnapshotJson.Serialize(RoundTrip(resources)));
        foreach (string property in new[] { nameof(NativeBattleGilgameshEffectResources.Files), nameof(NativeBattleGilgameshEffectResources.InitialStreamNumber), nameof(NativeBattleGilgameshEffectResources.SwordParticles) })
        {
            JsonObject missing = JsonNode.Parse(json)!.AsObject();
            missing.Remove(property);
            Assert.Throws<JsonException>(() => NativeBattleSnapshotJson.Deserialize<NativeBattleGilgameshEffectResources>(missing.ToJsonString()));
        }
    }

    private static T RoundTrip<T>(T value) => NativeBattleSnapshotJson.Deserialize<T>(NativeBattleSnapshotJson.Serialize(value))!;
}
