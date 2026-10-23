using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleSigh46PresentationTests
{
    [Fact]
    public void Original_Sigh46_preserves_current_material_VRAM_contacts_and_cold_boundaries()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([46], "MEMORIA_FF8_SIGH46", SelectOriginalFixture, PrepareDeclaredSource);
    }

    private static NativeEffectFixtureRequest SelectOriginalFixture(BattleAssets assets, ushort effect)
    {
        Assert.Equal((ushort)46, effect);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 85 && row.MagicId == effect && row.KernelSection == 3 && row.KernelIndex == 85);
        Assert.Null(action.TargetFlags);
        BattleEncounter formation = assets.LoadEncounter(213);
        Assert.Equal(54, formation.StageId);
        BattleEnemyPlacement enemy = Assert.Single(NativeBattleEffectFixtureExporter.SelectEnemies(formation, true));
        Assert.Equal(0, enemy.Slot);
        Assert.Equal(54, enemy.ModelId);
        Assert.Equal(255, enemy.Level);
        Assert.Equal(new NativeBattleVector(0, 0, -1800), enemy.NativePosition);
        Assert.False(enemy.Hidden);
        Assert.False(enemy.InitiallySuspended);
        Assert.False(enemy.Untargetable);
        Assert.Equal((byte)13, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(enemy.ModelId, enemy.Level), action));
        return new("sigh46", effect, 8, 85, 0, 1200, 240) { EncounterId = formation.Id, CasterActorIndex = 3, PreserveOriginalFormationEnemies = true, ControlledContactValue = 7 };
    }

    private static BattleSnapshot PrepareDeclaredSource(GameAssets game, BattleAssets assets, NativeEffectFixtureRequest request)
    {
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_SIGH46_EVIDENCE") ?? throw new InvalidDataException("Select an evidence directory for the original Sigh component.");
        NativeEffectFixtureExportResult exported = NativeBattleEffectFixtureExporter.Export(request, Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? throw new InvalidDataException("Select installed original resources."), Path.Combine(output, "fixture"));
        Assert.True(exported.Status != "blocked", exported.FirstBlocker);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, "fixture", "queued-action104.battle.json"));
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([46])[45];
        Assert.Equal(NativeBattleSigh46Profile.Entry, binding.Entry);
        Assert.Equal(NativeBattleSigh46Profile.Initializer, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag045.tim", NativeBattleSigh46Profile.InitializerGlobal), binding.InitializerResource);
        Assert.Equal(NativeBattleSigh46Profile.TextureLength, binding.InitializerAsset!.Data.Length);
        Assert.Same(game.ReadNativeSigh46Resources(), binding.Sigh46Resources);
        BattleCombatantDefinition definition = source.Definition.Combatants.Single(actor => actor.Id == source.NativeGraphics!.ActorIds[3]);
        NativeBattleGraphicsResource resource = assets.ReadNativeGraphicsResource(definition);
        NativeBattleGraphicsModel model = source.NativeGraphics!.Models[3];
        Assert.Equal(54, definition.ModelId);
        Assert.Equal(0, definition.SceneSlot);
        Assert.Equal(23, resource.PoseSource!.BoneCount);
        Assert.Equal(23, NativeBattleBodyBoneMatrices.Require(model, resource.PoseSource).Length);
        Assert.NotNull(model.GeometryWorldMatrix);
        Assert.NotNull(model.Rotation14);
        Assert.NotNull(model.ActiveResourceRow100);
        Assert.Null(model.ActiveStagedResourceRow100);
        NativeBattleRawMesh mesh = Assert.Single(resource.Geometry!.Value);
        Assert.Equal(258, mesh.Vertices.Length);
        Assert.Equal(434, mesh.Polygons.Count(face => !face.Quad));
        Assert.Equal(37, mesh.Polygons.Count(face => face.Quad));
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
        File.WriteAllText(Path.Combine(output, "controlled-contact.json"), JsonSerializer.Serialize(new { Scenario = "Original Sigh46 command8/action85/sequence13 formation213; one declared hostile party0 contact with logical HP/status retained; no ordinary formula, targeting, AI or all-target admission", Caster = new { definition.Id, definition.ModelId, definition.SceneSlot, ResourceRow100 = NativeBattleModelResourceRow.Address100(model), Bones = model.CurrentBodyBoneMatrices, World = model.GeometryWorldMatrix }, Header = header, Contact = contact }));
        return source;
    }
}
