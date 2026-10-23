using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleMiniPulseCannon286Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleMiniPulseCannon286ResourcesTests
{
    [Fact]
    public void Original_programmes_have_exact_workspaces_geometry_and_projected_sprites()
    {
        using GameAssets game = Open();
        var resources = game.ReadNativeMiniPulseCannon286Resources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeMiniPulseCannon286Resources());
        Assert.Equal(0x641C5u, resources.Sound);
        Assert.Equal(new short[] { -1718, 0, -737, 2 }, Enumerable.Range(0, 4).Select(index => BinaryPrimitives.ReadInt16LittleEndian(resources.SourcePoint8.AsSpan().Slice(index * 2))).ToArray());
        int maximum = 0;
        var selectedGroups = new HashSet<int>();
        foreach (var source in new[] { resources.ProgrammeA, resources.ProgrammeB, resources.ProgrammeC, resources.ProgrammeD })
        {
            int programme = source.Address == ProgrammeAAddress ? 0 : source.Address == ProgrammeBAddress ? 1 : source.Address == ProgrammeCAddress ? 2 : 3;
            int[] lengths = [ProgrammeALength, ProgrammeBLength, ProgrammeCLength, ProgrammeDLength];
            int[] nodeCounts = [7, 3, 9, 2];
            int[] durations = [42, 42, 20, 18];
            int[] workspaces = [ProgrammeAWorkspaceLength, ProgrammeBWorkspaceLength, ProgrammeCWorkspaceLength, ProgrammeDWorkspaceLength];
            int[] geometries = [5, 3, 7, 2];
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
        Assert.Equal(new[] { 2, 3, 4, 5, 6, 7 }, selectedGroups.Order().ToArray());
        Assert.Equal(9, resources.Sprite.Kind);
        Assert.Throws<InvalidDataException>(() => resources.Sprite.Validate());
        _ = resources.ReadSprite(SpriteAddress).ProjectedFrame571C80(9);
        NativeBattleGfAnimationWorkspace terminalWorkspace = new(new byte[ProgrammeCWorkspaceLength].ToImmutableArray());
        terminalWorkspace = terminalWorkspace.Initialize7016B0(resources.ProgrammeC, ProgrammeCWorkspaceLength);
        List<object> terminals = [];
        for (int frame = 0; frame < 20; frame++)
        {
            int captured = frame;
            int callbacks = 0;
            NativeBattleGfAnimationAdvance advanced = terminalWorkspace.Advance701970(resources.ProgrammeC, 0, (retained, node) =>
            {
                callbacks++;
                if (captured is 18 or 19)
                {
                    bool invisible = node.Vector24 is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0;
                    terminals.Add(new { Frame = captured, node.NodeIndex, Scale = node.Vector24, Cue = node.Scalar36, Color = node.Color32, Invisible = invisible });
                    Assert.True(invisible, JsonSerializer.Serialize(terminals[^1]));
                }
                return retained;
            });
            Assert.Equal(9, callbacks);
            terminalWorkspace = advanced.Workspace;
        }
        Assert.Equal(18, terminals.Count);
    }

    [Fact]
    public void Original_resources_round_trip_without_becoming_mutable_state_owners()
    {
        using GameAssets game = Open();
        var resources = game.ReadNativeMiniPulseCannon286Resources();
        var restored = JsonSerializer.Deserialize<NativeBattleMiniPulseCannon286Resources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restored));
        var state = NativeBattleMiniPulseCannon286State.FromUnknown().WriteDword(CursorGlobal, ArenaAddress + FirstPacketOffset);
        Assert.Throws<NotSupportedException>(() => state.ReadWord(BaseAddress));
        Assert.Throws<InvalidDataException>(() => state.ReadWord(ProgrammeAAddress));
        Assert.Throws<NotSupportedException>(() => resources.ReadGeometry(ArenaAddress + ScratchOffset));
        Assert.Throws<NotSupportedException>(() => resources.ReadProgramme(CameraAddress));
        Assert.Throws<NotSupportedException>(() => resources.ReadSprite(CameraAddress));
    }

    [Fact]
    public void Changed_source_extents_directory_faces_camera_sound_and_muzzle_fail()
    {
        using GameAssets game = Open();
        var resources = game.ReadNativeMiniPulseCannon286Resources();
        Assert.Throws<InvalidDataException>(() => (resources with { ProgrammeA = resources.ProgrammeA with { Data = resources.ProgrammeA.Data.RemoveAt(0) } }).Validate());
        byte[] bytes = resources.ProgrammeB.Data.ToArray();
        int table = checked((int)Dword(resources.ProgrammeB.Data, 4));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(table), 4);
        Assert.Throws<InvalidDataException>(() => (resources with { ProgrammeB = resources.ProgrammeB with { Data = bytes.ToImmutableArray() } }).Validate());
        bytes = resources.ProgrammeA.Data.ToArray();
        int geometry = checked((int)Dword(resources.ProgrammeA.Data, 8));
        int polygons = checked(geometry + (int)Dword(resources.ProgrammeA.Data, geometry));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(polygons + 24), 0xFFFF);
        Assert.Throws<InvalidDataException>(() => (resources with { ProgrammeA = resources.ProgrammeA with { Data = bytes.ToImmutableArray() } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Sound = 0 }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Camera = resources.Camera with { Data = resources.Camera.Data.RemoveAt(0) } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { SourcePoint8 = new byte[8].ToImmutableArray() }).Validate());
    }

    private static uint Dword(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset));

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original Mini Pulse Cannon resources.");
        return new(directory!);
    }
}
