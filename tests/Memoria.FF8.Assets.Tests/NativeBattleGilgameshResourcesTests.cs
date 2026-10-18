using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleGilgameshResourcesTests
{
    [Fact]
    public void Original_excalipoor_director_resources_preserve_selected_program_and_vibration_lookahead()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleGilgameshDirectorResources resources = game.ReadNativeGilgameshDirectorResources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeGilgameshDirectorResources());
        NativeExecutableMemory memory = new(game.ReadNativeExecutable());
        Assert.Equal(memory.Read(0xCD0874, 0xEA), resources.CameraProgram.Data.ToArray());
        Assert.Equal(memory.Read(0xCD0230, 0x14A), resources.Vibration.Bytes.ToArray());
        Assert.Equal(new byte[] { 0x93, 0xAA }, resources.Vibration.Bytes.AsSpan().Slice(0x148).ToArray());
        Assert.Equal(new uint[] { 0x3AA93, 0x3AA96, 0x3AA99, 0x3AAA5 }, new[] { resources.SoundInitial, resources.SoundMiddle, resources.SoundLate, resources.SoundClosing });
        NativeBattleGilgameshDirectorResources restored = JsonSerializer.Deserialize<NativeBattleGilgameshDirectorResources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restored));
        Assert.Throws<InvalidDataException>(() => (resources with { CameraProgram = resources.CameraProgram with { Data = resources.CameraProgram.Data.RemoveAt(0) } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Vibration = resources.Vibration with { Bytes = resources.Vibration.Bytes.RemoveAt(0x149) } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Vibration = resources.Vibration with { Bytes = resources.Vibration.Bytes.SetItem(0x148, 0) } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { SoundClosing = resources.SoundInitial }).Validate());

        NativeBattleActionMatrix identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        NativeBattleCameraPose pose = new(new(1, 2, 3), new(4, 5, 6), 288, 0);
        NativeBattleGuardianCameraTask task = NativeBattleGuardianCameraProgram.Begin63E960(NativeBattleGuardianCameraProgram.FromColdProcess(), resources.CameraProgram.Data, 0);
        int frames = 0;
        while (!task.Complete && frames < 4096)
        {
            NativeBattleGuardianCameraStep step = NativeBattleGuardianCameraProgram.Advance63E9C0(task, identity, pose, false);
            task = step.Task;
            pose = step.Pose;
            frames++;
        }
        Assert.True(task.Complete);
        Assert.Equal(0xEA, task.Offset);
    }

    [Fact]
    public void Original_excalipoor_files_preserve_actual_directory_identities_and_complete_bytes()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets battle = new(TestResources.GameDirectory);
        NativeBattleGilgameshResources resources = game.ReadNativeGilgameshResources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeGilgameshResources());
        Assert.Equal(new ushort[] { 745, 746, 747, 748, 749, 750, 751, 752, 753, 755, 757 }, resources.Files.Select(file => file.FileId));
        Assert.Equal(new[] { 103968, 30688, 132640, 33824, 31004, 4, 50592, 4, 4, 34336, 99360 }, resources.Files.Select(file => file.Bytes.Length));
        foreach (NativeBattleArchiveFile file in resources.Files)
        {
            NativeBattleArchiveFile original = battle.ReadNativeBattleFile(file.FileId);
            Assert.Equal(original.Name, file.Name);
            Assert.Equal(original.Bytes.ToArray(), file.Bytes.ToArray());
        }
        NativeBattleGilgameshResources restored = JsonSerializer.Deserialize<NativeBattleGilgameshResources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restored));
        Assert.Throws<NotSupportedException>(() => NativeBattleGilgameshResourcesReader.Read([], resources.Files));
        if (Environment.GetEnvironmentVariable("MEMORIA_FF8_GILGAMESH_RESOURCE_EXPORT") is { } export)
        {
            Assert.False(Directory.Exists(export) && Directory.EnumerateFileSystemEntries(export).Any(), "Export requires a fresh empty directory.");
            Directory.CreateDirectory(export);
            foreach (NativeBattleArchiveFile file in resources.Files)
            {
                using FileStream stream = new(Path.Combine(export, $"data-{file.FileId}.bin"), FileMode.CreateNew);
                stream.Write(file.Bytes.AsSpan());
            }
            using var magic = game.OpenResourceArchive("magic");
            var entry = Assert.Single(magic.Entries, entry => Path.GetFileName(entry.RelativePath.Replace('\\', '/')).Equals("mag326-329.tim", StringComparison.OrdinalIgnoreCase));
            using Stream source = entry.OpenForRead();
            using MemoryStream content = new();
            source.CopyTo(content);
            byte[] initializer = content.ToArray();
            using (FileStream stream = new(Path.Combine(export, "initial-tim.bin"), FileMode.CreateNew))
            {
                stream.Write(initializer);
            }
            using FileStream manifest = new(Path.Combine(export, "manifest.json"), FileMode.CreateNew);
            JsonSerializer.Serialize(manifest, new
            {
                Files = resources.Files.Select(file => new { file.FileId, file.Name, Bytes = file.Bytes.Length, Sha256 = Convert.ToHexString(SHA256.HashData(file.Bytes.AsSpan())) }),
                Initializer = new { Name = "mag326-329.tim", Bytes = initializer.Length, Sha256 = Convert.ToHexString(SHA256.HashData(initializer)) }
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [Fact]
    public void Excalipoor_file_profile_rejects_missing_reordered_misidentified_and_partial_files()
    {
        ushort[] ids = [745, 746, 747, 748, 749, 750, 751, 752, 753, 755, 757];
        string[] names = ["mag326_a.dat", "mag326_b.dat", "mag326_c.dat", "mag326_d.dat", "mag326_e.dat", "mag326_f.dat", "mag326_g.dat", "mag326_h.dat", "mag326_k.dat", "mag328_i.dat", "mag329_j.dat"];
        int[] lengths = [103968, 30688, 132640, 33824, 31004, 4, 50592, 4, 4, 34336, 99360];
        ImmutableArray<NativeBattleArchiveFile> files = ids.Select((id, index) => new NativeBattleArchiveFile(id, names[index], new byte[lengths[index]].ToImmutableArray())).ToImmutableArray();
        NativeBattleGilgameshResources resources = new(files);
        resources.Validate();
        Assert.Throws<InvalidDataException>(() => (resources with { Files = default }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Files = files.RemoveAt(10) }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Files = files.SetItem(0, files[1]) }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Files = files.SetItem(10, files[10] with { FileId = 756, Name = "mag328_j.dat" }) }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Files = files.SetItem(0, files[0] with { Name = "other.dat" }) }).Validate());
        for (int index = 0; index < files.Length; index++)
        {
            Assert.Throws<InvalidDataException>(() => (resources with { Files = files.SetItem(index, files[index] with { Bytes = files[index].Bytes.RemoveAt(0) }) }).Validate());
            Assert.Throws<InvalidDataException>(() => (resources with { Files = files.SetItem(index, files[index] with { Bytes = default }) }).Validate());
        }
        JsonObject missing = JsonNode.Parse(JsonSerializer.Serialize(resources))!.AsObject();
        missing.Remove(nameof(NativeBattleGilgameshResources.Files));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeBattleGilgameshResources>(missing.ToJsonString()));
    }
}
