using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect140Profile;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect140PresentationTests
{
    [Fact]
    public void Original_Effect140_declared_independent_component_preserves_current_geometry_and_lifecycle()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([140], "MEMORIA_FF8_EFFECT140");
    }

    internal static void RunComponent(string directory, string output)
    {
        using BattleAssets assets = new(directory);
        using GameAssets game = new(directory);
        BattleSnapshot source = PrepareSource(game, assets, output);
        BattleSession battle = Restore(source);
        BattleScenePresentation scene = new(assets, source.Definition, true);
        NativeBattleEffect140PresentationObserver observer = new();
        HashSet<string> restored = [];
        BattleSnapshot prior = source;
        BattleSnapshot peak = source;
        byte[]? peakPixels = null;
        byte[]? peakPlain = null;
        BattleSnapshot geometryPeak = source;
        byte[]? geometryPixels = null;
        byte[]? geometryPlain = null;
        int callbacks = 0;
        int completions = 0;
        int changedRgb = 0;
        int geometryChangedRgb = 0;
        bool cameraChanged = false;
        bool stageChanged = false;
        string boundary = "queued-component";
        NativeBattleRestorationPresentationTests.Publish(output, "prepared-source", source, null);
        try
        {
            ColdNext("before-worker", source);
            for (; callbacks < 1800; callbacks++)
            {
                boundary = "callback-" + callbacks;
                battle.Tick();
                BattleSnapshot current = battle.Capture();
                NativeBattleGraphicsState currentGraphics = current.NativeGraphics ?? throw new InvalidDataException("Effect140 lost its graphics state.");
                Assert.Equal(source.Combatants.Select(actor => new { actor.Id, actor.CurrentHp, Saved = actor.NativeStatus?.Saved, Battle = actor.NativeStatus?.Battle }).ToArray(), current.Combatants.Select(actor => new { actor.Id, actor.CurrentHp, Saved = actor.NativeStatus?.Saved, Battle = actor.NativeStatus?.Battle }).ToArray());

                cameraChanged |= JsonSerializer.Serialize(source.NativeCamera) != JsonSerializer.Serialize(current.NativeCamera);
                stageChanged |= JsonSerializer.Serialize(source.NativeStage) != JsonSerializer.Serialize(current.NativeStage);
                if (prior.NativeGraphics!.OriginalEffect is { Binding.Effect: 140, ReturnedDescriptor: > 0 } && currentGraphics.OriginalEffect is { Binding.Effect: 140, ReturnedDescriptor: 0 })
                {
                    completions++;
                }
                observer.Observe(prior, current, ColdNext);
                NativeBattleLogicalCommand[] commands = observer.CurrentCommands;
                if (commands.Length > 0)
                {
                    ImmutableHashSet<NativeBattlePrimitiveReference> owned = commands.Select(command => command.Packet).ToImmutableHashSet();
                    NativeBattlePrimitiveFrame primitive = currentGraphics.PrimitiveFrame!;
                    BattleSnapshot without = current with { NativeGraphics = currentGraphics with { PrimitiveFrame = primitive with { LastCommands = primitive.LastCommands.Where(command => !owned.Contains(command.Command.Packet)).ToImmutableArray() } } };
                    byte[] pixels = scene.Render(battle, 320, 224).ToArray();
                    byte[] plain = new BattleScenePresentation(assets, source.Definition, true).Render(BattleSession.Restore(without), 320, 224).ToArray();
                    int changed = CountRgb(pixels, plain);
                    if (changed > changedRgb)
                    {
                        changedRgb = changed;
                        peak = current;
                        peakPixels = pixels;
                        peakPlain = plain;
                    }
                    if (observer.CurrentFadeCommands.Length == 0 && changed > geometryChangedRgb)
                    {
                        geometryChangedRgb = changed;
                        geometryPeak = current;
                        geometryPixels = pixels;
                        geometryPlain = plain;
                    }
                }
                prior = current;
                if (Released(current))
                {
                    break;
                }
            }
            BattleSnapshot completed = battle.Capture();
            Assert.True(callbacks < 1800, "The original effect140 component did not release its native lifecycle.");
            Assert.Equal(1, completions);
            Assert.True(cameraChanged);

            Assert.True(changedRgb > 100, "No actual effect140 primitive changed the rendered RGB output.");
            Assert.True(geometryChangedRgb > 100, "No effect140 geometry changed rendered RGB outside its fullviewport fade.");
            Assert.Equal(1, observer.Contacts);
            Assert.Equal(1, observer.ContactGlyphs);
            observer.Accept(completed, output);
            ColdNext("effect140-visible-peak", peak);
            ColdNext("effect140-geometry-visible-peak", geometryPeak);
            ColdNext("completed", completed);
            NativeBattleRestorationPresentationTests.Publish(output, "visible", peak, peakPixels!);
            NativeBattleRestorationPresentationTests.Publish(output, "geometry-visible", geometryPeak, geometryPixels!);
            File.WriteAllBytes(Path.Combine(output, "geometry-without-effect.rgba"), geometryPlain!);
            File.WriteAllBytes(Path.Combine(output, "without-effect.rgba"), peakPlain!);
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "completed", Scenario = "Declared independent kernel240/action1/sequence0 post-formula Amount0 enemy renderer and lifecycle; owned model, current mutable meshes and VRAM copy; GF acquisition, inventory, automatic245 and revival producers excluded", Effect = Effect, Callbacks = callbacks + 1, Contacts = observer.Contacts, ContactGlyphs = observer.ContactGlyphs, Completions = completions, ChangedRgb = changedRgb, GeometryChangedRgb = geometryChangedRgb, CameraChanged = cameraChanged, StageChanged = stageChanged, RestoredBoundaries = restored.Order(), completed.Frame, NativeCallback = completed.NativeGraphics!.Callback }));
        }
        catch (Exception error)
        {
            NativeBattleRestorationPresentationTests.Publish(output, "failed-live", battle.Capture(), null);
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "blocked", Boundary = boundary, Callbacks = callbacks, Error = error.ToString() }));
            throw;
        }

        BattleSession Restore(BattleSnapshot snapshot)
        {
            BattleSession session = BattleSession.Restore(snapshot);
            session.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
            return session;
        }

        void ColdNext(string name, BattleSnapshot snapshot)
        {
            NativeBattleRestorationPresentationTests.Publish(output, name, snapshot, null);
            BattleSnapshot json = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, name + ".battle.json"));
            BattleSession cold = Restore(json);
            BattleSession direct = Restore(snapshot);
            Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(cold.Capture()));
            cold.Tick();
            direct.Tick();
            Assert.Equal(JsonSerializer.Serialize(direct.Capture()), JsonSerializer.Serialize(cold.Capture()));
            byte[] pixels = new BattleScenePresentation(assets, source.Definition, true).Render(cold, 320, 224).ToArray();
            Assert.Equal(new BattleScenePresentation(assets, source.Definition, true).Render(direct, 320, 224).ToArray(), pixels);
            NativeBattleRestorationPresentationTests.Publish(output, name + "-next", cold.Capture(), pixels);
            restored.Add(name);
        }
    }

    private static BattleSnapshot PrepareSource(GameAssets game, BattleAssets assets, string output)
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT140_SOURCE_SNAPSHOT") ?? throw new InvalidDataException("Select the current formation981 component source.");
        BattleSnapshot original = NativeBattleEffectFamilyRunner.LoadSnapshot(path);
        Assert.Equal(981, original.Definition.EncounterId);
        Assert.Equal(142, original.Definition.StageId);
        BattleSession restored = BattleSession.Restore(original);
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(restored.Capture()));
        Assert.True(restored.NativeEntryReady);
        NativeBattleGraphicsState graphics = original.NativeGraphics!;
        Assert.Empty(graphics.Descriptors.Queue);
        Assert.Equal(0, original.NativeEffectMemory!.Active);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleEffect140State.FromColdProcess()), JsonSerializer.Serialize(original.NativeEffectMemory.Effect140));
        string casterId = graphics.ActorIds[0] ?? throw new InvalidDataException("Effect140 requires its actual party caster.");
        string targetId = graphics.ActorIds[3] ?? throw new InvalidDataException("Effect140 requires its actual enemy recipient.");
        Assert.Equal(126, restored.GetCombatantDefinition(targetId).ModelId);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 240 && row.ActionId == 1 && row.MagicId == 140 && row.KernelSection == 9 && row.KernelIndex == 1);
        NativeBattleActionPacketsState packets = original.NativeActionPackets!;
        byte priorActions = packets.ActionCount;
        byte priorContacts = packets.ContactCount;
        (packets, int header) = packets.Prepare(new(0, 240, 0, -1, 1, 140, null));
        BattleCombatantSnapshot target = original.Combatants.Single(actor => actor.Id == targetId);
        NativeBattleStatusSnapshot status = target.NativeStatus ?? throw new InvalidDataException("Effect140 requires its actual current target status.");
        BattleCombatantSnapshot preparedTarget = target with { NativeStatus = status with { Presentation = new(status.Saved, status.Battle) } };
        NativeBattleContactPacket contact = new(new(3, action.DamagePresentation, 0, 0, status.Saved, 7, status.Battle), new(255, 0, 0, 0, 0, 0, 0));
        (packets, _) = packets.AppendContact(contact);
        (packets, _) = packets.Publish(1);
        Assert.Equal(priorActions, header);
        Assert.Equal(priorContacts + 1, packets.ContactCount);
        (NativeBattleDescriptorState descriptors, _) = graphics.Descriptors.Enqueue(104, 128, checked(0x1D280C4u + (uint)header * 20));
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([140])[139];
        Assert.Same(game.ReadNativeEffect140Resources(), binding.Effect140Resources);
        BattleSnapshot source = original with { Definition = original.Definition with { NativeEffectCatalog = original.Definition.NativeEffectCatalog.SetItem(139, binding) }, Combatants = original.Combatants.Select(actor => actor.Id == targetId ? preparedTarget : actor).ToArray(), NativeActionPackets = packets, NativeGraphics = graphics with { Descriptors = descriptors } };
        Assert.Equal(original.Combatants.Select(actor => actor.CurrentHp), source.Combatants.Select(actor => actor.CurrentHp));
        Assert.Equal(JsonSerializer.Serialize(graphics.Models), JsonSerializer.Serialize(source.NativeGraphics!.Models));
        Assert.Equal(JsonSerializer.Serialize(original.NativeEffectMemory), JsonSerializer.Serialize(source.NativeEffectMemory));
        Assert.Equal(JsonSerializer.Serialize(original.NativeModelCpuResources), JsonSerializer.Serialize(source.NativeModelCpuResources));
        Assert.Equal(JsonSerializer.Serialize(original.NativeClock), JsonSerializer.Serialize(source.NativeClock));
        File.WriteAllText(Path.Combine(output, "declared-component.json"), JsonSerializer.Serialize(new { Scenario = "Declared independent kernel240/action1/sequence0 Amount0 component; GF acquisition, item31, automatic245 and revival admission excluded", PhysicalCaster = 0, PhysicalTarget = 3, ActualCasterId = casterId, ActualTargetId = targetId, Action = action, Header = packets.Headers[header], PriorActionCount = priorActions, PriorContactCount = priorContacts, ContactCount = 1, Contact = contact, StatusBefore = status.Presentation, StatusAfter = preparedTarget.NativeStatus!.Presentation, StatusCache = "Declared47E2D0 projection copies actual current Saved/Battle on the prepared component copy; logical HP/status preserved" }));
        return source;
    }
    private static bool Released(BattleSnapshot snapshot) => snapshot.NativeGraphics?.OriginalEffect is { Binding.Effect: 140, ReturnedDescriptor: 0 } effect && snapshot.NativeEffectMemory!.Active == 0 && snapshot.ActiveAction is null && snapshot.QueuedActions.Length == 0 && !snapshot.NativeGraphics.Descriptors.Queue.Any(index => snapshot.NativeGraphics.Descriptors.Packets[index].Command == 104) && effect.Pools.IsEmpty;
    private static int CountRgb(byte[] first, byte[] second)
    {
        Assert.Equal(first.Length, second.Length);
        int count = 0;
        for (int index = 0; index < first.Length; index += 4)
        {
            if (first[index] != second[index] || first[index + 1] != second[index + 1] || first[index + 2] != second[index + 2])
            {
                count++;
            }
        }
        return count;
    }
}
