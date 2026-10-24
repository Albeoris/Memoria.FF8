using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect344Profile;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect344PresentationTests
{
    [Fact]
    public void Original_Effect344_declared_contact_preserves_static_music_camera_sequence_and_lifecycle()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([344], "MEMORIA_FF8_EFFECT344");
    }

    internal static void RunComponent(string directory, string output)
    {
        using BattleAssets assets = new(directory);
        using GameAssets game = new(directory);
        BattleSnapshot source = PrepareSource(game, assets, output);
        BattleSession battle = Restore(source);
        BattleScenePresentation scene = new(assets, source.Definition, true);
        NativeBattleEffect344PresentationObserver observer = new();
        HashSet<string> restored = [];
        HashSet<int> observedTexts = [];
        string casterId = source.NativeGraphics!.ActorIds[4] ?? throw new InvalidDataException("Effect344 requires its allocated original caster.");
        NativeBattleGraphicsResource casterTextResource = source.Definition.NativeGraphics!.Bindings.Single(item => item.ActorId == casterId).Resource;
        Dictionary<int, string> expectedTexts = Enumerable.Range(3, 4).ToDictionary(index => index, casterTextResource.ReadText91);
        BattleSnapshot prior = source;
        BattleSnapshot visible = source;
        byte[]? visiblePixels = null;
        int callbacks = 0;
        int completions = 0;
        byte[] sourcePixels = scene.Render(battle, 320, 224).ToArray();
        int changedSceneRgb = 0;
        bool cameraProjectionChanged = false;
        bool cameraChanged = false;
        string boundary = "queued-component";
        NativeBattleRestorationPresentationTests.Publish(output, "prepared-source", source, null);
        try
        {
            ColdNext("before-worker", source);
            for (; callbacks < 900; callbacks++)
            {
                boundary = "callback-" + callbacks;
                battle.Tick();
                BattleSnapshot current = battle.Capture();
                Assert.Equal(source.Combatants.Select(actor => new { actor.Id, actor.CurrentHp, Saved = actor.NativeStatus?.Saved, Battle = actor.NativeStatus?.Battle }).ToArray(), current.Combatants.Select(actor => new { actor.Id, actor.CurrentHp, Saved = actor.NativeStatus?.Saved, Battle = actor.NativeStatus?.Battle }).ToArray());
                cameraChanged |= JsonSerializer.Serialize(source.NativeCamera) != JsonSerializer.Serialize(current.NativeCamera);
                cameraProjectionChanged |= JsonSerializer.Serialize(source.NativeCamera!.Projection) != JsonSerializer.Serialize(current.NativeCamera!.Projection);
                if (prior.NativeGraphics!.OriginalEffect is { Binding.Effect: 344, ReturnedDescriptor: > 0 } && current.NativeGraphics!.OriginalEffect is { Binding.Effect: 344, ReturnedDescriptor: 0 })
                {
                    completions++;
                    ColdNext("effect344-root-retired", current);
                }
                observer.Observe(prior, current, ColdNext);
                foreach ((int index, string text) in expectedTexts)
                {
                    BattleEvent? message = current.Events.FirstOrDefault(item => item.Kind == "NativeText" && item.ActorId == casterId && item.Message == text && item.Frame > source.Frame);
                    if (message is not null && observedTexts.Add(index))
                    {
                        Assert.Equal(30, message.Amount);
                        Assert.Equal(3, message.NativeMessageMode);
                        ColdNext("effect344-sequence20-text-" + index, current);
                    }
                }
                if (observer.CameraPublished)
                {
                    byte[] pixels = scene.Render(battle, 320, 224).ToArray();
                    int changed = CountRgb(pixels, sourcePixels);
                    if (changed > changedSceneRgb)
                    {
                        changedSceneRgb = changed;
                        visible = current;
                        visiblePixels = pixels;
                    }
                }
                prior = current;
                if (Released(current) && observedTexts.Count == 4)
                {
                    break;
                }
            }
            BattleSnapshot completed = battle.Capture();
            Assert.True(callbacks < 900, "Effect344 did not release its actual root and child lifecycle.");
            Assert.Equal(1, completions);
            Assert.Equal(new[] { 3, 4, 5, 6 }, observedTexts.Order().ToArray());
            Assert.True(cameraChanged);
            Assert.True(cameraProjectionChanged);
            Assert.True(changedSceneRgb > 0);
            observer.Accept(completed, output);
            ColdNext("completed", completed);
            if (visiblePixels is not null)
            {
                ColdNext("camera-visible", visible);
                NativeBattleRestorationPresentationTests.Publish(output, "visible", visible, visiblePixels);
            }
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "completed", Scenario = "Declared original122 command8/action368/sequence16 post-formula lifecycle in formation509/stage142 with party0 contact; actual static music91 worker, camera and current caster sequence20; ordinary AI selector200 and formula admission excluded", Effect = Effect, Callbacks = callbacks + 1, Contacts = observer.Contacts, ContactGlyphs = observer.ContactGlyphs, Completions = completions, CameraChanged = cameraChanged, CameraProjectionChanged = cameraProjectionChanged, ChangedSceneRgb = changedSceneRgb, Sequence20TextIndices = observedTexts.Order(), RestoredBoundaries = restored.Order(), completed.Frame, NativeCallback = completed.NativeGraphics!.Callback }));
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
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT344_SOURCE_SNAPSHOT") ?? throw new InvalidDataException("Select a concrete original formation509/stage142 standalone source for effect344.");
        BattleSnapshot original = NativeBattleEffectFamilyRunner.LoadSnapshot(path);
        Assert.Equal(509, original.Definition.EncounterId);
        Assert.Equal(142, original.Definition.StageId);
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(BattleSession.Restore(original).Capture()));
        NativeBattleGraphicsState graphics = original.NativeGraphics!;
        Assert.Empty(graphics.Descriptors.Queue);
        Assert.DoesNotContain(graphics.Descriptors.Tasks, task => (task.Flags & 1) != 0 && task.Kind != NativeBattleDescriptorTaskKind.Pump500CC0);
        Assert.Equal(0, original.NativeEffectMemory!.Active);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleEffect344State.FromColdProcess()), JsonSerializer.Serialize(original.NativeEffectMemory.Effect344));
        BattleCombatantDefinition caster = original.Definition.Combatants.Single(actor => actor.Id == graphics.ActorIds[4]);
        Assert.Equal(122, caster.ModelId);
        Assert.Null(caster.NativeAi);
        Assert.Equal((byte)138, graphics.Models[4].ModelId4);
        Assert.True(original.Combatants.Single(actor => actor.Id == caster.Id).NativeParticipant!.Active);
        NativeBattleGraphicsDefinition originalGraphicsDefinition = original.Definition.NativeGraphics ?? throw new InvalidDataException("Effect344 requires its actual original graphics bindings.");
        int casterBindingIndex = Enumerable.Range(0, originalGraphicsDefinition.Bindings.Length).Single(index => originalGraphicsDefinition.Bindings[index].ActorId == caster.Id);
        NativeBattleGraphicsBinding originalCasterBinding = originalGraphicsDefinition.Bindings[casterBindingIndex];
        NativeBattleGraphicsResource loadedCasterResource = assets.ReadNativeGraphicsResource(caster);
        Assert.NotNull(loadedCasterResource.TextBank);
        NativeBattleGraphicsResource hydratedCasterResource = originalCasterBinding.Resource with { TextBank = loadedCasterResource.TextBank };
        Assert.Equal(JsonSerializer.Serialize(originalCasterBinding.Resource), JsonSerializer.Serialize(hydratedCasterResource with { TextBank = originalCasterBinding.Resource.TextBank }));
        NativeBattleGraphicsDefinition preparedGraphicsDefinition = originalGraphicsDefinition with { Bindings = originalGraphicsDefinition.Bindings.SetItem(casterBindingIndex, originalCasterBinding with { Resource = hydratedCasterResource }) };
        object textBankPreparation = new { Path = "Definition.NativeGraphics.Bindings[" + casterBindingIndex + "].Resource.TextBank", ActorId = caster.Id, Before = originalCasterBinding.Resource.TextBank, After = hydratedCasterResource.TextBank, Producer = "Original selected model DAT section8 text index+8/text+12 through the actor-original graphics binding; only TextBank is hydrated from the installed original resource", NativeAiBefore = caster.NativeAi, NativeAiAfter = caster.NativeAi, SelectedSequence20TextIndices = new[] { 3, 4, 5, 6 } };
        List<object> currentResources = [];
        foreach (byte actor in new byte[] { 0, 4 })
        {
            BattleCombatantDefinition definition = original.Definition.Combatants.Single(item => item.Id == graphics.ActorIds[actor]);
            NativeBattleGraphicsResource resource = assets.ReadNativeGraphicsResource(definition);
            NativeBattlePoseSource pose = resource.PoseSource ?? throw new InvalidDataException("Effect344 requires both current body skeletons.");
            ImmutableArray<NativeBattleActionMatrix> bones = NativeBattleBodyBoneMatrices.Require(graphics.Models[actor], pose);
            Assert.Equal(pose.BoneCount, bones.Length);
            Assert.NotNull(graphics.Models[actor].GeometryBounds52);
            Assert.NotNull(graphics.Models[actor].GeometryWorldMatrix);
            Assert.NotEmpty(resource.Geometry!.Value);
            currentResources.Add(new { Actor = actor, definition.ModelId, pose.BoneCount, graphics.Models[actor].GeometryBounds52, graphics.Models[actor].Flags0 });
        }
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 368 && row.MagicId == 344 && row.KernelSection == 3 && row.KernelIndex == 368);
        Assert.Equal((byte)16, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(122, 1), action));
        BattleCombatantSnapshot party = original.Combatants.Single(actor => actor.NativeParticipant!.ActorIndex == 0);
        NativeBattleStatusSnapshot status = party.NativeStatus ?? throw new InvalidDataException("Effect344 requires its current recipient status owner.");
        Assert.True(party.NativeParticipant!.Active);
        Assert.False(party.NativeParticipant.Hidden);
        // The declared post-formula input includes47E2D0's party B90/B18 to B92/B1C publication.
        BattleCombatantSnapshot preparedParty = party with { NativeStatus = status with { Presentation = new(status.Saved, status.Battle) } };
        NativeBattleActionPacketsState packets = original.NativeActionPackets!;
        byte priorActions = packets.ActionCount;
        byte priorContacts = packets.ContactCount;
        (packets, int header) = packets.Prepare(new(4, 8, 16, -1, 368, 344, null));
        NativeBattleContactPacket contact = new(new(0, action.DamagePresentation, 0, 0, status.Saved, 7, status.Battle), new(255, 0, 0, 0, 0, 0, 0));
        (packets, _) = packets.AppendContact(contact);
        (packets, _) = packets.Publish(1);
        Assert.Equal(priorActions, header);
        Assert.Equal(priorContacts, packets.Headers[header].FirstContactIndex);
        (NativeBattleDescriptorState descriptors, _) = graphics.Descriptors.Enqueue(104, 128, checked(0x1D280C4u + (uint)header * 20));
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([344])[343];
        Assert.Equal(Entry, binding.Entry);
        Assert.Equal(Initializer, binding.Initializer);
        Assert.Null(binding.InitializerResource);
        Assert.Same(game.ReadNativeEffect344Resources(), binding.Effect344Resources);
        BattleSnapshot source = original with { Definition = original.Definition with { NativeGraphics = preparedGraphicsDefinition, NativeEffectCatalog = original.Definition.NativeEffectCatalog.SetItem(343, binding) }, Combatants = original.Combatants.Select(actor => actor.Id == party.Id ? preparedParty : actor).ToArray(), NativeActionPackets = packets, NativeGraphics = graphics with { Descriptors = descriptors } };
        Assert.Equal(JsonSerializer.Serialize(graphics.Models), JsonSerializer.Serialize(source.NativeGraphics.Models));
        Assert.Equal(JsonSerializer.Serialize(original.NativeEffectMemory), JsonSerializer.Serialize(source.NativeEffectMemory));
        Assert.Equal(original.Combatants.Select(actor => new { actor.Id, actor.CurrentHp, Saved = actor.NativeStatus?.Saved, Battle = actor.NativeStatus?.Battle }).ToArray(), source.Combatants.Select(actor => new { actor.Id, actor.CurrentHp, Saved = actor.NativeStatus?.Saved, Battle = actor.NativeStatus?.Battle }).ToArray());
        File.WriteAllText(Path.Combine(output, "declared-contact.json"), JsonSerializer.Serialize(new { Scenario = "Declared post-formula party0 contact only; native47E2D0 current Saved/Battle projection before contact publication; no ordinary AI selector200, previous slot15, accuracy or formula admission", Action = action, Header = packets.Headers[header], Contact = contact, PriorActionCount = priorActions, PriorContactCount = priorContacts, StatusBefore = status.Presentation, StatusAfter = preparedParty.NativeStatus!.Presentation, TextBankPreparation = textBankPreparation, CurrentResources = currentResources }));
        return source;
    }

    private static bool Released(BattleSnapshot snapshot) => snapshot.NativeGraphics?.OriginalEffect is { Binding.Effect: 344, ReturnedDescriptor: 0 } effect && snapshot.NativeEffectMemory!.Active == 0 && snapshot.ActiveAction is null && snapshot.QueuedActions.Length == 0 && !snapshot.NativeGraphics.Descriptors.Queue.Any(index => snapshot.NativeGraphics.Descriptors.Packets[index].Command == 104) && effect.Pools.IsEmpty;

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
