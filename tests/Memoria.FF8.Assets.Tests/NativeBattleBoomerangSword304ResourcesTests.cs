using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleBoomerangSword304Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleBoomerangSword304ResourcesTests
{
    [Fact]
    public void Original_programme_direct_meshes_sprite_and_materials_have_exact_fronts()
    {
        using GameAssets game = Open();
        var resources = game.ReadNativeBoomerangSword304Resources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeBoomerangSword304Resources());
        Assert.Equal(0x641ACu, resources.Sound);
        var source = resources.Programme;
        Assert.Equal(ProgrammeLength, source.Data.Length);
        Assert.Equal(4u, Dword(source.Data, 0));
        int table = checked((int)Dword(source.Data, 4));
        Assert.Equal(6u, Dword(source.Data, table));
        Assert.Equal(14u, Dword(source.Data, table + 4));
        HashSet<int> groups = [];
        int[] vertices = [49, 49, 51, 25];
        for (int geometry = 0; geometry < 4; geometry++)
        {
            int offset = checked((int)Dword(source.Data, 8 + geometry * 4));
            var mesh = resources.ReadGeometry(source.Address + (uint)offset);
            Assert.Equal((uint)vertices[geometry], Dword(mesh.Bytes, 4));
            Assert.Equal(source.Data.AsSpan().Slice(offset, mesh.Bytes.Length).ToArray(), mesh.Bytes.ToArray());
            int cursor = checked((int)Dword(mesh.Bytes, 0));
            int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
            for (int group = 0; group < 8; group++)
            {
                uint faces = Dword(mesh.Bytes, cursor);
                if (faces != 0)
                {
                    groups.Add(group);
                }
                cursor = checked(cursor + 4 + (int)faces * strides[group]);
            }
            Assert.Equal(mesh.Bytes.Length, cursor);
        }
        Assert.Equal(new[] { 2, 3, 7 }, groups.Order().ToArray());
        var workspace = new NativeBattleGfAnimationWorkspace(new byte[ProgrammeWorkspaceLength].ToImmutableArray()).Initialize7016B0(source, ProgrammeWorkspaceLength);
        int maximumMorph = 0;
        int callbacks = 0;
        for (int frame = 0; frame < 14; frame++)
        {
            workspace = workspace.Advance701970(source, 0, (retained, node) =>
            {
                callbacks++;
                if (node.Word40 != node.Word42 && node.Scalar38 is not (0 or 4096))
                {
                    int offset = checked((int)Dword(source.Data, 8 + node.GeometryIndex * 4));
                    maximumMorph = Math.Max(maximumMorph, checked((int)Dword(source.Data, offset + 4) * 8));
                }
                return retained;
            }).Workspace;
        }
        Assert.Equal(84, callbacks);
        Assert.Equal(ScratchLength, maximumMorph);
        Assert.Equal(52u, Dword(resources.ReadMesh(MeshAddress).Bytes, 4));
        Assert.Equal(14u, Dword(resources.ReadMesh(TrailMeshAddress).Bytes, 4));
        foreach (var mesh in new[] { resources.Mesh, resources.TrailMesh })
        {
            int count = checked((int)Dword(mesh.Bytes, 4));
            Assert.All(Enumerable.Range(0, count), index => Assert.Equal((short)0, BinaryPrimitives.ReadInt16LittleEndian(mesh.Bytes.AsSpan().Slice(8 + index * 8 + 6))));
        }
        Assert.Equal(8, resources.Sprite.Kind);
        _ = resources.ReadSprite(SpriteAddress).ProjectedFrame571C80(9);
    }

    [Fact]
    public void Original_resources_round_trip_without_mirroring_COMMON_or_current_state()
    {
        using GameAssets game = Open();
        var resources = game.ReadNativeBoomerangSword304Resources();
        var restored = JsonSerializer.Deserialize<NativeBattleBoomerangSword304Resources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restored));
        var state = NativeBattleBoomerangSword304State.FromUnknown().WriteDword(CursorGlobal, ArenaAddress + FirstPacketOffset);
        Assert.Throws<NotSupportedException>(() => state.ReadDword(CasterGlobal));
        Assert.Throws<InvalidDataException>(() => state.ReadWord(MeshAddress));
        Assert.Throws<NotSupportedException>(() => resources.ReadGeometry(ArenaAddress + ScratchOffset));
        Assert.Throws<NotSupportedException>(() => resources.ReadProgramme(CameraAddress));
        Assert.Throws<NotSupportedException>(() => resources.ReadSprite(CameraAddress));
        Assert.Throws<NotSupportedException>(() => resources.ReadMesh(ProgrammeAddress));
    }

    [Fact]
    public void Changed_source_directory_faces_camera_sound_and_sprite_fail()
    {
        using GameAssets game = Open();
        var resources = game.ReadNativeBoomerangSword304Resources();
        Assert.Throws<InvalidDataException>(() => (resources with { Programme = resources.Programme with { Data = resources.Programme.Data.RemoveAt(0) } }).Validate());
        byte[] source = resources.Programme.Data.ToArray();
        int table = checked((int)Dword(resources.Programme.Data, 4));
        BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(table), 7);
        Assert.Throws<InvalidDataException>(() => (resources with { Programme = resources.Programme with { Data = source.ToImmutableArray() } }).Validate());
        source = resources.Programme.Data.ToArray();
        int geometry = checked((int)Dword(resources.Programme.Data, 8));
        int polygon = checked(geometry + (int)Dword(resources.Programme.Data, geometry));
        BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(polygon + 16), 0xFFFF);
        Assert.Throws<InvalidDataException>(() => (resources with { Programme = resources.Programme with { Data = source.ToImmutableArray() } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Mesh = resources.Mesh with { Bytes = resources.Mesh.Bytes.RemoveAt(0) } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { TrailMesh = resources.TrailMesh with { Bytes = resources.TrailMesh.Bytes.RemoveAt(0) } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Sound = 0 }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Camera = resources.Camera with { Data = resources.Camera.Data.RemoveAt(0) } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Sprite = resources.Sprite with { Bytes = resources.Sprite.Bytes.RemoveAt(0) } }).Validate());
    }

    private static uint Dword(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset));

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original Boomerang Sword resources.");
        return new(directory!);
    }
}
