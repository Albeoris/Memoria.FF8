using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleZantetsukenResourcesTests
{
    [Fact]
    public void Original_Zantetsuken_resources_have_no_final_file_and_retain_clipping_tables()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources to check Zantetsuken.");
        using GameAssets game = new(directory!);
        NativeBattleGilgameshResources files = game.ReadNativeGilgameshResources(327);
        files.Validate();
        Assert.Equal(10, files.Files.Length);
        Assert.DoesNotContain(files.Files, file => file.FileId is 0 or 754 or 756 or 757);
        Assert.Contains(files.Files, file => file.FileId == 753 && file.Name == "mag326_k.dat" && file.Bytes.Length == 4);
        NativeBattleGilgameshDirectorResources director = game.ReadNativeGilgameshDirectorResources(327);
        director.Validate();
        Assert.Equal("FF8_EN:CD05A0", director.CameraProgram.Identity);
        Assert.Equal(250, director.CameraProgram.Data.Length);
        Assert.Equal((ushort)0x8000, BinaryPrimitives.ReadUInt16LittleEndian(director.CameraProgram.Data.AsSpan().Slice(248)));
        Assert.Equal(326, director.Vibration.Bytes.Length);
        Assert.Equal((ushort)60, director.Vibration.ReadWord(0x100));
        Assert.Equal(0x3AA9Cu, director.SoundClosing);
        NativeBattleZantetsukenResources geometry = game.ReadNativeZantetsukenResources();
        Assert.Same(geometry, game.ReadNativeZantetsukenResources());
        geometry.Validate();
        Assert.Equal(0xCCDE04u, geometry.Flare.Address);
        Assert.Equal(2008, geometry.Flare.Bytes.Length);
        Assert.Equal(56, geometry.ClipIndices.Length);
        Assert.Equal(4, geometry.StageOrderPairs.Length);
        Assert.Equal(new NativeBattleZantetsukenStageOrdering(17, 2), geometry.StageOrderPairs[2]);
        Assert.Throws<InvalidDataException>(() => (geometry with { ClipIndices = geometry.ClipIndices.SetItem(0, 6) }).Validate());
        Assert.Throws<InvalidDataException>(() => (geometry with { Flare = geometry.Flare with { Address = 0xCCF2DC } }).Validate());
        Assert.Throws<InvalidDataException>(() => (geometry with { StageOrderPairs = geometry.StageOrderPairs.RemoveAt(3) }).Validate());
        NativeBattleZantetsukenResources restored = JsonSerializer.Deserialize<NativeBattleZantetsukenResources>(JsonSerializer.Serialize(geometry))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(geometry), JsonSerializer.Serialize(restored));
    }
}
