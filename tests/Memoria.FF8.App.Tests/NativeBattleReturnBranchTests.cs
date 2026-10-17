using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleReturnBranchTests
{
    [Theory]
    [InlineData(SceneKind.Field, false)]
    [InlineData(SceneKind.WorldMap, false)]
    [InlineData(SceneKind.WorldMap, true)]
    public void Missing_return_producers_refuse_before_the_outcome_callback(SceneKind source, bool scripted)
    {
        bool outcome = false;
        Assert.Throws<NotSupportedException>(() =>
        {
            GameWorld.RequireNativeBattleReturnBranch(source, scripted);
            outcome = true;
        });
        Assert.False(outcome);
    }

    [Fact]
    public void Scripted_field_branch_continues_to_the_separate_kind3_completion_preflight()
    {
        GameWorld.RequireNativeBattleReturnBranch(SceneKind.Field, true);
    }
}
