using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleBadBreath300ResourcesTests
{
    [Fact]
    public void Original_payloads_close_mesh_groups_ACT_sprite_camera_and_actual_lookup_table()
    {
        using GameAssets game = Open();
        NativeBattleBadBreath300Resources resources = game.ReadNativeBadBreath300Resources();
        Assert.Same(resources, game.ReadNativeBadBreath300Resources());
        NativeBattleBadBreath300ResourcesReader.ValidatePayloads(resources);
        Assert.Equal(0xC7C, resources.Target.Data.Length);
        Assert.Equal(480, resources.ParticleSprite.Bytes.Length);
        Assert.Equal(372, resources.Camera.Data.Length);
        Assert.Equal(0x1B74, resources.CasterDat.Data.Length);
        Assert.Equal(0x55781u, resources.Sound);
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
        NativeBattleBadBreath300Resources restored = JsonSerializer.Deserialize<NativeBattleBadBreath300Resources>(JsonSerializer.Serialize(resources))!;
        NativeBattleBadBreath300ResourcesReader.ValidatePayloads(restored);
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restored));
    }

    [Fact]
    public void Cold_sources_seed_once_and_warm_or_unknown_entry_never_reconstructs_them()
    {
        using GameAssets game = Open();
        NativeBattleBadBreath300Resources resources = game.ReadNativeBadBreath300Resources();
        NativeBattleBadBreath300State cold = NativeBattleBadBreath300State.FromColdProcess().SeedColdSources(resources);
        Assert.False(cold.PendingColdSources);
        ImmutableArray<byte> normalized = NativeBattleBadBreath300Controller.Normalize6B7CB0(cold.ReadMesh(NativeBattleBadBreath300Profile.MeshAAddress));
        NativeBattleBadBreath300State warm = cold.WriteMesh(NativeBattleBadBreath300Profile.MeshAAddress, normalized);
        Assert.Same(warm, warm.SeedColdSources(resources));
        Assert.Equal(normalized, warm.ReadMesh(NativeBattleBadBreath300Profile.MeshAAddress));
        NativeBattleBadBreath300State unknown = NativeBattleBadBreath300State.FromUnknown();
        Assert.Same(unknown, unknown.SeedColdSources(resources));
        Assert.Throws<NotSupportedException>(() => unknown.ReadMesh(NativeBattleBadBreath300Profile.MeshAAddress));
        NativeBattleBadBreath300State restored = JsonSerializer.Deserialize<NativeBattleBadBreath300State>(JsonSerializer.Serialize(warm))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(warm), JsonSerializer.Serialize(restored));
        Assert.Equal(normalized.AsSpan().ToArray(), NativeBattleBadBreath300Controller.Normalize6B7CB0(normalized).AsSpan().ToArray());
    }

    [Fact]
    public void Original_target_ACT_uses_the_actual_296_byte_workspace_and_26_tick_programme()
    {
        using GameAssets game = Open();
        NativeBattleBadBreath300Resources resources = game.ReadNativeBadBreath300Resources();
        NativeBattleGfAnimationWorkspace workspace = new NativeBattleGfAnimationWorkspace(new byte[0x128].ToImmutableArray()).Initialize7016B0(resources.Target, 0x128);
        int calls = 0;
        for (int tick = 0; tick < 26; tick++)
        {
            NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(resources.Target, 0, (current, node) =>
            {
                calls++;
                Assert.Equal(0, node.GeometryIndex);
                return current;
            });
            Assert.Equal(25 - tick, advanced.Remaining);
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
        NativeBattleBadBreath300Resources resources = game.ReadNativeBadBreath300Resources();
        NativeBattleBadBreath300Resources altered = selected == 0 ? resources with { MeshAInitial = resources.MeshAInitial.RemoveAt(resources.MeshAInitial.Length - 1) } : selected == 1 ? resources with { Target = resources.Target with { Data = resources.Target.Data.RemoveAt(resources.Target.Data.Length - 1) } } : resources with { TrigTable = resources.TrigTable.RemoveAt(resources.TrigTable.Length - 1) };
        Assert.Throws<InvalidDataException>(() => NativeBattleBadBreath300ResourcesReader.ValidatePayloads(altered));
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select original resources to validate Bad Breath ingress.");
        return new(directory!);
    }
}
