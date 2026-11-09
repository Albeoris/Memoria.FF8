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
        PaletteBufferStrideAndColors();
        NamingMenuOwnership();
        OffsetTimingAndRestore();
        EncounterOwnerRetainsReceipt();
        NativeLineContacts();
        BackgroundTriggerTransitions();
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

    private static void PaletteBufferStrideAndColors()
    {
        ushort[] row = new ushort[256];
        row[0] = 31;
        row[1] = 1024;
        Ff7FieldHost host = Host(new Ff7FieldProcess { PaletteRows = [row], Actors = [] });
        host.Execute(Request(1, new FieldPaletteCommand(FieldPaletteOperation.Store, 0, 0, 256)));
        host.Execute(Request(2, new FieldPaletteCommand(FieldPaletteOperation.Copy, 0, 16, 256)));
        Equal((ushort)31, host.Process.PaletteBuffer[256]);
        host.Execute(Request(3, new FieldPaletteCommand(FieldPaletteOperation.Add, 16, 16, 2, Red: 255, Blue: 255)));
        Equal((ushort)30, host.Process.PaletteBuffer[256]);
        Equal((ushort)32768, host.Process.PaletteBuffer[257]);
        host.Execute(Request(4, new FieldPaletteCommand(FieldPaletteOperation.Multiply, 0, 32, 256, Red: 64, Green: 64, Blue: 64)));
        Equal((ushort)31, host.Process.PaletteBuffer[512]);
        Equal((ushort)1024, host.Process.PaletteBuffer[513]);
        Equal((ushort)0, host.Process.PaletteBuffer[514]);
        var restored = Host(JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!);
        restored.Execute(Request(5, new FieldPaletteCommand(FieldPaletteOperation.Load, 32, 0, 256)));
        Equal(1L, restored.Process.PaletteVersion);
        Equal((ushort)31, restored.Process.PaletteRows[0][0]);
        Equal((ushort)1024, restored.Process.PaletteRows[0][1]);
        Equal((ushort)31, host.Process.PaletteRows[0][0]);
    }

    private static void NamingMenuOwnership()
    {
        Ff7FieldHost host = Host();
        FieldHostRequest request = Request(1, new FieldMenuCommand(6, 0));
        Equal(false, host.Execute(request).Completed);
        Equal(Ff7GlobalCommand.Menu, host.Process.GlobalCommand);
        Equal(false, host.Execute(Request(2, new FieldMovieCommand(FieldMovieOperation.Prepare, 53))).Completed);
        var restored = Host(JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!);
        Equal(false, restored.Execute(request).Completed);
        restored.CompleteMenu(request.Id);
        Equal(true, restored.Execute(request).Completed);
        Equal(Ff7GlobalCommand.None, restored.Process.GlobalCommand);
        Equal(true, restored.Execute(request).Completed);
    }

    private static void OffsetTimingAndRestore()
    {
        Ff7FieldHost host = Host();
        host.Execute(Request(1, new FieldActorOffset(40, -40, 12, 1, 4)));
        FieldHostRequest wait = Request(2, new FieldActorProperty(FieldActorPropertyKind.OffsetWait, 0));
        host.AdvanceActors();
        Equal(0, host.GetActor(0).OffsetX);
        host.AdvanceActors();
        Equal(10, host.GetActor(0).OffsetX);
        var restored = Host(JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!);
        for (int step = 2; step <= 4; step++)
        {
            Equal(false, host.Execute(wait).Completed);
            Equal(false, restored.Execute(wait).Completed);
            host.AdvanceActors();
            restored.AdvanceActors();
            Equal(JsonSerializer.Serialize(host.Capture()), JsonSerializer.Serialize(restored.Capture()));
            Equal(step * 10, host.GetActor(0).OffsetX);
            Equal(-step * 10, host.GetActor(0).OffsetY);
            Equal(0, host.GetActor(0).X4096);
            Equal(0, host.GetActor(0).Y4096);
        }
        Equal(true, host.Execute(wait).Completed);
        Equal(12, host.GetActor(0).OffsetZ);
        host.Execute(Request(3, new FieldActorOffset(0, 0, 0, 0, 0)));
        Equal(0, host.GetActor(0).OffsetX);
    }

    private static void EncounterOwnerRetainsReceipt()
    {
        Ff7FieldHost host = Host();
        int awards = 0;
        host.CompleteBattle = _ => { awards++; return 0; };
        FieldHostRequest request = Request(1L << 60, new FieldBattleCommand(300));
        host.StartEncounter(request, 4);
        host.RetireCompletedRequests(new HashSet<long>());
        Equal(1, host.Capture().Receipts.Length);
        var restored = Host(JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!);
        restored.CompleteBattle = _ => throw new Exception("Random encounter awarded twice after cold restore.");
        restored.ResumeEncounter();
        Equal(Ff7GlobalCommand.None, restored.Process.GlobalCommand);
        Equal(1, awards);
        restored.RetireCompletedRequests(new HashSet<long>());
        Equal(0, restored.Capture().Receipts.Length);
    }

    private static void NativeLineContacts()
    {
        Ff7Actor player = new() { Slot = 0, Entity = 0, Model = 0, CollisionRadius = 30, Direction = 64 };
        Ff7FieldHost host = Host(new Ff7FieldProcess { Actors = [player] });
        host.Execute(Request(1, new FieldLineCommand(FieldLineOperation.Define, 10, -20, 0, 10, 20, 0)) with { Entity = 1 });
        host.Execute(Request(2, new FieldLineCommand(FieldLineOperation.Slip, Value: 1)) with { Entity = 1 });
        host.UpdateLineContacts(player, player with { X4096 = 20 * 4096 }, true);
        Ff7Line line = host.Process.Lines.Single();
        Equal(true, line.Touch && line.Enter && line.Across && line.Push && line.OnLine);
        Equal(true, host.Process.LineBlocked);
        Equal(true, host.RequestLineTalk(player));
        var restored = Host(JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!);
        Equal(true, restored.Process.Lines[0].Talk);
        restored.ClearLineRequests();
        Equal(true, restored.Process.Lines[0].Touch);
        Equal(false, restored.Process.Lines[0].Enter || restored.Process.Lines[0].Across || restored.Process.Lines[0].Talk);
        restored.UpdateLineContacts(player with { X4096 = 50 * 4096 }, player with { X4096 = 50 * 4096 }, false);
        Equal(true, restored.Process.Lines[0].Leave);
        Equal(false, restored.Process.Lines[0].Touch || restored.Process.LineBlocked);
        restored.Execute(Request(3, new FieldLineCommand(FieldLineOperation.Enable, Value: 0)) with { Entity = 1 });
        Equal((byte)0, restored.Process.Lines[0].Enabled);
    }

    private static void BackgroundTriggerTransitions()
    {
        PcWalkmeshTriangle triangle = new(0, new Vector3(0, 0, 0), new Vector3(100, 0, 0), new Vector3(0, 100, 0), -1, -1, -1);
        PcFieldTrigger trigger = new(new Vector3(10, -20, 0), new Vector3(10, 20, 0), 1, 0, 2, 1);
        PcFieldTriggers triggers = new("trigger-fixture", 0, 0, 0, 0, 0, 0, [], [], [trigger], []);
        var field = new Ff7FieldResources("trigger-fixture", null!, null!, null!, [], [triangle], triggers, null!, null!);
        var host = new Ff7FieldHost(field, (_, _) => 1) { Movement = Host().Movement };
        List<FieldAudioCommand> sounds = [];
        host.AudioRequested = sounds.Add;
        host.InitializeBackgroundTriggers();
        Equal((ushort)0, host.Process.BackgroundStates[1]);
        Ff7Actor player = new() { Slot = 0, Entity = 0, Model = 0, CollisionRadius = 30, Direction = 64 };
        host.UpdateBackgroundTriggers(player);
        Equal((ushort)1, host.Process.BackgroundStates[1]);
        Equal(1, sounds.Count);
        Equal(54, sounds[0].Parameter1);
        host.UpdateBackgroundTriggers(player);
        Equal(1, sounds.Count);
        host.UpdateBackgroundTriggers(player with { X4096 = 50 * 4096 });
        Equal((ushort)0, host.Process.BackgroundStates[1]);
        Equal(2, sounds.Count);
        Equal(false, host.Process.PlayerLocked);
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
