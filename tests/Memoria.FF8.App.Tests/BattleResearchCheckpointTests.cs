using System.Reflection;
using System.Runtime.CompilerServices;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleResearchCheckpointTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Checkpoint_rejection_preserves_an_existing_file_when_battle_owns_the_boundary(bool laboratory)
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        RecreateGame host = CreateHost(session);
        BattleSession battle = new(new BattleDefinition
        {
            Combatants = [new() { Id = "ally", Name = "Ally", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }]
        }, BattleAtbMode.Normal);
        if (laboratory)
        {
            typeof(RecreateGame).GetField("debugBattle", PrivateInstance)!.SetValue(host, battle);
            Assert.False(session.HasActiveBattle);
        }
        else
        {
            session.SetLiveBattle(battle);
            Assert.True(session.HasActiveBattle);
        }
        string path = TestResources.ArtifactFile("blocked-research-checkpoint.json");
        File.WriteAllText(path, "original checkpoint");

        TargetInvocationException rejection = Assert.Throws<TargetInvocationException>(() => SaveCheckpoint(host, path));
        Assert.Contains("outside battle", Assert.IsType<InvalidOperationException>(rejection.InnerException).Message);
        Assert.Equal("original checkpoint", File.ReadAllText(path));
    }

    [Fact]
    public void Checkpoint_outside_battle_writes_a_loadable_field_save()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        RecreateGame host = CreateHost(session);
        string path = TestResources.ArtifactFile("field-research-checkpoint.json");

        SaveCheckpoint(host, path);

        GameState loaded = GameSession.Load(path);
        Assert.Equal(session.State.Scene, loaded.Scene);
        Assert.Equal(session.State.FieldId, loaded.FieldId);
        Assert.Null(loaded.Battle);
    }

    private static RecreateGame CreateHost(GameSession session)
    {
        var host = (RecreateGame)RuntimeHelpers.GetUninitializedObject(typeof(RecreateGame));
        typeof(RecreateGame).GetField("session", PrivateInstance)!.SetValue(host, session);
        return host;
    }

    private static void SaveCheckpoint(RecreateGame host, string path)
    {
        typeof(RecreateGame).GetMethod("SaveCheckpoint", PrivateInstance)!.Invoke(host, [path]);
    }
}
