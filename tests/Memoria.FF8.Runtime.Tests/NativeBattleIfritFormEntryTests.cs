using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleIfritFormEntryTests
{
    [Fact]
    public void Presentation_entry_uses_owned_self_positioning_without_a_combat_action()
    {
        var fixture = NativeBattleIfritEntryTests.Fixture();
        NativeBattleWaterInitializationInputs inputs = fixture.Inputs with { Caster = 0, FirstHeaderFirstActor = 0, Contacts = [0], ContactCount = 1, FirstHeaderCount = 1 };
        NativeBattleOriginalEffectState effect = fixture.Effect with { Parameters = null };
        NativeBattleCinematicEntryStep entry = NativeBattleCinematicEntry.InitializeIfritPresentation(fixture.Memory, effect, fixture.Directories, inputs);
        Assert.Null(entry.Effect.Parameters);
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
        NativeBattleWaterInitializationInputs inputs = fixture.Inputs with { Caster = 0, FirstHeaderFirstActor = 0, Contacts = [0], ContactCount = 1, FirstHeaderCount = 1 };
        Assert.Throws<InvalidDataException>(() => NativeBattleCinematicEntry.InitializeIfritPresentation(fixture.Memory, fixture.Effect, fixture.Directories, inputs));
        Assert.Throws<InvalidDataException>(() => NativeBattleCinematicEntry.InitializeIfritPresentation(fixture.Memory, fixture.Effect with { Parameters = null }, fixture.Directories, inputs with { Contacts = [3] }));
    }
}
