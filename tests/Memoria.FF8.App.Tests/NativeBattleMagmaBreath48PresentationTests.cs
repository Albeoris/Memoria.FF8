using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleMagmaBreath48PresentationTests
{
    [Fact]
    public void Original_MagmaBreath48_preserves_both_original_formation_actors_contacts_and_cold_boundaries()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([48], "MEMORIA_FF8_MAGMABREATH48", SelectOriginalFixture, PrepareDeclaredSource);
    }

    private static NativeEffectFixtureRequest SelectOriginalFixture(BattleAssets assets, ushort effect)
    {
        Assert.Equal((ushort)48, effect);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 120 && row.MagicId == effect && row.KernelSection == 3 && row.KernelIndex == 120);
        Assert.Null(action.TargetFlags);
        BattleEncounter formation = assets.LoadEncounter(147);
        Assert.Equal(100, formation.StageId);
        BattleEnemyPlacement[] enemies = NativeBattleEffectFixtureExporter.SelectEnemies(formation, true);
        Assert.Equal(2, enemies.Length);
        Assert.Equal(new[] { 0, 1 }, enemies.Select(enemy => enemy.Slot).ToArray());
        Assert.Equal(new[] { new NativeBattleVector(-1600, 0, -3300), new NativeBattleVector(1100, 0, -1200) }, enemies.Select(enemy => enemy.NativePosition ?? throw new InvalidDataException("The original formation enemy lacks its native position.")).ToArray());
        foreach (BattleEnemyPlacement enemy in enemies)
        {
            Assert.Equal(93, enemy.ModelId);
            Assert.Equal(119, enemy.Level);
            Assert.False(enemy.Hidden);
            Assert.False(enemy.InitiallySuspended);
            Assert.False(enemy.Untargetable);
            Assert.Equal((byte)12, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(enemy.ModelId, enemy.Level), action));
        }
        return new("magma-breath48", effect, 8, 120, 0, 1200, 240) { EncounterId = formation.Id, CasterActorIndex = 3, PreserveOriginalFormationEnemies = true, ControlledContactValue = 7 };
    }

    private static BattleSnapshot PrepareDeclaredSource(GameAssets game, BattleAssets assets, NativeEffectFixtureRequest request)
    {
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_MAGMABREATH48_EVIDENCE") ?? throw new InvalidDataException("Select an evidence directory for the original Magma Breath component.");
        NativeEffectFixtureExportResult exported = NativeBattleEffectFixtureExporter.Export(request, Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? throw new InvalidDataException("Select installed original resources."), Path.Combine(output, "fixture"));
        Assert.True(exported.Status != "blocked", exported.FirstBlocker);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, "fixture", "queued-action104.battle.json"));
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([48])[47];
        Assert.Equal(NativeBattleMagmaBreath48Profile.Entry, binding.Entry);
        Assert.Equal(NativeBattleMagmaBreath48Profile.Initializer, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag047.tim", NativeBattleMagmaBreath48Profile.InitializerGlobal), binding.InitializerResource);
        Assert.Equal(NativeBattleMagmaBreath48Profile.TextureLength, binding.InitializerAsset!.Data.Length);
        Assert.Same(game.ReadNativeMagmaBreath48Resources(), binding.MagmaBreath48Resources);
        List<object> originalActors = [];
        for (int index = 3; index <= 4; index++)
        {
            BattleCombatantDefinition definition = source.Definition.Combatants.Single(actor => actor.Id == source.NativeGraphics!.ActorIds[index]);
            NativeBattleGraphicsResource resource = assets.ReadNativeGraphicsResource(definition);
            NativeBattleGraphicsModel model = source.NativeGraphics!.Models[index];
            Assert.Equal(93, definition.ModelId);
            Assert.Equal(index - 3, definition.SceneSlot);
            Assert.Equal(2, model.Flags0 & 2);
            Assert.Equal(32, resource.PoseSource!.BoneCount);
            Assert.Equal(32, NativeBattleBodyBoneMatrices.Require(model, resource.PoseSource).Length);
            Assert.NotNull(model.GeometryWorldMatrix);
            Assert.NotNull(model.Rotation14);
            Assert.NotNull(model.ActiveResourceRow100);
            Assert.Null(model.ActiveStagedResourceRow100);
            NativeBattleRawMesh mesh = Assert.Single(resource.Geometry!.Value);
            Assert.Equal(679, mesh.Vertices.Length);
            Assert.Equal(1082, mesh.Polygons.Count(face => !face.Quad));
            Assert.Equal(86, mesh.Polygons.Count(face => face.Quad));
            originalActors.Add(new { Actor = index, Definition = definition.Id, definition.ModelId, definition.SceneSlot, model.Flags0, model.Rotation14, ResourceRow100 = NativeBattleModelResourceRow.Address100(model), Bones = model.CurrentBodyBoneMatrices, World = model.GeometryWorldMatrix });
        }
        Assert.NotEqual(source.NativeGraphics!.ActorIds[3], source.NativeGraphics.ActorIds[4]);
        NativeBattleActionPacketsState packets = source.NativeActionPackets!;
        NativeBattleActionHeaderSlot header = packets.Headers[0];
        Assert.Equal((byte)1, packets.ActionCount);
        Assert.Equal((byte)1, packets.ContactCount);
        Assert.Equal((byte)3, header.ReadByte(0));
        Assert.Equal((byte)8, header.ReadByte(1));
        Assert.Equal((byte)12, header.ReadByte(2));
        Assert.Equal((byte)1, header.ReadByte(16));
        Assert.Equal((byte)0, header.ReadByte(17));
        Assert.Equal((int?)0, header.FirstContactIndex);
        NativeBattleContactPacket contact = packets.ReadContact(0);
        Assert.Equal((byte)0, contact.Primary.ActorIndex);
        Assert.Equal((ushort)7, contact.Primary.Amount6);
        NativeBattleStatusSnapshot status = source.Combatants.Single(actor => actor.NativeParticipant!.ActorIndex == 0).NativeStatus!;
        Assert.Equal(status.Saved, contact.Primary.SavedStatuses4);
        Assert.Equal(status.Battle, contact.Primary.BattleStatuses8);
        File.WriteAllText(Path.Combine(output, "controlled-contact.json"), JsonSerializer.Serialize(new { Scenario = "Original MagmaBreath48 command8/action120/sequence12 formation147; two distinct actual physical formation actors3/4 and one declared hostile party0 contact, retained cold group byte17 zero; no ordinary formula, targeting, AI or all-target admission", ProgrammeActors = originalActors, Header = header, Contact = contact }));
        return source;
    }
}
