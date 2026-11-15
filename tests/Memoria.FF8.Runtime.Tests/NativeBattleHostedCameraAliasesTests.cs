using System.Text.Json;
using static Memoria.FF8.Tests.NativeBattleTypedFixture;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedCameraAliasesTests
{
    private static readonly BattleModelOwner Source = new BattlePartyActorModel(new("source"));
    private static readonly BattleModelOwner Target = new BattleEnemyActorModel(new("target"));
    private static readonly BattleModelOwner Original = OriginalOwner(0);

    [Fact]
    public void HostedAliasesKeepTheLastOwnerInModelOrderAndSurviveAPrivateCopy()
    {
        NativeBattleGraphicsState graphics = Graphics();
        BattleModelTargets targets = BattleModelTargets.From([Target, Original, Source]);
        NativeBattleModelAliases aliases = graphics.NativeBindings.CaptureAliases(graphics.ActorModels);
        NativeBattleCameraAliases observed = NativeBattleCameraAliases.FromColdProcess().Observe50D300(Source, targets, graphics.ActorModels, aliases);
        NativeBattleCameraAliases restored = NativeBattleTestSnapshots.Independent(observed);
        Assert.Equal(Source, restored.Resolve50D790(0x9F, graphics.ActorModels, graphics.NativeBindings));
        Assert.Equal(Target, restored.Resolve50D790(0xAF, graphics.ActorModels, graphics.NativeBindings));
        Assert.Null(restored.Actor1D99B95);
        Assert.Null(restored.Target1D99AAA);
        NativeBattleCameraAliases next = restored.Observe50D300(Original, BattleModelTargets.Empty, graphics.ActorModels, aliases);
        Assert.Equal(Original, next.Resolve50D790(0x9F, graphics.ActorModels, graphics.NativeBindings));
        Assert.Equal(Target, next.Resolve50D790(0xAF, graphics.ActorModels, graphics.NativeBindings));
        Assert.Equal((byte)0, next.Actor1D99B95);
    }

    [Fact]
    public void UnknownAndUnboundAliasesRejectOnlyWhenResolved()
    {
        NativeBattleGraphicsState graphics = Graphics();
        NativeBattleModelAliases bindings = graphics.NativeBindings.CaptureAliases(graphics.ActorModels);
        NativeBattleCameraAliases aliases = new(null, 6);
        aliases.Validate();
        Assert.Throws<NotSupportedException>(() => aliases.Resolve50D790(0x90, graphics.ActorModels, graphics.NativeBindings));
        Assert.Throws<InvalidDataException>(() => aliases.Resolve50D790(0xA0, graphics.ActorModels, graphics.NativeBindings));
        Assert.Throws<InvalidDataException>(() => aliases.Observe50D300(OriginalOwner(6), BattleModelTargets.Empty, graphics.ActorModels, bindings));
        Assert.Throws<InvalidDataException>(() => aliases.Observe50D300(Source, BattleModelTargets.From([OriginalOwner(6)]), graphics.ActorModels, bindings));
        Assert.Throws<InvalidDataException>(() => (aliases with { Target1D99AAA = 25 }).Validate());
    }

    [Fact]
    public void NativeAliasesKeepSevenCellValidationAndOriginalJson()
    {
        BattleModelOwner source = OriginalOwner(6);
        NativeBattleGraphicsModel model = new(0);
        NativeBattleGraphicsState graphics = OriginalGraphics([(0, Original, model), (6, source, model)]);
        NativeBattleModelAliases bindings = graphics.NativeBindings.CaptureAliases(graphics.ActorModels);
        NativeBattleCameraAliases original = NativeBattleCameraAliases.FromColdProcess().Observe50D300(source, BattleModelTargets.From([Original, source]), graphics.ActorModels, bindings);
        Assert.Equal(source, original.Resolve50D790(0x90, graphics.ActorModels, graphics.NativeBindings));
        Assert.Equal(source, original.Resolve50D790(0xA0, graphics.ActorModels, graphics.NativeBindings));
        string json = JsonSerializer.Serialize(original);
        Assert.DoesNotContain("\"PresentationRoster\"", json);
        NativeBattleCameraAliases retained = JsonSerializer.Deserialize<NativeBattleCameraAliases>(json)!;
        Assert.Null(retained.Source);
        Assert.Null(retained.Target);
        Assert.Equal(source, retained.Resolve50D790(0xA0, graphics.ActorModels, graphics.NativeBindings));
        Assert.Throws<InvalidDataException>(() => original.Observe50D300(Source, BattleModelTargets.From([Original]), graphics.ActorModels, bindings));
        Assert.Throws<InvalidDataException>(() => new NativeBattleCameraAliases(0, 7).Validate());
    }

    private static NativeBattleGraphicsState Graphics()
    {
        BattleModelRoster roster = new([Original, Source, Target]);
        return NativeBattleHostedTestFixture.Graphics(roster, new(roster, [(NativeBattleModelSlot.Decode(0), Original)]), _ => new NativeBattleGraphicsModel(0));
    }
}
