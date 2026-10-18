using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class NativeScanOrdinaryAcceptanceTests
{
    [Fact]
    public void Ordinary_Scan_cast_projects_system_text_advances_pages_and_releases_with_disk_continuations()
    {
        string? sourcePath = Environment.GetEnvironmentVariable("MEMORIA_FF8_SCAN_SNAPSHOT");
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(sourcePath is not null && File.Exists(sourcePath), "Select a standalone ready Scan component with MEMORIA_FF8_SCAN_SNAPSHOT.");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        BattleSnapshot source = JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(sourcePath!))!;
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(game.GameDirectory);
        BattleActionDefinition action = Assert.Single(assets.ActionDefinitions, candidate => candidate.Ability == BattleAbility.Magic && candidate.NativeAction is { MagicId: 40, AttackType: 12 });
        Assert.Contains(source.Definition.ActionDefinitions, candidate => candidate.Ability == BattleAbility.Magic && candidate.Id == action.Id && candidate.NativeAction?.MagicId == 40);
        BattleCombatantDefinition caster = Assert.Single(source.Definition.Combatants, actor => actor.IsPlayer && source.Combatants.Single(saved => saved.Id == actor.Id).NativeMagicInventory!.Any(stock => stock.SpellId == action.Id && stock.Count >= 2));
        BattleCombatantDefinition target = source.Definition.Combatants.First(actor => !actor.IsPlayer && source.Combatants.Single(saved => saved.Id == actor.Id).CurrentHp > 0);
        BattleSession battle = BattleSession.Restore(source);
        Assert.Null(source.ActiveAction);
        Assert.Empty(source.QueuedActions);
        Assert.True(battle.NativeEntryReady);
        Assert.True(battle.Combatants.Single(actor => actor.Id == caster.Id).IsReady);
        int stockBefore = Stock(source);
        int scansBefore = battle.Events.Count(entry => entry.Kind == "Scan");
        string output = Path.Combine(Environment.GetEnvironmentVariable("MEMORIA_FF8_SCAN_EVIDENCE") ?? Path.GetTempPath(), "scan-ordinary-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        Console.WriteLine($"Ordinary Scan action {action.Id}, effect40; evidence: {output}");
        BattleScenePresentation scene = new(assets, source.Definition, true);
        List<BattleSession> continuations = [];
        HashSet<string> boundaries = [];
        HashSet<ushort> pages = [];
        List<NativeBattleModuleSound> sounds = [];
        BattleAudioPresentation audio = new(_ => { }, playModule: sounds.Add);
        bool selected = false;
        bool cameraChanged = false;
        bool description = false;
        bool resultText = false;
        bool sentExit = false;
        int rgbChanges = 0;
        string originalCamera = JsonSerializer.Serialize(source.NativeCamera);
        BattleSnapshot prior = source;
        try
        {
            Save("before");
            battle.QueueAction(caster.Id, target.Id, BattleAbility.Magic, action.Id);
            Boundary("queued");
            for (int callback = 0; callback < 4096; callback++)
            {
                prior = battle.Capture();
                ushort buttons = Input(prior, callback);
                battle.PublishNativeScanMappedButtons(buttons);
                foreach (BattleSession restored in continuations)
                {
                    restored.PublishNativeScanMappedButtons(buttons);
                }
                battle.Tick();
                BattleSnapshot current = battle.Capture();
                foreach (BattleSession restored in continuations)
                {
                    restored.Tick();
                    Assert.Equal(JsonSerializer.Serialize(current), JsonSerializer.Serialize(restored.Capture()));
                    Assert.Equal(scene.Render(battle, 320, 224), new BattleScenePresentation(assets, source.Definition, true).Render(restored, 320, 224));
                }
                continuations.Clear();
                audio.Update(battle);
                audio.Update(battle);
                NativeBattleGraphicsState graphics = current.NativeGraphics!;
                if (graphics.OriginalEffect is { Binding.Effect: 40 } effect)
                {
                    selected = true;
                    cameraChanged |= JsonSerializer.Serialize(current.NativeCamera) != originalCamera;
                    if (effect.ReturnedDescriptor != 0)
                    {
                        Assert.Equal((byte)2, current.NativeActionScratch!.Command);
                        Assert.Equal((ushort)action.Id, current.NativeActionScratch.Action);
                        Boundary("opening");
                    }
                    NativeBattleScanProcess process = current.NativeEffectMemory!.Scan!;
                    NativeBattleScanTextFrame frame = process.Text.ReadFrame(graphics.UiServices!.Flags942);
                    IReadOnlyList<NativeScanWindowText> windows = NativeScanWindowTextProjection.ProjectForViewport(frame, battle.CreateNativeScanTextExpansionServices(), new(0, 0, 320, 224));
                    TextCanvas canvas = new();
                    NativeScanWindowDrawing.Draw(canvas, new UiTheme(), windows, new UiRect(0, 0, 320, 224), 0, 1);
                    Assert.Equal(0, canvas.ClipDepth);
                    if (windows.Any(window => window.Index == 0 && window.Lines.Any(line => line.Line.Runs.Any(run => run.Text.Length > 0))))
                    {
                        int targetIndex = graphics.ActorIds.IndexOf(target.Id);
                        Assert.InRange(targetIndex, 3, 6);
                        Assert.Equal(61, target.ModelId);
                        Assert.Equal((byte)77, graphics.Models[targetIndex].ModelId4);
                        NativeBattleScanTextResources textResources = effect.Binding.ScanTextResources!;
                        uint originalDescription = textResources.DescriptionPointer(checked((byte)target.ModelId));
                        uint graphicsCategoryDescription = textResources.DescriptionPointer(graphics.Models[targetIndex].ModelId4!.Value);
                        Assert.NotEqual(originalDescription, graphicsCategoryDescription);
                        Assert.Equal(originalDescription, frame.Windows.Row(0).ReadDword(8));
                        description = true;
                        Assert.NotEmpty(canvas.Runs);
                        Boundary("description-prefix");
                    }
                    if (windows.Any(window => window.Index == 1 && window.Lines.Any(line => line.Line.Runs.Any(run => run.Text.Length > 0))))
                    {
                        resultText = true;
                        Assert.NotEmpty(canvas.Runs);
                        Boundary("result-prefix");
                    }
                    NativeBattleModulePoolTask? result = Result(current);
                    if (result is not null && result.ReadByte(0x29) == 2)
                    {
                        ushort page = result.ReadWord(0x6C);
                        if (pages.Add(page))
                        {
                            Boundary("page-" + page);
                        }
                    }
                    NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame!;
                    if (rgbChanges == 0 && primitive.LastCommands.Any(command => command.Command.Packet.Bank == "20DFAB8"))
                    {
                        NativeBattlePrimitiveFrame plain = primitive with { LastCommands = primitive.LastCommands.Where(command => command.Command.Packet.Bank != "20DFAB8").ToImmutableArray() };
                        BattleSession background = BattleSession.Restore(current with { NativeGraphics = graphics with { PrimitiveFrame = plain } });
                        rgbChanges = RgbChanges(scene.Render(battle, 320, 224), new BattleScenePresentation(assets, source.Definition, true).Render(background, 320, 224));
                        if (rgbChanges > 0)
                        {
                            Boundary("visible-packets");
                        }
                    }
                    if (battle.Events.Count(entry => entry.Kind == "Scan") > scansBefore)
                    {
                        Boundary("contact");
                    }
                }
                if (selected && Released(current))
                {
                    break;
                }
            }
            BattleSnapshot completed = battle.Capture();
            Assert.True(selected);
            Assert.True(cameraChanged);
            Assert.True(description);
            Assert.True(resultText);
            Assert.True(sentExit);
            Assert.True(pages.Count > 1, "The declared component must expose more than one actual Scan result page.");
            Assert.True(rgbChanges > 0);
            uint effectSound = game.ReadNativeScanVisualResources().Sound15D0B3C;
            NativeBattleModuleSound sound = Assert.Single(sounds, item => item.Sound == effectSound);
            Assert.Equal((byte)128, sound.Pan);
            Assert.Equal(scansBefore + 1, battle.Events.Count(entry => entry.Kind == "Scan"));
            Assert.Single(battle.Events.Skip(source.Events.Length), entry => entry.Kind == "Scan" && entry.ActorId == caster.Id && entry.TargetId == target.Id);
            Assert.Equal(stockBefore - 1, Stock(completed));
            foreach (BattleCombatantSnapshot participant in source.Combatants)
            {
                Assert.Equal(participant.CurrentHp, completed.Combatants.Single(actor => actor.Id == participant.Id).CurrentHp);
            }
            Assert.True(Released(completed), "Scan must release its ordinary action, descriptor, root tasks and action pause.");
            Boundary("released");
            prior = completed;
            battle.PublishNativeScanMappedButtons(0);
            battle.Tick();
            foreach (BattleSession restored in continuations)
            {
                restored.PublishNativeScanMappedButtons(0);
                restored.Tick();
                Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
                Assert.Equal(scene.Render(battle, 320, 224), new BattleScenePresentation(assets, source.Definition, true).Render(restored, 320, 224));
            }
            Save("released-next");
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "passed", Action = action.Id, Effect = 40, completed.Frame, pages, boundaries, rgbChanges, SoundCount = sounds.Count }));
        }
        catch (Exception error)
        {
            NativeBattleRestorationPresentationTests.Publish(output, "before-failure", prior, null);
            NativeBattleRestorationPresentationTests.Publish(output, "failed", battle.Capture(), null);
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "failed", Exception = error.ToString(), prior.Frame, Callback = prior.NativeGraphics?.Callback }));
            throw;
        }

        int Stock(BattleSnapshot snapshot) => Assert.Single(snapshot.Combatants.Single(actor => actor.Id == caster.Id).NativeMagicInventory!, stock => stock.SpellId == action.Id).Count;

        ushort Input(BattleSnapshot snapshot, int callback)
        {
            NativeBattleModulePoolTask? result = Result(snapshot);
            uint count = snapshot.NativeEffectMemory?.Scan?.Results.Count ?? 0;
            if (description && resultText && count > 1 && pages.Count >= count && callback > 400)
            {
                sentExit = true;
                Boundary("before-exit");
                return 0x40;
            }
            return result is not null && result.ReadByte(0x29) == 2 && unchecked((short)result.ReadWord(0x66)) <= 30 && (callback & 3) == 0 ? (ushort)0x20 : (ushort)0;
        }

        void Boundary(string name)
        {
            if (!boundaries.Add(name))
            {
                return;
            }
            Save(name);
            using FileStream stream = File.OpenRead(Path.Combine(output, name + ".battle.json"));
            BattleSnapshot snapshot = JsonSerializer.Deserialize<BattleSnapshot>(stream)!;
            BattleSession restored = BattleSession.Restore(snapshot);
            Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(restored.Capture()));
            Assert.Equal(scene.Render(battle, 320, 224), new BattleScenePresentation(assets, snapshot.Definition, true).Render(restored, 320, 224));
            continuations.Add(restored);
        }

        void Save(string name)
        {
            NativeBattleRestorationPresentationTests.Publish(output, name, battle.Capture(), scene.Render(battle, 320, 224));
        }
    }

    private static NativeBattleModulePoolTask? Result(BattleSnapshot snapshot) => snapshot.NativeGraphics?.OriginalEffect?.Pools.SelectMany(pool => pool.Slots).FirstOrDefault(task => (task.Flags & 1) != 0 && task.Callback is 0x84FAF0 or 0x850FD0);

    private static bool Released(BattleSnapshot snapshot) => snapshot.ActiveAction is null && snapshot.QueuedActions.Length == 0 && snapshot.NativeClock!.ActionPause == 0 && snapshot.NativeGraphics!.Descriptors.Queue.IsEmpty && snapshot.NativeGraphics.OriginalEffect is { ReturnedDescriptor: 0 } effect && effect.Pools.All(pool => pool.Slots.All(task => (task.Flags & 1) == 0));

    private static int RgbChanges(byte[] first, byte[] second)
    {
        int changes = 0;
        for (int offset = 0; offset < first.Length; offset += 4)
        {
            if (!first.AsSpan(offset, 3).SequenceEqual(second.AsSpan(offset, 3)))
            {
                changes++;
            }
        }
        return changes;
    }

    private sealed class TextCanvas : IUiCanvas, INativeScanTextCanvas
    {
        public List<string> Runs { get; } = [];
        public int ClipDepth { get; private set; }

        public float ScanTextAdvance(string value, float size) => value.Length * size / 2;
        public void ScanTextRun(string value, float size, float x, float y, UiColor color)
        {
            Assert.True(ClipDepth > 0);
            Assert.True(float.IsFinite(x) && float.IsFinite(y) && float.IsFinite(size));
            Runs.Add(value);
        }
        public void PushClip(UiRect bounds)
        {
            ClipDepth++;
        }
        public void PopClip()
        {
            ClipDepth--;
        }
        public void Fill(UiRect bounds, UiColor color)
        {
        }
        public void Text(string text, float fontSize, UiRect bounds, UiColor color) => throw new NotSupportedException();
        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment) => throw new NotSupportedException();
        public void Line(float x1, float y1, float x2, float y2, UiColor color, float thickness)
        {
        }
        public void MenuPointer(UiRect bounds)
        {
        }
        public void Image(UiImageSource source, UiRect bounds)
        {
        }
    }
}
