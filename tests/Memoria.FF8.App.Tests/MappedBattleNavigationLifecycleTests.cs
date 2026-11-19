using System.Collections;
using System.Reflection;
using System.Text.Json;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Media;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class MappedBattleNavigationLifecycleTests(ITestOutputHelper output)
{
    [Fact]
    public void Actual_mapped_battle_settlement_keeps_live_navigation_across_two_encounters()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_AUTOPLAY_LIFECYCLE_SAVE");
        string? ff7Root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? ff8Root = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(path is not null && ff7Root is not null && ff8Root is not null, "Select both installations and an external mapped field autoplay fixture.");
        byte[] original = File.ReadAllBytes(path!);
        using MemoryStream input = new(original);
        CombinedSessionSave source = CombinedSessionCodec.Read(input);
        Assert.NotEmpty(source.GetMixedCombatBindings());
        Ff7Installation installation = Ff7Installation.Discover(ff7Root!);
        string temporary = Path.Combine(Path.GetTempPath(), "memoria-mapped-navigation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        try
        {
            using GpuTestDevice device = new();
            using SpriteBatch batch = new(device.Device);
            using Ff7Scene scene = new(device.Device, installation, "", new(50, 32000), Movie, source, audioEnabled: false);
            scene.ConfigureCharacterIdentity(ff8Root!);
            scene.Continue(source);
            scene.ConfigureSharedDialogueDefaults(batch, ff8Root!, temporary);
            scene.ConfigureSharedControls(new KeyboardInput { Profile = ControlsProfile.CreateDefault() }, new GameplayClock());
            scene.ConfigureAutoPlayCheckpoint(Path.Combine(temporary, "current.ffsave"));
            scene.ApplyAutoPlayCommand("start");
            Ff7Session native = scene.Session!;
            object policy = Field(scene, "autoPlayPolicy");
            object controller = Field(scene, "autoPlay");
            object driver = Field(controller, "driver");
            object cycleHistory = Field(policy, "recentPositions");
            Ff7BattleSession? encounteredBattle = null;
            int settlements = 0;
            int originalReceipts = source.GuestBattleSettlements?.Length ?? 0;
            scene.MixedCombatSettled += saved =>
            {
                int receipts = saved.GuestBattleSettlements?.Length ?? 0;
                if (receipts == originalReceipts + settlements)
                {
                    return;
                }
                Assert.Equal(originalReceipts + settlements + 1, receipts);
                Assert.Same(native, scene.Session);
                Assert.Same(policy, Field(scene, "autoPlayPolicy"));
                Assert.Same(controller, Field(scene, "autoPlay"));
                Assert.Same(driver, Field(controller, "driver"));
                Assert.Null(native.Battle);
                Assert.NotNull(saved.GuestBattleSettlements?.LastOrDefault());
                Assert.NotNull(native.GuestFieldVitals);
                var settledGuest = saved.GuestBattleSettlements!.Last().GetGuests()[0];
                Assert.Equal(settledGuest.SettledHp, native.GuestFieldVitals!.Read().Hp);
                Assert.Equal(JsonSerializer.Serialize(saved.Ff7! with { AudioPresentation = null }), JsonSerializer.Serialize(native.Capture()));
                string committed = JsonSerializer.Serialize(native.Capture());
                Assert.Throws<InvalidOperationException>(() => CrossoverBattleVitalsSettlement.CommitNativeResult(native, encounteredBattle!, saved.Ff7!, saved.Ff7!));
                Assert.Equal(committed, JsonSerializer.Serialize(native.Capture()));
                settlements++;
            };
            string? battleNavigation = null;
            string? battleHistory = null;
            bool encountered = false;
            long firstRequest = -1;
            var elapsed = System.Diagnostics.Stopwatch.StartNew();
            for (int frame = 0; frame < 15000 && settlements < 2 && elapsed.Elapsed < TimeSpan.FromMinutes(2); frame++)
            {
                scene.StepAutoPlay(1);
                Assert.Null(scene.Error);
                Assert.Same(native, scene.Session);
                Assert.Same(policy, Field(scene, "autoPlayPolicy"));
                Assert.Same(controller, Field(scene, "autoPlay"));
                Assert.Same(driver, Field(controller, "driver"));
                if (!encountered && native.Battle is { } battle)
                {
                    Assert.NotEqual(firstRequest, battle.RequestId);
                    firstRequest = battle.RequestId;
                    encounteredBattle = battle;
                    battleNavigation = NavigationSnapshot(driver);
                    battleHistory = SequenceSnapshot(cycleHistory);
                    Assert.NotEmpty(((IEnumerable)cycleHistory).Cast<object>());
                    Assert.NotNull(Field(driver, "finalTarget"));
                    encountered = true;
                    Assert.False((bool)policy.GetType().GetProperty("NativePlanningPending", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(policy)!);
                }
                else if (encountered && native.Battle is null)
                {
                    Assert.Same(cycleHistory, Field(policy, "recentPositions"));
                    Assert.Equal(battleNavigation, NavigationSnapshot(driver));
                    Assert.Equal(battleHistory, SequenceSnapshot(cycleHistory));
                    encountered = false;
                }
            }
            Assert.Equal(2, settlements);
            Assert.False(encountered);
            Assert.True(scene.AutoPlayRunning);
            Assert.Equal(original, File.ReadAllBytes(path!));
            output.WriteLine($"Two actual mapped encounters settled with unchanged Session/policy/controller/driver, contact/waypoint caches and position history; elapsed {elapsed.Elapsed.TotalSeconds:F3}s.");

            Ff7MovieSource Movie(int id)
            {
                Ff7MovieResources resource = Ff7MovieResources.Load(installation, id);
                Ff7MovieCameraTrack? track = resource.Cameras is { } cameras ? new(cameras) : null;
                return new(resource.Path, new MovieInformation(320, 240, 15, 120, 1800), 1800, 15) { CameraRecordCount = resource.Cameras?.Count ?? 0, SnapshotCameraFrame = track is null ? null : (frame, words) => track.SnapshotAt(frame, words) };
            }
        }
        finally
        {
            Directory.Delete(temporary, true);
        }
    }

    private static object Field(object owner, string name)
    {
        return owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
    }

    private static string SequenceSnapshot(object sequence)
    {
        return JsonSerializer.Serialize(((IEnumerable)sequence).Cast<object>().ToArray(), new JsonSerializerOptions { IncludeFields = true });
    }

    private static string NavigationSnapshot(object driver)
    {
        Dictionary<string, object?> facts = [];
        foreach (FieldInfo field in driver.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic).Where(field => new[] { "waypoint", "contact", "talk", "approach", "obstacle", "finalTarget", "finalApproach" }.Any(part => field.Name.Contains(part, StringComparison.OrdinalIgnoreCase))))
        {
            facts[field.Name] = field.GetValue(driver);
        }
        return JsonSerializer.Serialize(facts, new JsonSerializerOptions { IncludeFields = true });
    }
}
