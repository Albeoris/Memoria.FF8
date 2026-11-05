using System.Reflection;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;

namespace Memoria.FF8.App.Tests;

public sealed class WindowCloseTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "memoria-close-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void CloseDuringReportCanBeCancelledWithoutDismissingAnotherDialog()
    {
        using RecreateGame game = CreateGame();
        Set(game, "authoringWorkspaceInitialized", true);
        Set(game, "error", "Game error");
        GameConfirmationOverlay underlying = Get<GameConfirmationOverlay>(game, "confirmationOverlay");
        Task<bool> import = underlying.ShowAsync("Import changes?", "Import", TestContext.Current.CancellationToken);

        Assert.True(Close(game).Cancel);
        Task<bool> decision = Get<Task<bool>>(game, "windowCloseDecision");
        Assert.True(Close(game).Cancel);
        Assert.Same(decision, Get<Task<bool>>(game, "windowCloseDecision"));
        Get<GameConfirmationOverlay>(game, "windowCloseConfirmation").Handle(UiKey.Cancel);
        Invoke(game, "UpdateWindowCloseConfirmation");

        Assert.False(Get<bool>(game, "authoringExitRequested"));
        Assert.Equal("Game error", Get<string>(game, "error"));
        Assert.True(underlying.IsOpen);
        Assert.False(import.IsCompleted);
    }

    [Fact]
    public void AcceptedCloseCompletesWorkspaceSaveBeforeModalOrFocusEarlyReturns()
    {
        using RecreateGame game = CreateGame();
        Set(game, "authoringWorkspaceInitialized", true);
        Set(game, "authoringWorkspaceSavedAt", DateTime.UtcNow);
        TaskCompletionSource save = new();
        Set(game, "authoringWorkspaceSaving", save.Task);
        Set(game, "error", "Game error");
        Set(game, "userPaused", true);
        game.SetWindowActive(false);
        Assert.True(Close(game).Cancel);
        GameConfirmationOverlay close = Get<GameConfirmationOverlay>(game, "windowCloseConfirmation");
        close.Handle(UiKey.Right);
        close.Handle(UiKey.Confirm);
        Invoke(game, "UpdateWindowCloseConfirmation");
        // Exercise the native close callback after the user's decision, without running a graphics host.
        Assert.True(Close(game).Cancel);
        Assert.True(Get<bool>(game, "authoringExitRequested"));
        // Model completion of the final write on a later host frame.
        Set(game, "authoringExitNeedsFinalSave", false);
        save.SetResult();
        SynchronizationContext? previous = SynchronizationContext.Current;
        try
        {
            Get<GameDialogContext>(game, "dialogContext").OperationStarted();
            Invoke(game, "Update", new GameTime(TimeSpan.Zero, TimeSpan.FromMilliseconds(16)));
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        Assert.True(Get<bool>(game, "authoringExitAllowed"));
        Assert.False(Close(game).Cancel);
    }

    [Fact]
    public void RepeatedCloseOffersExitWhenEditorSaveIsStillPending()
    {
        using RecreateGame game = CreateGame();
        Set(game, "authoringWorkspaceInitialized", true);
        Set(game, "windowCloseApproved", true);
        Set(game, "authoringExitRequested", true);
        Set(game, "authoringWorkspaceSaving", new TaskCompletionSource().Task);

        Assert.True(Close(game).Cancel);
        GameConfirmationOverlay close = Get<GameConfirmationOverlay>(game, "windowCloseConfirmation");
        close.Handle(UiKey.Right);
        close.Handle(UiKey.Confirm);
        Invoke(game, "UpdateWindowCloseConfirmation");

        Assert.False(Close(game).Cancel);
    }

    [Fact]
    public void SaveFailureAllowsExplicitExitWithoutRetryingFailedStorage()
    {
        using RecreateGame game = CreateGame();
        Set(game, "authoringWorkspaceInitialized", true);
        Set(game, "authoringWorkspaceSavedAt", DateTime.UtcNow);
        Set(game, "windowCloseApproved", true);
        Set(game, "authoringExitRequested", true);
        Set(game, "authoringWorkspaceSaving", Task.FromException(new IOException("Storage unavailable")));
        Invoke(game, "PollAuthoringWorkspace");

        Assert.False(Get<bool>(game, "windowCloseApproved"));
        Assert.True(Close(game).Cancel);
        GameConfirmationOverlay close = Get<GameConfirmationOverlay>(game, "windowCloseConfirmation");
        close.Handle(UiKey.Right);
        close.Handle(UiKey.Confirm);
        Invoke(game, "UpdateWindowCloseConfirmation");
        Assert.False(Close(game).Cancel);
    }

    private RecreateGame CreateGame()
    {
        Directory.CreateDirectory(directory);
        return new RecreateGame(new LaunchOptions { MuteAudio = true, SettingsDirectory = directory, GameDirectory = directory, ControlExclusive = true }, new GameSession(new GameState()));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static ExitingEventArgs Close(RecreateGame game)
    {
        ExitingEventArgs args = new();
        Invoke(game, "OnExiting", game, args);
        return args;
    }

    private static object? Invoke(RecreateGame game, string method, params object[] args) => typeof(RecreateGame).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(game, args);
    private static T Get<T>(RecreateGame game, string name) => (T)typeof(RecreateGame).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(game)!;
    private static void Set(RecreateGame game, string name, object value) => typeof(RecreateGame).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(game, value);
}
