using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeLastInGameplayTests
{
    [Fact]
    public void ActualCastleEntryPublishesSealsAndReservePartyAndRestoresAfterLastIn()
    {
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(game), "Installed original resources are required.");
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "docs", "recreate-checkpoint.md")))
        {
            root = root.Parent;
        }
        string source = Path.Combine(root!.FullName, "../fixtures/external-fixture.json");
        byte[] original = File.ReadAllBytes(source);
        GameSession session = new(GameSession.Load(source));
        using GameWorld world = new(session, game);
        world.EnsureField();
        Assert.Equal(596, world.Events.FieldId);
        Assert.Equal(892, world.Events.Frame);
        bool executed = false;
        world.Events.CommandExecuted += command =>
        {
            if (command.Name == "LASTIN")
            {
                executed = true;
            }
        };
        for (int frame = 0; frame < 60 && !executed; frame++)
        {
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        }
        Assert.True(executed);
        Assert.Null(world.Events.LastFault);
        Assert.Equal(0x800u, session.WorldControls!.EventFlags104 & 0x800u);
        Assert.Equal(255, session.EventMemory.Read(243, 1).RequireExact());
        Assert.Equal(255u, session.InputConfiguration!.Options >> 24);
        byte[] reserve = Enumerable.Range(0, 6).Where(identity => !world.Events.Scene.Party.Contains(identity)).Select(identity => (byte)identity).ToArray();
        Assert.Equal(reserve.Concat(reserve), session.CharacterVitals!.JunctionPartyBackup);
        Assert.Equal((byte)255, NativeBattleLimitRules.ReadFieldBattleRules(session.EventMemory, session.WorldControls));
        world.EnsureField();
        Assert.Throws<NotSupportedException>(() => world.CreateEncounterBattle(0));
        GameSession resumedSession = new(session.State with { EventState = world.Events.Capture() });
        using GameWorld resumed = new(resumedSession, game);
        resumed.EnsureField();
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), resumed.Events.Capture()));
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.True(resumed.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), resumed.Events.Capture()));
        Assert.Equal(original, File.ReadAllBytes(source));
    }
}
