using System.Numerics;
using System.Text;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Mods.Tests;

public sealed class DemasterSceneLightingTests
{
    private const string FieldRules = """
        [rule city]
        field = city*, !cityhall
        time = cycle
        [rule parade]
        field = city*, !cityhall
        moment = 21-34
        time = night
        [rule dawn ceremony]
        field = citygate
        moment = 25
        time = day
        """;

    [Theory]
    [InlineData("citygate", 20, SceneTime.Cycle)]
    [InlineData("CITYGATE", 21, SceneTime.Night)]
    [InlineData("citygate", 25, SceneTime.Day)]
    [InlineData("citygate", 34, SceneTime.Night)]
    [InlineData("citygate", 35, SceneTime.Cycle)]
    [InlineData("cityhall", 22, SceneTime.Day)]
    public void LastMatchingStoryRuleOverridesOwnFieldTime(string field, int moment, SceneTime time)
    {
        SceneLightingProfile profile = Load(
            File("field_time.txt", FieldRules),
            File("textures/field_bg/ci/cityhall/cityhall_pbr.txt", "time = day"));
        Assert.Equal(time, profile.ResolveField(field, moment).Time);
    }

    [Fact]
    public void UsesLocalizedFieldMetadataAndNativePackageOverride()
    {
        SceneLightingProfile profile = Load(
            File("scene-lighting/field_time.txt", "[rule local]\nfield = cityhall\ntime = cycle", ModFormat.Memoria),
            File("field_time.txt", "[rule external]\nfield = cityhall\ntime = night"),
            File("textures/field_bg/ci/cityhall/cityhall_pbr.txt", "time = day"),
            File("textures/field_bg/ci/cityhall/cityhall_en_pbr.txt", "time = day\nsky = 1"));
        Assert.Equal(new(SceneTime.Cycle, true), profile.ResolveField("cityhall", 0));
    }

    [Fact]
    public void BattleUsesOriginAndStageRulesWhileWorldResumesCycling()
    {
        SceneLightingProfile profile = Load(File("scene_light.txt", """
            [battle]
            night = 1
            [rule stage override]
            stage = 2, 7-9
            moment = 20-30
            time = night
            night_tint = 1 2 3
            night_level = 0.5
            light = -1500 600 800 3 1.6 0.6 4000 night
            [rule later partial override]
            stage = 8
            ao = 0.25
            """));
        SceneFieldLighting origin = new(SceneTime.Night, true);
        SceneLightingState field = profile.ResolveFieldState(origin, 25, 100);
        Assert.Equal(SceneTime.Night, field.Time);
        SceneLightingState battle = profile.ResolveScene(true, 8, 25, origin, false, 100);
        Assert.Equal(SceneTime.Night, battle.Time);
        Assert.Equal(new Vector3(0.5f, 1, 1.5f), battle.Tint);
        Assert.Equal(0.25f, battle.Look!.Ao);
        Assert.Equal(SceneLightAvailability.Night, Assert.Single(battle.Look.Lights).When);
        Assert.Equal(SceneTime.Day, profile.ResolveScene(true, 8, 31, origin, false, 515).Time);
        Assert.Equal(SceneTime.Cycle, profile.ResolveScene(true, 8, 31, origin, true, 515).Time);
        SceneLightingState world = profile.ResolveScene(false, 8, 25, origin, false, 515);
        Assert.Equal(SceneTime.Cycle, world.Time);
        Assert.Empty(world.Look!.Lights);
        Assert.Equal(0.25f, world.Look.Sun);
    }

    [Fact]
    public void ClockCapsStepsSlowsBattlesAndRestoresFromAnOlderSave()
    {
        SceneLightingProfile profile = Load(File("demaster.conf", """
            DAY_NIGHT_DAY=100
            DAY_NIGHT_NIGHT=60
            DAY_NIGHT_FADE=20
            DAY_NIGHT_BATTLE=2
            DAY_NIGHT_EVENING=.8
            """));
        SceneLightingCycle cycle = profile.Cycle;
        Assert.Equal(0.5, cycle.NightAmount(0));
        Assert.Equal(0.5, cycle.NightAmount(100));
        Assert.Equal(0, cycle.NightAmount(90));
        Assert.Equal(1, cycle.NightAmount(110));
        Assert.True(cycle.EveningAmount(89) > cycle.NightAmount(89));
        const string older = """
            {"ExamTimer":null,"Playtime":null,"EventRandom":null,"CrtRandom":null,"MenuRandom":null,"GlobalRandom":null,"SeeD":null}
            """;
        GameplaySaveClocks oldClocks = JsonSerializer.Deserialize<GameplaySaveClocks>(older)!;
        Assert.Null(oldClocks.SceneClock);
        SceneLightingClock clock = new(cycle, null, 140);
        Assert.Equal(150, clock.Seconds);
        clock.Advance(cycle, TimeSpan.FromSeconds(5), false, true);
        Assert.Equal(150, clock.Seconds);
        clock.Advance(cycle, TimeSpan.FromSeconds(5), true, false);
        Assert.Equal(150.125, clock.Seconds);
        clock.Paused = true;
        clock.Advance(cycle, TimeSpan.FromSeconds(5), false, false);
        Assert.Equal(150.125, clock.Seconds);
        var updated = oldClocks with { SceneClock = new(clock.Seconds, clock.Paused) };
        GameplaySaveClocks restored = JsonSerializer.Deserialize<GameplaySaveClocks>(JsonSerializer.Serialize(updated))!;
        GameplaySaveSceneClock restoredClock = Assert.IsType<GameplaySaveSceneClock>(restored.SceneClock);
        SceneLightingClock resumed = new(cycle, new(restoredClock.Seconds, restoredClock.Paused), 3000);
        Assert.Equal(clock.Seconds, resumed.Seconds);
        Assert.True(resumed.Paused);
        resumed.Paused = false;
        resumed.Advance(cycle, TimeSpan.FromSeconds(5), false, false);
        Assert.Equal(150.375, resumed.Seconds);
    }

    private static SceneLightingProfile Load(params ModFile[] files)
    {
        List<ModDiagnostic> diagnostics = [];
        SceneLightingProfile profile = DemasterSceneLighting.Load(new(1, [], files), diagnostics, "en");
        Assert.Empty(diagnostics);
        return profile;
    }

    private static ModFile File(string path, string text, ModFormat format = ModFormat.Demaster)
    {
        return new("sample", format, path, 1, () => Encoding.UTF8.GetBytes(text));
    }
}
