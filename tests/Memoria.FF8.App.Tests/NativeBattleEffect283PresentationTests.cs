using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect283PresentationTests
{
    [Fact]
    public void Original_Effect283_selected_activated_actor_component_preserves_native_lifecycle_and_cold_boundaries()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([283], "MEMORIA_FF8_EFFECT283", SelectOriginalFixture, PrepareDeclaredSource);
    }

    private static NativeEffectFixtureRequest SelectOriginalFixture(BattleAssets assets, ushort effect)
    {
        Assert.Equal((ushort)283, effect);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 261 && row.MagicId == effect && row.KernelSection == 3 && row.KernelIndex == 261);
        BattleEncounter formation = assets.LoadEncounter(363);
        Assert.Equal(96, formation.StageId);
        BattleEnemyPlacement[] enemies = NativeBattleEffectFixtureExporter.SelectEnemies(formation, true);
        Assert.Equal(new[] { 11, 12 }, enemies.Select(enemy => enemy.ModelId).ToArray());
        Assert.Equal((byte)12, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(12, enemies[1].Level), action));
        return new("effect283", effect, 8, 261, 4, 1400, 240) { EncounterId = formation.Id, CasterActorIndex = 4, PreserveOriginalFormationEnemies = true, ControlledContactValue = 7 };
    }

    private static BattleSnapshot PrepareDeclaredSource(GameAssets game, BattleAssets assets, NativeEffectFixtureRequest request)
    {
        string sourcePath = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT283_SOURCE_SNAPSHOT") ?? throw new InvalidDataException("Select a compatible standalone activated-actor source snapshot for the effect283 component.");
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT283_EVIDENCE") ?? throw new InvalidDataException("Select an evidence directory for the effect283 component.");
        return PrepareFromCarrier(game, assets, request, output, NativeBattleEffectFamilyRunner.LoadSnapshot(sourcePath));
    }

    internal static BattleSnapshot PrepareFromCarrier(GameAssets game, BattleAssets assets, NativeEffectFixtureRequest request, string output, BattleSnapshot original)
    {
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(BattleSession.Restore(original).Capture()));
        BattleSession continuation = BattleSession.Restore(original);
        continuation.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
        continuation.Tick();
        BattleSnapshot continued = continuation.CaptureForContinuation();
        Assert.Equal(original.ReadFixturePresentation().NativeGraphics!.Callback + 1, continued.ReadFixturePresentation().NativeGraphics!.Callback);
        Assert.Equal(JsonSerializer.Serialize(original.ReadFixturePresentation().NativeEffectMemory!.Effect283), JsonSerializer.Serialize(continued.ReadFixturePresentation().NativeEffectMemory!.Effect283));
        NativeBattleRestorationPresentationTests.Publish(output, "source-owner-next", continued, null);
        NativeBattleGraphicsState graphics = original.ReadFixturePresentation().NativeGraphics ?? throw new InvalidDataException("The selected component requires its current native graphics owner.");
        Assert.Empty(graphics.Descriptors.Queue);
        Assert.DoesNotContain(graphics.Descriptors.Tasks, task => (task.Flags & 1) != 0 && task.Kind != NativeBattleDescriptorTaskKind.Pump500CC0);
        Assert.Equal(0u, graphics.OriginalEffect!.ReturnedDescriptor);
        Assert.NotNull(original.ReadFixturePresentation().NativeEffectMemory!.Effect283);
        original.ReadFixturePresentation().NativeEffectMemory.Effect283.Validate();
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(NativeBattleEffect283State.FromColdProcess())), JsonSerializer.Serialize(original.ReadFixturePresentation().NativeEffectMemory.Effect283));
        List<object> loaded = [];
        foreach ((byte actor, int modelId, byte nativeId) in new[] { ((byte)3, 11, (byte)27), ((byte)4, 12, (byte)28) })
        {
            NativeBattleGraphicsModel model = ReadOriginalModelCell(graphics, actor);
            BattleCombatantDefinition definition = original.Definition.Combatants.Single(item => item.Id == ReadOriginalModelActorId(graphics, actor));
            Assert.Equal(modelId, definition.ModelId);
            Assert.Null(definition.NativeAi);
            Assert.Equal(nativeId, model.ModelId4);
            Assert.NotNull(model.ActiveResourceRow100);
            Assert.Null(model.ActiveStagedResourceRow100);
            Assert.NotNull(model.GeometryWorldMatrix);
            Assert.NotNull(model.Transform);
            Assert.NotNull(model.Color44);
            NativeBattleGraphicsResource resource = assets.ReadNativeGraphicsResource(definition);
            NativeBattlePoseSource pose = resource.PoseSource ?? throw new InvalidDataException("The selected component requires both current pose resources.");
            ImmutableArray<NativeBattleActionMatrix> bones = NativeBattleBodyBoneMatrices.Require(model, pose);
            Assert.Equal(pose.BoneCount, bones.Length);
            Assert.Equal(46, bones.Length);
            ImmutableArray<NativeBattleRawMesh> geometry = resource.Geometry ?? throw new InvalidDataException("The selected component requires its original current raw body geometry.");
            Assert.NotEmpty(geometry);
            loaded.Add(new { ActorIndex = actor, definition.ModelId, pose.BoneCount, MaximumObjectVertices = geometry.Max(mesh => mesh.Vertices.Length), model.Flags0, model.Color44, model.Transform, model.CurrentBodyBoneMatrices, model.SequenceState });
        }
        BattleCombatantSnapshot target = original.Combatants.Single(actor => actor.NativeParticipant!.ActorIndex == 4);
        NativeBattleParticipantSnapshot participant = target.NativeParticipant!;
        Assert.True(participant.Active);
        Assert.False(participant.Hidden);
        Assert.False(participant.Suspended);
        Assert.False(participant.Untargetable);
        Assert.False(participant.AllocationPendingStatistics);
        NativeBattleStatusSnapshot status = target.NativeStatus ?? throw new InvalidDataException("The selected component requires its current target status owner.");
        NativeBattleActionPacketsState packets = original.NativeActionPackets ?? throw new InvalidDataException("The selected component requires its retained current action packet owner.");
        int priorActions = packets.ActionCount;
        int priorContacts = packets.ContactCount;
        (packets, BattleActionHeaderReference header) = packets.Prepare(new(packets.ModelAliases.DecodeLiteral(4), 8, 12, -1, 261, 283, null));
        Assert.Equal(priorActions, header.Ordinal);
        byte reaction = Assert.Single(assets.NativeActions, action => action.Command == 8 && action.ActionId == 261 && action.MagicId == 283).DamagePresentation;
        NativeBattleContactPacket contact = new(new(packets.ModelAliases.DecodeLiteral(4), reaction, 0, 0, status.Saved, 7, status.Battle), null);
        (packets, _) = packets.AppendContact(contact);
        (packets, _) = packets.Publish(1);
        Assert.Equal(priorContacts, packets.ReadHeader(header).FirstContact.Ordinal);
        Assert.Equal(JsonSerializer.Serialize(original.NativeActionPackets.ExportRetained().Headers.Take(priorActions)), JsonSerializer.Serialize(packets.ExportRetained().Headers.Take(priorActions)));
        (NativeBattleDescriptorState descriptors, _) = graphics.Descriptors.EnqueueAction(128, header);
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([283])[282];
        Assert.Equal(NativeBattleEffect283Profile.Entry, binding.Entry);
        Assert.Equal(NativeBattleEffect283Profile.Initializer, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag282.tim", NativeBattleEffect283Profile.InitializerGlobal), binding.InitializerResource);
        Assert.Equal(NativeBattleEffect283Profile.TextureLength, binding.InitializerAsset!.Data.Length);
        Assert.Same(game.ReadNativeEffect283Resources(), binding.Effect283Resources);
        BattleSnapshot source = (original with { Definition = original.Definition with { NativeEffectCatalog = original.Definition.NativeEffectCatalog.SetItem(282, binding) },NativeActionPackets = packets}).WithFixturePresentation(facts => facts with { NativeGraphics = graphics with { Descriptors = descriptors } });
        Assert.Equal(JsonSerializer.Serialize(original.Combatants), JsonSerializer.Serialize(source.Combatants));
        Assert.Equal(JsonSerializer.Serialize(ReadOriginalModelCells(graphics)), JsonSerializer.Serialize(ReadOriginalModelCells(source.ReadFixturePresentation().NativeGraphics)));
        Assert.Equal(JsonSerializer.Serialize(original.ReadFixturePresentation().NativeEffectMemory.Arena), JsonSerializer.Serialize(source.ReadFixturePresentation().NativeEffectMemory!.Arena));
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(BattleSession.Restore(source).Capture()));
        File.WriteAllText(Path.Combine(output, "controlled-contact.json"), JsonSerializer.Serialize(new { Scenario = "Original effect283 renderer and lifecycle from a compatible standalone activated actor pair; explicitly selected caster4/first-contact4, model12/native28, command8/action261/sequence12; declared seven-HP post-formula contact retains logical HP/status and current poses/colors/scales. This component does not claim ordinary AI self targeting.", Loaded = loaded, header = packets.ReadHeader(header), Contact = contact }));
        return source;
    }
}
