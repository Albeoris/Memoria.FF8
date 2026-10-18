using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleGilgamesh330PresentationTests
{
    private static readonly TimeSpan Callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);

    [Fact]
    public void External_original_support_producer_and_Excalipoor_complete_with_independent_cold_boundaries()
    {
        string? sourcePath = Environment.GetEnvironmentVariable("MEMORIA_FF8_GILGAMESH330_SOURCE");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(sourcePath), "Select an external standalone idle battle fixture.");
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_GILGAMESH330_OUTPUT") ?? throw new InvalidDataException("Select a distinct empty output directory.");
        Assert.False(Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any());
        Directory.CreateDirectory(output);
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(sourcePath!);
        Assert.Empty(source.QueuedActions);
        Assert.Null(source.ActiveAction);
        Assert.Empty(source.Definition.GuardianForces);
        Assert.Empty(source.GuardianForces);
        Assert.False(source.NativeGraphics!.Descriptors.Queue.Any(index => source.NativeGraphics.Descriptors.Packets[index].Command == 104));
        BattleNativeAction kernel = Assert.Single(assets.NativeActions, action => action.Command == 245 && action.ActionId == 8);
        Assert.Equal((ushort)330, kernel.MagicId);
        byte randomIndex = Enumerable.Range(0, 256).Select(index => (byte)index).First(index =>
        {
            NativeBattleRandomState candidate = new(Enumerable.Repeat(index, 8).ToImmutableArray(), 0);
            candidate = candidate.Draw(out byte first);
            _ = candidate.Draw(out byte second);
            return first <= 12 && second / 64 + 7 == 8;
        });
        NativeBattleRandomState random = new(Enumerable.Repeat(randomIndex, 8).ToImmutableArray(), 0);
        NativeBattleRandomState afterChance = random.Draw(out byte chance);
        _ = afterChance.Draw(out byte selector);
        Assert.True(chance <= 12);
        Assert.Equal(8, selector / 64 + 7);
        BattleGuardianForceVital[] guardianProfile = [new(0, 1000, 1000, true)];
        BattleDefinition definition = source.Definition with
        {
            NativeActions = assets.NativeActions.ToArray(),
            ActionDefinitions = assets.ActionDefinitions.Select(action => action.Ability == BattleAbility.GuardianForce && action.Id == 0 ? action with { Level = 10 } : action).ToArray(),
            GuardianForces = guardianProfile,
            NativeEffectCatalog = game.ReadNativeEffectCatalogForEffects([330]),
            NativeClockRules = assets.ReadNativeClockRules(),
            NativeSupportRules = assets.ReadNativeSupportRules(),
            NativeSavedBattleFlags = 8,
            NativeSavedBattleFlagsKnownMask = 255
        };
        NativeBattleEffectMemory memory = source.NativeEffectMemory ?? throw new InvalidDataException("The fixture requires its existing effect process.");
        Assert.Null(memory.Gilgamesh);
        Assert.Null(memory.PcRenderRequest);
        int atbMaximum = checked(4000 * ((definition.NativeBattleSpeed ?? throw new InvalidDataException("The fixture requires its actual saved battle-speed BYTE.")) + 1));
        BattleSnapshot prepared = source with
        {
            Definition = definition,
            NativeSupport = new(true, 0, 0),
            GuardianForces = guardianProfile,
            NativeClock = source.NativeClock ?? new(1, 0, 0),
            Combatants = source.Combatants.Select(combatant =>
            {
                NativeBattleAtbState gauge = combatant.NativeAtb ?? new(atbMaximum, checked((int)((long)atbMaximum * combatant.Atb / BattleSession.AtbMaximum)));
                Assert.Equal(combatant.Atb, gauge.Display);
                return combatant with { NativeAtb = gauge };
            }).ToArray(),
            Random = random,
            NativeEffectMemory = memory with { Gilgamesh = NativeBattleGilgameshProcess.FromColdProcess(), PcRenderRequest = NativeBattlePcRenderRequest.FromColdProcess() }
        };
        NativeBattleRestorationPresentationTests.Publish(output, "prepared-support", prepared, null);
        File.WriteAllText(Path.Combine(output, "fixture-changes.json"), JsonSerializer.Serialize(new
        {
            SourceSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sourcePath!))),
            Changes = new[] { "Installed native kernel actions and action definitions", "Explicit controlled acquired GF0 HP1000/1000 and level10 profile, shared by definition and snapshot; same profile as IndependentGuardianItemTests", "Selected original effect330 catalog resources", "Installed kernel ATB speed and support cooldown rules", "Explicit active battle clock with original UI admission and action-pause owners", "Missing physical ATB gauges converted from existing HUD values using saved battle speed; current HUD values preserved", "Known saved support flags8", "Explicit periodic support entry initialized, cooldown0, GilgameshUsed0", "Explicit battle RNG indexes/lane0 selected by actual chance and quartile table", "Authenticated cold Gilgamesh model and flare owners; cold PC request owner" },
            SourceGuardianForces = source.GuardianForces,
            PreparedGuardianForces = guardianProfile,
            AcquiredGuardianLevels = new[] { new { Id = 0, Level = 10 } },
            SourceClock = source.NativeClock,
            PreparedClock = prepared.NativeClock,
            AtbGauges = source.Combatants.Select((combatant, index) => new { combatant.Id, Hud = combatant.Atb, Before = combatant.NativeAtb, After = prepared.Combatants[index].NativeAtb }),
            RandomIndex = randomIndex, Chance = chance, Selector = selector,
            Kernel = kernel
        }, new JsonSerializerOptions { WriteIndented = true }));
        BattleSession battle = BattleSession.Restore(NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, "prepared-support.battle.json")));
        BattleScenePresentation scene = new(assets, definition, true);
        int ticks = 0;
        bool produced = false;
        bool initialized = false;
        bool completed = false;
        int callbacks = 0;
        HashSet<uint> observed = [];
        HashSet<string> pending = ["entry", "opening", "model-a", "swords", "attack", "exit"];
        List<string> boundaries = [];
        try
        {
            for (; ticks < 8 && !battle.QueuedActions.Any(action => action.NativeCallback?.Stage == 7); ticks++)
            {
                battle.Tick();
            }
            Assert.Contains(battle.QueuedActions, action => action.NativeCallback?.Stage == 7);
            Assert.Equal((byte)8, battle.Capture().NativeSupport!.SelectedAction);
            Assert.Equal((byte)1, battle.Capture().NativeSupport!.GilgameshUsed);
            NativeBattleRestorationPresentationTests.Publish(output, "callback7-produced", battle.Capture(), null);
            for (; callbacks < 2048; callbacks++)
            {
                BattleSnapshot before = battle.Capture();
                NativeBattleOriginalEffectState? effect = before.NativeGraphics?.OriginalEffect;
                NativeBattleModulePoolTask[] tasks = effect?.Pools.SelectMany(pool => pool.Slots).Where(task => (task.Flags & 1) != 0).ToArray() ?? [];
                observed.UnionWith(tasks.Select(task => task.Callback));
                string? boundary = new[]
                {
                    ("entry", effect?.Binding.Effect == 330 && effect.ReturnedDescriptor > 0),
                    ("opening", tasks.Any(task => task.Callback == 0x592E90)),
                    ("model-a", tasks.Any(task => task.Callback == 0x595EE0 && task.ReadWord(0x0C) > 0)),
                    ("swords", tasks.Any(task => task.Callback == 0x594FD0)),
                    ("attack", tasks.Any(task => task.Callback == 0x5920E0)),
                    ("exit", effect?.Binding.Effect == 330 && initialized && effect.ReturnedDescriptor == 0)
                }.Where(item => item.Item2 && pending.Contains(item.Item1)).Select(item => item.Item1).FirstOrDefault();
                BattleSession? cold = null;
                if (boundary is not null)
                {
                    pending.Remove(boundary);
                    byte[] pixels = scene.Render(battle, 320, 224).ToArray();
                    NativeBattleRestorationPresentationTests.Publish(output, boundary + "-before", before, pixels);
                    cold = BattleSession.Restore(NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, boundary + "-before.battle.json")));
                    Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(cold.Capture()));
                    Assert.Equal(pixels, new BattleScenePresentation(assets, definition, true).Render(cold, 320, 224));
                }
                if (!produced)
                {
                    battle.Tick();
                    cold?.Tick();
                }
                battle.AdvanceNativeGraphicsPresentation(Callback);
                cold?.AdvanceNativeGraphicsPresentation(Callback);
                BattleSnapshot after = battle.Capture();
                if (after.ActiveAction?.IndependentGuardian is { } invocation)
                {
                    Assert.Equal((byte)10, invocation.Level);
                }
                produced |= after.NativeGraphics!.Descriptors.Queue.Any(index => after.NativeGraphics.Descriptors.Packets[index].Command == 104);
                initialized |= after.NativeGraphics.OriginalEffect is { Binding.Effect: 330, ReturnedDescriptor: > 0 };
                if (cold is not null)
                {
                    Assert.Equal(JsonSerializer.Serialize(after), JsonSerializer.Serialize(cold.Capture()));
                    Assert.Equal(scene.Render(battle, 320, 224), new BattleScenePresentation(assets, definition, true).Render(cold, 320, 224));
                    NativeBattleRestorationPresentationTests.Publish(output, boundary + "-after", after, scene.Render(battle, 320, 224));
                    boundaries.Add(boundary!);
                }
                if (initialized && after.NativeGraphics.OriginalEffect is { Binding.Effect: 330, ReturnedDescriptor: 0 } && !after.NativeGraphics.Descriptors.Queue.Any(index => after.NativeGraphics.Descriptors.Packets[index].Command == 104))
                {
                    completed = true;
                    break;
                }
            }
            Assert.True(completed, "The finite diagnostic budget is not native completion.");
            Assert.Empty(pending);
            Assert.All(battle.NativeGraphics!.OriginalEffect!.Pools, pool => Assert.DoesNotContain(pool.Slots, task => (task.Flags & 1) != 0));
            NativeBattleRestorationPresentationTests.Publish(output, "completed", battle.Capture(), scene.Render(battle, 320, 224));
        }
        finally
        {
            NativeBattleRestorationPresentationTests.Publish(output, "last", battle.Capture(), null);
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Completed = completed, Produced = produced, Initialized = initialized, Ticks = ticks, Callbacks = callbacks, Observed = observed.Order(), Boundaries = boundaries, Pending = pending }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
