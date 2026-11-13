using System.Reflection;
using System.Runtime.CompilerServices;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.GameUi;
using Memoria.FF8.Media;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

public sealed class Ff7MovieTransitionTests
{
    [Fact]
    public void PresentationOnlyUpdatePreservesMovieOwnerAndWaitingScript()
    {
        Ff7Scene scene = CreateWaitingScene();
        Ff7Session session = scene.Session!;
        FieldVmSnapshot before = session.Machine.Capture();
        Ff7Movie owner = session.Host.Process.Movie!;
        Ff7Progress progress = session.Progress;
        // A completed decoder still owns its last image until the next field callback.
        // No decoder, graphics device or audio device is needed to observe this boundary.
        MoviePresentation surface = (MoviePresentation)RuntimeHelpers.GetUninitializedObject(typeof(MoviePresentation));
        Set(surface, "started", true);
        Set(scene, "movie", surface);
        Set(scene, "movieSourceIdentity", (session.FieldGeneration, owner.RequestId, owner.PreparedId));
        Set(scene, "movieCompletionPending", true);

        scene.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60)), new KeyboardState());

        Assert.Null(scene.Error);
        Assert.Same(surface, Get<MoviePresentation>(scene, "movie"));
        Assert.Equal(owner, session.Host.Process.Movie);
        Assert.Equal(Ff7GlobalCommand.Movie, session.Host.Process.GlobalCommand);
        Assert.Equal(owner.RequestId, session.Host.Process.GlobalRequestId);
        Assert.Equal(before.Tick, session.Machine.Tick);
        Assert.Equal(before.Entities[0].ProgramCounter, session.Machine.Capture().Entities[0].ProgramCounter);
        Assert.Equal(before.PendingRequests, session.Machine.Capture().PendingRequests);
        Assert.Equal(0, session.Machine.Variables.ReadByte(FieldBank.MapByte, 3));
        Assert.Equal(progress.Inventory, session.Progress.Inventory);
        Assert.Equal(progress.Gil, session.Progress.Gil);
        Assert.Equal(progress.FieldRandomIndex, session.Progress.FieldRandomIndex);
        Assert.False(scene.IsMovieBuffering);
        Set(scene, "movie", null);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PendingCompletionResumesMovieWaitWithinTheNextFieldCallback(bool skipped)
    {
        Ff7Scene scene = CreateWaitingScene();
        Ff7Session session = scene.Session!;
        long tick = session.Machine.Tick;
        long? request = session.Host.Process.GlobalRequestId;
        if (skipped)
        {
            KeyboardInput input = Get<KeyboardInput>(scene, "sharedInput");
            input.Profile = ControlsProfile.CreateDefault().WithBinding(ControlAction.Cancel, ControlDevice.Keyboard, new([Keys.J], []), replaceConflicts: true);
            input.Read(new KeyboardState(), true);
            input.Read(new KeyboardState(Keys.J), true);
            Assert.True(input.CancelHeld);
            Invoke(scene, "UpdateMovieSkip", TimeSpan.FromSeconds(1));
        }
        else
        {
            Set(scene, "movieCompletionPending", true);
        }

        Assert.True(Get<bool>(scene, "movieCompletionPending"));
        Assert.True(session.Host.Process.Movie!.Playing);
        Assert.Equal(request, session.Host.Process.GlobalRequestId);
        Assert.Equal(tick, session.Machine.Tick);
        Assert.Equal(0, session.Machine.Variables.ReadByte(FieldBank.MapByte, 3));

        Invoke(scene, "CompleteMovieBeforeFieldTick");
        session.Machine.AdvanceTick();

        Assert.Equal(tick + 1, session.Machine.Tick);
        Assert.True(session.Host.Process.Movie!.Completed);
        Assert.False(session.Host.Process.Movie.Playing);
        Assert.Equal(Ff7GlobalCommand.None, session.Host.Process.GlobalCommand);
        Assert.Null(session.Host.Process.GlobalRequestId);
        Assert.Empty(session.Machine.Capture().PendingRequests!);
        Assert.Equal(99, session.Machine.Variables.ReadByte(FieldBank.MapByte, 3));
        Assert.False(Get<bool>(scene, "movieCompletionPending"));
        Assert.Null(Get<Ff7MovieSource?>(scene, "movieSource"));
        Assert.False(scene.IsMovieBuffering);
    }

    [Fact]
    public void MovieWithoutItsFirstDecodedImageOwnsTheBufferingBoundary()
    {
        Ff7Scene scene = CreateWaitingScene();

        Assert.True(scene.IsMovieBuffering);
        MoviePresentation surface = (MoviePresentation)RuntimeHelpers.GetUninitializedObject(typeof(MoviePresentation));
        Set(scene, "movie", surface);
        Assert.True(scene.IsMovieBuffering);
        Set(surface, "started", true);
        Assert.False(scene.IsMovieBuffering);
        Set(scene, "movie", null);
        Set(scene, "movieCompletionPending", true);
        Invoke(scene, "CompleteMovieBeforeFieldTick");
        Assert.False(scene.IsMovieBuffering);
    }

    private static Ff7Scene CreateWaitingScene()
    {
        // Initialization RET, PMVIE 0, MOVIE, SETBYTE map[3] = 99, RET.
        // The post-movie write witnesses continuation without depending on presentation state.
        byte[] code = [0, 0xF8, 0, 0xF9, 0x80, 0x50, 3, 99, 0];
        FieldProgram program = new(code, [Enumerable.Repeat((ushort)0, 32).ToArray()]);
        Ff7FieldResources field = new("movie-fixture", null!, null!, null!, [], [], null!, null!, null!);
        Ff7FieldHost host = new(field, (_, _) => 1);
        FieldMachine machine = new(program, host: host);
        machine.Initialize();
        // PMVIE yields on its first delivery; the following callback admits MOVIE.
        machine.AdvanceTick();
        machine.AdvanceTick();
        Assert.True(host.Process.Movie!.Playing);
        Assert.Single(machine.Capture().PendingRequests!);
        Ff7Session session = (Ff7Session)RuntimeHelpers.GetUninitializedObject(typeof(Ff7Session));
        Set(session, "<Host>k__BackingField", host);
        Set(session, "<Machine>k__BackingField", machine);
        Set(session, "<Field>k__BackingField", field);
        Set(session, "<Progress>k__BackingField", new Ff7Progress { InitialKernelData = [], Characters = [], Party = [], Inventory = [new(0, 3)], Materia = [], Gil = 123, FieldRandomIndex = 37, Clock = new() });
        Ff7Scene scene = (Ff7Scene)RuntimeHelpers.GetUninitializedObject(typeof(Ff7Scene));
        Set(scene, "<Session>k__BackingField", session);
        Set(scene, "sharedInput", new KeyboardInput());
        Set(scene, "sharedClock", new GameplayClock());
        Set(scene, "soundChannels", new Ff7SoundChannels());
        Set(scene, "movieSkipGesture", new MovieSkipGesture());
        Set(scene, "noticeWindow", new NoticeWindow());
        Set(scene, "locationMovement", new LocationMovementGate());
        Set(scene, "envelope", new CombinedSessionSave { ActiveGame = SessionGame.Ff7 });
        Set(scene, "movieSource", new Ff7MovieSource("movie-fixture.avi", new MovieInformation(1, 1, 15, 1d / 15, 1), 1, 15));
        Ff7Scene.PublishMovieCameraFrame(host, Get<Ff7MovieSource>(scene, "movieSource"), 0);
        return scene;
    }

    private static void Invoke(Ff7Scene scene, string name, params object[] arguments)
    {
        typeof(Ff7Scene).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(scene, arguments);
    }

    private static T Get<T>(object target, string name)
    {
        return (T)target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(target)!;
    }

    private static void Set(object target, string name, object? value)
    {
        target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(target, value);
    }
}
