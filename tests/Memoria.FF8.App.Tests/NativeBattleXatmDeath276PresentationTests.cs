using static Memoria.FF8.App.Tests.NativeBattleOriginalModelAssertions;
using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleXatmDeath276PresentationTests
{
    [Fact]
    public void Selected_original_Xatm276_component_preserves_its_real_model_header_and_completes_cold_boundaries()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_SELECTED_EFFECT_SNAPSHOT");
        Assert.SkipUnless(directory is not null && path is not null, "Select installed original resources and a queued original X-ATM092 component.");
        Assert.Equal("276", Environment.GetEnvironmentVariable("MEMORIA_FF8_SELECTED_EFFECT_ID"));
        Assert.Equal("0", Environment.GetEnvironmentVariable("MEMORIA_FF8_SELECTED_EFFECT_EXPECTED_CONTACT_TRANSITIONS"));
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_SELECTED_EFFECT_EVIDENCE") ?? throw new InvalidDataException("Select a fresh evidence directory for the original276 component.");
        using BattleAssets assets = new(directory!);
        BattleSnapshot source = NativeBattleFixtureInspection.ReadLogicalSnapshot(path!);
        Assert.Equal(9, source.Definition.EncounterId);
        Assert.Equal(6, source.Definition.StageId);
        BattleEncounter formation = assets.LoadEncounter(9);
        BattleEnemyPlacement placement = Assert.Single(formation.Slots, slot => slot.Slot == 0);
        Assert.True(placement.Enabled);
        Assert.Equal(92, placement.ModelId);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 145 && row.MagicId == 276);
        Assert.Equal((byte)19, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(92, placement.Level), action));
        NativeBattleGraphicsState graphics = source.ReadFixturePresentation().NativeGraphics!;
        Assert.Null(graphics.OriginalEffect);
        int descriptor = Assert.Single(graphics.Descriptors.Queue, index => graphics.Descriptors.Packets[index].Command == 104);
        uint argument = graphics.Descriptors.Packets[descriptor].Argument;
        Assert.Equal(0u, (argument - 0x1D280C4u) % 20);
        int headerIndex = checked((int)((argument - 0x1D280C4u) / 20));
        NativeBattleActionHeaderSlot header = source.ReadFixturePresentation().NativeActionPackets!.ReadHeader(new BattleActionHeaderReference(headerIndex));
        Assert.Equal((byte)3, ReadOriginalModelOperand(source.ReadFixturePresentation().NativeActionPackets!.ModelAliases, header.Source));
        Assert.Equal((byte)8, header.Command);
        Assert.Equal((byte)19, header.Sequence);
        Assert.Equal((ushort)145, header.Action);
        Assert.Equal((ushort)276, header.Effect);
        Assert.NotNull(source.ReadFixturePresentation().NativeActionPackets!.ExportRetained().Headers[headerIndex].FirstContactIndex);
        Assert.Equal((byte)1, header.ReadContactCount());
        Assert.Equal((byte)0, header.ReadRepeatModeMinusOne());
        NativeBattleContactPacket contact = source.ReadFixturePresentation().NativeActionPackets!.ReadContact(header.FirstContact);
        Assert.Equal((byte)0, ReadOriginalModelOperand(source.ReadFixturePresentation().NativeActionPackets!.ModelAliases, contact.Primary.Recipient));
        Assert.Equal((ushort)0, contact.Primary.Amount6);
        Assert.Equal((byte)0, contact.Primary.HitFlags);
        Assert.Null(contact.Primary.CoverSource);
        Assert.Null(contact.Secondary);
        BattleCombatantDefinition caster = source.Definition.Combatants.Single(actor => actor.Id == ReadOriginalModelActorId(graphics, 3));
        Assert.Equal(92, caster.ModelId);
        Assert.Equal(0, caster.SceneSlot);
        NativeBattleGraphicsResource resource = assets.ReadNativeGraphicsResource(caster);
        NativeBattlePoseSource pose = resource.PoseSource ?? throw new InvalidDataException("The strict original276 component requires its current original skeleton.");
        Assert.Equal((byte)108, ReadOriginalModelCell(graphics, 3).ModelId4);
        Assert.Equal(pose.BoneCount, NativeBattleBodyBoneMatrices.Require(ReadOriginalModelCell(graphics, 3), pose).Length);
        Assert.NotNull(ReadOriginalModelCell(graphics, 3).GeometryWorldMatrix);
        Assert.NotNull(ReadOriginalModelCell(graphics, 3).GeometryBounds52);
        Assert.NotNull(ReadOriginalModelCell(graphics, 3).ActiveResourceRow100);
        Assert.NotNull(graphics.UiServices);
        int priorDamageEvents = BattleSession.Restore(source).Events.Count(entry => entry.Kind == "Damage");
        new NativeBattleSelectedEffectPresentationTests().Selected_queued_component_completes_and_restores_its_live_owners();
        BattleSnapshot completed = NativeBattleFixtureInspection.ReadLogicalSnapshot(Path.Combine(output, "completed.battle.json"));
        Assert.Equal(priorDamageEvents, BattleSession.Restore(completed).Events.Count(entry => entry.Kind == "Damage"));
    }
}
