using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEvilEye282Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEvilEye282ResourcesTests
{
    [Fact]
    public void Original_programmes_have_exact_workspaces_geometry_and_projected_sprites()
    {
        using GameAssets game = Open();
        var resources = game.ReadNativeEvilEye282Resources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeEvilEye282Resources());
        Assert.Equal(0x641AFu, resources.Sound);
        Assert.Equal(new short[] { -418, 0, -1701, 13 }, Enumerable.Range(0, 4).Select(index => BinaryPrimitives.ReadInt16LittleEndian(resources.SourcePoint8.AsSpan().Slice(index * 2))).ToArray());
        int maximum = 0;
        var selectedGroups = new HashSet<int>();
        foreach (var source in new[] { resources.ProgrammeA, resources.ProgrammeB })
        {
            bool first = source.Address == ProgrammeAAddress;
            Assert.Equal(first ? ProgrammeALength : ProgrammeBLength, source.Data.Length);
            int table = checked((int)Dword(source.Data, 4));
            Assert.Equal(first ? 12u : 5u, Dword(source.Data, table));
            Assert.Equal(first ? 21u : 32u, Dword(source.Data, table + 4));
            int workspace = first ? ProgrammeAWorkspaceLength : ProgrammeBWorkspaceLength;
            var initialized = new NativeBattleGfAnimationWorkspace(new byte[workspace].ToImmutableArray()).Initialize7016B0(source, workspace);
            Assert.Equal(source.Address, initialized.ResourceAddress);
            int count = checked((int)Dword(source.Data, 0));
            Assert.Equal(3, count);
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
        Assert.Equal(new[] { 4, 5, 6, 7 }, selectedGroups.Order().ToArray());
        Assert.Equal(8, resources.Sprite.Kind);
        Assert.Throws<InvalidDataException>(() => resources.Sprite.Validate());
        _ = resources.ReadSprite(SpriteAddress).ProjectedFrame571C80(15);
    }

    [Fact]
    public void Original_resources_round_trip_without_becoming_mutable_state_owners()
    {
        using GameAssets game = Open();
        var resources = game.ReadNativeEvilEye282Resources();
        var restored = JsonSerializer.Deserialize<NativeBattleEvilEye282Resources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restored));
        var state = NativeBattleEvilEye282State.FromUnknown().WriteDword(CursorGlobal, ArenaAddress + FirstPacketOffset);
        Assert.Throws<NotSupportedException>(() => state.ReadWord(NormalizedViewAddress));
        Assert.Throws<InvalidDataException>(() => state.ReadWord(ProgrammeAAddress));
        Assert.Throws<NotSupportedException>(() => resources.ReadGeometry(ArenaAddress + ScratchOffset));
        Assert.Throws<NotSupportedException>(() => resources.ReadProgramme(CameraAddress));
        Assert.Throws<NotSupportedException>(() => resources.ReadSprite(CameraAddress));
    }

    [Fact]
    public void Changed_source_extents_directory_faces_camera_sound_and_muzzle_fail()
    {
        using GameAssets game = Open();
        var resources = game.ReadNativeEvilEye282Resources();
        Assert.Throws<InvalidDataException>(() => (resources with { ProgrammeA = resources.ProgrammeA with { Data = resources.ProgrammeA.Data.RemoveAt(0) } }).Validate());
        byte[] bytes = resources.ProgrammeB.Data.ToArray();
        int table = checked((int)Dword(resources.ProgrammeB.Data, 4));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(table), 6);
        Assert.Throws<InvalidDataException>(() => (resources with { ProgrammeB = resources.ProgrammeB with { Data = bytes.ToImmutableArray() } }).Validate());
        bytes = resources.ProgrammeA.Data.ToArray();
        int geometry = checked((int)Dword(resources.ProgrammeA.Data, 8));
        int polygons = checked(geometry + (int)Dword(resources.ProgrammeA.Data, geometry));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(polygons + 32), 0xFFFF);
        Assert.Throws<InvalidDataException>(() => (resources with { ProgrammeA = resources.ProgrammeA with { Data = bytes.ToImmutableArray() } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Sound = 0 }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Camera = resources.Camera with { Data = resources.Camera.Data.RemoveAt(0) } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { SourcePoint8 = new byte[8].ToImmutableArray() }).Validate());
    }

    private static uint Dword(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset));

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original Evil-Eye resources.");
        return new(directory!);
    }
}
