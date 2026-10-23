using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEnemyMicroMissiles284Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEnemyMicroMissiles284ResourcesTests
{
    [Fact]
    public void Original_programmes_have_exact_workspaces_geometry_and_projected_sprites()
    {
        using GameAssets game = Open();
        var resources = game.ReadNativeEnemyMicroMissiles284Resources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeEnemyMicroMissiles284Resources());
        Assert.Equal(0x641CBu, resources.Sound);
        int maximum = 0;
        var selectedGroups = new HashSet<int>();
        foreach (var source in new[] { resources.Missile, resources.Impact, resources.WideImpact })
        {
            int programme = source.Address == MissileAddress ? 0 : source.Address == ImpactAddress ? 1 : 2;
            int[] lengths = [MissileLength, ImpactLength, WideImpactLength];
            int[] nodeCounts = [22, 2, 4];
            int[] durations = [45, 12, 16];
            int[] workspaces = [MissileWorkspaceLength, ImpactWorkspaceLength, WideImpactWorkspaceLength];
            int[] geometries = [3, 2, 3];
            Assert.Equal(lengths[programme], source.Data.Length);
            int table = checked((int)Dword(source.Data, 4));
            Assert.Equal((uint)nodeCounts[programme], Dword(source.Data, table));
            Assert.Equal((uint)durations[programme], Dword(source.Data, table + 4));
            int workspace = workspaces[programme];
            var initialized = new NativeBattleGfAnimationWorkspace(new byte[workspace].ToImmutableArray()).Initialize7016B0(source, workspace);
            Assert.Equal(source.Address, initialized.ResourceAddress);
            int count = checked((int)Dword(source.Data, 0));
            Assert.Equal(geometries[programme], count);
            for (int index = 0; index < count; index++)
            {
                int offset = checked((int)Dword(source.Data, 8 + index * 4));
                var mesh = resources.ReadGeometry(source.Address + (uint)offset);
                maximum = Math.Max(maximum, checked((int)Dword(mesh.Bytes, 4) * 8));
                Assert.Equal(source.Data.AsSpan().Slice(offset, mesh.Bytes.Length).ToArray(), mesh.Bytes.ToArray());
                int cursor = checked((int)Dword(mesh.Bytes, 0));
                int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
                for (int group = 0; group < 8; group++)
                {
                    uint faces = Dword(mesh.Bytes, cursor);
                    if (faces != 0)
                    {
                        selectedGroups.Add(group);
                    }
                    cursor = checked(cursor + 4 + (int)faces * strides[group]);
                }
                Assert.Equal(mesh.Bytes.Length, cursor);
            }
        }
        Assert.Equal(ScratchLength, maximum);
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5, 7 }, selectedGroups.Order().ToArray());
        Assert.Equal(8, resources.ParticleSprite.Kind);
        Assert.Equal(8, resources.TrailSprite.Kind);
        Assert.Equal(6, resources.ImpactSprite.Kind);
        _ = resources.ReadSprite(ParticleSpriteAddress).ProjectedFrame571C80(11);
        _ = resources.ReadSprite(TrailSpriteAddress).ProjectedFrame571C80(11);
        _ = resources.ReadSprite(ImpactSpriteAddress).ProjectedFrame571C80(13);
    }

    [Fact]
    public void Original_resources_round_trip_without_becoming_mutable_state_owners()
    {
        using GameAssets game = Open();
        var resources = game.ReadNativeEnemyMicroMissiles284Resources();
        var restored = JsonSerializer.Deserialize<NativeBattleEnemyMicroMissiles284Resources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restored));
        var accepted = game.ReadNativeMicroMissiles299Resources();
        Assert.Equal(accepted.WideImpact.Data.ToArray(), resources.WideImpact.Data.ToArray());
        Assert.Equal(accepted.ParticleSprite.Bytes.ToArray(), resources.ParticleSprite.Bytes.ToArray());
        Assert.Equal(accepted.TrailSprite.Bytes.ToArray(), resources.TrailSprite.Bytes.ToArray());
        Assert.Equal(accepted.ImpactSprite.Bytes.ToArray(), resources.ImpactSprite.Bytes.ToArray());
        Assert.False(accepted.Missile.Data.SequenceEqual(resources.Missile.Data));
        Assert.False(accepted.Impact.Data.SequenceEqual(resources.Impact.Data));
        var state = NativeBattleEnemyMicroMissiles284State.FromUnknown().WriteDword(CursorGlobal, ArenaAddress + FirstPacketOffset);
        Assert.Throws<NotSupportedException>(() => state.ReadWord(BaseAddress));
        Assert.Throws<InvalidDataException>(() => state.ReadWord(MissileAddress));
        Assert.Throws<NotSupportedException>(() => resources.ReadGeometry(ArenaAddress + ScratchOffset));
        Assert.Throws<NotSupportedException>(() => resources.ReadProgramme(CameraAddress));
        Assert.Throws<NotSupportedException>(() => resources.ReadSprite(CameraAddress));
    }

    [Fact]
    public void Changed_source_extents_directory_faces_camera_and_sound_fail()
    {
        using GameAssets game = Open();
        var resources = game.ReadNativeEnemyMicroMissiles284Resources();
        Assert.Throws<InvalidDataException>(() => (resources with { Missile = resources.Missile with { Data = resources.Missile.Data.RemoveAt(0) } }).Validate());
        byte[] bytes = resources.Impact.Data.ToArray();
        int table = checked((int)Dword(resources.Impact.Data, 4));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(table), 3);
        Assert.Throws<InvalidDataException>(() => (resources with { Impact = resources.Impact with { Data = bytes.ToImmutableArray() } }).Validate());
        bytes = resources.Missile.Data.ToArray();
        int geometry = checked((int)Dword(resources.Missile.Data, 8));
        int polygons = checked(geometry + (int)Dword(resources.Missile.Data, geometry));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(polygons + 8), 0xFFFF);
        Assert.Throws<InvalidDataException>(() => (resources with { Missile = resources.Missile with { Data = bytes.ToImmutableArray() } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Sound = 0 }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Camera = resources.Camera with { Data = resources.Camera.Data.RemoveAt(0) } }).Validate());
    }

    private static uint Dword(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset));

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original Enemy Micro Missiles resources.");
        return new(directory!);
    }
}
