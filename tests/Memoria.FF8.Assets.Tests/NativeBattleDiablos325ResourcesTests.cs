using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleDiablos325ResourcesTests
{
    [Fact]
    public void Installed_opening_programme_and_null_first_vibration_entry_preserve_original_bytes()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        using GameAssets game = new(directory!);
        NativeBattleDiablos325Resources resources = game.ReadNativeDiablos325Resources();
        Assert.Same(resources, game.ReadNativeDiablos325Resources());
        resources.Validate();
        Assert.Equal(Enumerable.Range(727, 5).Select(id => (short)id), resources.Files.Select(file => file.FileId));
        Assert.Equal(new[] { "MAG324_H.00", "MAG324_H.01", "MAG324_H.02", "MAG324_H.03", "MAG324_H.04" }, resources.Files.Select(file => file.Name));
        Assert.All(resources.Files, file => Assert.NotEmpty(file.Data));
        Assert.Equal(new uint[] { 240248, 240251, 240254, 240257, 240260, 240263 }, resources.Sounds);
        Assert.Equal(0xFD8584u, resources.OpeningProgramme.Address);
        Assert.Equal(0x3890, resources.OpeningProgramme.Data.Length);
        Assert.Equal(64, resources.Vibration.Offsets.Length);
        Assert.Equal(0, resources.Vibration.Offsets[0]);
        Assert.Equal(0x100, resources.Vibration.Offsets[1]);
        Assert.All(resources.Vibration.Offsets.Skip(2), value => Assert.Equal(0, value));
        Assert.Equal((ushort)62, resources.Vibration.ReadWord(0x100));
        Assert.Equal((ushort)4, resources.Vibration.ReadWord(0x102));
        Assert.Equal(0x148, resources.Vibration.Bytes.Length);
        NativeBattleGfAnimationWorkspace workspace = new NativeBattleGfAnimationWorkspace(ImmutableArray.CreateRange(new byte[0xE0])).Initialize7016B0(resources.OpeningProgramme, 0xE0);
        Assert.Equal(0xFD8584u, workspace.ResourceAddress);
        Assert.Equal(0, workspace.Frame);
        NativeBattleDiablos325Resources restored = JsonSerializer.Deserialize<NativeBattleDiablos325Resources>(JsonSerializer.Serialize(resources))!;
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restored));
        Assert.Throws<InvalidDataException>(() => (resources with { OpeningProgramme = resources.OpeningProgramme with { Address = 0 } }).Validate());
    }
}
