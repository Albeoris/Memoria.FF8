using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect83PresentationTests
{
    [Fact]
    public void Original_Effect83_selected_declared_alive_target_component_preserves_native_lifecycle_and_cold_boundaries()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([83], "MEMORIA_FF8_EFFECT83", SelectOriginalFixture, PrepareDeclaredSource);
    }

    private static NativeEffectFixtureRequest SelectOriginalFixture(BattleAssets assets, ushort effect)
    {
        Assert.Equal((ushort)83, effect);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 336 && row.MagicId == effect && row.KernelSection == 3 && row.KernelIndex == 336);
        BattleEncounter formation = assets.LoadEncounter(509);
        Assert.Equal(142, formation.StageId);
        BattleEnemyPlacement[] enemies = NativeBattleEffectFixtureExporter.SelectEnemies(formation, true);
        Assert.Equal(new[] { 123, 122 }, enemies.Select(enemy => enemy.ModelId).ToArray());
        Assert.Equal((byte)16, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(122, enemies[1].Level), action));
        return new("effect83", effect, 8, 336, 0, 1400, 240) { EncounterId = formation.Id, CasterActorIndex = 4, PreserveOriginalFormationEnemies = true, ControlledContactValue = 7 };
    }

    private static BattleSnapshot PrepareDeclaredSource(GameAssets game, BattleAssets assets, NativeEffectFixtureRequest request)
    {
        string sourcePath = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT83_SOURCE_SNAPSHOT") ?? throw new InvalidDataException("Select a compatible standalone original-carrier source snapshot for the effect83 component.");
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT83_EVIDENCE") ?? throw new InvalidDataException("Select an evidence directory for the effect83 component.");
        BattleSnapshot original = NativeBattleEffectFamilyRunner.LoadSnapshot(sourcePath);
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(BattleSession.Restore(original).Capture()));
        BattleSession continuation = BattleSession.Restore(original);
        continuation.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
        continuation.Tick();
        BattleSnapshot continued = continuation.Capture();
        Assert.Equal(original.NativeGraphics!.Callback + 1, continued.NativeGraphics!.Callback);
        Assert.Equal(JsonSerializer.Serialize(original.NativeEffectMemory!.Effect83), JsonSerializer.Serialize(continued.NativeEffectMemory!.Effect83));
        NativeBattleRestorationPresentationTests.Publish(output, "source-owner-next", continued, null);
        NativeBattleGraphicsState graphics = original.NativeGraphics ?? throw new InvalidDataException("The selected component requires its current native graphics owner.");
        Assert.Empty(graphics.Descriptors.Queue);
        Assert.DoesNotContain(graphics.Descriptors.Tasks, task => (task.Flags & 1) != 0 && task.Kind != NativeBattleDescriptorTaskKind.Pump500CC0);
        Assert.Equal(0u, graphics.OriginalEffect!.ReturnedDescriptor);
        Assert.NotNull(original.NativeEffectMemory!.Effect83);
        original.NativeEffectMemory.Effect83.Validate();
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(NativeBattleEffect83State.FromColdProcess())), JsonSerializer.Serialize(original.NativeEffectMemory.Effect83));
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
        BattleCombatantSnapshot selectedParty = original.Combatants.Single(actor => actor.NativeParticipant!.ActorIndex == 0);
        NativeBattleStatusSnapshot status = selectedParty.NativeStatus ?? throw new InvalidDataException("The selected component requires its current target status owner.");
        Assert.Equal(100, selectedParty.CurrentHp);
        Assert.Equal((ushort)768, status.Saved);
        Assert.Equal(0u, status.Battle);
        Assert.Equal((ushort)0, (ushort)(status.Saved & 1));
        Assert.Equal(new NativeBattleStatusMirror(768, 0), status.Presentation);
        NativeBattleActionPacketsState packets = original.NativeActionPackets ?? throw new InvalidDataException("The selected component requires its retained current action packet owner.");
        byte priorActions = packets.ActionCount;
        byte priorContacts = packets.ContactCount;
        (packets, int header) = packets.Prepare(new(4, 8, 16, -1, 336, 83, null));
        Assert.Equal(priorActions, header);
        byte reaction = Assert.Single(assets.NativeActions, action => action.Command == 8 && action.ActionId == 336 && action.MagicId == 83).DamagePresentation;
        NativeBattleContactPacket contact = new(new(0, reaction, 0, 0, status.Saved, 7, status.Battle), new(255, 0, 0, 0, 0, 0, 0));
        (packets, _) = packets.AppendContact(contact);
        (packets, _) = packets.Publish(1);
        Assert.Equal(priorContacts, packets.Headers[header].FirstContactIndex);
        Assert.Equal(JsonSerializer.Serialize(original.NativeActionPackets.Headers.Take(priorActions)), JsonSerializer.Serialize(packets.Headers.Take(priorActions)));
        (NativeBattleDescriptorState descriptors, _) = graphics.Descriptors.Enqueue(104, 128, checked(0x1D280C4u + (uint)header * 20));
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([83])[82];
        Assert.Equal(NativeBattleEffect83Profile.Entry, binding.Entry);
        Assert.Equal(NativeBattleEffect83Profile.Initializer, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag082.tim", NativeBattleEffect83Profile.InitializerGlobal), binding.InitializerResource);
        Assert.Equal(NativeBattleEffect83Profile.TextureLength, binding.InitializerAsset!.Data.Length);
        Assert.Same(game.ReadNativeEffect83Resources(), binding.Effect83Resources);
        BattleSnapshot source = original with { Definition = original.Definition with { NativeEffectCatalog = original.Definition.NativeEffectCatalog.SetItem(82, binding) }, NativeActionPackets = packets, NativeGraphics = graphics with { Descriptors = descriptors } };
        Assert.Equal(JsonSerializer.Serialize(original.Combatants), JsonSerializer.Serialize(source.Combatants));
        Assert.Equal(JsonSerializer.Serialize(graphics.Models), JsonSerializer.Serialize(source.NativeGraphics.Models));
        Assert.Equal(JsonSerializer.Serialize(original.NativeEffectMemory.Arena), JsonSerializer.Serialize(source.NativeEffectMemory!.Arena));
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(BattleSession.Restore(source).Capture()));
        File.WriteAllText(Path.Combine(output, "controlled-contact.json"), JsonSerializer.Serialize(new { Scenario = "Original effect83 renderer and lifecycle from a preserved standalone formation509/stage142 carrier; caster4/model122/native138, command8/action336/sequence16. Explicit aliveparty0 post-formula renderer/lifecycle input preserves sourceHP100, rawSaved768/Battle0 and retainedPresentation768/0. Original AI opcode38 criterion0 requires allocated deadparty; this alive target is not ordinarily eligible. This component excludes ordinary AI/dead-target/KO/formula admission and does not mutate sourceHP/status to fabricate eligibility.", Loaded = loaded, Header = packets.Headers[header], Contact = contact }));
        return source;
    }
}
