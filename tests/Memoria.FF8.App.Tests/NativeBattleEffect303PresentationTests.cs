using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect303PresentationTests
{
    [Fact]
    public void Original_Effect303_preserves_loaded_recipient_and_native_phase_activation()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([303], "MEMORIA_FF8_EFFECT303", SelectOriginalFixture, PrepareDeclaredSource);
    }

    private static NativeEffectFixtureRequest SelectOriginalFixture(BattleAssets assets, ushort effect)
    {
        Assert.Equal((ushort)303, effect);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 258 && row.MagicId == effect && row.KernelSection == 3 && row.KernelIndex == 258);
        BattleEncounter formation = assets.LoadEncounter(363);
        Assert.Equal(96, formation.StageId);
        BattleEnemyPlacement[] enemies = NativeBattleEffectFixtureExporter.SelectEnemies(formation, true);
        Assert.Equal(new[] { 11, 12 }, enemies.Select(enemy => enemy.ModelId).ToArray());
        Assert.All(enemies, enemy => Assert.Equal(new NativeBattleVector(0, 0, -3200), enemy.NativePosition));
        Assert.False(enemies[0].Hidden);
        Assert.False(enemies[0].InitiallySuspended);
        Assert.False(enemies[0].Untargetable);
        Assert.True(enemies[1].Hidden);
        Assert.True(enemies[1].InitiallySuspended);
        Assert.True(enemies[1].Untargetable);
        Assert.Equal((byte)13, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(11, enemies[0].Level), action));
        return new("effect303", effect, 8, 258, 4, 1400, 240) { EncounterId = formation.Id, CasterActorIndex = 3, PreserveOriginalFormationEnemies = true, ControlledContactValue = 7 };
    }

    private static BattleSnapshot PrepareDeclaredSource(GameAssets game, BattleAssets assets, NativeEffectFixtureRequest request)
    {
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT303_EVIDENCE") ?? throw new InvalidDataException("Select an evidence directory for the original effect303 component.");
        // Entry preparation loads the original complete pair. The declared component header below selects the actual latent recipient.
        NativeEffectFixtureExportResult exported = NativeBattleEffectFixtureExporter.Export(request with { TargetActorIndex = 0 }, Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? throw new InvalidDataException("Select installed original resources."), Path.Combine(output, "fixture"));
        Assert.True(exported.Status != "blocked", exported.FirstBlocker);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, "fixture", "queued-action104.battle.json"));
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([303])[302];
        Assert.Equal(NativeBattleEffect303Profile.Entry, binding.Entry);
        Assert.Equal(NativeBattleEffect303Profile.Initializer, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag302.tim", NativeBattleEffect303Profile.InitializerGlobal), binding.InitializerResource);
        Assert.Same(game.ReadNativeEffect303Resources(), binding.Effect303Resources);
        Assert.Equal(NativeBattleEffect303Profile.TextureLength, binding.InitializerAsset!.Data.Length);
        source = source with { Definition = source.Definition with { NativeEffectCatalog = source.Definition.NativeEffectCatalog.SetItem(302, binding) } };
        List<object> loaded = [];
        foreach ((byte index, int modelId, byte nativeId) in new[] { ((byte)3, 11, (byte)27), ((byte)4, 12, (byte)28) })
        {
            NativeBattleGraphicsModel model = source.NativeGraphics!.Models[index];
            BattleCombatantDefinition definition = source.Definition.Combatants.Single(actor => actor.Id == source.NativeGraphics.ActorIds[index]);
            Assert.Equal(modelId, definition.ModelId);
            Assert.Equal(index - 3, definition.SceneSlot);
            Assert.Equal(nativeId, model.ModelId4);
            Assert.NotNull(model.ActiveResourceRow100);
            Assert.Null(model.ActiveStagedResourceRow100);
            Assert.NotNull(model.GeometryWorldMatrix);
            NativeBattleGraphicsResource resource = assets.ReadNativeGraphicsResource(definition);
            NativeBattlePoseSource pose = resource.PoseSource ?? throw new InvalidDataException("The original phase pair requires both actual skeleton resources.");
            ImmutableArray<NativeBattleActionMatrix> bones = NativeBattleBodyBoneMatrices.Require(model, pose);
            Assert.Equal(pose.BoneCount, bones.Length);
            if (index == 3)
            {
                Assert.True(pose.BoneCount > 39);
                NativeBattleDecodedPose decoded = NativeBattlePoseMatrices.ReadDecodedPose(model.SequenceState ?? throw new InvalidDataException("The actual caster requires its current decoded pose."), pose);
                Assert.Equal(JsonSerializer.Serialize(NativeBattlePoseMatrices.StageBoneMatrices(pose, decoded)), JsonSerializer.Serialize(bones));
                _ = bones[3];
                _ = bones[39];
            }
            else
            {
                ImmutableArray<NativeBattleRawMesh> geometry = resource.Geometry ?? throw new InvalidDataException("The original recipient requires its actual raw body geometry.");
                Assert.NotEmpty(geometry);
                Assert.True(geometry.Max(mesh => mesh.Vertices.Length) * 12 <= NativeBattleEffect303Profile.BodyProjectionCapacity);
                Assert.True(resource.Contains(13));
            }
            loaded.Add(new { ActorIndex = index, definition.ModelId, definition.SceneSlot, SkeletonSha256 = Convert.ToHexString(SHA256.HashData(pose.Skeleton.AsSpan())), BoneCount = pose.BoneCount, CurrentBones = model.CurrentBodyBoneMatrices, model.GeometryWorldMatrix, model.ActiveResourceRow100, model.SequenceState });
        }
        NativeBattleParticipantSnapshot latent = source.Combatants.Single(actor => actor.NativeParticipant!.ActorIndex == 4).NativeParticipant!;
        Assert.True(latent.Active);
        Assert.True(latent.Hidden);
        Assert.True(latent.Suspended);
        Assert.True(latent.Untargetable);
        Assert.False(latent.AllocationPendingStatistics);
        NativeBattleStatusSnapshot status = source.Combatants.Single(actor => actor.NativeParticipant!.ActorIndex == 4).NativeStatus ?? throw new InvalidDataException("The original latent recipient requires its loaded status owner.");
        NativeBattleActionPacketsState packets = source.NativeActionPackets!;
        Assert.Equal((byte)1, packets.ActionCount);
        Assert.Equal((byte)1, packets.ContactCount);
        NativeBattleActionHeaderSlot header = packets.Headers[0];
        Assert.Equal((byte)3, header.ReadByte(0));
        Assert.Equal((byte)8, header.ReadByte(1));
        Assert.Equal((byte)13, header.ReadByte(2));
        Assert.Equal((int?)0, header.FirstContactIndex);
        byte reaction = Assert.Single(assets.NativeActions, action => action.Command == 8 && action.ActionId == 258 && action.MagicId == 303).DamagePresentation;
        NativeBattleContactPacket contact = new(new(4, reaction, 1, 0, status.Saved, 7, status.Battle), new(255, 0, 0, 0, 0, 0, 0));
        packets = packets with { Contacts = packets.Contacts.SetItem(0, contact) };
        packets.Validate();
        source = source with { NativeActionPackets = packets };
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(source))!).Capture()));
        File.WriteAllText(Path.Combine(output, "original-loaded-pair.json"), JsonSerializer.Serialize(new { Scenario = "Original formation363 pair after actual loading; declared first physical recipient4 remains latent until selected effect activation; logical HP/status retained", Loaded = loaded, Latent = latent, Header = header, Contact = contact }));
        return source;
    }
}
