using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Memoria.FF8.GameUi;

namespace Memoria.FF8.App.Tests;

[Collection("World handoff environment")]
public sealed class WorldTransferAuthoringTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "Memoria.WorldTransferAuthoring.Tests", Guid.NewGuid().ToString("N"));
    private readonly List<LocalizationWorkbenchController> workbenches = [];

    [Fact]
    public void Preloading_child_does_not_initialize_or_read_authoring_recovery()
    {
        RecreateGame game = (RecreateGame)RuntimeHelpers.GetUninitializedObject(typeof(RecreateGame));
        WorldProcessHandoff handoff = (WorldProcessHandoff)RuntimeHelpers.GetUninitializedObject(typeof(WorldProcessHandoff));
        PropertyInfo current = typeof(WorldProcessHandoff).GetProperty("Current", BindingFlags.Static | BindingFlags.NonPublic)!;
        object? previous = current.GetValue(null);
        current.SetValue(null, handoff);
        try
        {
            Invoke(game, "InitializeAuthoringWorkspace");
            Assert.False(Get<bool>(game, "authoringWorkspaceInitialized"));
            Assert.Null(Get<Task?>(game, "authoringWorkspaceLoading"));
        }
        finally
        {
            current.SetValue(null, previous);
        }
    }

    [Fact]
    public async Task Source_waits_for_prior_save_then_persists_the_final_workspace()
    {
        RecreateGame game = CreateGame();
        TaskCompletionSource priorSave = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Set(game, "authoringWorkspaceInitialized", true);
        Set(game, "authoringWorkspaceSaving", priorSave.Task);
        Set(game, "authoringWorkspaceSavedAt", DateTime.UtcNow);
        Set(game, "authoringRememberedProject", "prior");

        Assert.False((bool)Invoke(game, "PrepareWorldTransferAuthoring")!);
        Assert.Null(Get<Task?>(game, "worldTransferAuthoringSave"));
        Assert.False(File.Exists(Path.Combine(directory, "localization-authoring.json")));

        priorSave.SetResult();
        Invoke(game, "PollAuthoringWorkspace");
        Assert.Null(Get<Task?>(game, "authoringWorkspaceSaving"));
        Set(game, "authoringRememberedProject", "latest");
        Invoke(game, "PrepareWorldTransferAuthoring");
        Task finalSave = Get<Task>(game, "worldTransferAuthoringSave");
        Assert.NotSame(priorSave.Task, finalSave);
        await finalSave.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.True((bool)Invoke(game, "PrepareWorldTransferAuthoring")!);
        using JsonDocument saved = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(directory, "localization-authoring.json"), TestContext.Current.CancellationToken));
        Assert.Equal("latest", saved.RootElement.GetProperty("LastProject").GetString());
        Assert.False(Get<bool>(game, "authoringExitAllowed"));
    }

    [Fact]
    public void Failed_final_save_propagates_without_authorizing_platform_shutdown_or_exit()
    {
        RecreateGame game = CreateGame();
        Set(game, "authoringWorkspaceInitialized", true);
        Set(game, "worldTransferAuthoringSave", Task.FromException(new IOException("authoring write failed")));

        TargetInvocationException failure = Assert.Throws<TargetInvocationException>(() => Invoke(game, "PrepareWorldTransferAuthoring"));

        Assert.IsType<IOException>(failure.InnerException);
        Assert.False(Get<bool>(game, "achievementsSuspended"));
        Assert.False(Get<bool>(game, "achievementsDisposed"));
        Assert.False(Get<bool>(game, "authoringExitAllowed"));
    }

    [Fact]
    public async Task Completed_preflight_does_not_start_a_later_periodic_writer()
    {
        RecreateGame game = CreateGame();
        Set(game, "authoringWorkspaceInitialized", true);
        Set(game, "worldTransfer", RuntimeHelpers.GetUninitializedObject(typeof(WorldTransferPresentation)));
        Invoke(game, "PrepareWorldTransferAuthoring");
        await Get<Task>(game, "worldTransferAuthoringSave").WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Invoke(game, "PollAuthoringWorkspace");
        Set(game, "authoringWorkspaceSavedAt", DateTime.UtcNow.AddSeconds(-10));

        Invoke(game, "PollAuthoringWorkspace");

        Task? laterSave = Get<Task?>(game, "authoringWorkspaceSaving");
        if (laterSave is not null)
        {
            await laterSave.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        }
        Assert.Null(laterSave);
    }

    private RecreateGame CreateGame()
    {
        RecreateGame game = (RecreateGame)RuntimeHelpers.GetUninitializedObject(typeof(RecreateGame));
        LocalizationWorkbenchController workbench = new();
        workbenches.Add(workbench);
        Set(game, "globalSettings", new GlobalSettingsPaths(isolatedDirectory: directory));
        Set(game, "localizationWorkbench", workbench);
        Set(game, "authoringKeyboard", new AuthoringKeyboardInput());
        return game;
    }

    private static T Get<T>(object instance, string name)
    {
        return (T)instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!;
    }

    private static void Set(object instance, string name, object? value)
    {
        instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(instance, value);
    }

    private static object? Invoke(object instance, string name, params object[] arguments)
    {
        return instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(instance, arguments);
    }

    public void Dispose()
    {
        foreach (LocalizationWorkbenchController workbench in workbenches)
        {
            workbench.Dispose();
        }
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
