using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Mods;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class JunctionVoiceInstalledTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

    [Fact]
    public void InstalledArchiveKeepsValidFieldGroupsAndFrozenVoiceClipsWithOwningRejections()
    {
        string? configured = Environment.GetEnvironmentVariable("MEMORIA_FF8_VOICE_FIXTURE");
        Assert.SkipUnless(File.Exists(configured) && Directory.Exists(TestResources.GameDirectory), "Configure the optional voice fixture catalog and original game data.");
        string catalogPath = Path.GetFullPath(configured!);
        Fixture fixture = JsonSerializer.Deserialize<Fixture>(File.ReadAllText(catalogPath), JsonOptions)!;
        string archivePath = Path.GetFullPath(fixture.Archive, Path.GetDirectoryName(catalogPath)!);
        Assert.Equal(fixture.ArchiveSha256, FileHash(archivePath));
        Assert.NotEmpty(fixture.Groups);
        Assert.NotEmpty(fixture.Clips);
        string root = Path.Combine(Path.GetTempPath(), "ff8-voice-installed-" + Guid.NewGuid().ToString("N"));
        string junction = Path.Combine(root, "Junction");
        Directory.CreateDirectory(junction);
        string imported = Path.Combine(junction, "candidate.iroj");
        const string packageId = "Junction/candidate.iroj";
        try
        {
            File.Copy(archivePath, imported);
            File.WriteAllText(imported + ".ini", "enabled = 1");
            using IsolatedFieldGame game = new();
            using GameResourceHost host = new(game.GameDirectory, watch: false, modsDirectory: root);
            Evidence("initial-host.json", new { diagnostics = host.Mods.Diagnostics, packages = host.Mods.Catalog.Packages });
            Assert.Equal(ModEffectiveStatus.Active, Assert.Single(host.Mods.Catalog.Packages, package => package.Id == packageId).Status);
            Assert.False(host.Mods.View.IsReloadPending);
            Assert.NotNull(host.VoiceResolver);
            Assert.DoesNotContain(host.Mods.Diagnostics, item => item.Code is "Junction.InvalidVoiceResource" or "external-voice-config-invalid");
            HashSet<string> rejected = [];
            foreach (ModDiagnostic diagnostic in host.Mods.Diagnostics.Where(item => item.Code == "Junction.InvalidFieldResource" && item.PackageId == packageId))
            {
                Assert.NotNull(diagnostic.RelativePath);
                ResourceKey? address = JunctionEncodedResources.Address(diagnostic.RelativePath);
                Assert.True(address.HasValue, "Rejected field diagnostics must identify their owning resource.");
                string? field = JunctionEncodedResources.FieldGroup(address.Value);
                Assert.NotNull(field);
                rejected.Add(field);
            }
            Assert.All(fixture.RequiredRejectedFields, field => Assert.Contains(field, rejected));
            using IResourceSource original = GameDataSourceFactory.Open(game.GameDirectory);
            using GameAssets assets = new(game.GameDirectory, resources: host.Resources);
            JunctionArchive sourceArchive = new(imported);
            List<object> observations = [];
            List<object> sidecars = [];
            foreach (FieldGroup group in fixture.Groups)
            {
                bool excluded = rejected.Contains(group.Field);
                foreach (ResourceCase resource in group.Resources)
                {
                    ResourceKey key = new(resource.Canonical);
                    if (resource.Role == "unconsumed-sidecar")
                    {
                        Assert.Null(JunctionEncodedResources.Address(resource.Canonical));
                        Assert.Contains(host.Mods.Diagnostics, diagnostic => diagnostic.PackageId == packageId && diagnostic.RelativePath is not null && Path.GetFileName(diagnostic.RelativePath).Equals(Path.GetFileName(resource.Source), StringComparison.OrdinalIgnoreCase));
                        JunctionArchiveEntry entry = Assert.IsType<JunctionArchiveEntry>(sourceArchive.Find(resource.Source));
                        using Stream stored = entry.OpenRead();
                        Assert.Equal(resource.Sha256, Convert.ToHexString(SHA256.HashData(stored)));
                        sidecars.Add(new { group.Field, resource.Source, resource.Role, resource.Sha256, resource.Bytes });
                        continue;
                    }
                    Assert.Equal("runtime-resource", resource.Role);
                    if (excluded)
                    {
                        ResourceContent? native = original.Find(key);
                        if (native is null)
                        {
                            Assert.Throws<FileNotFoundException>(() => host.Resources.Get(key));
                        }
                        else
                        {
                            using Stream stream = native.OpenRead();
                            Assert.Equal(native.Provenance, host.Resources.Get(key).Provenance);
                            Assert.Equal(Convert.ToHexString(SHA256.HashData(stream)), Hash(host.Resources.Read(key)));
                        }
                    }
                    else
                    {
                        Assert.StartsWith(packageId + ":", host.Resources.Get(key).Provenance);
                        byte[] selected = host.Resources.Read(key);
                        Assert.Equal(resource.Bytes, selected.Length);
                        Assert.Equal(resource.Sha256, Hash(selected));
                    }
                }
                if (!excluded)
                {
                    FieldLogicResources logic = assets.ReadFieldLogicResources(group.Field);
                    JsmProgram program = JsmProgram.Read(logic.Program);
                    JsmResourceValidationResult validation = JsmResourceValidation.Validate(program, logic.Messages.Count);
                    Assert.Empty(validation.Errors);
                    observations.Add(new { group.Field, rejected = false, operations = program.Operations.Count, texts = logic.Messages.Count, validation.DynamicMessageReferences, validation.UnprovenStackOperations });
                }
                else
                {
                    Assert.DoesNotContain(host.VoiceResolver!.Files, file => ExternalVoiceResolver.Address(file)?.Path.StartsWith($"voice/{group.Field}/", StringComparison.Ordinal) == true);
                    observations.Add(new { group.Field, rejected = true, resources = group.Resources.Length });
                }
            }
            object? standalone = null;
            if (fixture.StandaloneSave is not null)
            {
                string save = Path.GetFullPath(fixture.StandaloneSave, Path.GetDirectoryName(catalogPath)!);
                Assert.Equal(fixture.StandaloneSha256, FileHash(save));
                GameState state = GameSession.Load(save);
                Assert.Equal(SceneKind.Field, state.Scene);
                Assert.True(state.EventState.HasValue, "A standalone field check requires its actual saved VM state.");
                JsonElement savedEvents = state.EventState.Value;
                string programHash = savedEvents.GetProperty("ProgramHash").GetString()!;
                int frame = savedEvents.GetProperty("Frame").GetInt32();
                using GameWorld world = new(new GameSession(state), game.GameDirectory, resources: host.Resources, readSceneMods: () => host.SceneMods);
                world.EnsureField();
                Assert.Equal(state.FieldId, world.Events.FieldId);
                Assert.Equal(frame, world.Events.Frame);
                Assert.Equal(programHash, Hash(world.Field!.Jsm));
                Assert.Null(world.Events.LastFault);
                Assert.Equal(fixture.StandaloneSha256, FileHash(save));
                standalone = new { field = world.Field.Name, state.FieldId, frame, programHash, preservedSource = true, advancedFrames = 0 };
            }
            string tools = MediaToolsInstallation.ResolveDirectory();
            bool canDecode = File.Exists(Path.Combine(tools, "ffmpeg.exe"));
            using FieldAudioPresentation? output = canDecode ? new FieldAudioPresentation(game.GameDirectory, tools) : null;
            output?.SetOutputPreferences(0, 0, 0);
            List<object> clips = [];
            foreach (Clip clip in fixture.Clips)
            {
                Assert.DoesNotContain(clip.Field, rejected);
                ExternalVoiceSelection selected = Assert.IsType<ExternalVoiceSelection>(host.VoiceResolver!.Resolve(clip.Field, clip.Window, clip.Message, clip.Page, clip.Choice));
                Assert.Equal(packageId, selected.PackageId);
                Assert.Equal(new ResourceKey(clip.Canonical), selected.Key);
                ResourceContent content = host.Resources.Get(selected.Key);
                Assert.StartsWith(packageId + ":", content.Provenance);
                using (Stream stream = content.OpenRead())
                {
                    Assert.Equal(clip.Sha256, Convert.ToHexString(SHA256.HashData(stream)));
                }
                byte[] encoded = host.Resources.Read(selected.Key);
                Assert.Equal(clip.Bytes, encoded.Length);
                Assert.Equal(clip.Sha256, Hash(encoded));
                Assert.True(encoded.AsSpan().StartsWith("OggS"u8));
                int? pcmBytes = null;
                if (canDecode)
                {
                    byte[] pcm = PcmAudioDecoder.Decode(tools, "pipe:0", encoded, 44100, 2);
                    Assert.NotEmpty(pcm);
                    Assert.Equal(0, pcm.Length % 4);
                    pcmBytes = pcm.Length;
                    object identity = new();
                    DialogueState dialogue = new(clip.Window, clip.Message, "", 0, 0, NativeWindow: new(1, 512, 0));
                    output!.SynchronizeVoices(clip.Field, identity, [new(dialogue, clip.Page)], host.Resources, host.VoiceResolver);
                    if (clip.Choice is int choice)
                    {
                        output.SynchronizeVoices(clip.Field, identity, [new(dialogue, clip.Page, choice)], host.Resources, host.VoiceResolver);
                    }
                    Assert.Null(output.Error);
                    Assert.Equal(1, output.DialogueVoiceCount);
                    Assert.True(output.IsDialogueVoicePlaying(clip.Window));
                    output.SetFocusPaused(true);
                    Assert.False(output.IsDialogueVoicePlaying(clip.Window));
                    output.SetFocusPaused(false);
                    Assert.True(output.IsDialogueVoicePlaying(clip.Window));
                    output.SetVoicePresentationPaused(true);
                    Assert.False(output.IsDialogueVoicePlaying(clip.Window));
                    output.SetVoicePresentationPaused(false);
                    Assert.True(output.IsDialogueVoicePlaying(clip.Window));
                    output.SynchronizeVoices(null, null, [], host.Resources, host.VoiceResolver);
                    Assert.Equal(0, output.DialogueVoiceCount);
                    Assert.Null(output.Error);
                }
                clips.Add(new { clip.Role, clip.Canonical, clip.Sha256, clip.Bytes, selected.Gain, pcmBytes });
            }
            Evidence("installed-voice.json", new { passed = true, fixture.ArchiveSha256, fixture.VoiceEntryCount, selectedVoiceCount = host.VoiceResolver!.Files.Count(), fields = observations, sidecars, standalone, clips, decoded = canDecode, diagnostics = host.Mods.Diagnostics, scope = "Installed archive, selected logic and encoded voice acquisition; audio device starts/pause/resume/close at zero output gain, no gameplay state advanced." });
        }
        finally
        {
            Assert.Equal(fixture.ArchiveSha256, FileHash(archivePath));
            Directory.Delete(root, true);
        }
    }

    [DllImport("user32.dll")]
    private static extern nint GetDesktopWindow();

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static string FileHash(string path)
    {
        using Stream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static void Evidence(string name, object value)
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF8_MOD_EVIDENCE_DIRECTORY");
        if (!string.IsNullOrWhiteSpace(root))
        {
            string directory = Path.Combine(root, "Voice");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, name), JsonSerializer.Serialize(value, JsonOptions));
        }
    }

    private sealed record Fixture(string Archive, string ArchiveSha256, FieldGroup[] Groups, string[] RequiredRejectedFields, Clip[] Clips, int VoiceEntryCount, string? StandaloneSave = null, string? StandaloneSha256 = null);
    private sealed record FieldGroup(string Field, ResourceCase[] Resources);
    private sealed record ResourceCase(string Canonical, string Source, string Sha256, int Bytes, string Role = "runtime-resource");
    private sealed record Clip(string Role, string Field, int Window, int Message, int Page, int? Choice, string Canonical, string Source, string Sha256, int Bytes);
}
