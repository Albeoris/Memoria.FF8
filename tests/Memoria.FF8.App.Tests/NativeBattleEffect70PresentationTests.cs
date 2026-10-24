using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect70Profile;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect70PresentationTests
{
    [Fact]
    public void Original_Effect70_declared_party_contact_preserves_current_resources_and_lifecycle()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([70], "MEMORIA_FF8_EFFECT70");
    }

    internal static void RunComponent(string directory, string output)
    {
        using BattleAssets assets = new(directory);
        using GameAssets game = new(directory);
        BattleSnapshot source = PrepareSource(game, assets, output);
        BattleSession battle = Restore(source);
        BattleScenePresentation scene = new(assets, source.Definition, true);
        NativeBattleEffect70PresentationObserver observer = new();
        HashSet<string> restored = [];
        BattleSnapshot prior = source;
        BattleSnapshot peak = source;
        byte[]? peakPixels = null;
        byte[]? peakPlain = null;
        int callbacks = 0;
        int completions = 0;
        int changedRgb = 0;
        bool cameraChanged = false;
        bool stageChanged = false;
        string boundary = "queued-component";
        NativeBattleRestorationPresentationTests.Publish(output, "prepared-source", source, null);
        try
        {
            for (; callbacks < 1800; callbacks++)
            {
                boundary = "callback-" + callbacks;
                battle.Tick();
                BattleSnapshot current = battle.Capture();
                Assert.Equal(source.Combatants.Select(actor => new { actor.Id, actor.CurrentHp, Saved = actor.NativeStatus?.Saved, Battle = actor.NativeStatus?.Battle }).ToArray(), current.Combatants.Select(actor => new { actor.Id, actor.CurrentHp, Saved = actor.NativeStatus?.Saved, Battle = actor.NativeStatus?.Battle }).ToArray());
                cameraChanged |= JsonSerializer.Serialize(source.NativeCamera) != JsonSerializer.Serialize(current.NativeCamera);
                stageChanged |= JsonSerializer.Serialize(source.NativeStage) != JsonSerializer.Serialize(current.NativeStage);
                if (prior.NativeGraphics!.OriginalEffect is { Binding.Effect: 70, ReturnedDescriptor: > 0 } && current.NativeGraphics!.OriginalEffect is { Binding.Effect: 70, ReturnedDescriptor: 0 })
                {
                    completions++;
                }
                observer.Observe(prior, current, ColdNext);
                NativeBattleLogicalCommand[] commands = observer.CurrentCommands;
                if (commands.Length > 0)
                {
                    ImmutableHashSet<NativeBattlePrimitiveReference> owned = commands.Select(command => command.Packet).ToImmutableHashSet();
                    NativeBattlePrimitiveFrame primitive = current.NativeGraphics!.PrimitiveFrame!;
                    BattleSnapshot without = current with { NativeGraphics = current.NativeGraphics with { PrimitiveFrame = primitive with { LastCommands = primitive.LastCommands.Where(command => !owned.Contains(command.Command.Packet)).ToImmutableArray() } } };
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
                }
                prior = current;
                if (Released(current))
                {
                    break;
                }
            }
            BattleSnapshot completed = battle.Capture();
            Assert.True(callbacks < 1800, "The original effect70 component did not release its native lifecycle.");
            Assert.Equal(1, completions);
            Assert.True(cameraChanged);
            Assert.True(stageChanged);
            Assert.True(changedRgb > 0, "No actual effect70 primitive changed the rendered RGB output.");
            Assert.Equal(1, observer.Contacts);
            Assert.Equal(1, observer.ContactGlyphs);
            observer.Accept(completed, output);
            ColdNext("effect70-visible-peak", peak);
            ColdNext("completed", completed);
            NativeBattleRestorationPresentationTests.Publish(output, "visible", peak, peakPixels!);
            File.WriteAllBytes(Path.Combine(output, "without-effect.rgba"), peakPlain!);
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "completed", Scenario = "Declared original model122 command8/action362/sequence14 post-formula renderer and lifecycle in formation509/stage142 with party0 contact; native status-cache publication, current model geometry, streamed textures and completion; ordinary AI selector204, previous slot15 and formula admission excluded", Effect = Effect, Callbacks = callbacks + 1, Contacts = observer.Contacts, ContactGlyphs = observer.ContactGlyphs, Completions = completions, ChangedRgb = changedRgb, CameraChanged = cameraChanged, StageChanged = stageChanged, RestoredBoundaries = restored.Order(), completed.Frame, NativeCallback = completed.NativeGraphics!.Callback }));
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
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT70_SOURCE_SNAPSHOT") ?? throw new InvalidDataException("Select a concrete original formation509/stage142 standalone source for effect70.");
        BattleSnapshot original = NativeBattleEffectFamilyRunner.LoadSnapshot(path);
        Assert.Equal(509, original.Definition.EncounterId);
        Assert.Equal(142, original.Definition.StageId);
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(BattleSession.Restore(original).Capture()));
        NativeBattleGraphicsState graphics = original.NativeGraphics!;
        Assert.Empty(graphics.Descriptors.Queue);
        Assert.DoesNotContain(graphics.Descriptors.Tasks, task => (task.Flags & 1) != 0 && task.Kind != NativeBattleDescriptorTaskKind.Pump500CC0);
        Assert.Equal(0, original.NativeEffectMemory!.Active);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleEffect70State.FromColdProcess()), JsonSerializer.Serialize(original.NativeEffectMemory.Effect70));
        BattleCombatantDefinition caster = original.Definition.Combatants.Single(actor => actor.Id == graphics.ActorIds[4]);
        Assert.Equal(122, caster.ModelId);
        Assert.Null(caster.NativeAi);
        Assert.Equal((byte)138, graphics.Models[4].ModelId4);
        Assert.True(original.Combatants.Single(actor => actor.Id == caster.Id).NativeParticipant!.Active);
        List<object> currentResources = [];
        foreach (byte actor in new byte[] { 0, 4 })
        {
            BattleCombatantDefinition definition = original.Definition.Combatants.Single(item => item.Id == graphics.ActorIds[actor]);
            NativeBattleGraphicsResource resource = assets.ReadNativeGraphicsResource(definition);
            NativeBattlePoseSource pose = resource.PoseSource ?? throw new InvalidDataException("Effect70 requires both current body skeletons.");
            ImmutableArray<NativeBattleActionMatrix> bones = NativeBattleBodyBoneMatrices.Require(graphics.Models[actor], pose);
            Assert.Equal(pose.BoneCount, bones.Length);
            Assert.NotNull(graphics.Models[actor].GeometryBounds52);
            Assert.NotNull(graphics.Models[actor].GeometryWorldMatrix);
            Assert.NotEmpty(resource.Geometry!.Value);
            currentResources.Add(new { Actor = actor, definition.ModelId, pose.BoneCount, graphics.Models[actor].GeometryBounds52, graphics.Models[actor].Flags0 });
        }
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 362 && row.MagicId == 70 && row.KernelSection == 3 && row.KernelIndex == 362);
        Assert.Equal((byte)14, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(122, 1), action));
        BattleCombatantSnapshot party = original.Combatants.Single(actor => actor.NativeParticipant!.ActorIndex == 0);
        NativeBattleStatusSnapshot status = party.NativeStatus ?? throw new InvalidDataException("Effect70 requires its current recipient status owner.");
        Assert.True(party.NativeParticipant!.Active);
        Assert.False(party.NativeParticipant.Hidden);
        // The declared post-formula input includes47E2D0's party B90/B18 to B92/B1C publication.
        BattleCombatantSnapshot preparedParty = party with { NativeStatus = status with { Presentation = new(status.Saved, status.Battle) } };
        NativeBattleActionPacketsState packets = original.NativeActionPackets!;
        byte priorActions = packets.ActionCount;
        byte priorContacts = packets.ContactCount;
        (packets, int header) = packets.Prepare(new(4, 8, 14, -1, 362, 70, null));
        NativeBattleContactPacket contact = new(new(0, action.DamagePresentation, 0, 0, status.Saved, 7, status.Battle), new(255, 0, 0, 0, 0, 0, 0));
        (packets, _) = packets.AppendContact(contact);
        (packets, _) = packets.Publish(1);
        Assert.Equal(priorActions, header);
        Assert.Equal(priorContacts, packets.Headers[header].FirstContactIndex);
        (NativeBattleDescriptorState descriptors, _) = graphics.Descriptors.Enqueue(104, 128, checked(0x1D280C4u + (uint)header * 20));
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([70])[69];
        Assert.Equal(Entry, binding.Entry);
        Assert.Equal(Initializer, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag069-259.tim", InitializerGlobal), binding.InitializerResource);
        Assert.Same(game.ReadNativeEffect70Resources(), binding.Effect70Resources);
        BattleSnapshot source = original with { Definition = original.Definition with { NativeEffectCatalog = original.Definition.NativeEffectCatalog.SetItem(69, binding) }, Combatants = original.Combatants.Select(actor => actor.Id == party.Id ? preparedParty : actor).ToArray(), NativeActionPackets = packets, NativeGraphics = graphics with { Descriptors = descriptors } };
        Assert.Equal(JsonSerializer.Serialize(graphics.Models), JsonSerializer.Serialize(source.NativeGraphics.Models));
        Assert.Equal(JsonSerializer.Serialize(original.NativeEffectMemory), JsonSerializer.Serialize(source.NativeEffectMemory));
        Assert.Equal(original.Combatants.Select(actor => new { actor.Id, actor.CurrentHp, Saved = actor.NativeStatus?.Saved, Battle = actor.NativeStatus?.Battle }).ToArray(), source.Combatants.Select(actor => new { actor.Id, actor.CurrentHp, Saved = actor.NativeStatus?.Saved, Battle = actor.NativeStatus?.Battle }).ToArray());
        File.WriteAllText(Path.Combine(output, "declared-contact.json"), JsonSerializer.Serialize(new { Scenario = "Declared post-formula party0 contact only; native47E2D0 current Saved/Battle projection before contact publication; no ordinary AI selector204, previous slot15, accuracy or formula admission", Action = action, Header = packets.Headers[header], Contact = contact, PriorActionCount = priorActions, PriorContactCount = priorContacts, StatusBefore = status.Presentation, StatusAfter = preparedParty.NativeStatus!.Presentation, CurrentResources = currentResources }));
        return source;
    }

    private static bool Released(BattleSnapshot snapshot) => snapshot.NativeGraphics?.OriginalEffect is { Binding.Effect: 70, ReturnedDescriptor: 0 } effect && snapshot.NativeEffectMemory!.Active == 0 && snapshot.ActiveAction is null && snapshot.QueuedActions.Length == 0 && !snapshot.NativeGraphics.Descriptors.Queue.Any(index => snapshot.NativeGraphics.Descriptors.Packets[index].Command == 104) && effect.Pools.IsEmpty;
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
