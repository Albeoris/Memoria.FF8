using System.Collections.Immutable;
using static Memoria.FF8.Tests.NativeBattleTypedFixture;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedEffectRosterTests
{
    private static readonly BattleModelOwner OriginalTarget = OriginalOwner(3);
    private static readonly BattleModelOwner Ally = new BattlePartyActorModel(new("ally"));
    private static readonly BattleModelOwner Enemy = new BattleEnemyActorModel(new("enemy"));
    private static readonly BattleModelOwner Missing = new BattleEnemyActorModel(new("missing"));
    private static readonly BattleModelRoster Roster = new([OriginalTarget, Ally, Enemy]);

    [Fact]
    public void Original_effect_preserves_bound_owners_through_private_copy()
    {
        NativeBattleEffectParameters parameters = new(OriginalOwner(6), 0, BattleModelTargets.From([OriginalTarget]), new BattleActionHeaderReference(0), 0, 0);
        NativeBattleOriginalEffectState effect = Effect(parameters);
        effect.Validate(OriginalRoster);
        Assert.Throws<InvalidDataException>(() => (effect with { Parameters = parameters with { Source = Missing } }).Validate(OriginalRoster));

        NativeBattleOriginalEffectState restored = NativeBattleTestSnapshots.Independent(effect);
        restored.Validate(OriginalRoster);
        Assert.Equal(parameters, restored.Parameters);
    }

    [Fact]
    public void Detached_and_graphics_owned_effects_validate_the_same_bound_parameters()
    {
        NativeBattleOriginalEffectState effect = Effect(Parameters());
        effect.Validate(Roster);
        (effect with { Parameters = null }).Validate(Roster);
        NativeBattleOriginalEffectState restored = NativeBattleTestSnapshots.Independent(effect);
        restored.Validate(Roster);
        Assert.Equal(effect.Parameters, restored.Parameters);

        NativeBattleGraphicsState graphics = Graphics(Roster) with { OriginalEffect = effect };
        graphics.Validate();
        NativeBattleGraphicsState restoredGraphics = NativeBattleTestSnapshots.Independent(graphics);
        restoredGraphics.Validate();
        Assert.Equal(graphics.ActorModels.Owners.ToArray(), restoredGraphics.ActorModels.Owners.ToArray());
        Assert.Equal(effect.Parameters, restoredGraphics.OriginalEffect!.Parameters);
    }

    [Fact]
    public void Hosted_effect_checks_owner_membership_without_a_fixed_header_or_completion_extent()
    {
        NativeBattleEffectParameters parameters = Parameters();
        NativeBattleOriginalEffectState effect = Effect(parameters);
        Assert.Throws<InvalidDataException>(() => (effect with { Parameters = parameters with { Source = Missing } }).Validate(Roster));
        Assert.Throws<InvalidDataException>(() => (effect with { Parameters = parameters with { TargetActors = BattleModelTargets.From([OriginalTarget, Missing]) } }).Validate(Roster));
        Assert.Throws<InvalidDataException>(() => effect.Validate(new([OriginalTarget, Enemy, new BattlePartyActorModel(new("replacement"))])));
        Assert.Throws<InvalidDataException>(() => effect.Validate(new([OriginalTarget, Enemy, new BattleEnemyActorModel(new("ally"))])));
        Assert.Throws<InvalidDataException>((Graphics(new([OriginalTarget, Ally])) with { OriginalEffect = effect }).Validate);

        (effect with { Parameters = parameters with { Header = new BattleActionHeaderReference(32) } }).Validate(Roster);
        BattleModelRoster larger = new(Roster.Owners.Concat(Enumerable.Range(0, 26).Select(index => new BattleEnemyActorModel(new($"extra-{index}")))));
        ImmutableDictionary<BattleModelOwner, int> completion = larger.Owners.ToImmutableDictionary(owner => owner, _ => 0);
        (effect with { FireModelCompletion = completion }).Validate(larger);
        Assert.Throws<InvalidDataException>(() => (effect with { FireModelCompletion = completion.Remove(Ally).Add(Missing, 0) }).Validate(larger));
    }

    [Fact]
    public void Hosted_graphics_validate_pending_contact_and_trigger_sound_ownership()
    {
        NativeBattleGraphicsState graphics = Graphics(Roster) with { PendingContacts = [Contact(Enemy, Ally), Contact(Ally, null)], PendingTriggerSoundActor = Enemy };
        graphics.Validate();
        Assert.Throws<InvalidDataException>((graphics with { PendingContacts = [Contact(Missing, Ally)] }).Validate);
        Assert.Throws<InvalidDataException>((graphics with { PendingContacts = [Contact(Enemy, Missing)] }).Validate);
        Assert.Throws<InvalidDataException>((graphics with { PendingTriggerSoundActor = Missing }).Validate);

        BattleModelOwner source = OriginalOwner(6);
        NativeBattleGraphicsState native = OriginalGraphics([(6, source, new NativeBattleGraphicsModel(0))]) with { PendingTriggerSoundActor = source, PendingContacts = [Contact(source, null)] };
        native.Validate();
        Assert.Throws<InvalidDataException>((native with { PendingTriggerSoundActor = Missing }).Validate);
        Assert.Throws<InvalidDataException>((native with { PendingContacts = [Contact(Missing, null)] }).Validate);
    }

    private static NativeBattleEffectParameters Parameters() => new(Enemy, 0, BattleModelTargets.From([OriginalTarget, Ally]), new BattleActionHeaderReference(0), 0, 0);

    private static NativeBattleOriginalEffectState Effect(NativeBattleEffectParameters parameters) => new(1, 0, new(1, 0x100, 0x200), parameters, null);

    private static NativeBattleGraphicsState Graphics(BattleModelRoster roster) => NativeBattleHostedTestFixture.Graphics(roster, new(roster, [(NativeBattleModelSlot.Decode(3), OriginalTarget)]), _ => new NativeBattleGraphicsModel(0));

    private static NativeBattleContactPacket Contact(BattleModelOwner primary, BattleModelOwner? secondary) => new(new(primary, 0, 0, 0, 0, 0, 0), secondary is null ? null : new(secondary, 0, 0, 0, 0, 0, 0));
}
