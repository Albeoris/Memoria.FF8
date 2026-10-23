using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleMegaSpark279PresentationTests
{
    [Fact]
    public void Original_MegaSpark279_preserves_current_bones_rows_attachments_and_cold_boundaries()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([279], "MEMORIA_FF8_MEGASPARK279", SelectOriginalFixture, PrepareDeclaredSource);
    }

    private static NativeEffectFixtureRequest SelectOriginalFixture(BattleAssets assets, ushort effect)
    {
        Assert.Equal((ushort)279, effect);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 106 && row.MagicId == effect && row.KernelSection == 3 && row.KernelIndex == 106);
        Assert.Null(action.TargetFlags);
        BattleEncounter formation = assets.LoadEncounter(377);
        Assert.Equal(127, formation.StageId);
        BattleEnemyPlacement enemy = Assert.Single(NativeBattleEffectFixtureExporter.SelectEnemies(formation, true));
        Assert.Equal(0, enemy.Slot);
        Assert.Equal(30, enemy.ModelId);
        Assert.Equal(151, enemy.Level);
        Assert.Equal(new NativeBattleVector(100, 0, -4600), enemy.NativePosition);
        Assert.False(enemy.Hidden);
        Assert.False(enemy.InitiallySuspended);
        Assert.False(enemy.Untargetable);
        Assert.Equal((byte)14, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(enemy.ModelId, enemy.Level), action));
        return new("mega-spark279", effect, 8, 106, 0, 1200, 240) { EncounterId = formation.Id, CasterActorIndex = 3, PreserveOriginalFormationEnemies = true, ControlledContactValue = 7 };
    }

    private static BattleSnapshot PrepareDeclaredSource(GameAssets game, BattleAssets assets, NativeEffectFixtureRequest request)
    {
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_MEGASPARK279_EVIDENCE") ?? throw new InvalidDataException("Select an evidence directory for the original Mega Spark component.");
        NativeEffectFixtureExportResult exported = NativeBattleEffectFixtureExporter.Export(request, Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? throw new InvalidDataException("Select installed original resources."), Path.Combine(output, "fixture"));
        Assert.True(exported.Status != "blocked", exported.FirstBlocker);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, "fixture", "queued-action104.battle.json"));
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([279])[278];
        Assert.Equal(NativeBattleMegaSpark279Profile.Entry, binding.Entry);
        Assert.Equal(NativeBattleMegaSpark279Profile.Initializer, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag278.tim", NativeBattleMegaSpark279Profile.InitializerGlobal), binding.InitializerResource);
        Assert.Equal(NativeBattleMegaSpark279Profile.TextureLength, binding.InitializerAsset!.Data.Length);
        Assert.Same(game.ReadNativeMegaSpark279Resources(), binding.MegaSpark279Resources);
        source = source with { Definition = source.Definition with { NativeEffectCatalog = source.Definition.NativeEffectCatalog.SetItem(278, binding) } };
        BattleCombatantDefinition definition = source.Definition.Combatants.Single(actor => actor.Id == source.NativeGraphics!.ActorIds[3]);
        NativeBattleGraphicsResource resource = assets.ReadNativeGraphicsResource(definition);
        NativeBattleGraphicsModel model = source.NativeGraphics!.Models[3];
        Assert.Equal(30, definition.ModelId);
        Assert.Equal(0, definition.SceneSlot);
        Assert.NotNull(resource.PoseSource);
        Assert.Equal(46, resource.PoseSource.BoneCount);
        Assert.Equal((byte)46, model.ModelId4);
        Assert.Equal("AFA1BB80548AD6F7BF6F8193D603FFC3C1E1F7899834644F36BB12FCCC4A0DBA", Convert.ToHexString(SHA256.HashData(resource.PoseSource.Skeleton.AsSpan())));
        Assert.Equal(resource.PoseSource.BoneCount, NativeBattleBodyBoneMatrices.Require(model, resource.PoseSource).Length);
        NativeBattleDecodedPose decoded = NativeBattlePoseMatrices.ReadDecodedPose(model.SequenceState ?? throw new InvalidDataException("Mega Spark requires its actual decoded caster pose."), resource.PoseSource);
        Assert.Equal(JsonSerializer.Serialize(NativeBattlePoseMatrices.StageBoneMatrices(resource.PoseSource, decoded)), JsonSerializer.Serialize(NativeBattleBodyBoneMatrices.Require(model, resource.PoseSource)));
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(source))!).Capture()));
        Assert.NotNull(model.GeometryWorldMatrix);
        Assert.NotNull(model.Rotation14);
        Assert.NotNull(model.ActiveResourceRow100);
        foreach (int bone in new[] { 10, 17, 18, 21 })
        {
            NativeBattleActionMatrix current = NativeBattleBodyBoneMatrices.Require(model, resource.PoseSource)[bone];
            current.Validate();
        }
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
        Assert.Equal((byte)14, header.ReadByte(2));
        Assert.Equal((byte)1, header.ReadByte(16));
        Assert.Equal((byte)0, header.ReadByte(17));
        Assert.Equal((int?)0, header.FirstContactIndex);
        NativeBattleContactPacket contact = packets.ReadContact(0);
        Assert.Equal((byte)0, contact.Primary.ActorIndex);
        Assert.Equal((ushort)7, contact.Primary.Amount6);
        NativeBattleStatusSnapshot status = source.Combatants.Single(actor => actor.NativeParticipant!.ActorIndex == 0).NativeStatus!;
        Assert.Equal(status.Saved, contact.Primary.SavedStatuses4);
        Assert.Equal(status.Battle, contact.Primary.BattleStatuses8);
        File.WriteAllText(Path.Combine(output, "controlled-contact.json"), JsonSerializer.Serialize(new { Scenario = "Original MegaSpark279 command8/action106/sequence14 formation377; one declared hostile party0 contact with logical HP/status retained; no ordinary formula, targeting, AI or all-target admission", Caster = new { definition.Id, definition.ModelId, definition.SceneSlot, ResourceRow100 = NativeBattleModelResourceRow.Address100(model), Bones = model.CurrentBodyBoneMatrices, World = model.GeometryWorldMatrix }, Header = header, Contact = contact }));
        return source;
    }
}
