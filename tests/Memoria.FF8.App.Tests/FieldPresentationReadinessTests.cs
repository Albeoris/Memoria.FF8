using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class FieldPresentationReadinessTests
{
    [Fact]
    public void UnobservedOutputWaitsWithoutSelectingHistoryOrMutatingTheState()
    {
        NativeFieldShake shake = NativeFieldShake.UnknownProcessHistory();
        JsonElement before = JsonSerializer.SerializeToElement(shake);
        Assert.True(FieldPresentationReadiness.HasUnobservedShakeOutput(shake));
        Assert.True(JsonElement.DeepEquals(before, JsonSerializer.SerializeToElement(shake)));
        Assert.Throws<NativeShakeRenderException>(() => shake.X.RequireUniqueOutput());

        NativeShakeAxis known = shake.X with { Alternatives = shake.X.Alternatives.Select(item => item with { Output = 0 }).ToArray() };
        Assert.True(FieldPresentationReadiness.HasUnobservedShakeOutput(shake with { X = known }));
        Assert.True(FieldPresentationReadiness.HasUnobservedShakeOutput(shake with { Y = known }));
        Assert.True(FieldPresentationReadiness.HasUnobservedShakeOutput(shake with { X = shake.X with { Enabled = true, Amplitude = 8, Duration = 4 } }));
        Assert.False(FieldPresentationReadiness.HasUnobservedShakeOutput(shake with { X = known, Y = known }));
        Assert.False(FieldPresentationReadiness.HasUnobservedShakeOutput(null));
    }

    [Fact]
    public void KnownDivergentOutputStillRequiresTheStrictNativeRenderContract()
    {
        NativeFieldShake shake = NativeFieldShake.UnknownProcessHistory();
        NativeShakeAxis divergent = shake.X with { Alternatives = shake.X.Alternatives.Select(item => item with { Output = (sbyte)(item.Cursor % 2) }).ToArray() };
        Assert.False(FieldPresentationReadiness.HasUnobservedShakeOutput(shake with { X = divergent, Y = divergent }));
        Assert.Throws<NativeShakeRenderException>(() => divergent.RequireUniqueOutput());
        Assert.Throws<InvalidDataException>(() => FieldPresentationReadiness.HasUnobservedShakeOutput(shake with { X = shake.X with { Alternatives = [] } }));
    }

    [Fact]
    public void FreshNewGameDefersOnlyPresentationUntilAnOrdinaryCallbackDefinesOutput()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original field corpus is required.");
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using GameWorld world = new(session, directory, persistReplayJournal: false);
        world.EnsureField();
        JsonElement vm = world.Events.Capture();
        long entries = session.ReplayJournal!.EntryCount;
        Assert.True(FieldPresentationReadiness.HasUnobservedShakeOutput(world.Events.Scene.Shake));
        Assert.Throws<NativeShakeRenderException>(() => world.Render(TimeSpan.Zero));
        Assert.True(JsonElement.DeepEquals(vm, world.Events.Capture()));
        Assert.Equal(entries, session.ReplayJournal.EntryCount);

        for (int callback = 0; callback < 60; callback++)
        {
            PlayerAction action = new(session.IsMoviePlaying ? PlayerActionKind.SkipMovie : PlayerActionKind.Wait);
            Assert.True(world.Apply(action).Accepted);
            world.EnsureField();
            if (session.State.Scene == SceneKind.Field && !FieldPresentationReadiness.HasUnobservedShakeOutput(world.Events.Scene.Shake))
            {
                Assert.Equal(0, world.Events.Scene.Shake!.X.RequireUniqueOutput());
                Assert.Equal(0, world.Events.Scene.Shake.Y.RequireUniqueOutput());
                Assert.NotEmpty(world.Render(TimeSpan.Zero));
                Assert.Null(world.Events.LastFault);
                return;
            }
        }
        Assert.Fail("Fresh opening did not produce an observed field frame within the ordinary callback budget.");
    }
}
