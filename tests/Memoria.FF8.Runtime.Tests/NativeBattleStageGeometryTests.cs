using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleStageGeometryTests
{
    [Fact]
    public void Stage137CaptureGeometryUsesTheRetainedDeformedMeshAfterSnapshotRoundtrip()
    {
        BattleSession battle = Session(NativeBattleStage137.CallbackAddress, Mesh(10));
        NativeBattleStageState state = NativeBattleStageState.FromColdProcess() with { MutableGeometry = [Mesh(25)] };
        SetStage(battle, JsonSerializer.Deserialize<NativeBattleStageState>(JsonSerializer.Serialize(state))!);
        Assert.Equal(new NativeBattleVector(25, 0, 0), Geometry(battle)[0].Vertices[0].Position);
        SetStage(battle, state with { MutableGeometry = [Mesh(40)] });
        Assert.Equal(new NativeBattleVector(40, 0, 0), Geometry(battle)[0].Vertices[0].Position);
    }

    [Fact]
    public void Stage137MissingRetainedGeometryFailsInsteadOfUsingTheOriginalMesh()
    {
        BattleSession battle = Session(NativeBattleStage137.CallbackAddress, Mesh(10));
        SetStage(battle, NativeBattleStageState.FromColdProcess());
        TargetInvocationException error = Assert.Throws<TargetInvocationException>(() => Geometry(battle));
        Assert.IsType<NotSupportedException>(error.InnerException);
    }

    [Fact]
    public void OtherStagesUseRetainedMutableGeometryAfterSnapshotRoundtrip()
    {
        BattleSession battle = Session(0x5149A0, Mesh(10));
        NativeBattleStageState state = NativeBattleStageState.FromColdProcess() with { MutableGeometry = [Mesh(25)] };
        SetStage(battle, JsonSerializer.Deserialize<NativeBattleStageState>(JsonSerializer.Serialize(state))!);
        Assert.Equal(new NativeBattleVector(25, 0, 0), Geometry(battle)[0].Vertices[0].Position);
    }

    [Fact]
    public void OtherStagesUseTheirOriginalGeometryWhenNoMutableGeometryIsRetained()
    {
        BattleSession battle = Session(0x5149A0, Mesh(10));
        SetStage(battle, NativeBattleStageState.FromColdProcess());
        Assert.Equal(new NativeBattleVector(10, 0, 0), Geometry(battle)[0].Vertices[0].Position);
    }

    private static BattleSession Session(uint callback, NativeBattleRawMesh mesh)
    {
        BattleDefinition definition = new()
        {
            NativeStageSource = new(0, [null, null, null, null], [], new("stage:TIM", [])) { Callback = new(callback, null), Geometry = [mesh] },
            Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }]
        };
        return new(definition, BattleAtbMode.TurnBased);
    }

    private static NativeBattleRawMesh Mesh(short x) => new([new(new(x, 0, 0), 0)], []) { StageGroup = 1 };

    private static void SetStage(BattleSession battle, NativeBattleStageState state)
    {
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeStage))!.SetValue(battle, state);
    }

    private static ImmutableArray<NativeBattleRawMesh> Geometry(BattleSession battle)
    {
        return (ImmutableArray<NativeBattleRawMesh>)typeof(BattleSession).GetMethod("RequireNativeStageGeometry", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(battle, null)!;
    }
}
