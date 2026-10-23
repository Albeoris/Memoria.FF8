using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleSnipeLaser302PresentationTests
{
    [Fact]
    public void Original_SnipeLaser302_preserves_current_bones_programmes_particles_and_cold_boundaries()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([302], "MEMORIA_FF8_SNIPELASER302", SelectOriginalFixture, PrepareDeclaredSource);
    }

    private static NativeEffectFixtureRequest SelectOriginalFixture(BattleAssets assets, ushort effect)
    {
        Assert.Equal((ushort)302, effect);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 155 && row.MagicId == effect && row.KernelSection == 3 && row.KernelIndex == 155);
        Assert.Null(action.TargetFlags);
        BattleEncounter formation = assets.LoadEncounter(155);
        Assert.Equal(40, formation.StageId);
        BattleEnemyPlacement enemy = Assert.Single(NativeBattleEffectFixtureExporter.SelectEnemies(formation, true));
        Assert.Equal(0, enemy.Slot);
        Assert.Equal(8, enemy.ModelId);
        Assert.Equal(255, enemy.Level);
        Assert.Equal(new NativeBattleVector(0, 0, -700), enemy.NativePosition);
        Assert.False(enemy.Hidden);
        Assert.False(enemy.InitiallySuspended);
        Assert.False(enemy.Untargetable);
        Assert.Equal((byte)13, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(enemy.ModelId, enemy.Level), action));
        return new("snipe-laser302", effect, 8, 155, 0, 1200, 240) { EncounterId = formation.Id, CasterActorIndex = 3, PreserveOriginalFormationEnemies = true, ControlledContactValue = 7 };
    }

    private static BattleSnapshot PrepareDeclaredSource(GameAssets game, BattleAssets assets, NativeEffectFixtureRequest request)
    {
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_SNIPELASER302_EVIDENCE") ?? throw new InvalidDataException("Select an evidence directory for the original Snipe Laser component.");
        NativeEffectFixtureExportResult exported = NativeBattleEffectFixtureExporter.Export(request, Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? throw new InvalidDataException("Select installed original resources."), Path.Combine(output, "fixture"));
        Assert.True(exported.Status != "blocked", exported.FirstBlocker);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, "fixture", "queued-action104.battle.json"));
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([302])[301];
        Assert.Equal(NativeBattleSnipeLaser302Profile.Entry, binding.Entry);
        Assert.Equal(NativeBattleSnipeLaser302Profile.Initializer, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag276-301.tim", NativeBattleSnipeLaser302Profile.InitializerGlobal), binding.InitializerResource);
        Assert.Equal(NativeBattleSnipeLaser302Profile.TextureLength, binding.InitializerAsset!.Data.Length);
        Assert.Same(game.ReadNativeSnipeLaser302Resources(), binding.SnipeLaser302Resources);
        source = source with { Definition = source.Definition with { NativeEffectCatalog = source.Definition.NativeEffectCatalog.SetItem(301, binding) } };
        BattleCombatantDefinition definition = source.Definition.Combatants.Single(actor => actor.Id == source.NativeGraphics!.ActorIds[3]);
        NativeBattleGraphicsResource resource = assets.ReadNativeGraphicsResource(definition);
        NativeBattleGraphicsModel model = source.NativeGraphics!.Models[3];
        Assert.Equal(8, definition.ModelId);
        Assert.Equal(0, definition.SceneSlot);
        Assert.NotNull(resource.PoseSource);
        Assert.True(resource.PoseSource.BoneCount > 11);
        Assert.Equal(resource.PoseSource.BoneCount, NativeBattleBodyBoneMatrices.Require(model, resource.PoseSource).Length);
        Assert.NotNull(model.GeometryWorldMatrix);
        Assert.NotNull(model.Rotation14);
        Assert.NotNull(model.ActiveResourceRow100);
        Assert.Null(model.ActiveStagedResourceRow100);
        BattleCombatantDefinition recipientDefinition = BattleSession.Restore(source).GetCombatantDefinition(source.NativeGraphics.ActorIds[0] ?? throw new InvalidDataException("The original recipient identity is missing."));
        Assert.Equal(0, recipientDefinition.ModelId);
        NativeBattlePoseSource recipientPose = assets.ReadNativeGraphicsResource(recipientDefinition).PoseSource ?? throw new InvalidDataException("The original recipient requires its current skeleton.");
        Assert.Equal(23, NativeBattleBodyBoneMatrices.Require(source.NativeGraphics.Models[0], recipientPose).Length);
        Assert.Equal(22, Enumerable.Range(0, recipientPose.BoneCount).Count(bone => System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(recipientPose.Skeleton.AsSpan().Slice(16 + bone * 48)) >= 0));
        NativeBattleActionPacketsState packets = source.NativeActionPackets!;
        NativeBattleActionHeaderSlot header = packets.Headers[0];
        Assert.Equal((byte)1, packets.ActionCount);
        Assert.Equal((byte)1, packets.ContactCount);
        Assert.Equal((byte)3, header.ReadByte(0));
        Assert.Equal((byte)8, header.ReadByte(1));
        Assert.Equal((byte)13, header.ReadByte(2));
        Assert.Equal((byte)1, header.ReadByte(16));
        Assert.Equal((byte)0, header.ReadByte(17));
        Assert.Equal((int?)0, header.FirstContactIndex);
        NativeBattleContactPacket contact = packets.ReadContact(0);
        Assert.Equal((byte)0, contact.Primary.ActorIndex);
        Assert.Equal((ushort)7, contact.Primary.Amount6);
        NativeBattleStatusSnapshot status = source.Combatants.Single(actor => actor.NativeParticipant!.ActorIndex == 0).NativeStatus!;
        Assert.Equal(status.Saved, contact.Primary.SavedStatuses4);
        Assert.Equal(status.Battle, contact.Primary.BattleStatuses8);
        File.WriteAllText(Path.Combine(output, "controlled-contact.json"), JsonSerializer.Serialize(new { Scenario = "Original SnipeLaser302 command8/action155/sequence13 formation155; one declared hostile party0 contact with logical HP/status retained; no ordinary formula, targeting, AI or all-target admission", Caster = new { definition.Id, definition.ModelId, definition.SceneSlot, ResourceRow100 = NativeBattleModelResourceRow.Address100(model), Bones = model.CurrentBodyBoneMatrices, World = model.GeometryWorldMatrix }, Header = header, Contact = contact }));
        return source;
    }
}
