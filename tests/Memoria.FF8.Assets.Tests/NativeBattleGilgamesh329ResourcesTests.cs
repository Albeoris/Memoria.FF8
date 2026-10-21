using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleGilgamesh329ResourcesTests
{
    [Fact]
    public void Excaliber_uses_its_actual_camera_vibration_stream_and_file_without_rebinding_Excalipoor()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleGilgameshEffectResources selected = game.ReadNativeGilgameshEffectResources(329);
        NativeBattleGilgameshEffectResources other = game.ReadNativeGilgameshEffectResources();
        Assert.Same(selected, game.ReadNativeGilgameshEffectResources(329));
        Assert.Same(other, game.ReadNativeGilgameshEffectResources());
        selected.Files.Validate();
        selected.Director.Validate();
        NativeExecutableMemory memory = new(game.ReadNativeExecutable());
        Assert.Equal((ushort)329, selected.Files.Effect);
        Assert.Equal((ushort)329, selected.Director.Effect);
        Assert.Equal("FF8_EN:CD0788", selected.Director.CameraProgram.Identity);
        Assert.Equal(memory.Read(0xCD0788, 0xEA), selected.Director.CameraProgram.Data.ToArray());
        Assert.Equal(other.Director.CameraProgram.Data.ToArray(), selected.Director.CameraProgram.Data.ToArray());
        Assert.Equal(memory.Read(0xCD00E4, 0x14E), selected.Director.Vibration.Bytes.ToArray());
        Assert.Equal((ushort)68, selected.Director.Vibration.ReadWord(0x100));
        Assert.Equal((ushort)0, selected.Director.Vibration.ReadWord(0x14C));
        Assert.Equal(173, selected.InitialStreamNumber);
        Assert.Equal(0x3AAA2u, selected.Director.SoundClosing);
        NativeBattleArchiveFile attack = Assert.Single(selected.Files.Files, file => file.FileId == 756);
        Assert.Equal("mag328_j.dat", attack.Name);
        Assert.Equal(99360, attack.Bytes.Length);
        Assert.Equal("2437BB67E55715537447F9C83D90CC981634BF431F58E681BA23E7184A3EE0B6", Convert.ToHexString(SHA256.HashData(attack.Bytes.AsSpan())));
        Assert.DoesNotContain(selected.Files.Files, file => file.FileId == 757);
        Assert.Equal((ushort)330, other.Files.Effect);
        Assert.Contains(other.Files.Files, file => file.FileId == 757);
        Assert.Equal(0x3AAA5u, other.Director.SoundClosing);
        NativeBattleGilgameshDirectorResources restored = JsonSerializer.Deserialize<NativeBattleGilgameshDirectorResources>(JsonSerializer.Serialize(selected.Director))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(selected.Director), JsonSerializer.Serialize(restored));
        Assert.Throws<InvalidDataException>(() => (selected.Director with { Effect = 330 }).Validate());
        Assert.Throws<InvalidDataException>(() => (selected.Files with { Effect = 330 }).Validate());
    }
}
