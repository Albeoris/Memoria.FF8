using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect251Profile;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect251SourceAdmissionTests
{
    [Fact]
    public void Fresh_source_preserves_suspended_companion_and_resolves_both_current_rigs_without_advancing()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT251_SOURCE_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT251_ADMISSION_EVIDENCE");
        Assert.SkipUnless(directory is not null && path is not null && output is not null, "Select original resources, a fresh component source and a new admission evidence directory.");
        Assert.False(Directory.Exists(output));
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(path!);
        string unchanged = JsonSerializer.Serialize(source);
        BattleSession session = BattleSession.Restore(source);
        Assert.True(session.NativeEntryReady);
        Assert.Equal(0, source.Frame);
        Assert.Equal(119, source.Definition.EncounterId);
        Assert.Equal(33, source.Definition.StageId);
        NativeBattleGraphicsState graphics = source.NativeGraphics!;
        Assert.Null(graphics.OriginalEffect);
        Assert.Equal(0, source.NativeEffectMemory!.Active);
        NativeBattleEffect251State state = source.NativeEffectMemory.Effect251!;
        Assert.NotNull(state);
        Assert.False(state.ScriptSourcesBound);
        Assert.Empty(state.ScriptStorage);
        Assert.Null(state.CallbackStackWord58);
        Assert.Single(state.FixedBanks);
        Assert.Equal(5, state.CommonQueueStorage.Count);
        Assert.Equal(new byte[RootBankLength], state.ReadBank(RootBank).Read(0, RootBankLength));
        Assert.Equal(new byte[4], NativeBattleTemporaryMemory.ReadView(source.NativeFileStaging!, PauseAddress, 4).Read(0, 4));
        List<object> rigs = [];
        for (int index = 0; index < 2; index++)
        {
            int physical = index + 3;
            int original = index == 0 ? 86 : 82;
            int bones = index == 0 ? 41 : 2;
            string actorId = graphics.ActorIds[physical]!;
            BattleCombatantDefinition definition = session.GetCombatantDefinition(actorId);
            BattleCombatantSnapshot actor = Assert.Single(source.Combatants, current => current.Id == actorId);
            Assert.Equal(original, definition.ModelId);
            Assert.Equal(index, definition.SceneSlot);
            Assert.True(actor.NativeParticipant!.Active);
            Assert.False(actor.NativeParticipant.Hidden);
            Assert.Equal(index == 1, actor.NativeParticipant.Suspended);
            Assert.Equal(index == 1, actor.NativeParticipant.Untargetable);
            NativeBattleGraphicsModel model = graphics.Models[physical];
            Assert.Equal((byte)(original + 16), model.ModelId4);
            NativeBattleGraphicsResource resource = NativeBattleEnemyResourceView.Resolve(source.NativeModelCpuResources!, source.NativeFileStaging!, model.ActiveResourceRow100!.Value, assets.ReadNativeGraphicsResource(definition));
            NativeBattlePoseSource pose = resource.PoseSource!;
            Assert.Equal(bones, pose.BoneCount);
            Assert.Equal(bones, NativeBattleBodyBoneMatrices.Require(model, pose).Length);
            model.GeometryWorldMatrix!.Validate();
            if (physical == 4)
            {
                NativeBattleEffect251BoneWorldOutput point = NativeBattleEffect251ModelServices.BoneWorld5FC0(model, pose, 1, NativeBattleEffectArena.Clear().MarkUnknown(0, 12), NativeBattleEffectArena.Clear().MarkUnknown(0, 8));
                Assert.Equal(new[] { false, false }, point.Position8.ReadSnapshot(6, 2).Known);
                _ = point.Position8.Read(0, 6);
                _ = point.Direction12.Read(0, 12);
            }
            rigs.Add(new { Physical = physical, Original = original, Bones = bones, actor.NativeParticipant, model.CurrentBodyBoneMatrices, model.GeometryWorldMatrix });
        }
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 314 && row.MagicId == 251);
        NativeBattleEffect251Resources resources = game.ReadNativeEffect251Resources();
        resources.Validate();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([251])[250];
        Assert.Same(resources, binding.Effect251Resources);
        Assert.Equal(Entry, binding.Entry);
        Assert.Equal(Initializer, binding.Initializer);
        Assert.Null(binding.InitializerResource);
        Assert.Equal(unchanged, JsonSerializer.Serialize(session.Capture()));
        Directory.CreateDirectory(output!);
        using FileStream evidence = new(Path.Combine(output!, "current-input-admission.json"), FileMode.CreateNew);
        JsonSerializer.Serialize(evidence, new { Status = "passed", Scope = "Read-only current rigs and authored companion flags; ordinary AI eligibility and formula excluded", source.Frame, source.Definition.EncounterId, source.Definition.StageId, Rigs = rigs, Action = new { action.Command, action.ActionId, action.MagicId }, SnapshotUnchanged = true });
    }
}
