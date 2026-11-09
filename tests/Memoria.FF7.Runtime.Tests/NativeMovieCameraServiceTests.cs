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
        Installed();
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
