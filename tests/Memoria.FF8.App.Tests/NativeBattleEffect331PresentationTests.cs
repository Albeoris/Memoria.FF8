using static Memoria.FF8.App.Tests.NativeBattleOriginalModelAssertions;
using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect331PresentationTests
{
    [Fact]
    public void Original_Effect331_selected_component_preserves_actual_model_header_and_complete_cold_boundaries()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_SELECTED_EFFECT_SNAPSHOT");
        Assert.SkipUnless(directory is not null && path is not null, "Select installed original resources and the queued original331 component.");
        Assert.Equal("331", Environment.GetEnvironmentVariable("MEMORIA_FF8_SELECTED_EFFECT_ID"));
        Assert.NotNull(Environment.GetEnvironmentVariable("MEMORIA_FF8_SELECTED_EFFECT_EXPECTED_CONTACT_TRANSITIONS"));
        using BattleAssets assets = new(directory!);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(path!);
        Assert.Equal(749, source.Definition.EncounterId);
        Assert.Equal(111, source.Definition.StageId);
        BattleCombatantDefinition caster = source.Definition.Combatants.Single(actor => actor.Id == ReadOriginalModelActorId(source.ReadFixturePresentation().NativeGraphics!, 3));
        Assert.Equal(118, caster.ModelId);
        Assert.Equal(0, caster.SceneSlot);
        NativeBattleGraphicsResource resource = assets.ReadNativeGraphicsResource(caster);
        Assert.Equal(resource.PoseSource!.BoneCount, NativeBattleBodyBoneMatrices.Require(ReadOriginalModelCell(source.ReadFixturePresentation().NativeGraphics!, 3), resource.PoseSource).Length);
        Assert.NotNull(ReadOriginalModelCell(source.ReadFixturePresentation().NativeGraphics!, 3).GeometryWorldMatrix);
        Assert.NotNull(ReadOriginalModelCell(source.ReadFixturePresentation().NativeGraphics!, 3).ActiveResourceRow100);
        Assert.NotNull(Assert.IsType<NativeBattleGraphicsState>(source.ReadFixturePresentation().NativeGraphics).UiServices);
        Assert.Null(source.ReadFixturePresentation().NativeGraphics!.OriginalEffect);
        int descriptor = Assert.Single(source.ReadFixturePresentation().NativeGraphics!.Descriptors.Queue, index => source.ReadFixturePresentation().NativeGraphics!.Descriptors.Packets[index].Command == 104);
        uint argument = source.ReadFixturePresentation().NativeGraphics!.Descriptors.Packets[descriptor].Argument;
        Assert.Equal(0u, (argument - 0x1D280C4u) % 20);
        int slot = checked((int)((argument - 0x1D280C4u) / 20));
        NativeBattleActionHeaderSlot header = source.ReadFixturePresentation().NativeActionPackets!.ReadHeader(new BattleActionHeaderReference(slot));
        Assert.Equal((byte)3, ReadOriginalModelOperand(source.ReadFixturePresentation().NativeActionPackets!.ModelAliases, header.Source));
        Assert.Equal((byte)8, header.Command);
        Assert.Equal((byte)13, header.Sequence);
        Assert.Equal((ushort)313, header.Action);
        Assert.Equal((ushort)331, header.Effect);
        Assert.Equal((byte)0, header.ReadRepeatModeMinusOne());
        BattleNativeAction action = Assert.Single(assets.NativeActions, item => item.Command == 8 && item.ActionId == 313 && item.MagicId == 331);
        Assert.Equal((byte)13, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(118, caster.Level), action));
        new NativeBattleSelectedEffectPresentationTests().Selected_queued_component_completes_and_restores_its_live_owners();
    }
}
