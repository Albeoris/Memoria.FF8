using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect303Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect303ResourcesTests
{
    [Fact]
    public void Original_programmes_advance_full_durations_with_exact_cache_and_morph_bounds()
    {
        using GameAssets game = Open();
        var resources = game.ReadNativeEffect303Resources();
        resources.Validate();
        NativeBattleGfAnimationSource[] sources = [resources.MainProgramme, resources.ChildProgramme];
        int[] clears = [416, 280];
        int[] durations = [22, 16];
        int[] nodes = [5, 4];
        int maximumMorph = 0;
        HashSet<int> groups = [];
        for (int index = 0; index < sources.Length; index++)
        {
            NativeBattleGfAnimationSource source = sources[index];
            for (int geometry = 0; geometry < 3; geometry++)
            {
                uint address = checked(source.Address + Dword(source.Data, 8 + geometry * 4));
                NativeBattleStaticMeshResource mesh = resources.ReadGeometry(address);
                int cursor = checked((int)Dword(mesh.Bytes, 0));
                int[] sizes = [12, 12, 20, 24, 20, 24, 28, 36];
                for (int group = 0; group < 8; group++)
                {
                    uint count = Dword(mesh.Bytes, cursor);
                    if (count != 0)
                    {
                        groups.Add(group);
                    }
                    cursor = checked(cursor + 4 + (int)count * sizes[group]);
                }
                Assert.Equal(mesh.Bytes.Length, cursor);
            }
            var workspace = new NativeBattleGfAnimationWorkspace(Enumerable.Repeat((byte)0xA5, clears[index] + 16).ToImmutableArray()).Initialize7016B0(source, clears[index]);
            for (int frame = 0; frame < durations[index]; frame++)
            {
                int callbacks = 0;
                workspace = workspace.Advance701970(source, 0, (retained, node) =>
                {
                    callbacks++;
                    if (node.Word40 != node.Word42 && node.Scalar38 is not (0 or 4096))
                    {
                        int geometry = checked((int)Dword(source.Data, 8 + node.GeometryIndex * 4));
                        maximumMorph = Math.Max(maximumMorph, checked((int)Dword(source.Data, geometry + 4) * 8));
                    }
                    return retained;
                }).Workspace;
                Assert.Equal(nodes[index], callbacks);
                Assert.Equal(frame + 1, workspace.Frame);
                Assert.All(NativeBattleStorageSnapshots.Capture(workspace).Data.Skip(clears[index]), value => Assert.Equal((byte)0xA5, value));
            }
        }
        Assert.Equal(new[] { 3 }, groups.Order().ToArray());
        Assert.Equal(ScratchLength, maximumMorph);
        for (int index = 0; index < 4; index++)
        {
            Assert.Equal(MeshLengths[index], resources.ReadMesh(MeshAddresses[index]).Bytes.Length);
            Assert.Equal((uint)MeshVertexCounts[index], Dword(resources.Meshes[index].Bytes, 4));
        }
        _ = resources.ParticleSprite.ProjectedFrame571C80(9);
        _ = resources.Sprite.ProjectedFrame571C80(11);
    }

    [Fact]
    public void Original_texture_vibration_completion_points_and_JSON_keep_distinct_owners()
    {
        using GameAssets game = Open();
        var resources = game.ReadNativeEffect303Resources();
        var restored = NativeBattleSnapshotJson.Deserialize<NativeBattleEffect303Resources>(NativeBattleSnapshotJson.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(NativeBattleSnapshotJson.Serialize(resources), NativeBattleSnapshotJson.Serialize(restored));
        Assert.Equal(0x6419Du, resources.Sound);
        Assert.Equal(new[] { 0, 256 }, resources.Vibration.Bank.Offsets.ToArray());
        Assert.Equal(14, resources.Vibration.Bank.ReadWord(256));
        Assert.Equal(274, resources.Vibration.Bank.Bytes.Length);
        Assert.Equal(360, resources.CompletionFile.FileId);
        Assert.Equal("MA8DEF_0.TIM", resources.CompletionFile.Name, ignoreCase: true);
        Assert.Equal(33824, resources.CompletionFile.Data.Length);
        Assert.Equal(new short[] { 1789, 0, -1099, 3, 464, -13, -1058, 39 }, Enumerable.Range(0, 8).Select(index => BinaryPrimitives.ReadInt16LittleEndian(resources.MuzzlePoints16.AsSpan().Slice(index * 2))));
        Assert.Equal(new short[] { 0, -100, 40, 25, 50, -80, 20, 10, -50, -80, 20, 15, 0, -50, 40, 20 }, Enumerable.Range(0, 16).Select(index => BinaryPrimitives.ReadInt16LittleEndian(resources.ChildPoints32.AsSpan().Slice(index * 2))));
        Assert.Equal(resources.ChildPoints32.ToArray(), resources.ReadStatic(ChildPointsAddress, 32).ToArray());
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(ChildPointsAddress + 31, 2));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(VibrationAddress + VibrationLength, 1));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(ArenaAddress + ScratchOffset, 8));
    }

    [Fact]
    public void Truncated_or_redirected_resource_fronts_fail_at_the_selected_boundary()
    {
        using GameAssets game = Open();
        var resources = game.ReadNativeEffect303Resources();
        Assert.Throws<InvalidDataException>(() => (resources with { MainProgramme = resources.MainProgramme with { Data = resources.MainProgramme.Data.RemoveAt(0) } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { ChildProgramme = resources.ChildProgramme with { Address = MainProgrammeAddress } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Meshes = resources.Meshes.SetItem(0, resources.Meshes[0] with { Bytes = resources.Meshes[0].Bytes.RemoveAt(0) }) }).Validate());
        byte[] badFace = resources.Meshes[0].Bytes.ToArray();
        int polygon = checked((int)Dword(resources.Meshes[0].Bytes, 0));
        BinaryPrimitives.WriteUInt16LittleEndian(badFace.AsSpan(polygon + 16), ushort.MaxValue);
        Assert.Throws<InvalidDataException>(() => (resources with { Meshes = resources.Meshes.SetItem(0, resources.Meshes[0] with { Bytes = badFace.ToImmutableArray() }) }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Camera = resources.Camera with { Data = resources.Camera.Data.RemoveAt(0) } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Sound = 0 }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { ParticleSprite = resources.ParticleSprite with { Address = SpriteAddress } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { CompletionFile = resources.CompletionFile with { FileId = 361 } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { MuzzlePoints16 = resources.MuzzlePoints16.SetItem(14, 38) }).Validate());
        Assert.Throws<NotSupportedException>(() => resources.ReadMesh(CameraAddress));
        Assert.Throws<NotSupportedException>(() => resources.ReadProgramme(SpriteAddress));
    }

    private static uint Dword(ImmutableArray<byte> data, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan().Slice(offset, 4));

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original Effect303 resources.");
        return new(directory!);
    }
}
