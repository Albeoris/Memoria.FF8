using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedStageContactSamplingTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [Theory]
    [InlineData(false, 15)]
    [InlineData(false, 24)]
    [InlineData(true, 15)]
    [InlineData(true, 24)]
    public void Hosted_stage_samples_the_first_loaded_enemy_after_skipping_holes_and_unloaded_candidates(bool final, int selected)
    {
        var (session, graphics, services) = Fixture(final);
        graphics = graphics with { Models = graphics.Models.SetItem(8, Loaded(8)).SetItem(15, selected == 15 ? Loaded(15) : Unloaded(15)).SetItem(24, Loaded(24)) };
        services = WithHealth(services, selected, 510, 1000);

        Update(session, final, graphics, services);

        Assert.Equal(2560, Ratio(session, final));
        Assert.Empty(session.Combatants);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Original_row_three_takes_priority_and_matches_native_four_row_math(bool final)
    {
        var (hosted, graphics, services) = Fixture(final);
        graphics = graphics with { Models = graphics.Models.SetItem(3, Loaded(3)).SetItem(15, Loaded(15)).SetItem(24, Loaded(24)) };
        services = WithHealth(services, 3, 200, 1000);
        services = WithHealth(services, 15, 800, 1000);
        services = WithHealth(services, 24, 1, 1000);

        Update(hosted, final, graphics, services);

        (BattleSession native, NativeBattleGraphicsState nativeGraphics) = NativeFixture(final);
        var before = native.Combatants.Select(actor => actor.CurrentHp).ToArray();
        Update(native, final, nativeGraphics, null);
        Assert.Equal(1024, Ratio(hosted, final));
        Assert.Equal(Ratio(native, final), Ratio(hosted, final));
        Assert.Equal(before, native.Combatants.Select(actor => actor.CurrentHp).ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void No_loaded_matching_enemy_leaves_the_stage_unchanged(bool final)
    {
        var (session, graphics, services) = Fixture(final);
        graphics = graphics with { Models = graphics.Models.SetItem(0, Loaded(0)).SetItem(8, Loaded(8)).SetItem(15, Unloaded(15)).SetItem(24, Loaded(24) with { ModelId4 = 61 }) };
        NativeBattleStageState before = Stage(session);

        Update(session, final, graphics, services);

        Assert.Equal(before, Stage(session));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Missing_selected_hp_inside_the_hosted_stage_input_is_rejected(bool final, bool missingMaximum)
    {
        var (session, graphics, services) = Fixture(final);
        graphics = graphics with { Models = graphics.Models.SetItem(15, Loaded(15)).SetItem(24, Loaded(24)) };
        services = WithHealth(services, 15, missingMaximum ? 510 : null, missingMaximum ? null : 1000);
        services = WithHealth(services, 24, 510, 1000);
        NativeBattleStageState before = Stage(session);

        TargetInvocationException invocation = Assert.Throws<TargetInvocationException>(() => Update(session, final, graphics, services));

        Assert.IsType<NotSupportedException>(invocation.InnerException);
        Assert.Equal(before, Stage(session));
    }

    private static (BattleSession Session, NativeBattleGraphicsState Graphics, NativeBattleModelLifecycleServices Services) Fixture(bool final)
    {
        BattleSession session = new(Definition(), BattleAtbMode.TurnBased);
        IEnumerable<int> boundRows = [0, 3, 15, 24];
        NativeBattleGraphicsRoster roster = new(Enumerable.Range(0, 25).Select(row => boundRows.Contains(row) ? new NativeBattleGraphicsActor($"row-{row}", row == 0) : null).ToImmutableArray());
        NativeBattleModelLifecycleServices services = new(roster.Rows.Select(actor => actor is null ? null : new NativeBattleModelActorFacts(actor.ActorId, actor.IsParty, 140, 0, 0)).ToImmutableArray());
        NativeBattleGraphicsModel[] models = NativeBattleGraphicsModels.ClearPresentationBank(roster);
        NativeBattleGraphicsState graphics = new(models.ToImmutableArray(), roster.Rows.Select(actor => actor?.ActorId).ToImmutableArray(), 0, 0) { PresentationRoster = roster };
        ((List<BattleCombatant>)typeof(BattleSession).GetField("combatants", PrivateInstance)!.GetValue(session)!).Clear();
        ((Dictionary<string, BattleCombatant>)typeof(BattleSession).GetField("byId", PrivateInstance)!.GetValue(session)!).Clear();
        BindStage(session, final);
        return (session, graphics, services);
    }

    private static (BattleSession Session, NativeBattleGraphicsState Graphics) NativeFixture(bool final)
    {
        BattleSession session = new(Definition(), BattleAtbMode.TurnBased);
        BattleCombatant enemy = session.Combatants.Single(actor => !actor.IsPlayer);
        enemy.CurrentHp = 200;
        NativeBattleGraphicsRoster roster = session.ReadExpandedPresentationRoster();
        NativeBattleGraphicsState graphics = new(NativeBattleGraphicsModels.ClearBank().ToImmutableArray().SetItem(3, Loaded(3)), roster.Rows.Select(actor => actor?.ActorId).ToImmutableArray(), 0, 0);
        BindStage(session, final);
        return (session, graphics);
    }

    private static BattleDefinition Definition()
    {
        return new() { Combatants = [new() { Id = "ally", Name = "Ally", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy", ModelId = 140, MaximumHp = 1000 }] };
    }

    private static void BindStage(BattleSession session, bool final)
    {
        FieldInfo definition = typeof(BattleSession).GetField("definition", PrivateInstance)!;
        BattleDefinition existing = (BattleDefinition)definition.GetValue(session)!;
        NativeBattleStageSource source = new(0, [null, null, null, null], [], new("stage", []))
        {
            Callback = new(final ? NativeBattleFinalStage142.CallbackAddress : NativeBattleStage37.CallbackAddress, false)
        };
        definition.SetValue(session, existing with { NativeStageSource = source });
        typeof(BattleSession).GetProperty("NativeStage", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(session, NativeBattleStageState.FromColdProcess());
    }

    private static NativeBattleGraphicsModel Loaded(int row) => new(row, 2) { ModelId4 = 140 };

    private static NativeBattleGraphicsModel Unloaded(int row) => Loaded(row) with { Flags0 = 0 };

    private static NativeBattleModelLifecycleServices WithHealth(NativeBattleModelLifecycleServices services, int row, int? hp, int? max)
    {
        return new(services.Facts.SetItem(row, services.Facts[row]! with { CurrentHp = hp, MaximumHp = max }));
    }

    private static void Update(BattleSession session, bool final, NativeBattleGraphicsState graphics, NativeBattleModelLifecycleServices? services)
    {
        string method = final ? "UpdateNativeFinalStage142Target3" : "UpdateNativeStage37Target3";
        typeof(BattleSession).GetMethod(method, PrivateInstance)!.Invoke(session, [graphics, services]);
    }

    private static NativeBattleStageState Stage(BattleSession session)
    {
        return (NativeBattleStageState)typeof(BattleSession).GetProperty("NativeStage", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(session)!;
    }

    private static int Ratio(BattleSession session, bool final)
    {
        return final ? Stage(session).FinalStage142!.TargetRatio : Stage(session).Extended!.Stage37.Hp;
    }
}
