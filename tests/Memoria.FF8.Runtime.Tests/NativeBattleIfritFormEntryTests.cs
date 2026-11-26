using static Memoria.FF8.Tests.NativeBattleTypedFixture;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleIfritFormEntryTests
{
    [Fact]
    public void Presentation_entry_uses_owned_self_positioning_without_a_combat_action()
    {
        var fixture = NativeBattleIfritEntryTests.Fixture();
        BattleModelOwner caster = fixture.Inputs.Selection.Caster;
        NativeBattleWaterInitializationInputs inputs = fixture.Inputs with { Selection = fixture.Inputs.Selection with { FirstHeaderFirstActor = caster, Contacts = [caster], Header = null, OriginalHeaderAddress = null }, FirstHeaderCount = 1 };
        NativeBattleScriptedEffectMemory core = fixture.Memory.ScriptedEffects! with { PresentationActors = NativeBattleCinematicActorContext.FromGraphics(inputs.Selection.Models) };
        NativeBattleEffectMemory memory = fixture.Memory with { ScriptedEffects = core };
        NativeBattleOriginalEffectState effect = fixture.Effect with { Parameters = null };
        NativeBattleCinematicEntryStep entry = NativeBattleCinematicEntry.InitializeIfritPresentation(memory, effect, fixture.Directories, inputs);
        Assert.Null(entry.Effect.Parameters);
        Assert.Null(entry.CurrentHeader);
        Assert.Equal(caster, Assert.Single(entry.Memory.ScriptedEffects!.PresentationActors!.CurrentActors));
        Assert.Equal(201, entry.Memory.Cinematic!.Effect);
        Assert.Equal(0xB25DF0u, Assert.Single(Assert.Single(entry.Effect.Pools).Slots).Callback);
        Assert.Throws<InvalidDataException>(() => NativeBattleCinematicEntry.Initialize(NativeBattleCinematicProfile.ForEffect(201), fixture.Memory, effect, fixture.Directories, inputs));
        NativeBattleCinematicEntryStep restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleCinematicEntryStep>(NativeBattleTestSnapshots.Json(entry))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(entry), NativeBattleTestSnapshots.Json(restored));
    }

    [Fact]
    public void Presentation_entry_rejects_offensive_or_foreign_positioning()
    {
        var fixture = NativeBattleIfritEntryTests.Fixture();
        NativeBattleWaterInitializationInputs inputs = fixture.Inputs with { Selection = fixture.Inputs.Selection with { Caster = OriginalOwner(0), FirstHeaderFirstActor = OriginalOwner(0), Contacts = [OriginalOwner(0)], Header = null, OriginalHeaderAddress = null }, FirstHeaderCount = 1 };
        Assert.Throws<InvalidDataException>(() => NativeBattleCinematicEntry.InitializeIfritPresentation(fixture.Memory, fixture.Effect, fixture.Directories, inputs));
        Assert.Throws<InvalidDataException>(() => NativeBattleCinematicEntry.InitializeIfritPresentation(fixture.Memory, fixture.Effect with { Parameters = null }, fixture.Directories, inputs with { Selection = inputs.Selection with { Contacts = [OriginalOwner(3)] } }));
    }
}
