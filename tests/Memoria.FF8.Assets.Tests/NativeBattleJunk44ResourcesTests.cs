using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleJunk44Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleJunk44ResourcesTests
{
    [Fact]
    public void Original_fronts_preserve_projected_kind6_programme_and_all_seven_debris_rows()
    {
        using GameAssets game = Open();
        var resources = game.ReadNativeJunk44Resources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeJunk44Resources());
        Assert.Equal(ProgrammeLength, resources.Programme.Data.Length);
        Assert.Equal(6, resources.Sprite.Kind);
        Assert.Equal(13, BinaryPrimitives.ReadUInt16LittleEndian(resources.Sprite.Bytes.AsSpan().Slice(8)));
        Assert.Throws<InvalidDataException>(() => resources.Sprite.Validate());
        for (int frame = 0; frame < 13; frame++)
        {
            Assert.Equal(20, resources.Sprite.ProjectedFrame571C80(frame).Quads.Length);
        }
        var geometry = resources.ReadGeometry(ProgrammeAddress + 12);
        Assert.Equal(212, geometry.Bytes.Length);
        Assert.Equal(9u, BinaryPrimitives.ReadUInt32LittleEndian(geometry.Bytes.AsSpan().Slice(4)));
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(geometry.Bytes.AsSpan().Slice(8)));
        Assert.Equal(72, ScratchLength);
        Assert.Equal(new[] { 1, 1, 1, 1, 1, 4, 1 }, Enumerable.Range(0, 7).Select(row => (int)BinaryPrimitives.ReadInt16LittleEndian(resources.ReadTable(row).AsSpan().Slice(16))).ToArray());
        Assert.Equal(10, resources.Meshes.Length);
        Assert.Equal(0x64292u, resources.Sound);
        Assert.Equal(CameraLength, resources.Camera.Data.Length);
        Assert.Equal(new short[] { 360, 361 }, resources.Files.Select(file => file.FileId).ToArray());
        Assert.All(resources.Files, file => Assert.Equal(33824, file.Data.Length));
    }

    [Fact]
    public void Immutable_resources_roundtrip_without_current_COMMON_aliases()
    {
        using GameAssets game = Open();
        var resources = game.ReadNativeJunk44Resources();
        var restored = JsonSerializer.Deserialize<NativeBattleJunk44Resources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restored));
        var state = NativeBattleJunk44State.FromColdProcess();
        Assert.Throws<InvalidDataException>(() => state.ReadWord(ScratchAddress));
        Assert.Throws<NotSupportedException>(() => restored.ReadGeometry(ScratchAddress));
        Assert.Throws<NotSupportedException>(() => restored.ReadStatic(DebrisTableAddress + 168, 1));
        Assert.Throws<NotSupportedException>(() => restored.ReadProgramme(CameraAddress));
    }

    [Fact]
    public void Truncation_changed_rows_faces_and_sound_are_rejected()
    {
        using GameAssets game = Open();
        var resources = game.ReadNativeJunk44Resources();
        Assert.Throws<InvalidDataException>(() => (resources with { Programme = resources.Programme with { Data = resources.Programme.Data.RemoveAt(0) } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Sprite = resources.Sprite with { Bytes = resources.Sprite.Bytes.RemoveAt(0) } }).Validate());
        byte[] table = resources.DebrisTable.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(table.AsSpan(5 * 24 + 16), 3);
        Assert.Throws<InvalidDataException>(() => (resources with { DebrisTable = table.ToImmutableArray() }).Validate());
        byte[] mesh = resources.Meshes[0].Bytes.ToArray();
        int cursor = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(mesh));
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < 8; group++)
        {
            int count = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(mesh.AsSpan(cursor)));
            if (count > 0)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(mesh.AsSpan(cursor + 8), 0xFFFF);
                break;
            }
            cursor += 4 + count * strides[group];
        }
        Assert.Throws<InvalidDataException>(() => (resources with { Meshes = resources.Meshes.SetItem(0, resources.Meshes[0] with { Bytes = mesh.ToImmutableArray() }) }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Sound = 0 }).Validate());
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original Junk resources.");
        return new(directory!);
    }
}
