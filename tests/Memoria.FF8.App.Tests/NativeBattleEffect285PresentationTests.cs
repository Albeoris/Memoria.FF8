using System.Text.Json;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect285PresentationTests
{
    [Fact]
    public void Archived_effect285_report_records_workspace_lifecycle_and_particle_phases()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT285_OBSERVATION_DIRECTORY");
        Assert.SkipUnless(directory is not null, "Select the archived Effect285 observation report.");
        using JsonDocument report = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory!, "effect285-native-observations.json")));
        JsonElement recorded = report.RootElement;
        foreach (string name in new[] { "WorkspaceSourcesObserved", "ManualCamera", "SelectiveCameraRestore", "Hidden", "FlagsRestored", "StageHidden", "StageVisible", "Intensity2400", "FirstAct", "SecondAct", "ActOverlap", "Morph896", "Particles", "CurrentBonePoint", "Geometry", "MainRetired", "Uploaded", "Drained" })
        {
            Assert.True(recorded.GetProperty(name).GetBoolean(), name);
        }
        Assert.Equal(72, recorded.GetProperty("ParticleBirths").GetInt32());
        Assert.Equal(1, recorded.GetProperty("Contacts").GetInt32());
        Assert.Equal(1, recorded.GetProperty("ContactGlyphs").GetInt32());
        Assert.Equal(new[] { 0, 1 }, recorded.GetProperty("PacketBanks").EnumerateArray().Select(value => value.GetInt32()));
        Assert.Equal(Enumerable.Range(1, 7), recorded.GetProperty("ParticlePhases").EnumerateArray().Select(value => value.GetInt32()));
    }
}
