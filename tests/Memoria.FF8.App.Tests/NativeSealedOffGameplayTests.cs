using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeSealedOffGameplayTests
{
    [Fact]
    public void OriginalSphinxRewardChoiceReleasesDrawAndRestoresBothAliases()
    {
        string game = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(game), "Installed original resources are required.");
        DirectoryInfo? root = new(TestResources.FixtureDirectory);
        string source = Path.Combine(root!.FullName, "../fixtures/external-fixture.json");
        byte[] original = File.ReadAllBytes(source);
        GameSession session = new(GameSession.Load(source));
        using GameWorld world = new(session, game);
        world.EnsureField();
        Assert.Equal(98, world.Events.FieldId);
        Assert.Equal(29, world.Events.Frame);
        Assert.Equal((byte)255, session.InputConfiguration!.CommandRestrictions);
        bool executed = false;
        world.Events.CommandExecuted += command =>
        {
            if (command.Name == "SEALEDOFF")
            {
                Assert.Equal(new[] { 8 }, command.Arguments);
                executed = true;
            }
        };
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Choose, 4)).Accepted);
        for (int callback = 0; callback < 60 && !executed; callback++)
        {
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        }
        Assert.True(executed);
        Assert.Null(world.Events.LastFault);
        Assert.Equal(247, session.EventMemory.Read(243, 1).RequireExact());
        Assert.Equal((byte)247, session.InputConfiguration!.CommandRestrictions);
        world.EnsureField();
        GameSession resumedSession = new(session.State with { EventState = world.Events.Capture() });
        using GameWorld resumed = new(resumedSession, game);
        resumed.EnsureField();
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), resumed.Events.Capture()));
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.True(resumed.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), resumed.Events.Capture()));
        Assert.Equal((byte)247, resumedSession.InputConfiguration!.CommandRestrictions);
        Assert.Equal(original, File.ReadAllBytes(source));
    }
}
