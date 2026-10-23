using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleStare45Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleStare45ResourcesTests
{
    [Fact]
    public void Original_programmes_keep_distinct_counts_caches_shapes_materials_and_camera_fronts()
    {
        using GameAssets game = Open();
        var resources = game.ReadNativeStare45Resources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeStare45Resources());
        Assert.Equal(ProgrammeALength, resources.ProgrammeA.Data.Length);
        Assert.Equal(ProgrammeBLength, resources.ProgrammeB.Data.Length);
        Assert.Equal(CameraLength, resources.Camera.Data.Length);
        Assert.Equal(0x61B1Eu, resources.Sound);
        foreach (var source in new[] { resources.ProgrammeA, resources.ProgrammeB })
        {
            bool first = source.Address == ProgrammeAAddress;
            int table = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(source.Data.AsSpan().Slice(4)));
            Assert.Equal(first ? 6u : 8u, BinaryPrimitives.ReadUInt32LittleEndian(source.Data.AsSpan().Slice(table)));
            Assert.Equal(first ? 30u : 40u, BinaryPrimitives.ReadUInt32LittleEndian(source.Data.AsSpan().Slice(table + 4)));
            int workspace = first ? ProgrammeAWorkspaceLength : ProgrammeBWorkspaceLength;
            var initialized = new NativeBattleGfAnimationWorkspace(new byte[workspace].ToImmutableArray()).Initialize7016B0(source, workspace);
            Assert.Equal(source.Address, initialized.ResourceAddress);
            Assert.Equal(0, initialized.Frame);
            for (int geometry = 0; geometry < 4; geometry++)
            {
                int offset = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(source.Data.AsSpan().Slice(8 + geometry * 4)));
                var mesh = resources.ReadGeometry(source.Address + (uint)offset);
                uint vertices = BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4));
                uint shapes = BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan().Slice(8));
                Assert.Equal(geometry == 0 ? first ? 78u : 100u : first && geometry == 2 ? 78u : 9u, vertices);
                Assert.Equal(12u + vertices * shapes * 8, BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan()));
                Assert.Equal(source.Data.AsSpan().Slice(offset, mesh.Bytes.Length).ToArray(), mesh.Bytes.ToArray());
            }
        }
        Assert.Throws<NotSupportedException>(() => resources.ReadProgramme(CameraAddress));
        Assert.Throws<NotSupportedException>(() => resources.ReadGeometry(EffectObjectAddress));
    }

    [Fact]
    public void Immutable_resources_and_current_scratch_masks_remain_separate_through_JSON()
    {
        using GameAssets game = Open();
        var resources = game.ReadNativeStare45Resources();
        var restored = JsonSerializer.Deserialize<NativeBattleStare45Resources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restored));
        var state = NativeBattleStare45State.FromUnknown().Write(ScratchAddress, new byte[] { 1, 2, 3, 4, 5, 6 }).WriteWord(EffectObjectAddress, 0x1234);
        Assert.Throws<NotSupportedException>(() => state.ReadWord(ScratchAddress + 6));
        Assert.Equal((ushort)0x1234, state.ReadWord(EffectObjectAddress));
        Assert.Equal(resources.ProgrammeA.Data.ToArray(), restored.ProgrammeA.Data.ToArray());
        Assert.Throws<InvalidDataException>(() => state.ReadWord(ScratchAddress + ScratchLength));
    }

    [Fact]
    public void Truncated_sources_changed_node_counts_face_indices_camera_and_sound_fail()
    {
        using GameAssets game = Open();
        var resources = game.ReadNativeStare45Resources();
        Assert.Throws<InvalidDataException>(() => (resources with { ProgrammeA = resources.ProgrammeA with { Data = resources.ProgrammeA.Data.RemoveAt(0) } }).Validate());
        byte[] bytes = resources.ProgrammeB.Data.ToArray();
        int table = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4)));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(table), 7);
        Assert.Throws<InvalidDataException>(() => (resources with { ProgrammeB = resources.ProgrammeB with { Data = bytes.ToImmutableArray() } }).Validate());
        bytes = resources.ProgrammeA.Data.ToArray();
        int geometry = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(8)));
        int polygons = geometry + checked((int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(geometry)));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(polygons + 16 + 4), 0xFFFF);
        Assert.Throws<InvalidDataException>(() => (resources with { ProgrammeA = resources.ProgrammeA with { Data = bytes.ToImmutableArray() } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Camera = resources.Camera with { Data = resources.Camera.Data.RemoveAt(0) } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Sound = 0 }).Validate());
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original Stare resources.");
        return new(directory!);
    }
}
