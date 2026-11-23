using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeMovieCameraServiceTests
{
    public static void Run()
    {
        PcFieldCamera fieldCamera = new(Vector3.UnitX * 4096, Vector3.UnitY * 4096, Vector3.UnitZ * 4096, 0, new(0, 0, 1000), 0, 200);
        PcMovieCamera[] rows = Enumerable.Range(0, 3).Select(index => new PcMovieCamera(fieldCamera with { FocalLength = checked((short)(200 + index)) }, 0)).ToArray();
        Ff7FieldHost Host(Ff7FieldProcess? process = null)
        {
            Ff7FieldResources field = new("movie-camera-fixture", null!, null!, null!, [fieldCamera], [], null!, null!, null!);
            return new(field, (_, _) => 1, process ?? new() { BlinkStateInitialized = true, Movie = new(53, true, 0, false, 1), GlobalCommand = Ff7GlobalCommand.Movie, GlobalRequestId = 1 });
        }
        Ff7FieldHost background = Host(new() { BlinkStateInitialized = true });
        Check(background.Execute(new(1, 0, 7, 0, new FieldMovieCommand(FieldMovieOperation.SetBackground, 1))).Completed && background.Process is { MovieBackgroundEnabled: true, Movie: null, GlobalCommand: Ff7GlobalCommand.None }, "Setting the native movie background must work before a movie is prepared.");
        Throws<InvalidDataException>(() => background.Execute(new(2, 0, 7, 2, new FieldMovieCommand(FieldMovieOperation.Play))));
        background = Host(JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(background.Capture()))!);
        Check(background.Execute(new(3, 0, 7, 5, new FieldMovieCommand(FieldMovieOperation.Prepare, 20))).Completed && background.Process is { MovieBackgroundEnabled: true, Movie.PreparedId: 20 }, "Preparing a movie must keep its independently configured background after restore.");
        Ff7FieldHost host = Host();
        host.NativeMovieCameraRecordCount = () => 3;
        host.PublishMovieFrame(0, false);
        Check(host.Process.Movie!.CameraConsumption == new Ff7MovieCameraConsumption(0, 0, 10), "Native movie publication did not own the first camera read budget.");
        string published = JsonSerializer.Serialize(host.Capture());
        host.PublishMovieFrame(0, false);
        Check(published == JsonSerializer.Serialize(host.Capture()), "Repeated native frame publication consumed a second camera row.");
        Check(host.PublishMovieCameraFrame(0, 3) == 0 && published == JsonSerializer.Serialize(host.Capture()), "An existing presentation publisher did not deduplicate against the native budget owner.");
        host.PublishMovieFrame(1, false);
        Ff7FieldHost cold = Host(JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!);
        cold.NativeMovieCameraRecordCount = () => 3;
        cold.PublishMovieFrame(2, true);
        Check(cold.Process.Movie is { Playing: false, Completed: true, CameraConsumption: { Frame: 2, WordsBeforeRead: 20, WordsAfterRead: 30 } }, "Terminal camera publication must occur before clearing the active movie owner.");

        Ff7FieldHost unload = Host(cold.Capture() with { MovieCameraDisabled = true });
        FieldHostRequest request = new(2, 0, 7, 10, new FieldMenuCommand(20, 255));
        Check(!unload.Execute(request).Completed && unload.Process.Movie == cold.Process.Movie, "Movie unload must first yield to its native service.");
        unload.AdvanceNativeMenu();
        Check(unload.Process.Movie is null && unload.Process.MovieCameraDisabled && unload.Process.GlobalCommand == Ff7GlobalCommand.Menu, "Movie unload changed MVCAM or released the menu owner before acknowledgement.");
        unload.AdvanceNativeMenu();
        Check(unload.Execute(request).Completed && unload.Process.GlobalCommand == Ff7GlobalCommand.None, "The owned movie unload failed to acknowledge completion.");

        host = Host();
        host.NativeMovieCameraRecordCount = () => -1;
        string before = JsonSerializer.Serialize(host.Capture());
        Throws<InvalidDataException>(() => host.PublishMovieFrame(1, false));
        Check(before == JsonSerializer.Serialize(host.Capture()), "Invalid camera metadata changed the movie frame before validation.");
        host = Host();
        host.NativeMovieCameraRecordCount = () => throw new NotSupportedException("Unproved missing native camera member.");
        before = JsonSerializer.Serialize(host.Capture());
        Throws<NotSupportedException>(() => host.PublishMovieFrame(1, false));
        Check(before == JsonSerializer.Serialize(host.Capture()), "Missing camera metadata changed its movie owner.");

        host = Host(new() { BlinkStateInitialized = true, Movie = new(53, true, 0, false, 1), MovieCameraDisabled = true, GlobalCommand = Ff7GlobalCommand.Movie, GlobalRequestId = 1 });
        int reads = 0;
        host.NativeMovieCameraRecordCount = () =>
        {
            reads++;
            return 3;
        };
        host.PublishMovieFrame(1, false);
        Check(reads == 0 && host.Process.Movie!.CameraConsumption!.WordsAfterRead == 0, "Disabled native movie cameras loaded or consumed rows.");
        host.Execute(new(2, 0, 7, 0, new FieldControlCommand(FieldControlKind.MovieCameraDisabled, 0)));
        host.PublishCurrentNativeMovieCameraFrame();
        Check(reads == 1 && host.Process.Movie!.CameraConsumption == new Ff7MovieCameraConsumption(1, 0, 10), "Enabling cameras inside the VM must publish the current unchanged movie frame at the native update boundary.");
        Ff7MovieCameraTrack track = new(rows);
        before = JsonSerializer.Serialize(host.Capture());
        Check(Ff7NativeMovieCamera.ResolvePresentationCamera(host.Process, fieldCamera, track.SnapshotAt) == rows[1].Camera, "Read-only native camera selection confused absolute frame and consumed budget.");
        Check(before == JsonSerializer.Serialize(host.Capture()), "Read-only native camera selection advanced its saved budget.");
        track.Publish(0, 0);
        _ = track.SnapshotAt(1, 10);
        Check(track.CameraAt(0) == rows[0], "Snapshot lookup changed the track's published owner.");
        Ff7MovieCameraTrack empty = new([]);
        Check(Ff7NativeMovieCamera.ResolvePresentationCamera(host.Process, fieldCamera, empty.SnapshotAt)!.AxisZ == Vector3.Zero, "A present empty native camera track must retain its explicit blank-camera gate.");
        Throws<NotSupportedException>(() => Ff7NativeMovieCamera.ResolvePresentationCamera(host.Process, fieldCamera, null));
        Check(Ff7NativeMovieCamera.ResolvePresentationCamera(host.Process with { MovieCameraDisabled = true }, fieldCamera, null) == fieldCamera, "Native MVCAM disable must use the ordinary field camera without requiring a camera member.");
        ModelEffectCamera();
        Installed();
        InstalledModelEffectCheckpoint();
        InstalledMissingCameraCheckpoint();
    }

    private static void ModelEffectCamera()
    {
        PcFieldCamera far = new(Vector3.UnitX * 4096, Vector3.UnitY * 4096, Vector3.UnitZ * 4096, 0, new(0, 0, 15360), 0, 200);
        PcFieldCamera near = far with { Translation = new(0, 0, 15356) };
        Ff7Actor actor = new()
        {
            Slot = 0,
            Entity = 0,
            Model = 0,
            Z4096 = 10 * 4096,
            NativeEyeControlPending = true,
            NativeColorOffset = Ff7NativeColorOffset.Request(null, [1, 0, 2, 0, 3, 0]),
            ModelEffect = Ff7NativeModelEffect.Request(null, 0, [35])
        };
        Ff7FieldProcess initial = new() { BlinkStateInitialized = true, Actors = [actor], Movie = new(53, true, 0, false, 1), GlobalCommand = Ff7GlobalCommand.Movie, GlobalRequestId = 1 };
        Ff7FieldHost Host(PcFieldCamera fieldCamera, Ff7FieldProcess process)
        {
            Ff7FieldResources field = new("movie-model-camera-fixture", null!, null!, null!, [fieldCamera], [], null!, null!, null!);
            return new(field, (_, _) => 1, process);
        }
        Ff7MovieCameraTrack track = new([new(near, 0), new(far, 0)]);
        Ff7FieldHost host = Host(far, initial);
        int reads = 0;
        host.NativeBlinkCamera = () =>
        {
            reads++;
            return Ff7NativeMovieCamera.ResolvePresentationCamera(host.Process, far, track.SnapshotAt);
        };
        Ff7MovieCameraConsumption? budget = host.Process.Movie!.CameraConsumption;
        host.AdvanceActors();
        Check(reads == 1 && host.GetActor(0) is { NativeEyeControlPending: false, NativeColorOffset.Pending: false, ModelEffect.Initialized: true }, "Movie model effects did not use the current native camera below depth3840.");
        Check(host.Process.Movie!.CameraConsumption == budget, "Movie model effects consumed a camera row.");
        host = Host(near, initial with { Movie = initial.Movie! with { Frame = 1, CameraConsumption = new(1, 0, 10) } });
        host.NativeBlinkCamera = () => Ff7NativeMovieCamera.ResolvePresentationCamera(host.Process, near, track.SnapshotAt);
        Ff7FieldHost cold = Host(near, JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!);
        cold.NativeBlinkCamera = () => Ff7NativeMovieCamera.ResolvePresentationCamera(cold.Process, near, track.SnapshotAt);
        host.AdvanceActors();
        cold.AdvanceActors();
        Check(host.GetActor(0) is { NativeEyeControlPending: true, NativeColorOffset.Pending: true, ModelEffect.Initialized: false }, "Depth3840 from the movie camera did not suspend model effects.");
        Check(JsonSerializer.Serialize(host.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Cold movie model-effect continuation changed the next callback.");
        foreach (Ff7FieldProcess process in new[]
        {
            initial with { MovieCameraDisabled = true },
            initial with { Movie = initial.Movie! with { CameraDisabled = true } },
            initial with { Movie = initial.Movie! with { Playing = false } },
            initial with { Movie = initial.Movie! with { Playing = false, Completed = true } }
        })
        {
            host = Host(near, process);
            host.NativeBlinkCamera = () => throw new InvalidOperationException("An inactive movie camera was read.");
            host.AdvanceActors();
            Check(!host.GetActor(0).NativeEyeControlPending, "Prepared, disabled or completed movies must retain the ordinary field-camera effect gate.");
        }
        host = Host(near, initial);
        Throws<NotSupportedException>(host.AdvanceActors);
        Check(host.GetActor(0).NativeEyeControlPending, "A missing required movie camera advanced model effects.");
    }

    private static void Installed()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP installed native movie camera service: optional original resources are unset.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(root);
        Ff7MovieResources source = Ff7MovieResources.Load(installation, 53);
        Check(source.Cameras?.Count == 1792, $"The original opening camera resource binding differs: {Path.GetFileName(source.Path)} cameraCount={source.Cameras?.Count}.");
        using Ff7Session session = Ff7Session.NewGame(installation);
        for (int tick = 0; tick < 12 && session.Host.Process.Movie is not { Playing: true }; tick++)
        {
            session.AdvanceTick();
        }
        Ff7Movie movie = session.Host.Process.Movie is { Playing: true } active ? active : throw new InvalidOperationException("The bounded original initializer did not start its opening movie.");
        Check(movie.PreparedId == 53 && session.Host.NativeBlinkCamera is not null && session.Host.NativeMovieCameraRecordCount is not null, "The headless session lacks its default native camera resources.");
        string before = JsonSerializer.Serialize(session.Capture());
        Check(session.ReadNativeMovieCamera() == source.Cameras![movie.Frame].Camera, "Headless native blinking did not resolve the original current camera.");
        Check(before == JsonSerializer.Serialize(session.Capture()), "Headless native camera observation changed gameplay or its native budget.");
        session.Host.PublishMovieFrame(1, false);
        using Ff7Session cold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(session.Capture()))!);
        Check(session.ReadNativeMovieCamera() == cold.ReadNativeMovieCamera(), "Cold headless native camera resolution changed.");
        Check(cold.Host.Process.Movie!.CameraConsumption == session.Host.Process.Movie!.CameraConsumption, "Cold camera observation consumed a saved row.");
    }

    private static void InstalledModelEffectCheckpoint()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_MOVIE_MODEL_EFFECT_SAVE");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP installed movie model-effect continuation: optional original resources or standalone fixture are unset.");
            return;
        }
        byte[] original = File.ReadAllBytes(path);
        using MemoryStream input = new(original);
        Ff7SessionSave saved = CombinedSessionCodec.Read(input).Ff7 ?? throw new InvalidDataException("The movie model-effect fixture lacks its FFVII session.");
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session session = Ff7Session.Restore(installation, saved);
        using Ff7Session cold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(saved))!);
        long tick = session.Host.Process.Tick;
        session.AdvanceTick();
        cold.AdvanceTick();
        Check(session.Host.Process.Tick == tick + 1, "The original movie-entry fixture did not complete its first neutral callback.");
        Check(JsonSerializer.Serialize(session.Capture()) == JsonSerializer.Serialize(cold.Capture()), "The original movie-entry fixture changed its first callback after cold restore.");
        Check(File.ReadAllBytes(path).SequenceEqual(original), "Movie model-effect validation changed the original standalone fixture.");
        Console.WriteLine($"PASS original movie model-effect first callback: field={session.Field.Name} tick={session.Host.Process.Tick} movie={session.Host.Process.Movie?.PreparedId} playing={session.Host.Process.Movie?.Playing} cameraDisabled={session.Host.Process.MovieCameraDisabled}");
    }

    private static void InstalledMissingCameraCheckpoint()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_MISSING_MOVIE_CAMERA_SAVE");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP installed missing movie camera: optional original resources or standalone fixture are unset.");
            return;
        }
        byte[] original = File.ReadAllBytes(path);
        using MemoryStream input = new(original);
        Ff7SessionSave saved = CombinedSessionCodec.Read(input).Ff7 ?? throw new InvalidDataException("The movie camera fixture lacks its FFVII session.");
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session session = Ff7Session.Restore(installation, saved);
        using Ff7Session cold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(saved))!);
        long tick = session.Host.Process.Tick;
        for (int callback = 0; callback < 2; callback++)
        {
            session.AdvanceTick();
            cold.AdvanceTick();
            Check(JsonSerializer.Serialize(session.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Cold original missing-camera continuation changed its next callback.");
        }
        Ff7Movie movie = session.Host.Process.Movie is { Playing: true, CameraDisabled: false } active ? active : throw new InvalidOperationException("The original fixture did not start its enabled movie camera owner.");
        Ff7MovieResources source = Ff7MovieResources.Load(installation, movie.PreparedId);
        Check(source.Cameras is null && !session.Host.Process.MovieCameraDisabled, "The original fixture no longer exercises its unavailable native CAM member.");
        PcFieldCamera blank = new(default, default, default, 0, default, 0, 0);
        string before = JsonSerializer.Serialize(session.Capture());
        Check(session.ReadNativeMovieCamera() == blank && cold.ReadNativeMovieCamera() == blank, "Unavailable native CAM must produce the original zero camera, rather than the field camera or a fabricated matrix.");
        Check(before == JsonSerializer.Serialize(session.Capture()), "Reading the missing native camera changed its saved owners.");
        Check(movie.CameraConsumption is { WordsBeforeRead: 0, WordsAfterRead: 0 }, "An unavailable native CAM consumed a camera row.");
        int frame = movie.Frame + 1;
        session.Host.PublishMovieFrame(frame, false);
        cold.Host.PublishMovieFrame(frame, false);
        before = JsonSerializer.Serialize(session.Capture());
        session.Host.PublishMovieFrame(frame, false);
        Check(before == JsonSerializer.Serialize(session.Capture()) && before == JsonSerializer.Serialize(cold.Capture()), "Repeated/cold unavailable-camera publication changed its read budget.");
        Check(session.Host.Process.Movie!.CameraConsumption is { WordsBeforeRead: 0, WordsAfterRead: 0 } && session.ReadNativeMovieCamera() == blank, "Later unavailable-camera frames changed the zero camera or consumed rows.");
        Check(session.Host.Process.Tick == tick + 2 && File.ReadAllBytes(path).SequenceEqual(original), "Missing-camera validation changed the standalone fixture or callback count.");
        Console.WriteLine($"PASS original missing movie camera: field={session.Field.Name} tick={session.Host.Process.Tick} movie={movie.PreparedId} path={Path.GetFileName(source.Path)}");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try
        {
            action();
        }
        catch (T)
        {
            return;
        }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
}
