using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedEffectRosterTests
{
    [Fact]
    public void Original_effect_keeps_its_native_seven_row_validation_and_json()
    {
        NativeBattleOriginalEffectState effect = Effect(new(6, 0, 0x4080, 0, 0, 0));
        effect.Validate();
        Assert.Throws<InvalidDataException>((effect with { Parameters = effect.Parameters! with { Actor78 = 7 } }).Validate);

        string json = JsonSerializer.Serialize(effect);
        using JsonDocument document = JsonDocument.Parse(json);
        Assert.False(document.RootElement.TryGetProperty(nameof(NativeBattleOriginalEffectState.PresentationRoster), out _));
        NativeBattleOriginalEffectState restored = JsonSerializer.Deserialize<NativeBattleOriginalEffectState>(json)!;
        restored.Validate();
        Assert.Equal(json, JsonSerializer.Serialize(restored));
    }

    [Fact]
    public void Detached_and_inherited_effect_rosters_validate_the_same_bound_parameters()
    {
        NativeBattleGraphicsRoster roster = Roster();
        NativeBattleOriginalEffectState effect = Effect(new(24, 0, 0x8, 0, 0, 0) { PresentationTargets = Rows(3, 7) }) with { PresentationRoster = roster };
        effect.Validate();
        (effect with { Parameters = null }).Validate();
        effect.Validate(Roster());
        string json = JsonSerializer.Serialize(effect);
        NativeBattleOriginalEffectState restored = JsonSerializer.Deserialize<NativeBattleOriginalEffectState>(json)!;
        restored.Validate();
        restored.Validate(roster);
        Assert.Equal(json, JsonSerializer.Serialize(restored));

        NativeBattleOriginalEffectState inherited = effect with { PresentationRoster = null };
        NativeBattleGraphicsState graphics = Graphics(roster) with { OriginalEffect = inherited };
        graphics.Validate();
        Assert.Throws<InvalidDataException>(inherited.Validate);
        string graphicsJson = JsonSerializer.Serialize(graphics);
        NativeBattleGraphicsState restoredGraphics = JsonSerializer.Deserialize<NativeBattleGraphicsState>(graphicsJson)!;
        restoredGraphics.Validate();
        Assert.Null(restoredGraphics.OriginalEffect!.PresentationRoster);
        Assert.Equal(graphicsJson, JsonSerializer.Serialize(restoredGraphics));
    }

    [Fact]
    public void Hosted_effect_rejects_invalid_projections_holes_and_different_roster_rows()
    {
        NativeBattleGraphicsRoster roster = Roster();
        NativeBattleEffectParameters parameters = new(24, 0, 0x8, 0, 0, 0) { PresentationTargets = Rows(3, 7) };
        NativeBattleOriginalEffectState effect = Effect(parameters) with { PresentationRoster = roster };
        Assert.Throws<InvalidDataException>((effect with { Parameters = parameters with { Actor78 = 23 } }).Validate);
        Assert.Throws<InvalidDataException>((effect with { Parameters = parameters with { PresentationTargets = Rows(3, 23) } }).Validate);
        Assert.Throws<InvalidDataException>((effect with { Parameters = parameters with { TargetMask7A = 0 } }).Validate);
        Assert.Throws<InvalidDataException>((effect with { Parameters = parameters with { HeaderIndex7C = 32 } }).Validate);
        Assert.Throws<InvalidDataException>(() => effect.Validate(new(roster.Rows.SetItem(7, new("replacement", true)))));
        Assert.Throws<InvalidDataException>(() => effect.Validate(new(roster.Rows.SetItem(7, new("ally", false)))));
        Assert.Throws<InvalidDataException>((Graphics(new(roster.Rows.SetItem(24, null))) with { OriginalEffect = effect }).Validate);
        Assert.Throws<InvalidDataException>((effect with { FireModelCompletion = Enumerable.Repeat(0, 25).ToImmutableArray() }).Validate);
    }

    [Fact]
    public void Hosted_graphics_validate_pending_contact_and_trigger_sound_ownership()
    {
        NativeBattleGraphicsState graphics = Graphics(Roster()) with { PendingContacts = [Contact(24, 7), Contact(7, 255)], PendingTriggerSoundActorIndex = 24 };
        graphics.Validate();
        Assert.Throws<InvalidDataException>((graphics with { PendingContacts = [Contact(23, 7)] }).Validate);
        Assert.Throws<InvalidDataException>((graphics with { PendingContacts = [Contact(24, 23)] }).Validate);
        Assert.Throws<InvalidDataException>((graphics with { PendingContacts = [Contact(25, 7)] }).Validate);
        Assert.Throws<InvalidDataException>((graphics with { PendingTriggerSoundActorIndex = 23 }).Validate);
        Assert.Throws<InvalidDataException>((graphics with { PendingTriggerSoundActorIndex = 25 }).Validate);

        NativeBattleGraphicsState native = new(NativeBattleGraphicsModels.ClearBank().ToImmutableArray(), [null, null, null, null, null, null, null], 0, 0) { PendingTriggerSoundActorIndex = 6, PendingContacts = [Contact(6, 255)] };
        native.Validate();
        Assert.Throws<InvalidDataException>((native with { PendingTriggerSoundActorIndex = 7 }).Validate);
        Assert.Throws<InvalidDataException>((native with { PendingContacts = [Contact(7, 255)] }).Validate);
    }

    private static NativeBattleOriginalEffectState Effect(NativeBattleEffectParameters parameters) => new(1, 0, new(1, 0x100, 0x200), parameters, null);

    private static NativeBattleGraphicsRoster Roster()
    {
        NativeBattleGraphicsActor?[] actors = new NativeBattleGraphicsActor?[25];
        actors[3] = new("native-target", false);
        actors[7] = new("ally", true);
        actors[24] = new("enemy", false);
        return new(actors.ToImmutableArray());
    }

    private static NativeBattleGraphicsState Graphics(NativeBattleGraphicsRoster roster) => new(NativeBattleGraphicsModels.ClearPresentationBank(roster).ToImmutableArray(), roster.Rows.Select(row => row?.ActorId).ToImmutableArray(), 0, 0) { PresentationRoster = roster };

    private static NativeBattleGraphicsActorSet Rows(int first, int second) => default(NativeBattleGraphicsActorSet).Add(first).Add(second);

    private static NativeBattleContactPacket Contact(byte primary, byte secondary) => new(new(primary, 0, 0, 0, 0, 0, 0), new(secondary, 0, 0, 0, 0, 0, 0));
}
