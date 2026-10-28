using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect79PresentationTests
{
    [Fact]
    public void Original_Effect79_selected_declared_death_action_component_preserves_native_lifecycle_and_cold_boundaries()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([79], "MEMORIA_FF8_EFFECT79", SelectOriginalFixture, PrepareDeclaredSource);
    }

    private static NativeEffectFixtureRequest SelectOriginalFixture(BattleAssets assets, ushort effect)
    {
        Assert.Equal((ushort)79, effect);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 351 && row.MagicId == effect && row.KernelSection == 3 && row.KernelIndex == 351);
        BattleEncounter formation = assets.LoadEncounter(509);
        Assert.Equal(142, formation.StageId);
        BattleEnemyPlacement[] enemies = NativeBattleEffectFixtureExporter.SelectEnemies(formation, true);
        Assert.Equal(new[] { 123, 122 }, enemies.Select(enemy => enemy.ModelId).ToArray());
        Assert.Equal((byte)15, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(122, enemies[1].Level), action));
        return new("effect79", effect, 8, 351, 3, 1800, 240) { EncounterId = formation.Id, CasterActorIndex = 4, PreserveOriginalFormationEnemies = true, ControlledContactValue = 7 };
    }

    private static BattleSnapshot PrepareDeclaredSource(GameAssets game, BattleAssets assets, NativeEffectFixtureRequest request)
    {
        string sourcePath = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT79_SOURCE_SNAPSHOT") ?? throw new InvalidDataException("Select a compatible standalone original-carrier source snapshot for the effect79 component.");
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT79_EVIDENCE") ?? throw new InvalidDataException("Select an evidence directory for the effect79 component.");
        BattleSnapshot original = NativeBattleEffectFamilyRunner.LoadSnapshot(sourcePath);
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(BattleSession.Restore(original).Capture()));
        BattleSession continuation = BattleSession.Restore(original);
        continuation.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
        continuation.Tick();
        BattleSnapshot continued = continuation.Capture();
        Assert.Equal(original.NativeGraphics!.Callback + 1, continued.NativeGraphics!.Callback);
        Assert.Equal(JsonSerializer.Serialize(original.NativeEffectMemory!.Effect79), JsonSerializer.Serialize(continued.NativeEffectMemory!.Effect79));
        NativeBattleRestorationPresentationTests.Publish(output, "source-owner-next", continued, null);
        NativeBattleGraphicsState graphics = original.NativeGraphics ?? throw new InvalidDataException("The selected component requires its current native graphics owner.");
        Assert.Empty(graphics.Descriptors.Queue);
        Assert.DoesNotContain(graphics.Descriptors.Tasks, task => (task.Flags & 1) != 0 && task.Kind != NativeBattleDescriptorTaskKind.Pump500CC0);
        Assert.Equal(0u, graphics.OriginalEffect!.ReturnedDescriptor);
        Assert.NotNull(original.NativeEffectMemory!.Effect79);
        original.NativeEffectMemory.Effect79.Validate();
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(NativeBattleEffect79State.FromColdProcess())), JsonSerializer.Serialize(original.NativeEffectMemory.Effect79));
        List<object> loaded = [];
        foreach ((byte actor, int modelId, byte nativeId) in new[] { ((byte)3, 123, (byte)139), ((byte)4, 122, (byte)138) })
        {
            NativeBattleGraphicsModel model = graphics.Models[actor];
            BattleCombatantDefinition definition = original.Definition.Combatants.Single(item => item.Id == graphics.ActorIds[actor]);
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
        BattleCombatantSnapshot selectedTarget = original.Combatants.Single(actor => actor.NativeParticipant!.ActorIndex == 3);
        NativeBattleStatusSnapshot status = selectedTarget.NativeStatus ?? throw new InvalidDataException("The selected component requires its current target status owner.");
        Assert.Equal(100, selectedTarget.CurrentHp);
        Assert.Equal((ushort)0, status.Saved);
        Assert.Equal(0u, status.Battle);
        Assert.True(selectedTarget.NativeParticipant!.Active);
        Assert.True(selectedTarget.NativeParticipant.Hidden);
        Assert.False(selectedTarget.NativeParticipant.Suspended);
        Assert.False(selectedTarget.NativeParticipant.Untargetable);
        NativeBattleActionPacketsState packets = original.NativeActionPackets ?? throw new InvalidDataException("The selected component requires its retained current action packet owner.");
        byte priorActions = packets.ActionCount;
        byte priorContacts = packets.ContactCount;
        (packets, int header) = packets.Prepare(new(4, 8, 15, -1, 351, 79, null));
        Assert.Equal(priorActions, header);
        byte reaction = Assert.Single(assets.NativeActions, action => action.Command == 8 && action.ActionId == 351 && action.MagicId == 79).DamagePresentation;
        NativeBattleContactPacket contact = new(new(3, reaction, 0, 0, status.Saved, 7, status.Battle), new(255, 0, 0, 0, 0, 0, 0));
        (packets, _) = packets.AppendContact(contact);
        (packets, _) = packets.Publish(1);
        Assert.Equal(priorContacts, packets.Headers[header].FirstContactIndex);
        Assert.Equal(JsonSerializer.Serialize(original.NativeActionPackets.Headers.Take(priorActions)), JsonSerializer.Serialize(packets.Headers.Take(priorActions)));
        (NativeBattleDescriptorState descriptors, _) = graphics.Descriptors.Enqueue(104, 128, checked(0x1D280C4u + (uint)header * 20));
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([79])[78];
        Assert.Equal(NativeBattleEffect79Profile.Entry, binding.Entry);
        Assert.Equal(NativeBattleEffect79Profile.Initializer, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag078.tim", NativeBattleEffect79Profile.InitializerGlobal), binding.InitializerResource);
        Assert.Equal(NativeBattleEffect79Profile.TextureLength, binding.InitializerAsset!.Data.Length);
        Assert.Same(game.ReadNativeEffect79Resources(), binding.Effect79Resources);
        BattleSnapshot source = original with { Definition = original.Definition with { NativeEffectCatalog = original.Definition.NativeEffectCatalog.SetItem(78, binding) }, NativeActionPackets = packets, NativeGraphics = graphics with { Descriptors = descriptors } };
        Assert.Equal(JsonSerializer.Serialize(original.Combatants), JsonSerializer.Serialize(source.Combatants));
        Assert.Equal(JsonSerializer.Serialize(graphics.Models), JsonSerializer.Serialize(source.NativeGraphics.Models));
        Assert.Equal(JsonSerializer.Serialize(original.NativeEffectMemory.Arena), JsonSerializer.Serialize(source.NativeEffectMemory!.Arena));
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(BattleSession.Restore(source).Capture()));
        File.WriteAllText(Path.Combine(output, "controlled-contact.json"), JsonSerializer.Serialize(new { Scenario = "Original effect79 renderer and lifecycle from a preserved standalone formation509/stage142 carrier; caster4/model122/native138, command8/action351/sequence15. Explicit post-formula death-action component retains current hidden companion3/model123/native139 with HP100 and rawSaved0/Battle0. The original callback3 death-action selector139 is recorded as provenance; this component excludes ordinary death-AI/KO/selector-admission/formula execution and does not mutate HP, status or activation to fabricate eligibility.", Loaded = loaded, Header = packets.Headers[header], Contact = contact }));
        return source;
    }
}
