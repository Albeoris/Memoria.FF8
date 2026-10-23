using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleSnipeLaser302Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleSnipeLaser302ResourcesTests
{
    [Fact]
    public void Original_programmes_keep_direct_geometry_separate_from_actual_morph_extent()
    {
        using GameAssets game = Open();
        var resources = game.ReadNativeSnipeLaser302Resources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeSnipeLaser302Resources());
        Assert.Equal(0x61B7Fu, resources.Sound);
        Assert.Equal(new short[] { -934, 1057, -4219, 11, -934, -1056, -4219, 11 }, Enumerable.Range(0, 8).Select(index => BinaryPrimitives.ReadInt16LittleEndian(resources.SourcePoints16.AsSpan().Slice(index * 2))).ToArray());
        int maximumDirect = 0;
        int maximumMorph = 0;
        HashSet<int> selectedGroups = [];
        NativeBattleGfAnimationSource[] sources = [resources.ProgrammeA, resources.ProgrammeB, resources.ProgrammeC, resources.ProgrammeD];
        int[] counts = [5, 2, 2, 2];
        int[] nodes = [12, 3, 5, 3];
        int[] frames = [38, 36, 24, 10];
        int[] workspaces = [ProgrammeAWorkspaceLength, ProgrammeBWorkspaceLength, ProgrammeCWorkspaceLength, ProgrammeDWorkspaceLength];
        int[] lengths = [ProgrammeALength, ProgrammeBLength, ProgrammeCLength, ProgrammeDLength];
        for (int programme = 0; programme < sources.Length; programme++)
        {
            var source = sources[programme];
            Assert.Equal(lengths[programme], source.Data.Length);
            Assert.Equal((uint)counts[programme], Dword(source.Data, 0));
            int table = checked((int)Dword(source.Data, 4));
            Assert.Equal((uint)nodes[programme], Dword(source.Data, table));
            Assert.Equal((uint)frames[programme], Dword(source.Data, table + 4));
            var workspace = new NativeBattleGfAnimationWorkspace(new byte[workspaces[programme]].ToImmutableArray()).Initialize7016B0(source, workspaces[programme]);
            Assert.Equal(0, workspace.Frame);
            Assert.Equal(source.Address, workspace.ResourceAddress);
            for (int geometry = 0; geometry < counts[programme]; geometry++)
            {
                int offset = checked((int)Dword(source.Data, 8 + geometry * 4));
                var mesh = resources.ReadGeometry(source.Address + (uint)offset);
                maximumDirect = Math.Max(maximumDirect, checked((int)Dword(mesh.Bytes, 4)));
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
            if (programme == 0)
            {
                continue;
            }
            for (int frame = 0; frame < frames[programme]; frame++)
            {
                workspace = workspace.Advance701970(source, 0, (retained, node) =>
                {
                    if (node.Word40 != node.Word42 && node.Scalar38 is not (0 or 4096))
                    {
                        int offset = checked((int)Dword(source.Data, 8 + node.GeometryIndex * 4));
                        maximumMorph = Math.Max(maximumMorph, checked((int)Dword(source.Data, offset + 4) * 8));
                    }
                    return retained;
                }).Workspace;
            }
        }
        Assert.Equal(64, maximumDirect);
        Assert.Equal(ScratchLength, maximumMorph);
        Assert.Equal(new[] { 2, 3, 4, 5, 7 }, selectedGroups.Order().ToArray());
        Assert.Equal(220u, Dword(resources.StaticMesh.Bytes, 4));
        Assert.Same(resources.StaticMesh, resources.ReadStatic(StaticMeshAddress));
        Assert.Equal(new short[] { 0, 119 }, Enumerable.Range(0, 220).Select(index => BinaryPrimitives.ReadInt16LittleEndian(resources.StaticMesh.Bytes.AsSpan().Slice(8 + index * 8 + 6))).Distinct().Order().ToArray());
        Assert.Equal(7, resources.SpriteA.Kind);
        Assert.Equal(0, resources.SpriteB.Kind);
        _ = resources.ReadSprite(SpriteAAddress).ProjectedFrame571C80(15);
        _ = resources.ReadSprite(SpriteBAddress).ProjectedFrame571C80(15);
    }

    [Fact]
    public void Original_resources_round_trip_and_do_not_create_COMMON_or_state_aliases()
    {
        using GameAssets game = Open();
        var resources = game.ReadNativeSnipeLaser302Resources();
        var restored = JsonSerializer.Deserialize<NativeBattleSnipeLaser302Resources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restored));
        var state = NativeBattleSnipeLaser302State.FromUnknown().WriteDword(CursorGlobal, ArenaAddress + FirstPacketOffset);
        Assert.Throws<NotSupportedException>(() => state.ReadDword(CasterGlobal));
        Assert.Throws<InvalidDataException>(() => state.ReadWord(StaticMeshAddress));
        Assert.Throws<NotSupportedException>(() => resources.ReadGeometry(ArenaAddress + ScratchOffset));
        Assert.Throws<NotSupportedException>(() => resources.ReadProgramme(CameraAddress));
        Assert.Throws<NotSupportedException>(() => resources.ReadSprite(CameraAddress));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(ProgrammeAAddress));
    }

    [Fact]
    public void Changed_original_extents_faces_descriptors_and_points_fail()
    {
        using GameAssets game = Open();
        var resources = game.ReadNativeSnipeLaser302Resources();
        Assert.Throws<InvalidDataException>(() => (resources with { ProgrammeA = resources.ProgrammeA with { Data = resources.ProgrammeA.Data.RemoveAt(0) } }).Validate());
        byte[] source = resources.ProgrammeB.Data.ToArray();
        int table = checked((int)Dword(resources.ProgrammeB.Data, 4));
        BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(table), 4);
        Assert.Throws<InvalidDataException>(() => (resources with { ProgrammeB = resources.ProgrammeB with { Data = source.ToImmutableArray() } }).Validate());
        source = resources.ProgrammeA.Data.ToArray();
        int geometry = checked((int)Dword(resources.ProgrammeA.Data, 8));
        int polygon = checked(geometry + (int)Dword(resources.ProgrammeA.Data, geometry));
        BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(polygon + 28), 0xFFFF);
        Assert.Throws<InvalidDataException>(() => (resources with { ProgrammeA = resources.ProgrammeA with { Data = source.ToImmutableArray() } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { StaticMesh = resources.StaticMesh with { Bytes = resources.StaticMesh.Bytes.RemoveAt(0) } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Sound = 0 }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Camera = resources.Camera with { Data = resources.Camera.Data.RemoveAt(0) } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { SourcePoints16 = new byte[16].ToImmutableArray() }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { SpriteA = resources.SpriteA with { Bytes = resources.SpriteA.Bytes.RemoveAt(0) } }).Validate());
    }

    private static uint Dword(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset));

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original Snipe Laser resources.");
        return new(directory!);
    }
}
