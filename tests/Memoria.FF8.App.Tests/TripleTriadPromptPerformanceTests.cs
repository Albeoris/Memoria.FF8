using System.Diagnostics;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class TripleTriadPromptPerformanceTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void PromptCallbacksReuseFrozenFieldAndRestoreBeforeCompleting(int choice)
    {
        string source = TestResources.FixturePath("App.Tests.TripleTriadPromptPerformanceTests.RulesPrompt");
        GameSession session = new(GameSession.Load(source));
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        var controller = world.EnsureTripleTriad()!;
        Assert.True(controller.AwaitingRules);
        Assert.Equal(TripleTriadPromptPhase.AwaitingChoice, session.State.TripleTriadPlay!.Prompt.Phase);
        JsonElement frozen = session.State.EventState!.Value;
        long frame = world.Events.Frame;
        TripleTriadPromptState expected = session.State.TripleTriadPlay.Prompt;
        long started = Stopwatch.GetTimestamp();
        for (int callback = 0; callback < 120; callback++)
        {
            expected = expected.Advance();
            world.AdvanceTripleTriadPrompt();
            Assert.Equal(expected, session.State.TripleTriadPlay!.Prompt);
            Assert.Equal(expected.RandomState, session.State.TripleTriadPlay.RandomState);
            Assert.True(frozen.Equals(session.State.EventState!.Value), "Prompt callbacks must retain the frozen field snapshot instead of recapturing the VM.");
        }
        Console.WriteLine($"120 rules prompt callbacks: {Stopwatch.GetElapsedTime(started).TotalMilliseconds:F3} ms");
        string path = Path.Combine(Path.GetTempPath(), "triad-prompt-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.Save(path);
            GameSession restoredSession = new(GameSession.Load(path));
            using GameWorld restored = new(restoredSession, TestResources.GameDirectory);
            restored.EnsureField();
            Assert.True(restored.EnsureTripleTriad()!.AwaitingRules);
            Assert.Equal(expected, restoredSession.State.TripleTriadPlay!.Prompt);
            world.AdvanceTripleTriadPrompt(choice);
            restored.AdvanceTripleTriadPrompt(choice);
            JsonElement fading = session.State.EventState!.Value;
            for (int callback = 0; callback < 14; callback++)
            {
                world.AdvanceTripleTriadPrompt();
                restored.AdvanceTripleTriadPrompt();
                Assert.True(fading.Equals(session.State.EventState!.Value));
                Assert.Equal(session.State.TripleTriadPlay!.Prompt, restoredSession.State.TripleTriadPlay!.Prompt);
            }
            Assert.Equal(frame, world.Events.Frame);
            world.AdvanceTripleTriadPrompt();
            restored.AdvanceTripleTriadPrompt();
            Assert.Equal(world.Events.Frame, restored.Events.Frame);
            Assert.True(JsonElement.DeepEquals(session.State.EventState!.Value, restoredSession.State.EventState!.Value));
            if (choice == 0)
            {
                Assert.True(session.State.TripleTriadPlay!.RulesAccepted);
                Assert.Equal(session.State.TripleTriadPlay.RandomState, restoredSession.State.TripleTriadPlay!.RandomState);
                Assert.Equal(session.State.TripleTriadPlay.OpponentHand, restoredSession.State.TripleTriadPlay.OpponentHand);
            }
            else
            {
                Assert.Null(session.State.TripleTriadPlay);
                Assert.Null(world.Events.PendingTripleTriad);
                Assert.False(fading.Equals(session.State.EventState!.Value));
            }
        }
        finally
        {
            File.Delete(path);
        }
    }
}
