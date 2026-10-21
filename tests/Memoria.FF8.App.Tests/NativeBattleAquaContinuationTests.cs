using System.Collections.Immutable;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleAquaContinuationTests
{
    [Fact]
    public void Concrete_live_Aqua_standalone_continues_without_replaying_its_prefix()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? sourcePath = Environment.GetEnvironmentVariable("MEMORIA_FF8_AQUA_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_AQUA_EVIDENCE");
        Assert.SkipUnless(directory is not null && sourcePath is not null && output is not null, "Select installed resources, a concrete Aqua standalone and a distinct output directory.");
        Assert.False(Directory.Exists(output));
        Directory.CreateDirectory(output!);
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(sourcePath!);
        ushort effect = source.NativeGraphics!.OriginalEffect!.Binding.Effect;
        Assert.Contains(effect, new ushort[] { 55, 72 });
        Assert.True(source.NativeGraphics.OriginalEffect.ReturnedDescriptor > 0);
        int maximum = int.Parse(Environment.GetEnvironmentVariable("MEMORIA_FF8_AQUA_CALLBACKS") ?? "15", System.Globalization.CultureInfo.InvariantCulture);
        Assert.InRange(maximum, 1, 128);
        BattleSession battle = Restore(source);
        BattleScenePresentation scene = new(assets, source.Definition, true);
        BattleSnapshot current = source;
        BattleSnapshot prior = source;
        int callbacks = 0;
        int peakChangedRgb = 0;
        BattleSnapshot? peak = null;
        BattleSnapshot? peakWithout = null;
        byte[]? peakPixels = null;
        byte[]? peakPlain = null;
        List<object> progression = [];
        try
        {
            CompareNext("before", source);
            for (; callbacks < maximum; callbacks++)
            {
                prior = current;
                battle.Tick();
                current = battle.Capture();
                Assert.Equal(effect, current.NativeGraphics!.OriginalEffect!.Binding.Effect);
                Assert.Equal(source.Combatants.Select(actor => actor.CurrentHp), current.Combatants.Select(actor => actor.CurrentHp));
                NativeBattleAquaState state = effect == 55 ? current.NativeEffectMemory!.Aqua55! : current.NativeEffectMemory!.Aqua72!;
                byte[] pixels = scene.Render(battle, 320, 224).ToArray();
                WritePng(Path.Combine(output!, $"frame-{callbacks + 1:D3}.png"), pixels);
                ImmutableHashSet<NativeBattlePrimitiveReference> owned = NativeBattleAquaPresentationTests.AquaPacketReferences(prior, current);
                int changed = 0;
                if (!owned.IsEmpty)
                {
                    NativeBattlePrimitiveFrame frame = current.NativeGraphics.PrimitiveFrame!;
                    BattleSnapshot without = current with { NativeGraphics = current.NativeGraphics with { PrimitiveFrame = frame with { LastCommands = frame.LastCommands.Where(command => !owned.Contains(command.Command.Packet)).ToImmutableArray() } } };
                    byte[] plain = new BattleScenePresentation(assets, source.Definition, true).Render(Restore(without), 320, 224).ToArray();
                    for (int offset = 0; offset < pixels.Length; offset += 4)
                    {
                        if (!pixels.AsSpan(offset, 3).SequenceEqual(plain.AsSpan(offset, 3)))
                        {
                            changed++;
                        }
                    }
                    if (changed > peakChangedRgb)
                    {
                        peakChangedRgb = changed;
                        peak = current;
                        peakWithout = without;
                        peakPixels = pixels;
                        peakPlain = plain;
                    }
                }
                progression.Add(new { current.Frame, NativeCallback = current.NativeGraphics.Callback, SharedPhase = state.Word(0x40), RequestedPhase = state.Word(0x42), NextPhase = state.Word(0x44), Age = state.Word(0x46), Contact = state.Word(0x48), Released = state.Word(0x4C), changed, OwnedPackets = owned.Count, Tasks = current.NativeGraphics.OriginalEffect.Pools.SelectMany((pool, index) => pool.Slots.Where(task => (task.Flags & 1) != 0).Select(task => new { Pool = index, task.Callback, Phase = task.ReadByte(0x29), Age = task.ReadWord(0x24), Children = task.ReadByte(0x28) })).ToArray() });
            }
            Assert.True(peakChangedRgb > 0, "The standalone continuation produced no isolated Aqua packet contribution.");
            NativeBattleRestorationPresentationTests.Publish(output!, "peak", peak!, peakPixels);
            NativeBattleRestorationPresentationTests.Publish(output!, "without-effect", peakWithout!, peakPlain);
            CompareNext("after", current);
            File.WriteAllText(Path.Combine(output!, "result.json"), JsonSerializer.Serialize(new { Status = "completed", Scenario = "Concrete Aqua standalone visual continuation; no prefix replay or new whole-action acceptance", Effect = effect, SourceFrame = source.Frame, SourceNativeCallback = source.NativeGraphics.Callback, callbacks, peakChangedRgb, current.Frame, NativeCallback = current.NativeGraphics!.Callback, Progression = progression }));
        }
        catch (Exception error)
        {
            NativeBattleRestorationPresentationTests.Publish(output!, "before-boundary", prior, null);
            NativeBattleRestorationPresentationTests.Publish(output!, "failed-live", battle.Capture(), null);
            File.WriteAllText(Path.Combine(output!, "result.json"), JsonSerializer.Serialize(new { Status = "blocked", Effect = effect, callbacks, Error = error.ToString(), Progression = progression }));
            throw;
        }

        BattleSession Restore(BattleSnapshot snapshot)
        {
            BattleSession restored = BattleSession.Restore(snapshot);
            restored.AttachNativeEffectResourceResolver(selected => game.ReadNativeEffectCatalogForEffects([selected])[selected - 1]);
            return restored;
        }

        void CompareNext(string name, BattleSnapshot snapshot)
        {
            NativeBattleRestorationPresentationTests.Publish(output!, name, snapshot, scene.Render(Restore(snapshot), 320, 224).ToArray());
            BattleSession cold = Restore(NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output!, name + ".battle.json")));
            BattleSession direct = Restore(snapshot);
            Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(cold.Capture()));
            cold.Tick();
            direct.Tick();
            Assert.Equal(JsonSerializer.Serialize(direct.Capture()), JsonSerializer.Serialize(cold.Capture()));
            byte[] pixels = new BattleScenePresentation(assets, source.Definition, true).Render(cold, 320, 224).ToArray();
            Assert.Equal(new BattleScenePresentation(assets, source.Definition, true).Render(direct, 320, 224).ToArray(), pixels);
            NativeBattleRestorationPresentationTests.Publish(output!, name + "-next", cold.Capture(), pixels);
        }
    }

    private static void WritePng(string path, byte[] rgba)
    {
        using Bitmap bitmap = new(320, 224);
        for (int y = 0; y < 224; y++)
        {
            for (int x = 0; x < 320; x++)
            {
                int offset = (y * 320 + x) * 4;
                bitmap.SetPixel(x, y, Color.FromArgb(rgba[offset + 3], rgba[offset], rgba[offset + 1], rgba[offset + 2]));
            }
        }
        using FileStream stream = new(path, FileMode.CreateNew);
        bitmap.Save(stream, ImageFormat.Png);
    }
}
