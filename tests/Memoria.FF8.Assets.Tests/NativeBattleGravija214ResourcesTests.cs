using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleGravija214Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleGravija214ResourcesTests
{
    [Fact]
    public void Original_full_copy_and_seven_programme_geometry_fronts_remain_distinct()
    {
        using GameAssets game = Open();
        var resources = game.ReadNativeGravija214Resources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeGravija214Resources());
        Assert.Equal(30528, resources.EntryCopySeed.Data.Length);
        Assert.Equal(241u, BinaryPrimitives.ReadUInt32LittleEndian(resources.EntryCopySeed.Data.AsSpan().Slice(4)));
        Assert.Equal(ProgrammeLength, resources.Programme.Data.Length);
        Assert.Equal(756, ProgrammeWorkspaceLength);
        var workspace = new NativeBattleGfAnimationWorkspace(new byte[ProgrammeWorkspaceLength].ToImmutableArray()).Initialize7016B0(resources.Programme, ProgrammeWorkspaceLength);
        Assert.Equal(ProgrammeAddress, workspace.ResourceAddress);
        Assert.Equal(0, workspace.Frame);
        int[] vertices = [72, 72, 128, 72, 25, 25, 25];
        int[] shapes = [2, 2, 2, 1, 1, 1, 1];
        for (int index = 0; index < 7; index++)
        {
            uint offset = BinaryPrimitives.ReadUInt32LittleEndian(resources.Programme.Data.AsSpan().Slice(8 + index * 4));
            var geometry = resources.ReadGeometry(ProgrammeAddress + offset);
            Assert.Equal((uint)vertices[index], BinaryPrimitives.ReadUInt32LittleEndian(geometry.Bytes.AsSpan().Slice(4)));
            Assert.Equal((uint)shapes[index], BinaryPrimitives.ReadUInt32LittleEndian(geometry.Bytes.AsSpan().Slice(8)));
            Assert.Equal(12u + (uint)(vertices[index] * shapes[index] * 8), BinaryPrimitives.ReadUInt32LittleEndian(geometry.Bytes.AsSpan()));
        }
        Assert.Equal(50, BinaryPrimitives.ReadUInt16LittleEndian(resources.Sprite.Bytes.AsSpan().Slice(8)));
        for (int phase = 0; phase < 50; phase++)
        {
            _ = resources.Sprite.ProjectedFrame571C80(phase);
        }
        Assert.Equal(CameraLength, resources.Camera.Data.Length);
        Assert.Equal(0x642BFu, resources.Sound);
    }

    [Fact]
    public void Native_copy_and_lookup_owners_roundtrip_without_reseeding_current_writes()
    {
        using GameAssets game = Open();
        var resources = game.ReadNativeGravija214Resources();
        var restored = JsonSerializer.Deserialize<NativeBattleGravija214Resources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restored));
        Assert.Equal(192, resources.SqrtTable.Length);
        Assert.Equal(4096, resources.SineTable.Length);
        Assert.Equal(resources.SqrtTable[0], BinaryPrimitives.ReadUInt16LittleEndian(resources.ReadStatic(SqrtTableAddress, 2).AsSpan()));
        Assert.Equal(resources.SineTable[4095], BinaryPrimitives.ReadInt16LittleEndian(resources.ReadStatic(SineTableAddress + 8190, 2).AsSpan()));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(0x1178A50, 2));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(SineTableAddress + SineTableLength, 1));
        var state = NativeBattleGravija214State.FromColdProcess().CopyEntrySnapshot(resources).WriteWord(CopiedMeshAddress + 8, 0x1234);
        Assert.Equal((ushort)0x1234, state.ReadWord(CopiedMeshAddress + 8));
        Assert.NotEqual((ushort)0x1234, BinaryPrimitives.ReadUInt16LittleEndian(resources.EntryCopySeed.Data.AsSpan().Slice(8)));
        Assert.Equal(new byte[32], state.Read(MatrixAddress, 32));
    }

    [Fact]
    public void Truncated_sources_changed_groups_indices_or_lookup_extents_fail()
    {
        using GameAssets game = Open();
        var resources = game.ReadNativeGravija214Resources();
        Assert.Throws<InvalidDataException>(() => (resources with { EntryCopySeed = resources.EntryCopySeed with { Data = resources.EntryCopySeed.Data.RemoveAt(0) } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Programme = resources.Programme with { Data = resources.Programme.Data.RemoveAt(0) } }).Validate());
        byte[] bytes = resources.EntryCopySeed.Data.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(1960), 0xFFFF);
        Assert.Throws<InvalidDataException>(() => (resources with { EntryCopySeed = resources.EntryCopySeed with { Data = bytes.ToImmutableArray() } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { SqrtTable = resources.SqrtTable.RemoveAt(0) }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { SineTable = resources.SineTable.RemoveAt(0) }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Sound = 0 }).Validate());
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original Gravija resources.");
        return new(directory!);
    }
}
