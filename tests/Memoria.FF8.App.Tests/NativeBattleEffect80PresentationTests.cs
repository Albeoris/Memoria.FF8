using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect80Profile;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect80PresentationTests
{
    [Fact]
    public void Original_Effect80_declared_allocated_recipient_contact_preserves_current_resources_and_lifecycle()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([80], "MEMORIA_FF8_EFFECT80");
    }

    internal static void RunComponent(string directory, string output)
    {
        using BattleAssets assets = new(directory);
        using GameAssets game = new(directory);
        BattleSnapshot source = PrepareSource(game, assets, output);
        source = NativeBattleRetainedEffectMigrationAcceptance.Prepare(source, 80, output);
        BattleSession battle = Restore(source);
        BattleScenePresentation scene = new(assets, source.Definition);
        NativeBattleEffect80PresentationObserver observer = new();
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
            ColdNext("before-worker", source);
            for (; callbacks < 1800; callbacks++)
            {
                boundary = "callback-" + callbacks;
                battle.Tick();
                BattleSnapshot current = battle.CaptureForContinuation();
                Assert.Equal(source.Combatants.Select(actor => new { actor.Id, actor.CurrentHp, Saved = actor.NativeStatus?.Saved, Battle = actor.NativeStatus?.Battle }).ToArray(), current.Combatants.Select(actor => new { actor.Id, actor.CurrentHp, Saved = actor.NativeStatus?.Saved, Battle = actor.NativeStatus?.Battle }).ToArray());
                cameraChanged |= JsonSerializer.Serialize(source.ReadFixturePresentation().NativeCamera) != JsonSerializer.Serialize(current.ReadFixturePresentation().NativeCamera);
                stageChanged |= JsonSerializer.Serialize(source.ReadFixturePresentation().NativeStage) != JsonSerializer.Serialize(current.ReadFixturePresentation().NativeStage);
                if (prior.ReadFixturePresentation().NativeGraphics!.OriginalEffect is { Binding.Effect: 80, ReturnedDescriptor: > 0 } && current.ReadFixturePresentation().NativeGraphics!.OriginalEffect is { Binding.Effect: 80, ReturnedDescriptor: 0 })
                {
                    completions++;
                }
                observer.Observe(prior, current, ColdNext);
                NativeBattleLogicalCommand[] commands = observer.CurrentCommands;
                NativeBattleReachedCaptureAssertions.Moving(80, current, commands);
                if (commands.Length > 0)
                {
                    ImmutableHashSet<NativeBattlePrimitiveReference> owned = commands.Select(command => command.Packet).ToImmutableHashSet();
                    NativeBattlePrimitiveFrame primitive = current.ReadFixturePresentation().NativeGraphics!.PrimitiveFrame!;
                    BattleSnapshot without = current.WithFixturePresentation(facts => facts with { NativeGraphics = current.ReadFixturePresentation().NativeGraphics! with { PrimitiveFrame = primitive with { LastCommands = primitive.LastCommands.Where(command => !owned.Contains(command.Command.Packet)).ToImmutableArray() } } });
                    byte[] pixels = scene.Render(battle, 320, 224).ToArray();
                    byte[] plain = new BattleScenePresentation(assets, source.Definition).Render(BattleSession.Restore(without), 320, 224).ToArray();
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
            BattleSnapshot completed = battle.CaptureForContinuation();
            Assert.True(callbacks < 1800, "The original effect80 component did not release its native lifecycle.");
            Assert.Equal(1, completions);
            Assert.True(cameraChanged);
            Assert.True(stageChanged);
            Assert.True(changedRgb > 100, "No actual effect80 primitive changed the rendered RGB output.");
            Assert.Equal(1, observer.Contacts);
            Assert.Equal(1, observer.ContactGlyphs);
            observer.Accept(completed, output);
            ColdNext("effect80-visible-peak", peak);
            ColdNext("completed", completed);
            NativeBattleRestorationPresentationTests.Publish(output, "visible", peak, peakPixels!);
            File.WriteAllBytes(Path.Combine(output, "without-effect.rgba"), peakPlain!);
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "completed", Scenario = "Declared allocated model123 command8/action352/sequence13 post-formula Amount0 renderer and lifecycle in formation511 with actual model124 recipient5; current status-cache publication, body, nodes, ACT, sprites and trails; ordinary AI, deaths, action74 and formula admission excluded", Effect = Effect, Callbacks = callbacks + 1, Contacts = observer.Contacts, ContactGlyphs = observer.ContactGlyphs, Completions = completions, ChangedRgb = changedRgb, CameraChanged = cameraChanged, StageChanged = stageChanged, RestoredBoundaries = restored.Order(), completed.Frame, NativeCallback = completed.ReadFixturePresentation().NativeGraphics!.Callback }));
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
            byte[] pixels = new BattleScenePresentation(assets, source.Definition).Render(cold, 320, 224).ToArray();
            Assert.Equal(new BattleScenePresentation(assets, source.Definition).Render(direct, 320, 224).ToArray(), pixels);
            NativeBattleRestorationPresentationTests.Publish(output, name + "-next", cold.Capture(), pixels);
            restored.Add(name);
        }
    }

    private static BattleSnapshot PrepareSource(GameAssets game, BattleAssets assets, string output)
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT80_SOURCE_SNAPSHOT") ?? throw new InvalidDataException("Select the concrete allocated formation511 standalone source for effect80.");
        return PrepareFromEntry(game, assets, output, NativeBattleEffectFamilyRunner.LoadSnapshot(path));
    }

    internal static BattleSnapshot PrepareFromEntry(GameAssets game, BattleAssets assets, string output, BattleSnapshot original)
    {
        Assert.Equal(511, original.Definition.EncounterId);
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(BattleSession.Restore(original).Capture()));
        BattleSession sourceSession = BattleSession.Restore(original);
        NativeBattleGraphicsState graphics = original.ReadFixturePresentation().NativeGraphics!;
        Assert.Empty(graphics.Descriptors.Queue);
        Assert.DoesNotContain(graphics.Descriptors.Tasks, task => (task.Flags & 1) != 0 && task.Kind != NativeBattleDescriptorTaskKind.Pump500CC0);
        Assert.Equal(0, original.ReadFixturePresentation().NativeEffectMemory!.Active);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(NativeBattleEffect80State.FromColdProcess())), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(sourceSession.Presentation!.Memory.Effect80)));
        Assert.Equal((ushort)1448, original.ReadFixturePresentation().NativeModelCpuResources!.ScratchMaximum);
        Assert.Equal(new NativeBattleClockState(1, 0, 0), original.NativeClock);
        BattleCombatantDefinition caster = sourceSession.GetCombatantDefinition(ReadOriginalModelActorId(graphics, 4) ?? throw new InvalidDataException("Effect80 requires its actual caster allocation."));
        BattleCombatantDefinition recipient = sourceSession.GetCombatantDefinition(ReadOriginalModelActorId(graphics, 5) ?? throw new InvalidDataException("Effect80 requires its actual recipient allocation."));
        Assert.Equal(123, caster.ModelId);
        Assert.Equal(124, recipient.ModelId);
        NativeBattleEnemyAllocation allocation = Assert.Single(original.NativeAllocations, item => item.Id == recipient.Id);
        Assert.Equal(3, allocation.SceneSlot);
        Assert.Equal(1, allocation.Generation);
        BattleCombatantDefinition prototype = Assert.Single(original.Definition.Combatants, actor => !actor.IsPlayer && actor.SceneSlot == allocation.SceneSlot);
        Assert.Equal(124, prototype.ModelId);
        Assert.NotEqual(prototype.Id, recipient.Id);
        Assert.Equal((byte)139, ReadOriginalModelCell(graphics, 4).ModelId4);
        Assert.Equal((byte)140, ReadOriginalModelCell(graphics, 5).ModelId4);
        foreach ((byte index, int bones) in new[] { ((byte)4, 2), ((byte)5, 71) })
        {
            BattleCombatantDefinition actor = sourceSession.GetCombatantDefinition(ReadOriginalModelActorId(graphics, index) ?? throw new InvalidDataException("Effect80 requires its current physical allocation."));
            NativeBattlePoseSource pose = assets.ReadNativeGraphicsResource(actor).PoseSource ?? throw new InvalidDataException("Effect80 requires its current allocated skeleton.");
            Assert.Equal(bones, NativeBattleBodyBoneMatrices.Require(ReadOriginalModelCell(graphics, index), pose).Length);
            Assert.NotNull(ReadOriginalModelCell(graphics, index).GeometryBounds52);
            Assert.NotNull(ReadOriginalModelCell(graphics, index).GeometryWorldMatrix);
        }
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 352 && row.MagicId == 80 && row.KernelSection == 3 && row.KernelIndex == 352);
        Assert.Equal((byte)13, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(123, 1), action));
        BattleCombatantSnapshot target = original.Combatants.Single(actor => actor.Id == recipient.Id);
        NativeBattleStatusSnapshot status = target.NativeStatus ?? throw new InvalidDataException("Effect80 requires its allocated recipient status owner.");
        Assert.True(target.NativeParticipant!.Active);
        BattleCombatantSnapshot preparedTarget = target with { NativeStatus = status with { Presentation = new(status.Saved, status.Battle) } };
        NativeBattleActionPacketsState packets = original.ReadFixturePresentation().NativeActionPackets!;
        int priorActions = packets.ActionCount;
        int priorContacts = packets.ContactCount;
        (packets, BattleActionHeaderReference header) = packets.Prepare(new(packets.ModelAliases.DecodeLiteral(4), 8, 13, -1, 352, 80, null));
        NativeBattleContactPacket contact = new(new(packets.ModelAliases.DecodeLiteral(5), action.DamagePresentation, 0, 0, status.Saved, 7, status.Battle), null);
        (packets, _) = packets.AppendContact(contact);
        (packets, _) = packets.Publish(1);
        Assert.Equal(priorActions, header.Ordinal);
        Assert.Equal(priorContacts, packets.ReadHeader(header).FirstContact.Ordinal);
        (NativeBattleDescriptorState descriptors, _) = graphics.Descriptors.EnqueueAction(128, header);
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([80])[79];
        Assert.Equal(Entry, binding.Entry);
        Assert.Equal(Initializer, binding.Initializer);
        Assert.Same(game.ReadNativeEffect80Resources(), binding.Effect80Resources);
        NativeBattlePresentationStatusMemory presentationStatus = (original.NativePresentationStatusMemory ?? throw new InvalidDataException("Effect80 requires its current physical presentation-status bank.")).Observe(5, preparedTarget.NativeStatus!.Presentation);
        BattleSnapshot source = ((original with { Definition = original.Definition with { NativeEffectCatalog = original.Definition.NativeEffectCatalog.SetItem(79, binding) },Combatants = original.Combatants.Select(actor => actor.Id == target.Id ? preparedTarget : actor).ToArray(),NativePresentationStatusMemory = presentationStatus}).WithFixturePresentation(facts => facts with { NativeActionPackets = packets })).WithFixturePresentation(facts => facts with { NativeGraphics = graphics with { Descriptors = descriptors } });
        Assert.Equal(JsonSerializer.Serialize(ReadOriginalModelCells(graphics)), JsonSerializer.Serialize(ReadOriginalModelCells(source.ReadFixturePresentation().NativeGraphics!)));
        Assert.Equal(JsonSerializer.Serialize(original.ReadFixturePresentation().NativeModelCpuResources), JsonSerializer.Serialize(source.ReadFixturePresentation().NativeModelCpuResources));
        Assert.Equal(JsonSerializer.Serialize(original.NativeClock), JsonSerializer.Serialize(source.NativeClock));
        File.WriteAllText(Path.Combine(output, "declared-contact.json"), JsonSerializer.Serialize(new { Scenario = "Declared post-formula Amount0 contact after actual native allocation; no ordinary AI, death admission, action74 replay or damage formula", Action = action, header = packets.ReadHeader(header), Contact = contact, PriorActionCount = priorActions, PriorContactCount = priorContacts, CacheProducer = "47E2D0 publishes current Saved/Battle into the presentation cache", StatusBefore = status.Presentation, StatusAfter = preparedTarget.NativeStatus!.Presentation, CpuScratchMaximum = original.ReadFixturePresentation().NativeModelCpuResources!.ScratchMaximum, SourceNativeClock = original.NativeClock }));
        return source;
    }

    private static bool Released(BattleSnapshot snapshot) => snapshot.ReadFixturePresentation().NativeGraphics?.OriginalEffect is { Binding.Effect: 80, ReturnedDescriptor: 0 } effect && snapshot.ReadFixturePresentation().NativeEffectMemory!.Active == 0 && snapshot.ActiveAction is null && snapshot.QueuedActions.Length == 0 && !snapshot.ReadFixturePresentation().NativeGraphics!.Descriptors.Queue.Any(index => snapshot.ReadFixturePresentation().NativeGraphics!.Descriptors.Packets[index].Command == 104) && effect.Pools.IsEmpty;
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
