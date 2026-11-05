using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayCardPromptTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeCardInvitationsAreDeclinedWhileAcceptedMatchesRequireActualContinuation(bool acceptFirst)
    {
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayCardPromptTests.1");
        string gameDirectory = TestResources.GameDirectory;
        byte[] originalManifest = Hash(manifest);
        AutoPlayRunState saved = JsonSerializer.Deserialize<AutoPlayRunState>(File.ReadAllText(manifest), AutoPlayController.JsonOptions)!;
        string snapshot = Path.GetFullPath(saved.Snapshot!, Path.GetDirectoryName(manifest)!);
        byte[] originalSnapshot = Hash(snapshot);
        GameSession session = GameSession.CreateColdProcess();
        GameWorld? world = null;
        string output = Path.Combine(Path.GetTempPath(), "autoplay-card-prompt-" + Guid.NewGuid().ToString("N"));
        try
        {
            GameWorld GetWorld() => world ??= new(session, gameDirectory, validateWorldPresentation: false);
            void Restore(GameState state)
            {
                world?.Dispose();
                world = null;
                session.Restore(state);
            }
            AutoPlayController controller = new(session, GetWorld, Restore, output);
            controller.ResumeFrom(manifest);
            GetWorld().EnsureField();
            Assert.NotNull(world!.Events.PendingTripleTriad);
            Assert.True(world.EnsureTripleTriad()!.AwaitingRules);
            Assert.False(session.State.TripleTriadPlay!.RulesAccepted);
            NativeCardInventory originalCards = session.State.CardInventory!.DeepCopy();
            long fieldFrame = world.Events.Frame;
            if (acceptFirst)
            {
                for (int batch = 0; batch < 2 && session.State.TripleTriadPlay!.Prompt.Phase == TripleTriadPromptPhase.FadingIn; batch++)
                {
                    Assert.True(world.Apply(new(Memoria.FF8.Events.PlayerActionKind.Wait, Frames: 15)).Accepted);
                }
                Assert.Equal(TripleTriadPromptPhase.AwaitingChoice, session.State.TripleTriadPlay.Prompt.Phase);
                Assert.True(world.Apply(new(Memoria.FF8.Events.PlayerActionKind.Confirm)).Accepted);
                Assert.True(session.State.TripleTriadPlay!.RulesAccepted);

                controller.Advance(1);

                Assert.False(controller.Running);
                Assert.Contains("accepted match", controller.Error);
                Assert.NotNull(GetWorld().Events.PendingTripleTriad);
                Assert.True(session.State.TripleTriadPlay!.RulesAccepted);
                Assert.Equal(originalCards.Cards, session.State.CardInventory!.Cards);
                Assert.Equal(originalCards.TripleTriadStatistics, session.State.CardInventory.TripleTriadStatistics);
                Assert.Equal(originalManifest, Hash(manifest));
                Assert.Equal(originalSnapshot, Hash(snapshot));
                return;
            }
            for (int step = 0; step < 30 && GetWorld().Events.PendingTripleTriad is not null && controller.Error is null; step++)
            {
                controller.Advance(1);
            }

            Assert.Null(controller.Error);
            Assert.True(controller.Running);
            Assert.Null(GetWorld().Events.PendingTripleTriad);
            Assert.Null(session.State.TripleTriadPlay);
            Assert.Equal(originalCards.Cards, session.State.CardInventory!.Cards);
            Assert.Equal(originalCards.ObtainedRareCards, session.State.CardInventory.ObtainedRareCards);
            Assert.Equal(originalCards.CollectionFlags, session.State.CardInventory.CollectionFlags);
            Assert.Equal(originalCards.TripleTriadStatistics, session.State.CardInventory.TripleTriadStatistics);
            controller.Advance(1);
            Assert.Null(controller.Error);
            Assert.True(GetWorld().Events.Frame > fieldFrame);
            Assert.Equal(originalManifest, Hash(manifest));
            Assert.Equal(originalSnapshot, Hash(snapshot));
        }
        finally
        {
            world?.Dispose();
            if (Directory.Exists(output))
            {
                Directory.Delete(output, recursive: true);
            }
        }
    }

    private static byte[] Hash(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return SHA256.HashData(stream);
    }
}
