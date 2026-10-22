using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleBadBreath131ResourcesTests
{
    [Fact]
    public void Original_payloads_close_mesh_groups_ACT_sprite_camera_and_actual_lookup_table()
    {
        using GameAssets game = Open();
        NativeBattleBadBreath131Resources resources = game.ReadNativeBadBreath131Resources();
        Assert.Same(resources, game.ReadNativeBadBreath131Resources());
        NativeBattleBadBreath131ResourcesReader.ValidatePayloads(resources);
        Assert.Equal(0xC7C, resources.Target.Data.Length);
        Assert.Equal(480, resources.ParticleSprite.Bytes.Length);
        Assert.Equal(420, resources.Camera.Data.Length);
        Assert.Equal(0x64194u, resources.Sound);
        Assert.Equal(8192, resources.TrigTable.Length);
        Assert.Equal((short)4095, resources.TrigTable[3]);
        Assert.NotEqual(NativeBattleCameraMath.Cosine(1), (int)resources.TrigTable[3]);
        foreach (ImmutableArray<byte> mesh in new[] { resources.MeshAInitial, resources.MeshBInitial })
        {
            Assert.Equal(5248, mesh.Length);
            Assert.Equal(165, BinaryPrimitives.ReadInt32LittleEndian(mesh.AsSpan().Slice(4)));
            int cursor = 1328;
            int[] expected = [0, 0, 0, 36, 0, 0, 0, 84];
            for (int group = 0; group < 8; group++)
            {
                int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.AsSpan().Slice(cursor));
                Assert.Equal(expected[group], count);
                cursor += 4 + count * (group == 3 ? 24 : 36);
            }
            Assert.Equal(mesh.Length, cursor);
        }
        NativeBattleBadBreath131Resources restored = JsonSerializer.Deserialize<NativeBattleBadBreath131Resources>(JsonSerializer.Serialize(resources))!;
        NativeBattleBadBreath131ResourcesReader.ValidatePayloads(restored);
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restored));
    }

    [Fact]
    public void Cold_mesh_seed_is_once_only_and_preserves_both_warm_mutations_and_JSON()
    {
        using GameAssets game = Open();
        NativeBattleBadBreath131Resources resources = game.ReadNativeBadBreath131Resources();
        NativeBattleBadBreath131State cold = NativeBattleBadBreath131State.FromColdProcess().SeedColdSources(resources);
        Assert.False(cold.PendingColdSources);
        Assert.Equal(resources.MeshAInitial, cold.ReadMesh(NativeBattleBadBreath131Profile.MeshAAddress));
        Assert.Equal(resources.MeshBInitial, cold.ReadMesh(NativeBattleBadBreath131Profile.MeshBAddress));
        ImmutableArray<byte> changedA = cold.ReadMesh(NativeBattleBadBreath131Profile.MeshAAddress).SetItem(8, 123);
        ImmutableArray<byte> changedB = cold.ReadMesh(NativeBattleBadBreath131Profile.MeshBAddress).SetItem(14, 45);
        NativeBattleBadBreath131State warm = cold.WriteMesh(NativeBattleBadBreath131Profile.MeshAAddress, changedA).WriteMesh(NativeBattleBadBreath131Profile.MeshBAddress, changedB);
        Assert.Same(warm, warm.SeedColdSources(resources));
        NativeBattleBadBreath131State restored = JsonSerializer.Deserialize<NativeBattleBadBreath131State>(JsonSerializer.Serialize(warm))!;
        restored.Validate();
        Assert.Same(restored, restored.SeedColdSources(resources));
        Assert.Equal(changedA.ToArray(), restored.ReadMesh(NativeBattleBadBreath131Profile.MeshAAddress).ToArray());
        Assert.Equal(changedB.ToArray(), restored.ReadMesh(NativeBattleBadBreath131Profile.MeshBAddress).ToArray());
        Assert.Throws<NotSupportedException>(() => NativeBattleBadBreath131State.FromUnknown().SeedColdSources(resources));
        Assert.Throws<NotSupportedException>(() => NativeBattleBadBreath131State.FromUnknown().ReadMesh(NativeBattleBadBreath131Profile.MeshAAddress));
    }

    [Fact]
    public void Original_target_ACT_uses_the_actual_296_byte_workspace_and_34_tick_programme()
    {
        using GameAssets game = Open();
        NativeBattleBadBreath131Resources resources = game.ReadNativeBadBreath131Resources();
        NativeBattleGfAnimationWorkspace workspace = new NativeBattleGfAnimationWorkspace(new byte[0x128].ToImmutableArray()).Initialize7016B0(resources.Target, 0x128);
        int calls = 0;
        for (int tick = 0; tick < 34; tick++)
        {
            NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(resources.Target, 0, (current, node) =>
            {
                calls++;
                Assert.Equal(0, node.GeometryIndex);
                return current;
            });
            Assert.Equal(33 - tick, advanced.Remaining);
            workspace = advanced.Workspace;
        }
        Assert.True(calls > 0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Malformed_actual_resource_boundaries_fail_before_runtime(int selected)
    {
        using GameAssets game = Open();
        NativeBattleBadBreath131Resources resources = game.ReadNativeBadBreath131Resources();
        NativeBattleBadBreath131Resources altered = selected == 0 ? resources with { MeshAInitial = resources.MeshAInitial.RemoveAt(resources.MeshAInitial.Length - 1) } : selected == 1 ? resources with { Target = resources.Target with { Data = resources.Target.Data.RemoveAt(resources.Target.Data.Length - 1) } } : resources with { TrigTable = resources.TrigTable.RemoveAt(resources.TrigTable.Length - 1) };
        Assert.Throws<InvalidDataException>(() => NativeBattleBadBreath131ResourcesReader.ValidatePayloads(altered));
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select original resources to validate Bad Breath ingress.");
        return new(directory!);
    }
}
