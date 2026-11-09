using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeProcessTests
{
    public static void Run()
    {
        TurnTimingAndRestore();
        CoordinateCameraAndWait();
        MovieAndBattleOwnership();
        PlayerControlTarget();
        ResetFade();
    }

    private static void TurnTimingAndRestore()
    {
        Ff7FieldHost host = Host();
        FieldHostRequest request = Request(1, new FieldActorTurn(16, 2, 4, 1));
        Equal(false, host.Execute(request).Completed);
        host.AdvanceActors();
        Equal(240, host.GetActor(0).Direction);
        host.AdvanceActors();
        Equal(248, host.GetActor(0).Direction);
        Ff7FieldHost restored = Host(JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!);
        for (int tick = 0; tick < 3; tick++)
        {
            Equal(false, host.Execute(request).Completed);
            Equal(false, restored.Execute(request).Completed);
            host.AdvanceActors();
            restored.AdvanceActors();
            Equal(JsonSerializer.Serialize(host.Capture()), JsonSerializer.Serialize(restored.Capture()));
        }
        Equal(16, host.GetActor(0).Direction);
        Equal(true, host.Execute(request).Completed);
        Equal(0, host.GetActor(0).TurnMode);
        FieldHostRequest signed = Request(2, new FieldActorTurn(-16, 2, 4, 1, true));
        Equal(false, host.Execute(signed).Completed);
        host.AdvanceActors();
        host.AdvanceActors();
        Equal(8, host.GetActor(0).Direction);
        host.Execute(Request(3, new FieldActorProperty(FieldActorPropertyKind.Direction, 80)));
        Equal(0, host.GetActor(0).TurnMode);
        host.AdvanceActors();
        Equal(80, host.GetActor(0).Direction);
    }

    private static void CoordinateCameraAndWait()
    {
        Ff7FieldHost host = Host();
        Equal(true, host.Execute(Request(1, new FieldCameraCommand(FieldCameraOperation.Coordinates, 5, 40, -40, 4))).Completed);
        FieldHostRequest wait = Request(2, new FieldCameraCommand(FieldCameraOperation.Wait));
        for (int step = 0; step <= 4; step++)
        {
            Equal(false, host.Execute(wait).Completed);
            host.AdvanceActors();
            Equal(step * 10, host.Process.CameraX);
            Equal(-step * 10, host.Process.CameraY);
        }
        Equal(true, host.Execute(wait).Completed);
        Equal(true, host.Execute(Request(3, new FieldCameraCommand(FieldCameraOperation.Coordinates, 6, 100, 100, 0))).Completed);
        host.AdvanceActors();
        Equal(40, host.Process.CameraX);
        Equal(true, host.Execute(Request(4, new FieldCameraCommand(FieldCameraOperation.Wait))).Completed);
    }

    private static void MovieAndBattleOwnership()
    {
        Ff7FieldHost host = Host();
        host.Execute(Request(1, new FieldMovieCommand(FieldMovieOperation.Prepare, 53)));
        FieldHostRequest movie = Request(2, new FieldMovieCommand(FieldMovieOperation.Play));
        Equal(false, host.Execute(movie).Completed);
        FieldHostRequest transition = Request(3, new FieldTransitionCommand(1, 0, 0, 0, 0));
        Equal(false, host.Execute(transition).Completed);
        Equal(false, host.Execute(Request(4, new FieldMovieCommand(FieldMovieOperation.Prepare, 1))).Completed);
        int victories = 0;
        host.CompleteBattle = _ => { victories++; return 0; };
        FieldHostRequest battle = Request(5, new FieldBattleCommand(300));
        Equal(false, host.Execute(battle).Completed);
        Equal(0, victories);
        host.PublishMovieFrame(1799, true);
        Equal(true, host.Execute(movie).Completed);
        Equal(true, host.Execute(battle).Completed);
        Equal(Ff7GlobalCommand.Battle, host.Process.GlobalCommand);
        Equal(false, host.Execute(transition).Completed);
        Ff7FieldHost restored = Host(JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!);
        restored.CompleteBattle = _ => throw new Exception("Battle receipt repeated reward transaction.");
        Equal(true, restored.Execute(battle).Completed);
        Equal(Ff7GlobalCommand.None, restored.Process.GlobalCommand);
        Equal(true, restored.Execute(transition).Completed);
        Equal(1, victories);
    }

    private static void PlayerControlTarget()
    {
        Ff7FieldHost host = Host(new Ff7FieldProcess { Actors = [new() { Entity = 0, Slot = 0, Model = 0, PlayerControlled = true }, new() { Entity = 4, Slot = 1, Model = 0 }] });
        host.Execute(Request(1, new FieldActorProperty(FieldActorPropertyKind.PlayerCharacter, 4)) with { Entity = 9 });
        Equal(4, host.Process.Actors.Single(actor => actor.PlayerControlled).Entity);
    }

    private static void ResetFade()
    {
        Ff7FieldHost host = Host();
        host.Execute(Request(1, new FieldFadeCommand(4)));
        host.Execute(Request(2, new FieldFadeCommand(0, NewFade: true)));
        Equal(0, host.Process.Fade!.Type);
        Equal(true, host.Execute(Request(3, new FieldFadeCommand(0, Wait: true))).Completed);
    }

    private static Ff7FieldHost Host(Ff7FieldProcess? process = null)
    {
        PcWalkmeshTriangle triangle = new(0, new Vector3(0, 0, 0), new Vector3(100, 0, 0), new Vector3(0, 100, 0), -1, -1, -1);
        var field = new Ff7FieldResources("process-fixture", null!, null!, null!, [], [triangle], null!, null!, null!);
        short[] directions = new short[512];
        directions[256 + 1] = -4096;
        directions[384 + 1] = 0;
        directions[1] = 4096;
        process ??= new Ff7FieldProcess { Actors = [new() { Entity = 0, Slot = 0, Model = 0, Direction = 240 }] };
        return new Ff7FieldHost(field, (_, _) => 1, process) { Movement = new([triangle], directions, new byte[129]) };
    }

    private static FieldHostRequest Request(long id, FieldCommand command) => new(id, 0, 7, 16, command);

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new Exception($"Expected {expected}, got {actual}.");
        }
    }
}
