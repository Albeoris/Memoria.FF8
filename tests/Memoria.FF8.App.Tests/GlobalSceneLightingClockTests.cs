using System.Text;
using Memoria.FF8.Mods;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GlobalSceneLightingClockTests
{
    [Theory]
    [InlineData("", true, false)]
    [InlineData("DAY_NIGHT_SAVES=0", false, false)]
    [InlineData("DAY_NIGHT_SAVES=1", true, false)]
    [InlineData("DAY_NIGHT_SAVES=2", true, true)]
    public void ReadsPublishedClockModes(string setting, bool perSave, bool invalid)
    {
        ModFile file = new("fixture", ModFormat.Demaster, "demaster.conf", 1, () => Encoding.UTF8.GetBytes(setting));
        List<ModDiagnostic> diagnostics = [];
        SceneLightingProfile profile = DemasterSceneLighting.Load(new(1, [], [file]), diagnostics);
        Assert.Equal(perSave, profile.ClockPerSave);
        Assert.Equal(invalid, diagnostics.Any(diagnostic => diagnostic.Message.Contains("DAY_NIGHT_SAVES", StringComparison.Ordinal)));
    }

    [Fact]
    public void SharedClockStartsAtMorningAndRestartsFromHostPreferences()
    {
        string root = Path.Combine(Path.GetTempPath(), "MemoriaSceneClock-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllBytes(Path.Combine(root, "main.zzz"), new byte[4]);
            File.WriteAllBytes(Path.Combine(root, "other.zzz"), new byte[4]);
            GlobalSettingsPaths settings = new(isolatedDirectory: Path.Combine(root, "settings"));
            SceneLightingCycle cycle = new();
            using (GameResourceHost host = Open())
            {
                host.ConfigureGlobalSceneClock(settings);
                SceneLightingClock clock = host.ReadGlobalSceneClock(cycle);
                Assert.Equal(cycle.Fade / 2, clock.Seconds);
                clock.Advance(cycle, TimeSpan.FromSeconds(0.25), battle: false, held: true);
                Assert.Equal(cycle.Fade / 2, clock.Seconds);
                clock.Advance(cycle, TimeSpan.FromSeconds(0.25), battle: false, held: false);
                Assert.Same(clock, host.ReadGlobalSceneClock(cycle));
            }
            Assert.Single(Directory.GetFiles(settings.DirectoryPath));
            using GameResourceHost restarted = Open();
            restarted.ConfigureGlobalSceneClock(settings);
            Assert.Equal(cycle.Fade / 2 + 0.25, restarted.ReadGlobalSceneClock(cycle).Seconds);

            GameResourceHost Open()
            {
                return new(root, applicationBaseDirectory: root, watch: false, modsDirectory: Path.Combine(root, "Mods"));
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(0, 20.25)]
    [InlineData(1, 400)]
    public void WorldReplacementUsesConfiguredClockOwner(int mode, double expected)
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_REMASTERED_DIR");
        Assert.SkipUnless(game is not null && File.Exists(Path.Combine(game, "main.zzz")), "Set MEMORIA_FF8_REMASTERED_DIR to run the installed resource-host check.");
        string root = Path.Combine(Path.GetTempPath(), "MemoriaSceneClock-" + Guid.NewGuid().ToString("N"));
        string mods = Path.Combine(root, "Mods");
        string package = Path.Combine(mods, "Demaster", "Clock");
        Directory.CreateDirectory(package);
        try
        {
            File.WriteAllText(Path.Combine(package, "demaster.conf"), $"DAY_NIGHT_SAVES={mode}");
            using GameResourceHost host = new(game!, applicationBaseDirectory: root, watch: false, modsDirectory: mods);
            host.ConfigureGlobalSceneClock(new(isolatedDirectory: Path.Combine(root, "settings")));
            Assert.Equal(mode == 1, host.SceneLightingProfile.ClockPerSave);
            GameState original = host.CreateColdSession().State with { Scene = SceneKind.Field, FieldId = 95, FieldName = "testbl6" };
            using (GameWorld first = World(200))
            {
                Assert.Equal(mode == 0 ? 20 : 200, first.ReadSceneLighting()!.ClockSeconds);
                first.AdvanceSceneLighting(TimeSpan.FromSeconds(0.25), battleTransition: false);
            }
            using GameWorld second = World(400);
            Assert.Equal(expected, second.ReadSceneLighting()!.ClockSeconds);

            GameWorld World(double seconds)
            {
                GameSession session = new(original with { SceneClock = new(seconds, false) });
                return new(session, game!, resources: host.Resources, modelOverrides: host);
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
