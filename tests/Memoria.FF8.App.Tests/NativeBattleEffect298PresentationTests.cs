using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect298PresentationTests
{
    [Fact]
    public void Original_Effect298_preserves_current_pose_script_owners_and_cold_boundaries()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([298], "MEMORIA_FF8_EFFECT298", SelectOriginalFixture, PrepareDeclaredSource);
    }

    private static NativeEffectFixtureRequest SelectOriginalFixture(BattleAssets assets, ushort effect)
    {
        Assert.Equal((ushort)298, effect);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 119 && row.MagicId == effect && row.KernelSection == 3 && row.KernelIndex == 119);
        Assert.Null(action.TargetFlags);
        BattleEncounter formation = assets.LoadEncounter(194);
        Assert.Equal(52, formation.StageId);
        BattleEnemyPlacement enemy = Assert.Single(NativeBattleEffectFixtureExporter.SelectEnemies(formation, true));
        Assert.Equal(0, enemy.Slot);
        Assert.Equal(102, enemy.ModelId);
        Assert.Equal(128, enemy.Level);
        Assert.Equal(new NativeBattleVector(100, 0, -3700), enemy.NativePosition);
        Assert.False(enemy.Hidden);
        Assert.False(enemy.InitiallySuspended);
        Assert.False(enemy.Untargetable);
        Assert.Equal((byte)14, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(enemy.ModelId, enemy.Level), action));
        return new("effect298", effect, 8, 119, 0, 1200, 240) { EncounterId = formation.Id, CasterActorIndex = 3, PreserveOriginalFormationEnemies = true, ControlledContactValue = 7 };
    }

    private static BattleSnapshot PrepareDeclaredSource(GameAssets game, BattleAssets assets, NativeEffectFixtureRequest request)
    {
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT298_EVIDENCE") ?? throw new InvalidDataException("Select an evidence directory for the original effect298 component.");
        NativeEffectFixtureExportResult exported = NativeBattleEffectFixtureExporter.Export(request, Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? throw new InvalidDataException("Select installed original resources."), Path.Combine(output, "fixture"));
        Assert.True(exported.Status != "blocked", exported.FirstBlocker);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, "fixture", "queued-action104.battle.json"));
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([298])[297];
        Assert.Equal(NativeBattleEffect298Profile.Entry, binding.Entry);
        Assert.Equal(NativeBattleEffect298Profile.Initializer, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag297.tim", NativeBattleEffect298Profile.InitializerGlobal), binding.InitializerResource);
        Assert.Equal(NativeBattleEffect298Profile.TextureLength, binding.InitializerAsset!.Data.Length);
        Assert.Same(game.ReadNativeEffect298Resources(), binding.Effect298Resources);
        source = source with { Definition = source.Definition with { NativeEffectCatalog = source.Definition.NativeEffectCatalog.SetItem(297, binding) } };
        BattleCombatantDefinition definition = source.Definition.Combatants.Single(actor => actor.Id == source.NativeGraphics!.ActorIds[3]);
        NativeBattleGraphicsResource resource = assets.ReadNativeGraphicsResource(definition);
        NativeBattleGraphicsModel model = source.NativeGraphics!.Models[3];
        Assert.Equal(102, definition.ModelId);
        Assert.Equal(0, definition.SceneSlot);
        Assert.NotNull(resource.PoseSource);
        Assert.Equal(45, resource.PoseSource.BoneCount);
        Assert.Equal((byte)118, model.ModelId4);
        Assert.Equal("3E2A29DF495089D7D3221B84E7F06721869B08DAB3405D90A4D310A59655BBDC", Convert.ToHexString(SHA256.HashData(resource.PoseSource.Skeleton.AsSpan())));
        Assert.Equal(Convert.ToHexString(SHA256.HashData(resource.PoseSource.Skeleton.AsSpan())), model.CurrentBodyBoneMatrices!.SkeletonSha256);
        Assert.Equal(resource.PoseSource.BoneCount, NativeBattleBodyBoneMatrices.Require(model, resource.PoseSource).Length);
        NativeBattleDecodedPose decoded = NativeBattlePoseMatrices.ReadDecodedPose(model.SequenceState ?? throw new InvalidDataException("effect298 requires its actual decoded caster pose."), resource.PoseSource);
        Assert.Equal(JsonSerializer.Serialize(NativeBattlePoseMatrices.StageBoneMatrices(resource.PoseSource, decoded)), JsonSerializer.Serialize(NativeBattleBodyBoneMatrices.Require(model, resource.PoseSource)));
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(source))!).Capture()));
        Assert.NotNull(model.GeometryWorldMatrix);
        Assert.NotNull(model.Rotation14);
        Assert.NotNull(model.ActiveResourceRow100);
        foreach (int bone in Enumerable.Range(0, resource.PoseSource.BoneCount))
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
        File.WriteAllText(Path.Combine(output, "controlled-contact.json"), JsonSerializer.Serialize(new { Scenario = "Original Effect298 command8/action119/sequence14 formation194; one declared hostile party0 contact with logical HP/status retained; no ordinary formula, targeting, AI or all-target admission", Caster = new { definition.Id, definition.ModelId, definition.SceneSlot, SkeletonSha256 = Convert.ToHexString(SHA256.HashData(resource.PoseSource.Skeleton.AsSpan())), ResourceRow100 = NativeBattleModelResourceRow.Address100(model), Bones = model.CurrentBodyBoneMatrices, World = model.GeometryWorldMatrix }, Header = header, Contact = contact }));
        return source;
    }
}
