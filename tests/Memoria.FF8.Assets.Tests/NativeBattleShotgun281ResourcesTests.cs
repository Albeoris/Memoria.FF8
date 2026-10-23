using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleShotgun281Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleShotgun281ResourcesTests
{
    [Fact]
    public void Original_programmes_have_exact_workspaces_geometry_and_projected_sprites()
    {
        using GameAssets game = Open();
        var resources = game.ReadNativeShotgun281Resources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeShotgun281Resources());
        Assert.Equal(0x61B74u, resources.Sound);
        Assert.Equal(new short[] { -388, -2563, -1896, 23 }, Enumerable.Range(0, 4).Select(index => BinaryPrimitives.ReadInt16LittleEndian(resources.MuzzlePoint8.AsSpan().Slice(index * 2))).ToArray());
        int maximum = 0;
        var selectedGroups = new HashSet<int>();
        foreach (var source in new[] { resources.ProgrammeA, resources.ProgrammeB })
        {
            bool first = source.Address == ProgrammeAAddress;
            Assert.Equal(first ? ProgrammeALength : ProgrammeBLength, source.Data.Length);
            int table = checked((int)Dword(source.Data, 4));
            Assert.Equal(first ? 6u : 4u, Dword(source.Data, table));
            Assert.Equal(first ? 14u : 32u, Dword(source.Data, table + 4));
            int workspace = first ? ProgrammeAWorkspaceLength : ProgrammeBWorkspaceLength;
            var initialized = new NativeBattleGfAnimationWorkspace(new byte[workspace].ToImmutableArray()).Initialize7016B0(source, workspace);
            Assert.Equal(source.Address, initialized.ResourceAddress);
            int count = checked((int)Dword(source.Data, 0));
            Assert.Equal(first ? 5 : 3, count);
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
        Assert.Equal(new[] { 3, 4, 5, 6, 7 }, selectedGroups.Order().ToArray());
        Assert.Equal(8, resources.SpriteA.Kind);
        Assert.Equal(8, resources.SpriteB.Kind);
        Assert.Throws<InvalidDataException>(() => resources.SpriteA.Validate());
        _ = resources.ReadSprite(SpriteAAddress).ProjectedFrame571C80(7);
        _ = resources.ReadSprite(SpriteBAddress).ProjectedFrame571C80(9);
    }

    [Fact]
    public void Original_resources_round_trip_without_becoming_mutable_state_owners()
    {
        using GameAssets game = Open();
        var resources = game.ReadNativeShotgun281Resources();
        var restored = JsonSerializer.Deserialize<NativeBattleShotgun281Resources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restored));
        var state = NativeBattleShotgun281State.FromUnknown().WriteDword(CursorGlobal, ArenaAddress + FirstPacketOffset);
        Assert.Throws<NotSupportedException>(() => state.ReadWord(BasisAAddress));
        Assert.Throws<InvalidDataException>(() => state.ReadWord(ProgrammeAAddress));
        Assert.Throws<NotSupportedException>(() => resources.ReadGeometry(ArenaAddress + ScratchOffset));
        Assert.Throws<NotSupportedException>(() => resources.ReadProgramme(CameraAddress));
        Assert.Throws<NotSupportedException>(() => resources.ReadSprite(CameraAddress));
    }

    [Fact]
    public void Changed_source_extents_directory_faces_camera_sound_and_muzzle_fail()
    {
        using GameAssets game = Open();
        var resources = game.ReadNativeShotgun281Resources();
        Assert.Throws<InvalidDataException>(() => (resources with { ProgrammeA = resources.ProgrammeA with { Data = resources.ProgrammeA.Data.RemoveAt(0) } }).Validate());
        byte[] bytes = resources.ProgrammeB.Data.ToArray();
        int table = checked((int)Dword(resources.ProgrammeB.Data, 4));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(table), 5);
        Assert.Throws<InvalidDataException>(() => (resources with { ProgrammeB = resources.ProgrammeB with { Data = bytes.ToImmutableArray() } }).Validate());
        bytes = resources.ProgrammeA.Data.ToArray();
        int geometry = checked((int)Dword(resources.ProgrammeA.Data, 8));
        int polygons = checked(geometry + (int)Dword(resources.ProgrammeA.Data, geometry));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(polygons + 16 + 4), 0xFFFF);
        Assert.Throws<InvalidDataException>(() => (resources with { ProgrammeA = resources.ProgrammeA with { Data = bytes.ToImmutableArray() } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Sound = 0 }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Camera = resources.Camera with { Data = resources.Camera.Data.RemoveAt(0) } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { MuzzlePoint8 = new byte[8].ToImmutableArray() }).Validate());
    }

    private static uint Dword(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset));

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original Shotgun resources.");
        return new(directory!);
    }
}
