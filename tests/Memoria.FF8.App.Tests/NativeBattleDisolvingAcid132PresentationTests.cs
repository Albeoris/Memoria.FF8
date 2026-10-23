using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleDisolvingAcid132PresentationTests
{
    [Fact]
    public void Original_Disolving_Acid_component_presents_original_ACTs_linked_trails_and_native_contact()
    {
        NativeBattleActorDirectorPresentationTests.RunSelected([132], "MEMORIA_FF8_DISOLVING_ACID132", SelectOriginalFixture, PrepareDeclaredSource);
    }

    private static NativeEffectFixtureRequest SelectOriginalFixture(BattleAssets assets, ushort effect)
    {
        Assert.Equal((ushort)132, effect);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 183 && row.MagicId == effect && row.KernelSection == 3 && row.KernelIndex == 183);
        Assert.Equal((byte)7, action.AttackType);
        Assert.Equal((byte)6, action.Power);
        Assert.Equal((ushort)0, action.Statuses0);
        Assert.Equal(0x01000000u, action.Statuses1);
        BattleEncounter formation = assets.LoadEncounter(376);
        Assert.Equal(127, formation.StageId);
        BattleEnemyPlacement caster = Assert.Single(NativeBattleEffectFixtureExporter.SelectEnemies(formation, true));
        Assert.Equal(0, caster.Slot);
        Assert.Equal(48, caster.ModelId);
        Assert.Equal(new NativeBattleVector(-100, 0, -4200), caster.NativePosition);
        Assert.False(caster.Hidden);
        Assert.False(caster.InitiallySuspended);
        Assert.False(caster.Untargetable);
        Assert.Equal((byte)12, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(caster.ModelId, caster.Level), action));
        return new("disolving-acid132", effect, 8, 183, 0, 800, 240) { EncounterId = formation.Id, CasterActorIndex = 3, PreserveOriginalFormationEnemies = true, ControlledContactValue = 7 };
    }

    private static BattleSnapshot PrepareDeclaredSource(GameAssets game, BattleAssets assets, NativeEffectFixtureRequest request)
    {
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_DISOLVING_ACID132_EVIDENCE") ?? throw new InvalidDataException("Select an evidence directory for the original Disolving Acid component.");
        NativeEffectFixtureExportResult exported = NativeBattleEffectFixtureExporter.Export(request, Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? throw new InvalidDataException("Select installed original resources."), Path.Combine(output, "fixture"));
        Assert.True(exported.Status != "blocked", exported.FirstBlocker);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, "fixture", "queued-action104.battle.json"));
        BattleCombatantSnapshot caster = Assert.Single(source.Combatants, actor => actor.NativeParticipant!.ActorIndex == 3);
        BattleCombatantDefinition casterSource = Assert.Single(source.Definition.Combatants, actor => actor.Id == caster.Id);
        Assert.Equal(0, casterSource.SceneSlot);
        Assert.Equal(48, casterSource.ModelId);
        Assert.Equal((byte?)64, source.NativeGraphics!.Models[3].ModelId4);
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([132])[131];
        Assert.Equal(NativeBattleDisolvingAcid132Profile.Entry, binding.Entry);
        Assert.Equal(NativeBattleDisolvingAcid132Profile.Initializer, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag131.tim", NativeBattleDisolvingAcid132Profile.InitializerGlobal), binding.InitializerResource);
        Assert.Equal(150048, binding.InitializerAsset!.Data.Length);
        Assert.Same(game.ReadNativeDisolvingAcid132Resources(), binding.DisolvingAcid132Resources);
        Assert.Equal(0u, source.NativeEffectMemory!.DisolvingAcid132!.ReadDword(NativeBattleDisolvingAcid132Profile.WorkingGlobal));
        NativeBattleContactPacket contact = source.NativeActionPackets!.ReadContact(0);
        Assert.Equal((byte)0, contact.Primary.ActorIndex);
        Assert.Equal((ushort)7, contact.Primary.Amount6);
        NativeBattleStatusSnapshot status = source.Combatants.Single(actor => actor.NativeParticipant!.ActorIndex == request.TargetActorIndex).NativeStatus!;
        Assert.Equal(status.Saved, contact.Primary.SavedStatuses4);
        Assert.Equal(status.Battle, contact.Primary.BattleStatuses8);
        string? selectedSeed = Environment.GetEnvironmentVariable("MEMORIA_FF8_DISOLVING_ACID132_CRT_SEED");
        if (selectedSeed is not null)
        {
            source = source with { NativeCrtRandom = new(uint.Parse(selectedSeed, System.Globalization.CultureInfo.InvariantCulture)) };
            File.WriteAllText(Path.Combine(output, "declared-crt-seed.json"), JsonSerializer.Serialize(new { Seed = source.NativeCrtRandom.Seed, Scope = "Explicit native CRT state for the isolated presentation component; original action and resources retained" }));
        }
        File.WriteAllText(Path.Combine(output, "controlled-contact.json"), JsonSerializer.Serialize(new { Scenario = "Original command8/action183/sequence12 and formation376; declared seven-HP post-formula presentation with logical HP/status retained; no magic damage/status formula, accuracy, ordinary command or AI turn admission", Contact = contact }));
        return source;
    }
}
