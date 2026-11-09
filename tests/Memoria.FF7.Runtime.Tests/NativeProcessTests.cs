using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeProcessTests
{
    public static void RunViewOffset()
    {
        ViewOffsetTimingAndRestore();
        NativeProjectedCameraTracking();
    }

    public static void RunPalette()
    {
        PaletteBufferStrideAndColors();
        InstalledCachedPaletteInitializationAndCompatibility();
    }

    public static void Run()
    {
        TurnTimingAndRestore();
        CoordinateCameraAndWait();
        MovieAndBattleOwnership();
        PlayerControlTarget();
        ResetFade();
        PaletteBufferStrideAndColors();
        InstalledCachedPaletteInitializationAndCompatibility();
        NamingMenuOwnership();
        OffsetTimingAndRestore();
        EncounterOwnerRetainsReceipt();
        NativeLineContacts();
        BackgroundTriggerTransitions();
        ModelEffectHostOwnership();
        JoinTimingAndRestore();
        FaceActorTimingAndRestore();
        PositionQueryUsesPhysicalCoordinates();
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

    private static void ViewOffsetTimingAndRestore()
    {
        Ff7FieldHost host = Host();
        Equal((short?)0, host.Process.CameraFocusOffset);
        Equal(true, host.Execute(Request(700, new FieldCameraCommand(FieldCameraOperation.ViewOffset, X: 90))).Completed);
        Equal((short?)90, host.Process.CameraFocusOffset);
        host.Execute(Request(701, new FieldCameraCommand(FieldCameraOperation.ViewOffset, 1, -300, Steps: 4)));
        host.AdvanceActors();
        Equal((short?)90, host.Process.CameraFocusOffset);
        host.AdvanceActors();
        Equal((short?)249, host.Process.CameraFocusOffset);
        Ff7FieldHost restored = Host(JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!);
        foreach (short expected in new short[] { 151, 54, 212, 212 })
        {
            host.AdvanceActors();
            restored.AdvanceActors();
            Equal((short?)expected, host.Process.CameraFocusOffset);
            Equal(JsonSerializer.Serialize(host.Capture()), JsonSerializer.Serialize(restored.Capture()));
        }
        Equal((byte)3, host.Process.CameraViewOffset!.Mode);
        host.Execute(Request(702, new FieldCameraCommand(FieldCameraOperation.ViewOffset, X: -300)));
        Equal((short?)-300, host.Process.CameraFocusOffset);
        Equal<Ff7CameraViewOffset?>(null, host.Process.CameraViewOffset);
    }

    private static void NativeProjectedCameraTracking()
    {
        PcFieldCamera camera = new(new Vector3(4096, 0, 0), new Vector3(0, 0, 4096), new Vector3(0, 4096, 0), 0, new Vector3(0, 0, 1000), 0, 1000);
        PcFieldTriggers triggers = new("camera-fixture", 0, 0, -256, -256, 256, 256, [], [], [], []);
        Ff7NativeCamera projection = new(camera, triggers);
        Ff7Actor actor = new() { Slot = 0, Entity = 0, Model = 0, X4096 = 40 * 4096, Z4096 = 10 * 4096 };
        Equal(((short)-40, (short)-10), projection.Follow(actor, 0));
        Equal(((short)-40, (short)-100), projection.Follow(actor, 90));
        Equal(((short)-96, (short)-136), projection.Follow(actor with { X4096 = 1000 * 4096, Z4096 = 1000 * 4096 }, 0));
        PcFieldTriggers diagonal = triggers with { CameraLeft = -320, CameraBottom = -280, CameraRight = 320, CameraTop = 280, CameraScrollConstraint = 1 };
        Equal(((short)-50, (short)-50), new Ff7NativeCamera(camera, diagonal).Follow(actor with { X4096 = 100 * 4096, Z4096 = 0 }, 0));
        Equal(((short)-50, (short)50), new Ff7NativeCamera(camera, diagonal with { CameraScrollConstraint = 2 }).Follow(actor with { X4096 = 100 * 4096, Z4096 = 0 }, 0));
        Ff7FieldHost legacy = Host(new() { Actors = [actor], CameraEntity = 0, CameraX = 12, CameraY = 34 });
        Equal((bool?)false, legacy.Process.CameraFollowPlayer);
        Equal(0, legacy.Process.CameraTracking!.Slot);
        Equal(1, legacy.Process.CameraTracking.Mode);
        Equal(12, legacy.Process.CameraX);
        Equal(34, legacy.Process.CameraY);
        Ff7FieldHost host = Host(new() { Actors = [actor], CameraFocusOffset = 90 });
        host.CameraProjection = projection;
        host.Execute(Request(710, new FieldCameraCommand(FieldCameraOperation.Entity, 2, Steps: 4, Entity: 0)));
        host.AdvanceActors();
        Equal(0, host.Process.CameraX);
        Equal(0, host.Process.CameraY);
        host.AdvanceActors();
        Equal(-10, host.Process.CameraX);
        Equal(-25, host.Process.CameraY);
        Ff7FieldHost restored = Host(JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!);
        restored.CameraProjection = projection;
        host.Execute(Request(711, new FieldActorPlacement(80, 0, 10, 0)));
        restored.Execute(Request(711, new FieldActorPlacement(80, 0, 10, 0)));
        for (int tick = 2; tick <= 4; tick++)
        {
            host.AdvanceActors();
            restored.AdvanceActors();
            Equal(-20 * tick, host.Process.CameraX);
            Equal(-25 * tick, host.Process.CameraY);
            Equal(JsonSerializer.Serialize(host.Capture()), JsonSerializer.Serialize(restored.Capture()));
        }
        Equal(true, host.Execute(Request(712, new FieldCameraCommand(FieldCameraOperation.Wait))).Completed);
        host.Execute(Request(713, new FieldActorPlacement(0, 0, 10, 0)));
        host.AdvanceActors();
        Equal(-80, host.Process.CameraX);
        host.Execute(Request(714, new FieldCameraCommand(FieldCameraOperation.Entity, 1, Entity: 0)));
        host.AdvanceActors();
        Equal(0, host.Process.CameraX);
        Equal(-100, host.Process.CameraY);
        Equal(false, host.Execute(Request(715, new FieldCameraCommand(FieldCameraOperation.Wait))).Completed);
        host.Execute(Request(716, new FieldCameraCommand(FieldCameraOperation.Entity, 2, Steps: 0, Entity: 0)));
        host.AdvanceActors();
        Equal(-100, host.Process.CameraY);
        Equal(true, host.Execute(Request(717, new FieldCameraCommand(FieldCameraOperation.Wait))).Completed);
        host.Execute(Request(718, new FieldActorPlacement(80, 0, 10, 0)));
        host.Execute(Request(719, new FieldCameraCommand(FieldCameraOperation.Entity, 3, Steps: 2, Entity: 0)));
        host.AdvanceActors();
        Equal(0, host.Process.CameraX);
        host.AdvanceActors();
        Equal(-40, host.Process.CameraX);
        host.AdvanceActors();
        Equal(-80, host.Process.CameraX);
        Ff7FieldHost player = Host(new() { Actors = [actor with { PlayerControlled = true }] });
        player.CameraProjection = projection;
        player.AdvanceActors();
        Equal(-10, player.Process.CameraY);
        player.Execute(Request(720, new FieldCameraCommand(FieldCameraOperation.ViewOffset, X: 90)));
        player.AdvanceActors();
        Equal(-100, player.Process.CameraY);
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

    private static void InstalledCachedPaletteInitializationAndCompatibility()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            return;
        }
        var installation = Ff7Installation.Discover(root);
        foreach (string name in new[] { "md1stin", "nmkin_4" })
        {
            Ff7FieldResources field = Ff7FieldResources.Load(installation, name);
            int row = Enumerable.Range(0, field.Palettes.Palettes.Count).First(index => field.Background.PaletteHasTransparentColor[index] != 0 && field.Palettes.Palettes[index][0] != 0);
            ushort first = field.Palettes.Palettes[row][0];
            int exchanged = Enumerable.Range(1, 255).First(index => field.Palettes.Palettes[row][index] == 0);
            Ff7FieldHost host = new(field, (_, _) => 1);
            Equal(true, host.Process.PaletteRowsUseCachedIndices);
            Equal((ushort)0, host.Process.PaletteRows[row][0]);
            Equal(first, host.Process.PaletteRows[row][exchanged]);
            host.Execute(Request(201, new FieldPaletteCommand(FieldPaletteOperation.Store, row, 0, 256)));
            Equal((ushort)0, host.Process.PaletteBuffer[0]);
            Equal(first, host.Process.PaletteBuffer[exchanged]);
            host.Execute(Request(202, new FieldPaletteCommand(FieldPaletteOperation.Copy, 0, 16, 256)));
            Equal(first, host.Process.PaletteBuffer[256 + exchanged]);
            host.Execute(Request(203, new FieldPaletteCommand(FieldPaletteOperation.Add, 16, 16, 256, Red: 1)));
            ushort added = (ushort)((first & ~31) | Math.Min(31, (first & 31) + 1));
            Equal(added, host.Process.PaletteBuffer[256 + exchanged]);
            host.Execute(Request(204, new FieldPaletteCommand(FieldPaletteOperation.Multiply, 0, 32, 256, Red: 0, Green: 0, Blue: 0)));
            Equal((ushort)0, host.Process.PaletteBuffer[512]);
            Equal((ushort)32768, host.Process.PaletteBuffer[512 + exchanged]);
            host.Execute(Request(205, new FieldPaletteCommand(FieldPaletteOperation.Load, 32, row, 256)));
            Equal((ushort)0, host.Process.PaletteRows[row][0]);
            Equal((ushort)32768, host.Process.PaletteRows[row][exchanged]);
            Ff7FieldProcess saved = JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!;
            Ff7FieldHost restored = new(field, (_, _) => 1, saved);
            Equal(JsonSerializer.Serialize(saved), JsonSerializer.Serialize(restored.Capture()));
            var legacy = saved with { Tick = 991, PaletteRowsUseCachedIndices = false, PaletteRows = field.Palettes.Palettes.Select(colors => colors.ToArray()).ToArray() };
            string original = JsonSerializer.Serialize(legacy);
            Ff7FieldHost compatible = new(field, (_, _) => 1, legacy);
            Equal(true, compatible.Process.PaletteRowsUseCachedIndices);
            Equal(991L, compatible.Process.Tick);
            Equal(legacy.PaletteVersion + 1, compatible.Process.PaletteVersion);
            Equal((ushort)0, compatible.Process.PaletteRows[row][0]);
            Equal(first, compatible.Process.PaletteRows[row][exchanged]);
            Equal(true, compatible.Process.PaletteBuffer.All(color => color == 0));
            Equal(original, JsonSerializer.Serialize(legacy));
            Equal(JsonSerializer.Serialize(legacy.Receipts), JsonSerializer.Serialize(compatible.Process.Receipts));
        }
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

    private static void ModelEffectHostOwnership()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(game))
        {
            return;
        }
        Ff7FieldResources field = Ff7FieldResources.Load(Ff7Installation.Discover(game), "nmkin_1");
        Ff7Actor actor = new() { Slot = 0, Entity = 0, Model = 8, X4096 = 385 * 4096, Y4096 = 3125 * 4096, Z4096 = -272 * 4096, Visible = false };
        Ff7FieldHost Build(Ff7FieldProcess process) => new(field, (_, _) => 1, process) { ModelPartVertexCounts = _ => new[] { 16, 42, 16, 33 } };
        Ff7FieldHost host = Build(new() { Actors = [actor] });
        Equal(true, host.Execute(Request(100, new FieldModelEffectCommand(13, 0))).Completed);
        Equal(false, host.GetActor(0).ModelEffect!.Initialized);
        host.AdvanceActors();
        Equal(true, host.GetActor(0).ModelEffect!.Initialized);
        Ff7FieldHost restored = Build(JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!);
        for (int tick = 0; tick < 120; tick++)
        {
            host.AdvanceActors();
            restored.AdvanceActors();
            Equal(JsonSerializer.Serialize(host.Capture()), JsonSerializer.Serialize(restored.Capture()));
        }
        Equal(true, host.GetActor(0).ModelEffect!.Applied);
        Equal(true, host.Execute(Request(101, new FieldModelEffectCommand(13, 2))).Completed);
        FieldHostRequest wait = Request(102, new FieldModelEffectCommand(13, Wait: true));
        Equal(false, host.Execute(wait).Completed);
        host.AdvanceActors();
        Equal(false, host.Execute(wait).Completed);
        host.AdvanceActors();
        Equal(true, host.Execute(wait).Completed);
        Equal(true, host.GetActor(0).ModelEffect is null);
        PcFieldCamera gateCamera = new(Vector3.UnitX * 4096, Vector3.UnitY * 4096, Vector3.UnitZ * 4096, 0, new Vector3(0, 0, 15360), 0, 240);
        Ff7Actor gated = actor with { Z4096 = 10 * 4096, OffsetZ = -20000, ModelEffect = Ff7NativeModelEffect.Request(null, 0, new[] { 16, 42, 16, 33 }) };
        Ff7FieldHost rejected = new(field with { Cameras = new[] { gateCamera } }, (_, _) => 1, new() { Actors = [gated] });
        rejected.AdvanceActors();
        Equal(false, rejected.GetActor(0).ModelEffect!.Initialized);
        Ff7FieldHost accepted = new(field with { Cameras = new[] { gateCamera with { Translation = new Vector3(0, 0, 15359) } } }, (_, _) => 1, new() { Actors = [gated] });
        accepted.AdvanceActors();
        Equal(true, accepted.GetActor(0).ModelEffect!.Initialized);
    }

    private static void JoinTimingAndRestore()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(game))
        {
            return;
        }
        Ff7PcExecutableData native = Ff7PcExecutableData.Load(Ff7Installation.Discover(game));
        PcWalkmeshTriangle triangle = new(0, Vector3.Zero, new(0, 100, 0), new(100, 0, 0), -1, -1, -1);
        Ff7FieldResources field = new("join-fixture", null!, null!, null!, [], [triangle], null!, null!, null!);
        Ff7FieldHost Build(Ff7FieldProcess process) => new(field, (_, _) => 8, process) { Movement = new([triangle], native.NativeSinCos, native.NativeArcAngles) };
        Ff7FieldHost host = Build(new()
        {
            Actors =
            [
                new() { Slot = 0, Entity = 0, Model = 0, PartyCharacter = 0, X4096 = 60 * 4096, Y4096 = 10 * 4096, CollisionEnabled = true },
                new() { Slot = 1, Entity = 1, Model = 1, PartyCharacter = 1, X4096 = 20 * 4096, Y4096 = 10 * 4096, Direction = 240, MoveSpeed = 1024 },
                new() { Slot = 2, Entity = 2, Model = 2, PartyCharacter = 2, X4096 = 20 * 4096, Y4096 = 30 * 4096, Direction = 0, MoveSpeed = 2048 }
            ]
        });
        FieldHostRequest request = Request(200, new FieldPartyJoin(30, 0, 1, 2));
        Equal(false, host.Execute(request).Completed);
        Equal(2, host.Process.Join!.Followers[0].Stage);
        Equal(true, Math.Abs(host.GetActor(1).TurnEnd - 240) <= 128);
        for (int tick = 0; tick < 8; tick++)
        {
            host.AdvanceActors();
            Equal(false, host.Execute(request).Completed);
        }
        Ff7Actor leader = host.GetActor(0);
        host.ApplyPlayerMovement(new(leader with { X4096 = 70 * 4096 }, false, true, []));
        Ff7FieldHost restored = Build(JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!);
        bool completed = false;
        for (int tick = 0; tick < 100 && !completed; tick++)
        {
            host.AdvanceActors();
            restored.AdvanceActors();
            completed = host.Execute(request).Completed;
            Equal(completed, restored.Execute(request).Completed);
            Equal(JsonSerializer.Serialize(host.Capture()), JsonSerializer.Serialize(restored.Capture()));
        }
        Equal(true, completed);
        Equal(true, host.Process.Join is null);
        Equal(true, host.GetActor(0).CollisionEnabled);
        foreach (int entity in new[] { 1, 2 })
        {
            Ff7Actor follower = host.GetActor(entity);
            Equal(70 * 4096, follower.X4096);
            Equal(10 * 4096, follower.Y4096);
            Equal(false, follower.Visible || follower.TalkEnabled || follower.CollisionEnabled);
            Equal(entity == 1 ? 1024 : 2048, follower.MoveSpeed);
        }
        FieldHostRequest split = Request(201, new FieldPartySplit(20, 0, 1, 2, 30, 10, 0, 30, 30, 32, new(FieldBank.MapByte, 0)));
        byte lateDirection = 0;
        host.ReadFieldByte = _ => lateDirection;
        Equal(false, host.Execute(split).Completed);
        Equal(true, host.Process.Join!.Split);
        Equal(70 * 4096, host.GetActor(1).X4096);
        Equal(true, host.GetActor(1).Visible);
        for (int tick = 0; tick < 10; tick++)
        {
            host.AdvanceActors();
            Equal(false, host.Execute(split).Completed);
        }
        lateDirection = 240;
        restored = Build(JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!);
        restored.ReadFieldByte = _ => lateDirection;
        completed = false;
        for (int tick = 0; tick < 100 && !completed; tick++)
        {
            host.AdvanceActors();
            restored.AdvanceActors();
            completed = host.Execute(split).Completed;
            Equal(completed, restored.Execute(split).Completed);
            Equal(JsonSerializer.Serialize(host.Capture()), JsonSerializer.Serialize(restored.Capture()));
        }
        Equal(true, completed);
        foreach (int entity in new[] { 1, 2 })
        {
            Ff7Actor follower = host.GetActor(entity);
            Equal(30 * 4096, follower.X4096);
            Equal((entity == 1 ? 10 : 30) * 4096, follower.Y4096);
            Equal(entity == 1 ? 240 : 32, follower.Direction);
            Equal(true, follower.Visible && follower.TalkEnabled && follower.CollisionEnabled);
            Equal(entity == 1 ? 1024 : 2048, follower.MoveSpeed);
        }
    }

    private static void FaceActorTimingAndRestore()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(game))
        {
            return;
        }
        Ff7PcExecutableData native = Ff7PcExecutableData.Load(Ff7Installation.Discover(game));
        PcWalkmeshTriangle triangle = new(0, Vector3.Zero, new(0, 100, 0), new(100, 0, 0), -1, -1, -1);
        Ff7FieldHost Build(Ff7FieldProcess process)
        {
            Ff7FieldHost result = Host(process);
            result.Movement = new([triangle], native.NativeSinCos, native.NativeArcAngles);
            return result;
        }
        Ff7FieldProcess process = new() { Actors = [new() { Slot = 0, Entity = 0, Model = 0, Direction = 240 }, new() { Slot = 1, Entity = 1, Model = 1 }] };
        foreach (byte policy in new byte[] { 0, 1, 2 })
        {
            Ff7FieldHost host = Build(process);
            FieldHostRequest request = Request(300, new FieldActorFace(1, 4, policy));
            Equal(false, host.Execute(request).Completed);
            Equal(policy == 0 ? 448 : 192, host.GetActor(0).TurnEnd);
            host.AdvanceActors();
            Ff7Actor target = host.GetActor(1);
            host.ApplyPlayerMovement(new(target with { X4096 = 50 * 4096 }, false, true, []));
            Equal(false, host.Execute(request).Completed);
            Ff7FieldHost restored = Build(JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!);
            bool complete = false;
            for (int tick = 0; tick < 4; tick++)
            {
                host.AdvanceActors();
                restored.AdvanceActors();
                complete = host.Execute(request).Completed;
                Equal(complete, restored.Execute(request).Completed);
                Equal(JsonSerializer.Serialize(host.Capture()), JsonSerializer.Serialize(restored.Capture()));
            }
            Equal(true, complete);
            Equal(192, host.GetActor(0).Direction);
        }
    }

    private static void PositionQueryUsesPhysicalCoordinates()
    {
        Ff7Actor actor = new() { Slot = 0, Entity = 0, Model = 0, X4096 = -123 * 4096 - 1, Y4096 = 456 * 4096 + 2048, Z4096 = -7 * 4096 + 100, Triangle = 321, OffsetX = 50, OffsetY = 60, OffsetZ = 70 };
        Ff7FieldHost host = Host(new() { Actors = [actor] });
        FieldHostReply reply = host.Execute(Request(600, new FieldActorQuery(FieldActorQueryKind.Position, 0)));
        Equal(true, reply.Completed);
        Equal(new FieldActorPosition(-124, 456, -7, 321), reply.Position);
        Ff7FieldHost restored = Host(JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!);
        Equal(reply, restored.Execute(Request(600, new FieldActorQuery(FieldActorQueryKind.Position, 0))));
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
