using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleForeignReadySequenceTests
{
    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void Ready_target_selection_uses_host_reactions_without_fabricating_native_ready_flags(int sequence)
    {
        var fixture = Fixture(true);
        NativeBattleGraphicsState selected = Select(fixture.Battle, fixture.Graphics, sequence);
        NativeBattleForeignSequenceRequest request = Assert.Single(Requests(fixture.Battle));
        Assert.Equal(3, request.ActorIndex);
        Assert.Equal(NativeBattleForeignSequenceKind.Hit, request.Kind);
        Assert.Same(fixture.Graphics.Models[3], selected.Models[3]);
        Assert.Null(selected.Models[3].ResourceFlags44);
        Assert.Null(selected.Models[3].SequenceState);
    }

    [Fact]
    public void Missing_host_reaction_and_actual_native_unknown_ready_flags_keep_their_own_boundaries()
    {
        var unavailable = Fixture(false);
        TargetInvocationException error = Assert.Throws<TargetInvocationException>(() => Select(unavailable.Battle, unavailable.Graphics, 4));
        Assert.Contains("host presentation service", Assert.IsType<NotSupportedException>(error.InnerException).Message);
        Assert.Empty(Requests(unavailable.Battle));
        var native = Fixture(true, new NativeBattleGraphicsResource([]));
        error = Assert.Throws<TargetInvocationException>(() => Select(native.Battle, native.Graphics, 4));
        Assert.Contains("resource WORD44", Assert.IsType<NotSupportedException>(error.InnerException).Message);
        Assert.Empty(Requests(native.Battle));
    }

    private static (BattleSession Battle, NativeBattleGraphicsState Graphics) Fixture(bool canReact, NativeBattleGraphicsResource? resource = null)
    {
        BattleSession battle = new(new BattleDefinition { Combatants = [new() { Id = "source", Name = "Source", IsPlayer = true }, new() { Id = "target", Name = "Target" }] }, BattleAtbMode.Normal);
        NativeBattleModelTransform transform = new(new(100, 200, 300), new(100, 200, 300), 0, 0, 4096);
        NativeBattleForeignActor actor = new(3, transform, new(100, 200, 300, 0), (_, _) => throw new InvalidOperationException()) { CanReact = canReact, NativeResource = resource };
        typeof(BattleSession).GetField("presentationForeignActors", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(battle, new Dictionary<int, NativeBattleForeignActor> { [3] = actor });
        NativeBattleGraphicsState graphics = new(NativeBattleGraphicsModels.ClearBank().ToImmutableArray().SetItem(3, new(3, 2) { Transform = transform }), [null, null, null, "target", null, null, null], 0, 0);
        return (battle, graphics);
    }

    private static NativeBattleGraphicsState Select(BattleSession battle, NativeBattleGraphicsState graphics, int sequence)
    {
        MethodInfo select = typeof(BattleSession).GetMethod("SelectNativeReadyHeaderSequence505CE0", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (NativeBattleGraphicsState)select.Invoke(battle, [graphics, (byte)3, sequence])!;
    }

    private static IEnumerable<NativeBattleForeignSequenceRequest> Requests(BattleSession battle)
    {
        return (IEnumerable<NativeBattleForeignSequenceRequest>)typeof(BattleSession).GetField("presentationSequences", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(battle)!;
    }
}
