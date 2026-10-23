using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect283Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect283ResourceTests
{
    [Fact]
    public void Original_programmes_preserve_geometry_groups_shapes_and_complete_animation_caches()
    {
        using GameAssets game = Open();
        NativeBattleEffect283Resources resources = game.ReadNativeEffect283Resources();
        resources.Validate();
        NativeBattleGfAnimationSource[] sources = [resources.MainProgramme, resources.SecondaryProgramme];
        int[] geometries = [5, 2];
        int[] clears = [1212, 352];
        int[] durations = [60, 36];
        int[] nodes = [17, 4];
        int maximumVertices = 0;
        int maximumMorph = 0;
        HashSet<int> groups = [];
        for (int sourceIndex = 0; sourceIndex < sources.Length; sourceIndex++)
        {
            NativeBattleGfAnimationSource source = sources[sourceIndex];
            Assert.Equal((uint)geometries[sourceIndex], Dword(source.Data, 0));
            for (int geometry = 0; geometry < geometries[sourceIndex]; geometry++)
            {
                NativeBattleStaticMeshResource mesh = resources.ReadGeometry(checked(source.Address + Dword(source.Data, 8 + geometry * 4)));
                int vertices = checked((int)Dword(mesh.Bytes, 4));
                int shapes = checked((int)Dword(mesh.Bytes, 8));
                maximumVertices = Math.Max(maximumVertices, vertices);
                int cursor = checked((int)Dword(mesh.Bytes, 0));
                Assert.Equal(12 + vertices * shapes * 8, cursor);
                int[] strides = [14, 18, 20, 24, 20, 24, 28, 36];
                for (int group = 0; group < strides.Length; group++)
                {
                    uint count = Dword(mesh.Bytes, cursor);
                    if (count != 0)
                    {
                        groups.Add(group);
                    }
                    cursor = checked(cursor + 4 + (int)count * strides[group]);
                }
                Assert.Equal(mesh.Bytes.Length, cursor);
                if (shapes > 1)
                {
                    maximumMorph = Math.Max(maximumMorph, vertices * 8);
                }
            }
            NativeBattleGfAnimationWorkspace workspace = new(Enumerable.Repeat((byte)0xA5, clears[sourceIndex] + 16).ToImmutableArray());
            workspace = workspace.Initialize7016B0(source, clears[sourceIndex]);
            for (int frame = 0; frame < durations[sourceIndex]; frame++)
            {
                int callbacks = 0;
                workspace = workspace.Advance701970(source, 0, (retained, node) =>
                {
                    callbacks++;
                    return retained;
                }).Workspace;
                Assert.Equal(nodes[sourceIndex], callbacks);
                Assert.Equal(frame + 1, workspace.Frame);
                Assert.All(workspace.Data.Skip(clears[sourceIndex]), value => Assert.Equal((byte)0xA5, value));
            }
        }
        Assert.Equal(96, maximumVertices);
        Assert.Equal(512, maximumMorph);
        Assert.Equal(new[] { 3, 4, 5, 6, 7 }, groups.Order().ToArray());
    }

    [Fact]
    public void Warp_readers_share_actual_alias_and_retain_the_inclusive_distance_extension()
    {
        using GameAssets game = Open();
        NativeBattleEffect283Resources resources = game.ReadNativeEffect283Resources();
        Assert.Equal(21377, resources.WarpTables21377.Length);
        for (int index = 0; index < 128; index++)
        {
            ushort aliased = checked((ushort)(resources.ReadDistance(index * 2) | resources.ReadDistance(index * 2 + 1) << 8));
            Assert.Equal(aliased, resources.ReadWarpFactor(20, index));
        }
        Assert.Equal(resources.WarpTables21377[21376], resources.ReadDistance(16512));
        Assert.Equal(resources.WarpTables21377.ToArray(), resources.ReadStatic(0x10997AC, 21377).ToArray());
        Assert.Equal(System.Text.Encoding.ASCII.GetBytes("mag282.tim\0"), resources.ReadStatic(0x109EAAC, 11).ToArray());
        Assert.Throws<InvalidDataException>(() => resources.ReadWarpFactor(0, 0));
        Assert.Throws<InvalidDataException>(() => resources.ReadWarpFactor(21, 0));
        Assert.Throws<InvalidDataException>(() => resources.ReadWarpFactor(1, 128));
        Assert.Throws<InvalidDataException>(() => resources.ReadDistance(16513));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(0x10996AC, 1));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(0x109EB2D, 1));
    }

    [Fact]
    public void Complete_fronts_roundtrip_and_reject_truncation_or_redirected_owners()
    {
        using GameAssets game = Open();
        NativeBattleEffect283Resources resources = game.ReadNativeEffect283Resources();
        NativeBattleEffect283Resources restored = JsonSerializer.Deserialize<NativeBattleEffect283Resources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restored));
        Assert.Equal(0x61B71u, resources.Sound);
        Assert.Equal(new uint[] { 0x00005000, 0x00500000, 0x00500050, 0x00000050, 0x00005000, 0x00500000, 0x00500050, 0x00000050 }, resources.Palette8.ToArray());
        Assert.Throws<InvalidDataException>(() => (resources with { MainProgramme = resources.MainProgramme with { Data = resources.MainProgramme.Data.RemoveAt(0) } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { SecondaryProgramme = resources.SecondaryProgramme with { Address = MainProgrammeAddress } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Camera = resources.Camera with { Data = resources.Camera.Data.RemoveAt(0) } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Sound = 0 }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Palette8 = resources.Palette8.RemoveAt(0) }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { WarpTables21377 = resources.WarpTables21377.RemoveAt(0) }).Validate());
        Assert.Throws<NotSupportedException>(() => resources.ReadProgramme(CameraAddress));
        Assert.Throws<NotSupportedException>(() => resources.ReadGeometry(CameraAddress));
    }

    private static uint Dword(ImmutableArray<byte> data, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan().Slice(offset, 4));

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original Effect283 resources.");
        return new(directory!);
    }
}
