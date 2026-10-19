using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleCarbuncle278ReturnContinuationTests
{
    [Fact]
    public void Named_standalone_checks_corrected_sound_owner_and_finishes_return_effect_and_action()
    {
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_CARBUNCLE278_SNAPSHOT");
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_CARBUNCLE278_EVIDENCE");
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(input is not null && File.Exists(input) && !string.IsNullOrWhiteSpace(evidence) && directory is not null && Directory.Exists(directory), "Select installed resources, a corrected sound-owner return standalone and a distinct evidence directory.");
        string original = File.ReadAllText(input!);
        BattleSnapshot source = JsonSerializer.Deserialize<BattleSnapshot>(original)!;
        Assert.Equal((ushort)215, Creature(source).ReadWord(0xC));
        Assert.Equal((byte?)4, source.NativeGlobalSoundProcess!.Rotation);
        Assert.Equal<uint?>(240060u, source.NativeGlobalSoundProcess.OriginalEffectIds[28]);
        string output = Path.Combine(evidence!, "carbuncle-continued-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        Console.WriteLine($"Carbuncle standalone continuation evidence: {output}");
        using GameAssets game = new(directory!);
        JsonNode resources = JsonSerializer.SerializeToNode(game.ReadNativeCarbuncle278Resources())!;
        JsonNode node = JsonNode.Parse(original)!;
        List<object> changes = [];
        int owners = 0;
        AddResources(node, "");
        Assert.Equal(3, owners);
        Assert.Equal(3, changes.Count);
        string migrated = node.ToJsonString();
        File.WriteAllText(Path.Combine(output, "return-sprite-resources.battle.json"), migrated);
        File.WriteAllText(Path.Combine(output, "resource-migration.json"), JsonSerializer.Serialize(new { OriginalSha256 = Hash(original), MigratedSha256 = Hash(migrated), Changes = changes }));
        source = JsonSerializer.Deserialize<BattleSnapshot>(migrated)!;
        BattleSession first = BattleSession.Restore(source);
        BattleSession second = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(migrated)!);
        using BattleAssets assets = new(directory!);
        BattleScenePresentation scene = new(assets, source.Definition, true);
        TimeSpan callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);
        NativeBattleRestorationPresentationTests.Publish(output, "source", source, null);
        List<object> rows = [];
        int callbacks = 0;
        while (first.Capture().NativeGraphics!.OriginalEffect!.ReturnedDescriptor != 0)
        {
            Assert.True(callbacks < 128, "Native Carbuncle return did not release its root.");
            BattleSnapshot before = first.Capture();
            try
            {
                first.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
                second.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
            }
            catch
            {
                NativeBattleRestorationPresentationTests.Publish(output, "failed-before-callback-" + callbacks, before, null);
                throw;
            }
            BattleSnapshot current = first.Capture();
            callbacks++;
            Assert.Equal(JsonSerializer.Serialize(current), JsonSerializer.Serialize(second.Capture()));
            byte[] pixels = scene.Render(first, 320, 224).ToArray();
            Assert.Equal(pixels, scene.Render(second, 320, 224).ToArray());
            ushort counter = Creature(current).ReadWord(0xC);
            rows.Add(new { current.Frame, NativeCallback = current.NativeGraphics!.Callback, Creature = counter, JsonSha256 = Hash(JsonSerializer.Serialize(current)), PixelsSha256 = Convert.ToHexString(SHA256.HashData(pixels)) });
            if (callbacks == 1)
            {
                NativeBattleRestorationPresentationTests.Publish(output, "after-first-return-callback", current, pixels);
                NativeBattleModuleSound sound = current.Events.Last(item => item.NativeModuleSound?.Sound == 240063).NativeModuleSound!;
                Assert.Equal(new NativeGlobalSoundAllocation(4, 5, 29), sound.Allocation);
                Assert.Equal((byte?)5, current.NativeGlobalSoundProcess!.Rotation);
                Assert.Equal<uint?>(240063u, current.NativeGlobalSoundProcess.OriginalEffectIds[29]);
                Assert.All(current.NativeGraphics!.Models.Where(model => model.Index < 3), model => Assert.Equal(4, model.Flags0 & 4));
                Assert.Equal(0u, Creature(current).ReadDword(0x78));
            }
            if (counter is 232 or 248 or 264 or 280 or 283)
            {
                NativeBattleRestorationPresentationTests.Publish(output, "creature-" + counter, current, pixels);
            }
        }
        BattleSnapshot finished = first.Capture();
        Assert.Equal((byte)255, finished.NativeEffectMemory!.Carbuncle278!.Done81D8);
        Assert.Equal(0u, finished.NativeEffectMemory.Carbuncle278.Flash81D4);
        Assert.All(finished.NativeGraphics!.OriginalEffect!.Pools.SelectMany(pool => pool.Slots), task => Assert.Equal(0, task.Flags & 1));
        Assert.All(finished.NativeGraphics.Models.Where(model => model.Index < 3), model => Assert.Equal(0, model.Flags0 & 4));
        foreach (NativeBattleGraphicsModel prior in source.NativeGraphics!.Models.Where(model => model.Index >= 3 && (model.Flags0 & 2) != 0))
        {
            Assert.Equal(unchecked((short)(prior.Transform!.Position28.Z - source.NativeEffectMemory!.Carbuncle278!.Lift81D0)), finished.NativeGraphics.Models[prior.Index].Transform!.Position28.Z);
        }
        NativeBattleRestorationPresentationTests.Publish(output, "root-released", finished, scene.Render(first, 320, 224).ToArray());
        for (int tick = 0; tick < 128 && !Released(first.Capture()); tick++)
        {
            first.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
            second.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
            Assert.Equal(JsonSerializer.Serialize(first.Capture()), JsonSerializer.Serialize(second.Capture()));
            Assert.Equal(scene.Render(first, 320, 224).ToArray(), scene.Render(second, 320, 224).ToArray());
        }
        NativeBattleRestorationPresentationTests.Publish(output, "after-action-completion-bound", first.Capture(), null);
        Assert.True(Released(first.Capture()));
        NativeBattleRestorationPresentationTests.Publish(output, "completed", first.Capture(), scene.Render(first, 320, 224).ToArray());
        first.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
        second.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
        Assert.Equal(JsonSerializer.Serialize(first.Capture()), JsonSerializer.Serialize(second.Capture()));
        Assert.Equal(scene.Render(first, 320, 224).ToArray(), scene.Render(second, 320, 224).ToArray());
        NativeBattleRestorationPresentationTests.Publish(output, "completed-next", first.Capture(), scene.Render(first, 320, 224).ToArray());
        File.WriteAllText(Path.Combine(output, "callback-proofs.json"), JsonSerializer.Serialize(rows));
        File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "completed", finished.Frame, NativeCallback = finished.NativeGraphics!.Callback, Callbacks = callbacks }));
        Assert.Equal(original, File.ReadAllText(input!));

        void AddResources(JsonNode current, string path)
        {
            if (current is JsonObject obj)
            {
                foreach (var entry in obj.ToArray())
                {
                    if (entry.Key == "Carbuncle278Resources" && entry.Value is JsonObject retained)
                    {
                        Assert.Equal(resources["MirrorBoundsMesh"]!.ToJsonString(), retained["MirrorBoundsMesh"]!.ToJsonString());
                        if (retained["CreatureProgramme"] is JsonNode firstProgramme)
                        {
                            Assert.Equal(resources["CreatureProgramme"]!.ToJsonString(), firstProgramme.ToJsonString());
                        }
                        owners++;
                        foreach (string name in new[] { "ReturnSprites" })
                        {
                            Assert.False(retained.ContainsKey(name));
                            JsonNode added = resources[name]!.DeepClone();
                            retained[name] = added;
                            changes.Add(new { Path = path + "/" + entry.Key + "/" + name, Operation = "add", ValueSha256 = Hash(added.ToJsonString()) });
                        }
                    }
                    else if (entry.Value is not null)
                    {
                        AddResources(entry.Value, path + "/" + entry.Key);
                    }
                }
            }
            else if (current is JsonArray array)
            {
                for (int index = 0; index < array.Count; index++)
                {
                    if (array[index] is JsonNode value)
                    {
                        AddResources(value, path + "/" + index);
                    }
                }
            }
        }
    }

    [Fact]
    public void Named_root_released_standalone_completes_ordinary_action_on_the_presentation_clock()
    {
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_CARBUNCLE278_RELEASE_SNAPSHOT");
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_CARBUNCLE278_EVIDENCE");
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(input is not null && File.Exists(input) && !string.IsNullOrWhiteSpace(evidence) && directory is not null && Directory.Exists(directory), "Select installed resources, a root-released standalone and a distinct evidence directory.");
        string original = File.ReadAllText(input!);
        BattleSnapshot source = JsonSerializer.Deserialize<BattleSnapshot>(original)!;
        Assert.Equal((ushort)278, source.NativeGraphics!.OriginalEffect!.RequestedEffect);
        Assert.Equal(0u, source.NativeGraphics.OriginalEffect.ReturnedDescriptor);
        Assert.Equal((byte)255, source.NativeEffectMemory!.Carbuncle278!.Done81D8);
        string output = Path.Combine(evidence!, "carbuncle-release-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        Console.WriteLine($"Carbuncle release continuation evidence: {output}");
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        BattleSession first = BattleSession.Restore(source);
        BattleSession second = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(original)!);
        first.AttachNativeEffectResourceResolver(id => game.ReadNativeEffectCatalogForEffects([id])[id - 1]);
        second.AttachNativeEffectResourceResolver(id => game.ReadNativeEffectCatalogForEffects([id])[id - 1]);
        BattleScenePresentation scene = new(assets, source.Definition, true);
        TimeSpan callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);
        NativeBattleRestorationPresentationTests.Publish(output, "source", source, null);
        BattleSnapshot before = source;
        try
        {
            for (int index = 0; index < 128 && !Released(first.Capture()); index++)
            {
                before = first.Capture();
                first.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
                second.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
                Assert.Equal(JsonSerializer.Serialize(first.Capture()), JsonSerializer.Serialize(second.Capture()));
                Assert.Equal(scene.Render(first, 320, 224).ToArray(), scene.Render(second, 320, 224).ToArray());
            }
            NativeBattleRestorationPresentationTests.Publish(output, "after-completion-bound", first.Capture(), null);
            Assert.True(Released(first.Capture()));
            NativeBattleRestorationPresentationTests.Publish(output, "completed", first.Capture(), scene.Render(first, 320, 224).ToArray());
            first.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
            second.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
            Assert.Equal(JsonSerializer.Serialize(first.Capture()), JsonSerializer.Serialize(second.Capture()));
            Assert.Equal(scene.Render(first, 320, 224).ToArray(), scene.Render(second, 320, 224).ToArray());
            NativeBattleRestorationPresentationTests.Publish(output, "completed-next", first.Capture(), scene.Render(first, 320, 224).ToArray());
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "completed", first.Capture().Frame, NativeCallback = first.Capture().NativeGraphics!.Callback }));
        }
        catch
        {
            NativeBattleRestorationPresentationTests.Publish(output, "before-boundary", before, null);
            NativeBattleRestorationPresentationTests.Publish(output, "failed-live", first.Capture(), null);
            throw;
        }
        Assert.Equal(original, File.ReadAllText(input!));
    }

    private static NativeBattleModulePoolTask Creature(BattleSnapshot snapshot) => snapshot.NativeGraphics!.OriginalEffect!.Pools.Single(pool => pool.DescriptorAddress == 0x2508188).Slots[0];
    private static bool Released(BattleSnapshot snapshot) => snapshot.ActiveAction is null && snapshot.QueuedActions.Length == 0 && snapshot.GuardianForceCasts.Length == 0 && snapshot.NativeClock!.ActionPause == 0 && snapshot.NativeGraphics!.Descriptors.Queue.IsEmpty && snapshot.NativeGraphics.OriginalEffect is { ReturnedDescriptor: 0 };
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));
}
